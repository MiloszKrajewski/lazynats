namespace lazynats.Core;

// NATS JetStream server sentinel values for "no limit is enforced" differ by field type: -1 for
// count fields, TimeSpan.Zero for duration fields (see openspec/changes/clarify-unlimited-detail-
// values/design.md's Context section). Shared here so the sentinel-to-label mapping isn't
// copy-pasted across StreamDetails/ConsumerDetails/Values's and Objects' BucketDetails.
internal static class LimitFormat
{
    public static string Count(long value) => value < 0 ? "(unlimited)" : value.ToString();
    public static string Duration(TimeSpan value) => value <= TimeSpan.Zero ? "(unlimited)" : value.ToString();
}
