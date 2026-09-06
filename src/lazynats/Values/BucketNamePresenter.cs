using lazynats.Components;

namespace lazynats.Values;

internal sealed class BucketNamePresenter: IValuePresenter<KvBucketItem>
{
    public string Format(KvBucketItem value) => value.Name;
}
