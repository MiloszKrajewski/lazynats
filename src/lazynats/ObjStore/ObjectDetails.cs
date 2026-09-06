using lazynats.Components;
using NATS.Client.ObjectStore;
using NATS.Client.ObjectStore.Models;

namespace lazynats.ObjStore;

// Read-only readout of a single object's metadata, per nats-obj's "Object Detail Panel Shows
// Metadata Only, Never Content" requirement - never focusable, never edits anything, no BuildBody
// override (content/bytes are out of scope entirely, unlike Kv/KeyDetails' UTF-8 body). An object
// that's vanished by poll time (deleted, or otherwise not retrievable) is treated identically to
// no target being set: FetchAsync returns null, which Show(null) already renders as an empty
// panel - see design.md's "Object detail panel" decision.
internal sealed class ObjectDetails: PollingDetailsView<(string Bucket, string Name), ObjectMetadata>
{
    private readonly INatsObjContext _obj;

    public ObjectDetails(INatsObjContext obj) => _obj = obj;

    public void SetTarget(string? bucket, string? name)
    {
        if (bucket is not null && name is not null) SetPollTarget((bucket, name)); else ClearPollTarget();
    }

    protected override async Task<ObjectMetadata?> FetchAsync((string Bucket, string Name) target)
    {
        var store = await _obj.GetObjectStoreAsync(target.Bucket);
        try {
            return await store.GetInfoAsync(target.Name, showDeleted: false);
        } catch (NatsObjNotFoundException) {
            // Unlike KV's TryGetEntryAsync, GetInfoAsync throws rather than returning a
            // non-throwing result - only this specific "not found" exception is swallowed (per
            // design.md's Risks section), any other exception propagates to
            // PollingDetailsView.FetchInternalAsync's own outer catch.
            return null;
        }
    }

    protected override (string Label, string Value)[] BuildRows(ObjectMetadata metadata) => [
        ("Name", metadata.Name),
        ("Description", metadata.Description ?? "(none)"),
        ("Size", $"{metadata.Size} bytes"),
        ("Chunks", metadata.Chunks.ToString()),
        ("Digest", metadata.Digest ?? "(none)"),
        ("Modified", metadata.MTime.ToString()),
    ];
}
