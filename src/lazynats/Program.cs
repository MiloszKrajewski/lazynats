using System.Threading.Channels;
using lazynats;
using lazynats.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using Terminal.Gui.App;

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
Application.Create().Run<MainWindow>().Dispose();