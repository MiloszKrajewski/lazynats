using NATS.Client.KeyValueStore;

namespace lazynats.Values;

// CreateBucketDialog's own result type, kept separate from NatsKVConfig's wire representation -
// see openspec/changes/add-kv-bucket-create/design.md's "NewBucketOptions" decision. Mirrors
// Streams/NewStreamOptions.cs's split for the same reason: null means "the user left it unset",
// not a particular CLR-default value the reader has to know is special.
internal sealed record NewBucketOptions(
    string Name,
    NatsKVStorageType Storage,
    int? History,
    TimeSpan? MaxAge,
    TimeSpan? LimitMarkerTTL)
{
    // The single place that translates "what the user asked for" into NatsKVConfig's wire shape -
    // CreateBucketDialog itself never constructs or references NatsKVConfig.
    public NatsKVConfig ToNatsKVConfig() =>
        new(Name) {
            Storage = Storage,
            // A bare `new NatsKVConfig(Name)` leaves History at the CLR default of 0, which means
            // "keep zero revisions" - not a usable bucket, and not "unset" the way MaxAge/
            // LimitMarkerTTL's CLR default coincidentally is (their own TimeSpan.Zero is a
            // genuine "unlimited"/"disabled" sentinel). History is user-facing, but its CLR
            // default still isn't a safe "unset" value, so an unset field substitutes 1 (keep
            // only the latest revision per key), matching what `nats kv add` defaults to.
            // TryParseHistory already rejects any user-entered value below 1, so the only null
            // this sees is the genuine "left unset" case.
            History = History ?? 1,
            MaxAge = MaxAge ?? TimeSpan.Zero,
            // Unset LimitMarkerTTL is left at TimeSpan.Zero deliberately - its own doc comment
            // states 0 means "markers are not supported", a legitimate default, not a CLR-default
            // landmine the way History = 0 would be. Verified against a live NATS server 2.12.8
            // (well above the 2.11 minimum this feature's own doc comment calls out) during
            // implementation - a non-zero value round-trips correctly (created bucket's "TTL
            // Limit" detail row matched the entered value); pre-2.11 server behavior (reject vs.
            // silently ignore) remains unverified, per design.md's risk on this field.
            LimitMarkerTTL = LimitMarkerTTL ?? TimeSpan.Zero,
            // Same CLR-default-0-means-"unusable" reasoning as History - a bucket with zero
            // replicas isn't a meaningful request either.
            NumberOfReplicas = 1,
        };

    // Empty/whitespace input is a valid "leave it unset" - same empty-means-null shape as
    // TryParseMaxAge/TryParseLimitMarkerTTL below, for consistency, even though this parses an
    // int rather than a TimeSpan and doesn't share any logic with them. Non-empty text must parse
    // as an int and be >= 1 - a "keep < 1 revisions" bucket isn't usable, the same reasoning
    // ToNatsKVConfig()'s `History ?? 1` substitution uses for the unset case.
    public static bool TryParseHistory(string text, out int? result)
    {
        if (string.IsNullOrWhiteSpace(text)) {
            result = null;
            return true;
        }

        if (int.TryParse(text.Trim(), out var parsed) && parsed >= 1) {
            result = parsed;
            return true;
        }

        result = null;
        return false;
    }

    // Empty/whitespace input is a valid "leave it unset" - only text that fails to parse as a
    // TimeSpan is actually invalid. Same shape as Streams/NewStreamOptions.TryParseMaxAge.
    public static bool TryParseMaxAge(string text, out TimeSpan? result)
    {
        if (string.IsNullOrWhiteSpace(text)) {
            result = null;
            return true;
        }

        if (TimeSpan.TryParse(text.Trim(), out var parsed)) {
            result = parsed;
            return true;
        }

        result = null;
        return false;
    }

    // Same empty-means-null/TimeSpan.Parse-otherwise shape as TryParseMaxAge, kept as its own
    // method rather than a call to TryParseMaxAge - Max Age and Limit Marker TTL are conceptually
    // distinct fields (one bounds entry age, the other bounds tombstone retention) even though
    // today's parsing happens to coincide; see design.md's "conceptually distinct fields" decision.
    public static bool TryParseLimitMarkerTTL(string text, out TimeSpan? result)
    {
        if (string.IsNullOrWhiteSpace(text)) {
            result = null;
            return true;
        }

        if (TimeSpan.TryParse(text.Trim(), out var parsed)) {
            result = parsed;
            return true;
        }

        result = null;
        return false;
    }
}
