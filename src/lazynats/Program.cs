using System.Threading.Channels;
using lazynats;
using lazynats.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

var connection = new NatsConnection(new NatsOpts { Url = "nats://localhost:4222" });
await connection.ConnectAsync();

var channel = Channel.CreateUnbounded<FeedEnvelope>();
var registry = new SubscriptionRegistry(connection, channel.Writer);

var services = new ServiceCollection();
services.AddSingleton(connection);
services.AddSingleton(registry);
services.AddSingleton(channel.Reader);
services.AddSingleton(new MessageDeduplicator(TimeSpan.FromMilliseconds(50)));
Services.Configure(services);

Application.MaximumIterationsPerSecond = 60;
var app = Application.Create();
ApplyColorTheme();
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