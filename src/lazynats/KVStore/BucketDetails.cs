using lazynats.Components;
using NATS.Client.KeyValueStore;

namespace lazynats.KVStore;

// Read-only readout of a single KV bucket's stats, per nats-kv's "Bucket Detail Panel"
// requirement - never focusable, never edits anything. Mirrors StreamDetails deliberately
// closely (a KV bucket is a stream under the hood - NatsKVStatus.Info is a StreamInfo).
internal sealed class BucketDetails: PollingDetailsView<string, NatsKVStatus>
{
    private readonly INatsKVContext _kv;

    public BucketDetails(INatsKVContext kv) => _kv = kv;

    public void SetTarget(string? bucket)
    {
        if (bucket is not null) SetPollTarget(bucket); else ClearPollTarget();
    }

    protected override async Task<NatsKVStatus?> FetchAsync(string bucket) =>
        await (await _kv.GetStoreAsync(bucket)).GetStatusAsync();

    protected override (string Label, string Value)[] BuildRows(NatsKVStatus status)
    {
        var config = status.Info.Config;
        var state = status.Info.State;
        var ttlLimit = status.LimitMarkerTTL > TimeSpan.Zero ? status.LimitMarkerTTL.ToString() : "(none)";

        return [
            ("Bucket", BucketName.From(status)),
            ("Compressed", status.IsCompressed.ToString()),
            ("TTL Limit", ttlLimit),
            (string.Empty, string.Empty),
            // State.Messages counts every historical revision, not distinct live keys - labeled
            // "Entries" rather than "Keys" so it isn't read as a live-key count.
            ("Entries", state.Messages.ToString()),
            ("Bytes", state.Bytes.ToString()),
            ("History", config.MaxMsgsPerSubject.ToString()),
            ("Max Age", config.MaxAge.ToString()),
        ];
    }
}
