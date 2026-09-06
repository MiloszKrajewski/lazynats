using System.IO.Hashing;
using System.Text;

namespace lazynats.AotProbe.Probes;

internal static class XxHash3Probe
{
    public static Task Run()
    {
        var hasher = new XxHash3();
        hasher.Append(Encoding.UTF8.GetBytes("subject"));
        hasher.Append(Encoding.UTF8.GetBytes("header-key"));
        hasher.Append(Encoding.UTF8.GetBytes("header-value"));
        hasher.Append(Encoding.UTF8.GetBytes("payload"));

        var hash = hasher.GetCurrentHashAsUInt64();

        if (hash == 0)
            throw new InvalidOperationException("Expected a non-zero hash from XxHash3.");

        return Task.CompletedTask;
    }
}
