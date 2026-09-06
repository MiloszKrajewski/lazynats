using System.Text.RegularExpressions;
using NATS.Client.JetStream.Models;

namespace lazynats.Core;

// A KV/OBJ bucket is, by convention (the same one the `nats` CLI and reference SDK use), a stream
// named "KV_<bucket>"/"OBJ_<bucket>" whose subjects are rooted at "$KV.<bucket>."/"$O.<bucket>."
// for that same, stripped name - the name prefix alone is a cheap, definitive negative for the
// overwhelming majority of streams, so it's checked before scanning Subjects. Shared across
// Streams (to exclude bucket-backing streams), Values, and Objects (to decide what counts as a
// bucket) - see openspec/changes/filter-bucket-backed-streams/design.md.
internal static partial class BucketExtensions
{
    [GeneratedRegex("^KV_(?<bucket>.+)$")]
    private static partial Regex KvNamePattern();

    [GeneratedRegex("^OBJ_(?<bucket>.+)$")]
    private static partial Regex ObjNamePattern();

    public static string? TryGetKvBucketName(this StreamConfig config) =>
        TryGetBucketName(config, KvNamePattern(), "$KV.");

    public static string? TryGetObjBucketName(this StreamConfig config) =>
        TryGetBucketName(config, ObjNamePattern(), "$O.");

    private static string? TryGetBucketName(
        StreamConfig config, Regex streamNameRegex, string subjectPrefix)
    {
        if (config.Name is not { } name) return null;

        var match = streamNameRegex.Match(name);
        if (!match.Success) return null;

        var bucket = match.Groups["bucket"].Value;
        var expectedSubject = $"{subjectPrefix}{bucket}.";
        var hasMatchingSubject = config.Subjects?.Any(s => s.StartsWith(expectedSubject, StringComparison.Ordinal));
        return hasMatchingSubject ?? false ? bucket : null;
    }
}
