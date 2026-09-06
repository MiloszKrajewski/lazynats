using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats;

internal sealed class MainWindow: Runnable
{
    public MainWindow()
    {
        var registry = Services.Root.GetRequiredService<SubscriptionRegistry>();
        var connection = Services.Root.GetRequiredService<NatsConnection>();
        var feedReader = Services.Root.GetRequiredService<ChannelReader<FeedEnvelope>>();
        var dedup = Services.Root.GetRequiredService<MessageDeduplicator>();

        var subscriptionsView = new SubscriptionsView(registry) { Title = "Subscriptions" };
        var publishView = new PublishView(connection) { Title = "Publish" };
        var tabs = new Tabs { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Percent(75) };
        tabs.Add(subscriptionsView, publishView);
        tabs.Value = subscriptionsView;

        var liveUpdates = new LiveUpdatesView(feedReader, dedup) { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        liveUpdates.ItemSelected += envelope => MessageBox.Query(App!, "Selected", envelope.Message.Subject, "_Ok");

        // Dim.Fill(1) leaves the bottom row free for the StatusBar, which sits outside this frame.
        var feedFrame = new FrameView { Title = " Live Feed ", X = 0, Y = Pos.Bottom(tabs), Width = Dim.Fill(), Height = Dim.Fill(1) };
        feedFrame.Add(liveUpdates);

        var quitShortcut = new Shortcut { Text = "Quit", Key = Key.Q.WithAlt, BindKeyToApplication = true };
        quitShortcut.Action = () => App!.RequestStop();

        var clearShortcut = new Shortcut { Text = "Clear", Key = Key.C, Visible = false };
        clearShortcut.Action = liveUpdates.Clear;
        liveUpdates.HasFocusChanged += (_, _) => clearShortcut.Visible = liveUpdates.HasFocus;

        var publishStatusShortcut = new Shortcut { Text = string.Empty, Visible = false };
        publishView.StatusChanged += message => {
            publishStatusShortcut.Text = message;
            publishStatusShortcut.Visible = true;
        };

        var statusBar = new StatusBar([quitShortcut, clearShortcut, publishStatusShortcut]);

        Add(tabs, feedFrame, statusBar);
    }
}