using System.Text.RegularExpressions;
using NATS.Client.JetStream.Models;

namespace lazynats.Components;

// A KV/OBJ bucket is, by convention (the same one the `nats` CLI and reference SDK use), a stream
// named "KV_<bucket>"/"OBJ_<bucket>" whose subjects are rooted at "$KV.<bucket>."/"$O.<bucket>."
// for that same, stripped name - the name prefix alone is a cheap, definitive negative for the
// overwhelming majority of streams, so it's checked before scanning Subjects. Shared across
// Streams (to exclude bucket-backing streams), Values, and Objects (to decide what counts as a
// bucket) - see openspec/changes/filter-bucket-backed-streams/design.md.
internal static partial class BucketName
{
    [GeneratedRegex(@"^KV_(?<bucket>.+)$")]
    private static partial Regex KvStreamName();

    [GeneratedRegex(@"^OBJ_(?<bucket>.+)$")]
    private static partial Regex ObjStreamName();

    public static string? TryGetKvBucketName(StreamConfig config) =>
        TryGetBucketName(config, KvStreamName(), "$KV.");

    public static string? TryGetObjBucketName(StreamConfig config) =>
        TryGetBucketName(config, ObjStreamName(), "$O.");

    private static string? TryGetBucketName(StreamConfig config, Regex streamNameRegex, string subjectPrefix)
    {
        if (config.Name is not { } name) return null;

        var match = streamNameRegex.Match(name);
        if (!match.Success) return null;

        var bucket = match.Groups["bucket"].Value;
        var expectedSubject = $"{subjectPrefix}{bucket}.";
        if (config.Subjects?.Any(s => s.StartsWith(expectedSubject, StringComparison.Ordinal)) != true)
            return null;

        return bucket;
    }
}
