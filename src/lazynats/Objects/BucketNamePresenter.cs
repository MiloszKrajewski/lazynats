using lazynats.Components;

namespace lazynats.Objects;

internal sealed class BucketNamePresenter: IValuePresenter<ObjBucketItem>
{
    public string Format(ObjBucketItem value) => value.Name;
}
