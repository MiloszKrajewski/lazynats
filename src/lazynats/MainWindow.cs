using lazynats.Components;
using lazynats.Core;
using lazynats.LiveFeed;
using lazynats.Objects;
using lazynats.Publish;
using lazynats.Streams;
using lazynats.Subscriptions;
using lazynats.Values;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using NATS.Client.ObjectStore;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats;

internal sealed class MainWindow: Runnable
{
    private readonly StatusBar _statusBar;

    public MainWindow()
    {
        var registry = Services.Root.GetRequiredService<SubscriptionRegistry>();
        var connection = Services.Root.GetRequiredService<NatsConnection>();
        var jetStream = Services.Root.GetRequiredService<INatsJSContext>();
        var kvContext = Services.Root.GetRequiredService<INatsKVContext>();
        var objContext = Services.Root.GetRequiredService<INatsObjContext>();
        var feed = Services.Root.GetRequiredService<IObservable<FeedEnvelope>>();
        var dedup = Services.Root.GetRequiredService<MessageDeduplicator>();

        var subscribeTab = new SubscribeTab(registry) { Title = " 1:Subscribe ", Padding = { Thickness = new Thickness(1) } };
        var streamsTab = new StreamsTab(jetStream) { Title = " 2:Streams ", Padding = { Thickness = new Thickness(1) } };
        var valuesTab = new ValuesTab(kvContext) { Title = " 3:Values ", Padding = { Thickness = new Thickness(1) } };
        var objectsTab = new ObjectsTab(jetStream, objContext) { Title = " 4:Objects ", Padding = { Thickness = new Thickness(1) } };
        var tabs = new ManagementTabs { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Percent(75) };
        tabs.Add(subscribeTab, streamsTab, valuesTab, objectsTab);
        tabs.SelectTab(subscribeTab);

        var liveUpdates = new LiveUpdatesView(feed, dedup) { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        liveUpdates.ItemSelected += envelope => MessageBox.Query(
            App!, " Selected ", envelope.Message.Subject.Pad(), "_Ok");

        // Dim.Fill(1) leaves the bottom row free for the StatusBar, which sits outside this frame.
        var feedFrame = new FrameView
            { Title = " Live Feed ", X = 0, Y = Pos.Bottom(tabs), Width = Dim.Fill(), Height = Dim.Fill(1) };
        feedFrame.Add(liveUpdates);

        // The single source of truth for the app's fixed, always-available shortcuts - both the
        // StatusBar widgets below and ShortcutPickerDialog's hardcoded half are built from this
        // same list, per openspec/changes/add-shortcut-picker/design.md, so there's one place to
        // edit rather than two lists drifting apart.
        var topLevelShortcuts = new List<ShortcutHint> {
            new(Key.Q.WithAlt, "Quit", () => App!.RequestStop()),
            new(Key.D1.WithAlt, "Subscribe", () => tabs.SelectTab(subscribeTab)),
            new(Key.D2.WithAlt, "Streams", () => tabs.SelectTab(streamsTab)),
            new(Key.D3.WithAlt, "Values", () => tabs.SelectTab(valuesTab)),
            new(Key.D4.WithAlt, "Objects", () => tabs.SelectTab(objectsTab)),
        };
        // Deferred via AddTimeout(Zero, ...) rather than calling App!.Run directly: this Action
        // runs from inside the very same Alt+P key dispatch that's still unwinding, and Run()
        // pumps a nested loop that re-observes that same in-flight keypress as unhandled, feeding
        // it back into this global binding and recursively stacking PublishDialog instances.
        // Deferring to the next main-loop iteration runs it after that dispatch has fully
        // unwound, breaking the re-entrancy.
        topLevelShortcuts.Add(
            new ShortcutHint(
                Key.P.WithAlt, "Publish", () => App!.AddTimeout(
                    TimeSpan.Zero, () => {
                        App!.Run(new PublishDialog(connection));
                        return false;
                    })));
        // Same re-entrancy hazard as Publish above (this Action also opens a nested modal via
        // App!.Run from inside a still-unwinding global key dispatch), same AddTimeout(Zero, ...)
        // fix. Snapshots the focus chain at the moment the dialog actually opens (one main-loop
        // iteration after this shortcut was pressed - focus can't have moved in between) rather
        // than continuously tracking it, per design.md's "compute on demand" decision.
        //
        // Key is Alt-K, not Ctrl-/, Alt-/, or F1: the first two were confirmed dead on the user's
        // real Windows terminal (Ctrl+/ is explicitly excluded from Terminal.Gui's own default
        // Windows key bindings - e.g. their built-in Undo binding is
        // `Bind.AllPlus("Ctrl+Z", nonWindows: ["Ctrl+/"])" - and Alt+/ fared no better); F1
        // worked but function keys are unreliable on some laptop keyboards (Fn-lock). Alt+<letter>
        // matches the rest of this top-level set and has been reliable throughout.
        topLevelShortcuts.Add(
            new ShortcutHint(
                Key.K.WithAlt, "Shortcuts", () => App!.AddTimeout(
                    TimeSpan.Zero, () => {
                        // Deliberately excludes topLevelShortcuts: those are already permanently
                        // visible in the status bar, unlike the per-view ones this picker exists
                        // to surface because they don't fit there - listing them again here would
                        // just be redundant.
                        var hints = ShortcutAggregator.Collect(App!.TopRunnableView?.MostFocused);
                        var dialog = new ShortcutPickerDialog(hints);
                        App!.Run(dialog);
                        dialog.Result?.Action();
                        return false;
                    })));

        // Deliberately NOT BindKeyToApplication: that binds the key at the Application level,
        // bypassing normal modal key-routing entirely - confirmed via tmux that it lets e.g. Alt+3
        // silently switch tabs out from under an already-open dialog (ShortcutPickerDialog
        // included, which could even re-trigger itself and stack). Subscribing to this View's own
        // KeyDown below instead means these only fire while MainWindow itself is part of the
        // current key-dispatch chain - i.e. while no modal Dialog (a separate top-level session
        // with no SuperView link back here) is running - the same way ListEditorView's Ctrl+N/E/D
        // already work "from anywhere in this component" without needing BindKeyToApplication.
        // The widgets below stay for status-bar display and mouse-click support only.
        var topLevelWidgets = topLevelShortcuts.Select(
            hint => {
                var shortcut = new Shortcut { Text = hint.Text, Key = hint.Key };
                shortcut.Action = hint.Action;
                return shortcut;
            }).ToArray();

        KeyDown += (_, key) => {
            foreach (var hint in topLevelShortcuts)
                if (key == hint.Key)
                {
                    hint.Action();
                    key.Handled = true;
                    return;
                }
        };

        var clearShortcut = new Shortcut { Text = "Clear", Key = Key.C, Visible = false };
        clearShortcut.Action = liveUpdates.Clear;
        liveUpdates.HasFocusChanged += (_, _) => clearShortcut.Visible = liveUpdates.HasFocus;

        var streamsStatusShortcut = new Shortcut { Text = string.Empty, Visible = false };
        streamsTab.StatusChanged += message => {
            streamsStatusShortcut.Text = message;
            streamsStatusShortcut.Visible = true;
        };

        var valuesStatusShortcut = new Shortcut { Text = string.Empty, Visible = false };
        valuesTab.StatusChanged += message => {
            valuesStatusShortcut.Text = message;
            valuesStatusShortcut.Visible = true;
        };

        var objectsStatusShortcut = new Shortcut { Text = string.Empty, Visible = false };
        objectsTab.StatusChanged += message => {
            objectsStatusShortcut.Text = message;
            objectsStatusShortcut.Visible = true;
        };

        _statusBar = new StatusBar(
        [
            ..topLevelWidgets, clearShortcut,
            streamsStatusShortcut, valuesStatusShortcut, objectsStatusShortcut,
        ]);

        Add(tabs, feedFrame, _statusBar);
    }
}
