using lazynats.Components;
using lazynats.Values;
using lazynats.LiveFeed;
using lazynats.Objects;
using lazynats.Publish;
using lazynats.Streams;
using lazynats.Subscriptions;
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
    private readonly ShortcutTracker _shortcutTracker;
    private readonly StatusBar _statusBar;
    private readonly List<Shortcut> _dynamicShortcuts = [];
    private readonly int _staticShortcutCount;

    public MainWindow()
    {
        var registry = Services.Root.GetRequiredService<SubscriptionRegistry>();
        var connection = Services.Root.GetRequiredService<NatsConnection>();
        var jetStream = Services.Root.GetRequiredService<INatsJSContext>();
        var kv = Services.Root.GetRequiredService<INatsKVContext>();
        var obj = Services.Root.GetRequiredService<INatsObjContext>();
        var feed = Services.Root.GetRequiredService<IObservable<FeedEnvelope>>();
        var dedup = Services.Root.GetRequiredService<MessageDeduplicator>();
        _shortcutTracker = Services.Root.GetRequiredService<ShortcutTracker>();

        var subscribeTab = new SubscribeTab(registry) { Title = " 1:Subscribe ", Padding = { Thickness = new Thickness(1) } };
        var streamsTab = new StreamsTab(jetStream) { Title = " 2:Streams ", Padding = { Thickness = new Thickness(1) } };
        var valuesTab = new ValuesTab(kv) { Title = " 3:Values ", Padding = { Thickness = new Thickness(1) } };
        var objectsTab = new ObjectsTab(jetStream, obj) { Title = " 4:Objects ", Padding = { Thickness = new Thickness(1) } };
        var tabs = new ManagementTabs { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Percent(75) };
        tabs.Add(subscribeTab, streamsTab, valuesTab, objectsTab);
        tabs.Value = subscribeTab;

        var liveUpdates = new LiveUpdatesView(feed, dedup) { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        liveUpdates.ItemSelected += envelope => MessageBox.Query(App!, DialogText.Pad("Selected"), DialogText.Pad(envelope.Message.Subject), "_Ok");

        // Dim.Fill(1) leaves the bottom row free for the StatusBar, which sits outside this frame.
        var feedFrame = new FrameView { Title = " Live Feed ", X = 0, Y = Pos.Bottom(tabs), Width = Dim.Fill(), Height = Dim.Fill(1) };
        feedFrame.Add(liveUpdates);

        var quitShortcut = new Shortcut { Text = "Quit", Key = Key.Q.WithAlt, BindKeyToApplication = true };
        quitShortcut.Action = () => App!.RequestStop();

        var subscribeTabShortcut = new Shortcut { Text = "Subscribe", Key = Key.D1.WithAlt, BindKeyToApplication = true };
        subscribeTabShortcut.Action = () => tabs.Value = subscribeTab;

        var streamsTabShortcut = new Shortcut { Text = "Streams", Key = Key.D2.WithAlt, BindKeyToApplication = true };
        streamsTabShortcut.Action = () => tabs.Value = streamsTab;

        var valuesTabShortcut = new Shortcut { Text = "Values", Key = Key.D3.WithAlt, BindKeyToApplication = true };
        valuesTabShortcut.Action = () => tabs.Value = valuesTab;

        var objectsTabShortcut = new Shortcut { Text = "Objects", Key = Key.D4.WithAlt, BindKeyToApplication = true };
        objectsTabShortcut.Action = () => tabs.Value = objectsTab;

        var publishShortcut = new Shortcut { Text = "Publish", Key = Key.P.WithAlt, BindKeyToApplication = true };
        // Deferred via AddTimeout(Zero, ...) rather than calling App!.Run directly: this Action
        // runs from inside the very same Alt+P key dispatch that's still unwinding, and Run()
        // pumps a nested loop that re-observes that same in-flight keypress as unhandled, feeding
        // it back into this global binding and recursively stacking PublishDialog instances.
        // Deferring to the next main-loop iteration runs it after that dispatch has fully
        // unwound, breaking the re-entrancy.
        publishShortcut.Action = () => App!.AddTimeout(TimeSpan.Zero, () => { App!.Run(new PublishDialog(connection)); return false; });

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

        _statusBar = new StatusBar([
            quitShortcut, subscribeTabShortcut, streamsTabShortcut, valuesTabShortcut, objectsTabShortcut, publishShortcut, clearShortcut,
            streamsStatusShortcut, valuesStatusShortcut, objectsStatusShortcut,
        ]);
        _staticShortcutCount = _statusBar.SubViews.Count;

        // Appends/replaces only the dynamic tail - the fixed shortcuts above and their own
        // visibility wiring (e.g. clearShortcut) are never touched by this.
        _shortcutTracker.ShortcutsChanged += SyncDynamicShortcuts;

        Add(tabs, feedFrame, _statusBar);
    }

    private void SyncDynamicShortcuts(IReadOnlyList<ShortcutHint> hints)
    {
        while (_statusBar.SubViews.Count > _staticShortcutCount) _statusBar.RemoveShortcut(_staticShortcutCount);
        _dynamicShortcuts.Clear();

        // Plain Add, not AddShortcutAt: this always appends at the end, and AddShortcutAt's
        // insert-at-index implementation removes and re-adds every SubView in the Bar (statics
        // included) to do that, once per hint. Beyond being wasteful, this refresh runs from
        // ShortcutTracker.Refresh(), which itself can fire from inside Terminal.Gui's own
        // in-progress focus-change dispatch (SetHasFocusTrue raises FocusedChanged before the
        // new focus has fully settled) - repeatedly tearing down and rebuilding the StatusBar
        // while that's happening was observed to leave keyboard Tab navigation one press behind
        // until it self-corrected.
        foreach (var hint in hints) {
            var shortcut = new Shortcut { Text = hint.Text, Key = hint.Key };
            shortcut.Action = hint.Action;
            _dynamicShortcuts.Add(shortcut);
            _statusBar.Add(shortcut);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _shortcutTracker.ShortcutsChanged -= SyncDynamicShortcuts;
        base.Dispose(disposing);
    }
}