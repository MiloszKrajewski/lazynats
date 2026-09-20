using lazynats.Core.Payloads;

namespace lazynats.Values;

// CreateKeyDialog's own result type - mirrors NewBucketOptions' split from the wire shape, though
// here there's no separate wire type to translate into: Name/PayloadType/Value are written
// directly via INatsKVStore.PutAsync(Name, PayloadEncoding.ToBytes(PayloadType, Value)) in
// ValuesTab.
internal sealed record NewKeyOptions(string Name, PayloadType PayloadType, string Value);
