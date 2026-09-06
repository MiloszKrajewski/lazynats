using lazynats.Kv;
using lazynats.LiveFeed;
using lazynats.Streams;
using lazynats.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
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
        var feed = Services.Root.GetRequiredService<IObservable<FeedEnvelope>>();
        var dedup = Services.Root.GetRequiredService<MessageDeduplicator>();
        _shortcutTracker = Services.Root.GetRequiredService<ShortcutTracker>();

        var subscribeTab = new SubscribeTab(registry) { Title = " 1:Subscribe ", Padding = { Thickness = new Thickness(1) } };
        var publishTab = new PublishTab(connection, _shortcutTracker) { Title = " 2:Publish ", Padding = { Thickness = new Thickness(1) } };
        var streamsTab = new StreamsTab(jetStream) { Title = " 3:Streams ", Padding = { Thickness = new Thickness(1) } };
        var kvTab = new KvTab(kv) { Title = " 4:KV ", Padding = { Thickness = new Thickness(1) } };
        var tabs = new ManagementTabs { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Percent(75) };
        tabs.Add(subscribeTab, publishTab, streamsTab, kvTab);
        tabs.Value = subscribeTab;

        var liveUpdates = new LiveUpdatesView(feed, dedup) { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        liveUpdates.ItemSelected += envelope => MessageBox.Query(App!, "Selected", envelope.Message.Subject, "_Ok");

        // Dim.Fill(1) leaves the bottom row free for the StatusBar, which sits outside this frame.
        var feedFrame = new FrameView { Title = " Live Feed ", X = 0, Y = Pos.Bottom(tabs), Width = Dim.Fill(), Height = Dim.Fill(1) };
        feedFrame.Add(liveUpdates);

        var quitShortcut = new Shortcut { Text = "Quit", Key = Key.Q.WithAlt, BindKeyToApplication = true };
        quitShortcut.Action = () => App!.RequestStop();

        var subscribeTabShortcut = new Shortcut { Text = "Subscribe", Key = Key.D1.WithAlt, BindKeyToApplication = true };
        subscribeTabShortcut.Action = () => tabs.Value = subscribeTab;

        var publishTabShortcut = new Shortcut { Text = "Publish", Key = Key.D2.WithAlt, BindKeyToApplication = true };
        publishTabShortcut.Action = () => tabs.Value = publishTab;

        var streamsTabShortcut = new Shortcut { Text = "Streams", Key = Key.D3.WithAlt, BindKeyToApplication = true };
        streamsTabShortcut.Action = () => tabs.Value = streamsTab;

        var kvTabShortcut = new Shortcut { Text = "KV", Key = Key.D4.WithAlt, BindKeyToApplication = true };
        kvTabShortcut.Action = () => tabs.Value = kvTab;

        var clearShortcut = new Shortcut { Text = "Clear", Key = Key.C, Visible = false };
        clearShortcut.Action = liveUpdates.Clear;
        liveUpdates.HasFocusChanged += (_, _) => clearShortcut.Visible = liveUpdates.HasFocus;

        var publishStatusShortcut = new Shortcut { Text = string.Empty, Visible = false };
        publishTab.StatusChanged += message => {
            publishStatusShortcut.Text = message;
            publishStatusShortcut.Visible = true;
        };

        var streamsStatusShortcut = new Shortcut { Text = string.Empty, Visible = false };
        streamsTab.StatusChanged += message => {
            streamsStatusShortcut.Text = message;
            streamsStatusShortcut.Visible = true;
        };

        var kvStatusShortcut = new Shortcut { Text = string.Empty, Visible = false };
        kvTab.StatusChanged += message => {
            kvStatusShortcut.Text = message;
            kvStatusShortcut.Visible = true;
        };

        _statusBar = new StatusBar([
            quitShortcut, subscribeTabShortcut, publishTabShortcut, streamsTabShortcut, kvTabShortcut, clearShortcut,
            publishStatusShortcut, streamsStatusShortcut, kvStatusShortcut,
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