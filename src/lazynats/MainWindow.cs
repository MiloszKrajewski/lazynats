using System.Threading.Channels;
using lazynats.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using Terminal.Gui.Drawing;
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

        var subscribeTab = new SubscribeTab(registry) { Title = " Subscribe ", Padding = { Thickness = new Thickness(1) } };
        var publishTab = new PublishTab(connection) { Title = " Publish ", Padding = { Thickness = new Thickness(1) } };
        var tabs = new ManagementTabs { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Percent(75) };
        tabs.Add(subscribeTab, publishTab);
        tabs.Value = subscribeTab;

        var liveUpdates = new LiveUpdatesView(feedReader, dedup) { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        liveUpdates.ItemSelected += envelope => MessageBox.Query(App!, "Selected", envelope.Message.Subject, "_Ok");

        // Dim.Fill(1) leaves the bottom row free for the StatusBar, which sits outside this frame.
        var feedFrame = new FrameView { Title = " Live Feed ", X = 0, Y = Pos.Bottom(tabs), Width = Dim.Fill(), Height = Dim.Fill(1) };
        feedFrame.Add(liveUpdates);

        var quitShortcut = new Shortcut { Text = "Quit", Key = Key.Q.WithAlt, BindKeyToApplication = true };
        quitShortcut.Action = () => App!.RequestStop();

        var subscribeTabShortcut = new Shortcut { Text = "Subscribe", Key = Key.B.WithAlt, BindKeyToApplication = true };
        subscribeTabShortcut.Action = () => tabs.Value = subscribeTab;

        var publishTabShortcut = new Shortcut { Text = "Publish", Key = Key.P.WithAlt, BindKeyToApplication = true };
        publishTabShortcut.Action = () => tabs.Value = publishTab;

        var clearShortcut = new Shortcut { Text = "Clear", Key = Key.C, Visible = false };
        clearShortcut.Action = liveUpdates.Clear;
        liveUpdates.HasFocusChanged += (_, _) => clearShortcut.Visible = liveUpdates.HasFocus;

        var publishStatusShortcut = new Shortcut { Text = string.Empty, Visible = false };
        publishTab.StatusChanged += message => {
            publishStatusShortcut.Text = message;
            publishStatusShortcut.Visible = true;
        };

        var statusBar = new StatusBar([quitShortcut, subscribeTabShortcut, publishTabShortcut, clearShortcut, publishStatusShortcut]);

        Add(tabs, feedFrame, statusBar);
    }
}