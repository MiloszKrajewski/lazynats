using lazynats.AotProbe.Probes;

var probes = new (string Name, Func<Task> Run)[] {
    ("Nats", NatsProbe.Run),
    ("Rx", RxProbe.Run),
    ("Di", DiProbe.Run),
};

var passed = 0;
foreach (var (name, run) in probes) {
    try {
        await run();
        Console.WriteLine($"✅ {name}");
        passed++;
    } catch (Exception ex) {
        Console.WriteLine($"❌ {name}: {ex.GetType().Name}: {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"{passed}/{probes.Length} probes passed");

return passed == probes.Length ? 0 : 1;
