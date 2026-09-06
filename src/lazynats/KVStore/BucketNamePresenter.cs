using lazynats.Components;
using NATS.Client.KeyValueStore;

namespace lazynats.KVStore;

internal sealed class BucketNamePresenter: IValuePresenter<NatsKVStatus>
{
    public string Format(NatsKVStatus value) => BucketName.From(value);
}
