using System.Reactive.Subjects;
using lazynats;
using lazynats.LiveFeed;
using lazynats.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using NATS.Net;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

var connection = new NatsConnection(new NatsOpts { Url = "nats://localhost:4222" });
await connection.ConnectAsync();

// Synchronize() is load-bearing: SubscriptionRegistry runs one task per active subscription, all
// calling OnNext concurrently, and a raw Subject<T> isn't safe under concurrent OnNext. Only ever
// register this synchronized instance below (never the raw Subject type) so producers/consumers
// can't accidentally bypass the serialization.
var feed = Subject.Synchronize(new Subject<FeedEnvelope>());
var registry = new SubscriptionRegistry(connection, feed);
var jetStream = connection.CreateJetStreamContext();

// Application.Create() must run before Services.Configure() - ShortcutTracker needs a live
// IApplication at construction time (to subscribe to Navigation.FocusedChanged), and unlike every
// other View.App usage in this codebase (always deferred to a callback), it can't wait for that.
Application.MaximumIterationsPerSecond = 60;
var app = Application.Create();
ApplyColorTheme();

var services = new ServiceCollection();
services.AddSingleton(connection);
services.AddSingleton(registry);
services.AddSingleton(jetStream);
services.AddSingleton<IObserver<FeedEnvelope>>(feed);
services.AddSingleton<IObservable<FeedEnvelope>>(feed);
services.AddSingleton(new MessageDeduplicator(TimeSpan.FromMilliseconds(50)));
services.AddSingleton(app);
services.AddSingleton(new ShortcutTracker(app));
Services.Configure(services);

app.Run<MainWindow>().Dispose();

// Overrides Terminal.Gui's stock "Base"/"Dialog" schemes with the app's own dark palette (see
// openspec/changes/add-dark-theme). Must run after Application.Create(), which is what
// initializes SchemeManager's built-in schemes, and before Run<MainWindow>() draws anything.
static void ApplyColorTheme()
{
    var normal = new Attribute(ColorName16.Gray, ColorName16.Black);
    var editable = new Attribute(ColorName16.White, Theme.EditableBackground);

    SchemeManager.AddScheme("Base", new Scheme(SchemeManager.GetScheme("Base")) {
        Normal = normal,
        Editable = editable,
    });

    SchemeManager.AddScheme("Dialog", new Scheme(SchemeManager.GetScheme("Dialog")) {
        Normal = normal,
        Focus = new Attribute(ColorName16.Black, ColorName16.White),
        Editable = editable,
    });
}