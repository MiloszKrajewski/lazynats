using lazynats.Components;
using NATS.Client.JetStream.Models;

namespace lazynats.Objects;

internal sealed class BucketNamePresenter: IValuePresenter<StreamInfo>
{
    public string Format(StreamInfo value) => BucketName.From(value);
}
