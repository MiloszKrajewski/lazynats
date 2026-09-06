using NATS.Client.KeyValueStore;

namespace lazynats.Kv;

// NatsKVContext.GetStatusesAsync() (v2.8.2) returns every JetStream stream on the server, not
// just KV-backed ones, and its NatsKVStatus.Bucket is just the raw, unstripped stream name -
// confirmed live against a server with a mix of real buckets and plain streams (see
// openspec/changes/add-kv-tab/design.md's "Bucket list filtering" decision). A KV bucket is,
// by convention (the same one the `nats` CLI and server use), any stream named "KV_<bucket>";
// this is the single place that convention is applied.
internal static class BucketName
{
    private const string Prefix = "KV_";

    public static bool IsKvStream(string? streamName) =>
        streamName is not null && streamName.StartsWith(Prefix, StringComparison.Ordinal);

    public static string From(NatsKVStatus status) => status.Info.Config.Name![Prefix.Length..];
}
