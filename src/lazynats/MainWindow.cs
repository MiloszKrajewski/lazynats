using lazynats.Components;
using lazynats.LiveFeed;
using lazynats.Objects;
using lazynats.Publish;
using lazynats.Streams;
using lazynats.Subscriptions;
using lazynats.Templates;
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
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats;

internal sealed class MainWindow: Runnable
{
    // Trailing indicator glyph in the Live Feed border status: "following down" as new messages
    // arrive, vs. a stopped/pinned marker while sticky.
    private const char FollowingGlyph = '↓';
    private const char StickyGlyph = '●';

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
        var templatesTab = new TemplatesTab(kvContext) { Title = " 5:Templates ", Padding = { Thickness = new Thickness(1) } };
        var tabs = new ManagementTabs { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Percent(75) };
        tabs.Add(subscribeTab, streamsTab, valuesTab, objectsTab, templatesTab);
        tabs.SelectTab(subscribeTab);

        var liveUpdates = new LiveUpdatesView(feed, dedup) { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        liveUpdates.ItemSelected += envelope => App!.Run(new MessageDetailDialog(envelope));

        // Dim.Fill(1) leaves the bottom row free for the StatusBar, which sits outside this frame.
        var feedFrame = new FrameView
            { Title = " 0:Live Feed ", X = 0, Y = Pos.Bottom(tabs), Width = Dim.Fill(), Height = Dim.Fill(1) };
        feedFrame.Add(liveUpdates);

        // Sibling of feedFrame (not a child of its Border, not inside LiveUpdatesView itself, per
        // the live-feed spec's "No In-View Header" requirement) so it draws after feedFrame's own
        // border paint completes - no LineCanvas.Exclude bookkeeping needed. `Pos.AnchorEnd()`
        // (no offset) tracks the label's own current Width (it's the width-aware form, unlike
        // `AnchorEnd(n)` which is a fixed offset that a growing digit count - up to 5 at the
        // 10,000-message cap - would run past, overwriting the border's corner glyph), flush
        // against MainWindow's right edge - which is also the corner glyph's own column, so `- 2`
        // shifts the label two columns further left: one to clear the corner's column entirely,
        // one more to leave an actual blank gutter column before it (confirmed empirically against
        // the running app - `- 1` still abuts the corner with no visible gap). `Pos.Bottom(feedFrame)
        // - 1` puts it on the bottom border row instead - same right-corner column, one row up from
        // feedFrame's own bottom edge.
        // See openspec/changes/live-feed-message-count/design.md decisions 2-4.
        var feedStatusLabel = new Label
        {
            X = Pos.AnchorEnd() - 2, Y = Pos.Bottom(feedFrame) - 1, Width = Dim.Auto(), Height = 1,
            CanFocus = false, Text = FormatFeedStatus(new LiveFeedStatus(0, true, 0)),
        };
        // Whole-label color override (per-view Scheme, not per-run text markup - Terminal.Gui
        // Labels have no inline "markdown-like" styling): green while following, yellow while
        // sticky. Background is read once, before the first override, so later overrides keep
        // matching the ambient border color rather than whatever the previous override left behind.
        var feedStatusBackground = feedStatusLabel.GetAttributeForRole(VisualRole.Normal).Background;
        feedStatusLabel.SetScheme(new Scheme(new Attribute(Theme.LiveFeedFollowingColor, feedStatusBackground)));
        liveUpdates.StatusChanged += status => {
            feedStatusLabel.Text = FormatFeedStatus(status);
            var color = status.Following ? Theme.LiveFeedFollowingColor : Theme.LiveFeedStickyColor;
            feedStatusLabel.SetScheme(new Scheme(new Attribute(color, feedStatusBackground)));
            feedStatusLabel.SetNeedsDraw();
        };
        // Sibling dirty-tracking doesn't cascade: feedFrame's own border repaint (resize, or its
        // subtree's focus state changing) can redraw over the label without touching it, so it
        // must be explicitly invalidated alongside those triggers too.
        feedFrame.FrameChanged += (_, _) => feedStatusLabel.SetNeedsDraw();
        feedFrame.HasFocusChanged += (_, _) => feedStatusLabel.SetNeedsDraw();

        // Following: total message count plus a down arrow ("following down" as new messages
        // arrive). Sticky (paused): the selected message's 1-based position out of the total,
        // plus a filled circle marking that the feed is stopped/pinned rather than scrolling.
        // Padded with a leading/trailing space for breathing room against the border line.
        static string FormatFeedStatus(LiveFeedStatus status) => status.Following
            ? $" {status.Count} {FollowingGlyph} "
            : $" {status.SelectedIndex + 1}/{status.Count} {StickyGlyph} ";

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
            new(Key.D5.WithAlt, "Templates", () => tabs.SelectTab(templatesTab)),
            new(Key.D0.WithAlt, "Live Feed", () => liveUpdates.SetFocus()),
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
        // Key is bare `?`, not Ctrl-/, Alt-/, F1, or the originally-shipped Alt-K: the first two
        // were confirmed dead on the user's real Windows terminal (Ctrl+/ is explicitly excluded
        // from Terminal.Gui's own default Windows key bindings - e.g. their built-in Undo binding
        // is `Bind.AllPlus("Ctrl+Z", nonWindows: ["Ctrl+/"])" - and Alt+/ fared no better); F1
        // worked but function keys are unreliable on some laptop keyboards (Fn-lock). `?` is safe
        // for the same reason bare-letter list shortcuts are (see tab-scoped-list-shortcuts):
        // Terminal.Gui's key dispatch is strictly depth-first, so a focused text field (FilterBox,
        // any dialog field) always gets first refusal at a keystroke and consumes a literal `?`
        // as text before MainWindow's own KeyDown subscriber below ever sees it - this handler
        // only fires once nothing more specific already claimed the key. It also echoes `/`'s
        // existing role as a punctuation-key global shortcut and reads naturally as "help".
        //
        // ShortcutPickerLauncher.MakeAction owns the deferred-collect-run-invoke sequence (see its
        // own comments for the re-entrancy/deferral reasoning shared with Publish above).
        // App!.TopRunnableView?.MostFocused - not App! itself or any fixed view - is the actual
        // app-wide keyboard focus, wherever it is (several levels deep inside whichever tab is
        // active); MainWindow itself never implements IShortcutSource, so its own hardcoded
        // topLevelShortcuts are structurally excluded from Collect's upward walk rather than
        // filtered out after the fact. Re-evaluated lazily by MakeAction each time the picker
        // opens, not once here - see ShortcutPickerLauncher's own comment on why.
        topLevelShortcuts.Add(
            new ShortcutHint(
                ShortcutPickerLauncher.Key, "Shortcuts",
                ShortcutPickerLauncher.MakeAction(this, () => App!.TopRunnableView?.MostFocused)));

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

        var templatesStatusShortcut = new Shortcut { Text = string.Empty, Visible = false };
        templatesTab.StatusChanged += message => {
            templatesStatusShortcut.Text = message;
            templatesStatusShortcut.Visible = true;
        };

        _statusBar = new StatusBar(
        [
            ..topLevelWidgets,
            streamsStatusShortcut, valuesStatusShortcut, objectsStatusShortcut, templatesStatusShortcut,
        ]);

        Add(tabs, feedFrame, feedStatusLabel, _statusBar);
    }
}
