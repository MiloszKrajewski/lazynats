using System.Text;
using lazynats.Components;
using NATS.Client.KeyValueStore;

namespace lazynats.Values;

// Read-only readout of a single key's metadata + value, per nats-kv's "Key Detail Panel"
// requirement - this pane is never itself focusable or editable (same as BucketDetails).
// Creating/editing/deleting a key is triggered from KeyListView's Ctrl+N/E/D instead (see ValuesTab),
// which does its own fresh fetch rather than reusing whatever this pane last polled - see
// design.md's "Printable-text guard" decision in add-kv-key-crud. A key that's vanished by poll
// time (deleted, purged, or otherwise not retrievable) is treated identically to no target being set:
// FetchAsync returns null, which Show(null) already renders as an empty panel - see design.md's
// "Deleted/missing key" decision.
internal sealed class KeyDetails: PollingDetailsView<(string Bucket, string Key), KeyDetails.Entry>
{
    // NatsKVEntry<T> is a value type - PollingDetailsView's unconstrained `TInfo?` return only
    // erases consistently with a reference-type TInfo (both existing subclasses use one), so a
    // struct TInfo hits a base/override return-type mismatch (CS0508). This record class is
    // purely that struct-to-reference-type shim, not a "deleted" indicator - a missing key still
    // just produces a null Entry, same as any other PollingDetailsView subclass's null.
    public sealed record Entry(string Key, ulong Revision, DateTimeOffset Created, NatsKVOperation Operation, byte[] Value);

    private readonly INatsKVContext _kv;

    public KeyDetails(INatsKVContext kv) => _kv = kv;

    public void SetTarget(string? bucket, string? key)
    {
        if (bucket is not null && key is not null) SetPollTarget((bucket, key)); else ClearPollTarget();
    }

    protected override async Task<Entry?> FetchAsync((string Bucket, string Key) target)
    {
        var store = await _kv.GetStoreAsync(target.Bucket);
        var result = await store.TryGetEntryAsync<byte[]>(target.Key);
        if (!result.Success) return null;

        var entry = result.Value;
        return new Entry(entry.Key, entry.Revision, entry.Created, entry.Operation, entry.Value ?? []);
    }

    protected override (string Label, string Value)[] BuildRows(Entry entry) => [
        ("Key", entry.Key),
        ("Revision", entry.Revision.ToString()),
        ("Created", entry.Created.ToString()),
        ("Operation", entry.Operation.ToString()),
        ("Size", $"{entry.Value.Length} bytes"),
    ];

    protected override string? BuildBody(Entry entry) => Encoding.UTF8.GetString(entry.Value);
}
