using System.Reactive.Linq;
using lazynats;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using Terminal.Gui.App;

var connection = new NatsConnection(new NatsOpts { Url = "nats://localhost:4222" });
await connection.ConnectAsync();

var heartbeat = Observable
    .Interval(TimeSpan.FromSeconds(0.1))
    .Select(tick => $"event #{tick}")
    .Publish().RefCount();

var services = new ServiceCollection();
services.AddSingleton(connection);
services.AddKeyedSingleton("heartbeat", heartbeat);
Services.Configure(services);

Application.MaximumIterationsPerSecond = 120;
Application.Create().Run<MainWindow>().Dispose();