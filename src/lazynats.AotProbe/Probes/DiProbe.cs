using Microsoft.Extensions.DependencyInjection;

namespace lazynats.AotProbe.Probes;

internal static class DiProbe
{
    private interface IGreeter
    {
        string Greet();
    }

    private sealed class Greeter: IGreeter
    {
        public string Greet() => "hello";
    }

    private interface IGreetingService
    {
        string Announce();
    }

    private sealed class GreetingService(IGreeter greeter): IGreetingService
    {
        public string Announce() => $"{greeter.Greet()}, world";
    }

    public static Task Run()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGreeter, Greeter>();
        services.AddSingleton<IGreetingService, GreetingService>();

        using var provider = services.BuildServiceProvider();
        var greetingService = provider.GetRequiredService<IGreetingService>();

        var result = greetingService.Announce();
        if (result != "hello, world")
            throw new InvalidOperationException($"Expected 'hello, world', got '{result}'.");

        return Task.CompletedTask;
    }
}
