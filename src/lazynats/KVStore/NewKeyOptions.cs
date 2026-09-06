namespace lazynats.KVStore;

// CreateKeyDialog's own result type - mirrors NewBucketOptions' split from the wire shape, though
// here there's no separate wire type to translate into: Name/Value are written directly via
// INatsKVStore.PutAsync(Name, Encoding.UTF8.GetBytes(Value)) in KvTab.
internal sealed record NewKeyOptions(string Name, string Value);
