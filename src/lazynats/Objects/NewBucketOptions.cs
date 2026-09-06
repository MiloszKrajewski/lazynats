using NATS.Client.ObjectStore;

namespace lazynats.Objects;

// CreateBucketDialog's own result type, kept separate from NatsObjConfig's wire representation -
// see openspec/changes/add-obj-bucket-create/design.md's "NewBucketOptions" decision. Mirrors
// Values/NewBucketOptions.cs's split for the same reason: null means "the user left it unset",
// not a particular CLR-default value the reader has to know is special.
internal sealed record NewBucketOptions(
    string Name,
    TimeSpan? MaxAge)
{
    // The single place that translates "what the user asked for" into NatsObjConfig's wire shape -
    // CreateBucketDialog itself never constructs or references NatsObjConfig.
    public NatsObjConfig ToNatsObjConfig() =>
        new(Name) {
            // Unset MaxAge is left at TimeSpan.Zero deliberately - its own doc comment states this
            // means "unlimited", a genuine safe default, not a CLR-default landmine the way
            // NatsKVConfig.History = 0 would be.
            MaxAge = MaxAge ?? TimeSpan.Zero,
            // Same CLR-default-0-means-"unusable" reasoning as Values/NewBucketOptions - a bucket
            // with zero replicas isn't a meaningful request either. Storage is left unassigned -
            // its own documented default is File, a genuine safe default, and this dialog offers
            // no way to choose Memory.
            NumberOfReplicas = 1,
        };

    // Empty/whitespace input is a valid "leave it unset" - only text that fails to parse as a
    // TimeSpan is actually invalid. Same shape as Values/NewBucketOptions.TryParseMaxAge.
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
}
