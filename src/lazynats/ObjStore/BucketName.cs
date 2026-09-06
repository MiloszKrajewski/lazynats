using NATS.Client.JetStream.Models;

namespace lazynats.ObjStore;

// INatsObjContext has no bulk bucket-status listing call, so the bucket list is sourced from
// JetStream's plain stream list instead (see design.md's "Bucket list" decision) - mirroring
// BucketName.IsKvStream's "KV_<bucket>" convention, an OBJ bucket is, by the same server/`nats`
// CLI convention, any stream named "OBJ_<bucket>". This is the single place that convention is
// applied.
internal static class BucketName
{
    private const string Prefix = "OBJ_";

    public static bool IsObjStream(string? streamName) =>
        streamName is not null && streamName.StartsWith(Prefix, StringComparison.Ordinal);

    public static string From(StreamInfo info) => info.Config.Name![Prefix.Length..];
}
