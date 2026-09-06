using lazynats.Components;
using NATS.Client.ObjectStore;

namespace lazynats.ObjStore;

// Read-only readout of a single OBJ bucket's stats, per nats-obj's "Bucket Detail Panel"
// requirement - never focusable, never edits anything. Mirrors Kv/BucketDetails deliberately
// closely (an OBJ bucket is a stream under the hood too - NatsObjStatus.Info is a StreamInfo),
// minus the KV-only TTL Limit row (NatsObjStatus has no LimitMarkerTTL equivalent).
internal sealed class BucketDetails: PollingDetailsView<string, NatsObjStatus>
{
    private readonly INatsObjContext _obj;

    public BucketDetails(INatsObjContext obj) => _obj = obj;

    public void SetTarget(string? bucket)
    {
        if (bucket is not null) SetPollTarget(bucket); else ClearPollTarget();
    }

    protected override async Task<NatsObjStatus?> FetchAsync(string bucket) =>
        await (await _obj.GetObjectStoreAsync(bucket)).GetStatusAsync();

    protected override (string Label, string Value)[] BuildRows(NatsObjStatus status)
    {
        var config = status.Info.Config;
        var state = status.Info.State;

        return [
            // status.Bucket is the raw, unstripped stream name (same caveat as NatsKVStatus.Bucket
            // - see Kv/BucketName.cs's comment), so go through BucketName.From(status.Info) instead.
            ("Bucket", BucketName.From(status.Info)),
            ("Compressed", status.IsCompressed.ToString()),
            (string.Empty, string.Empty),
            // State.Messages counts stream messages, not necessarily distinct live objects - same
            // caveat KV's "Entries" label carries, see Kv/BucketDetails.cs.
            ("Objects", state.Messages.ToString()),
            ("Bytes", state.Bytes.ToString()),
            ("Replicas", config.NumReplicas.ToString()),
            ("Max Age", config.MaxAge.ToString()),
        ];
    }
}
