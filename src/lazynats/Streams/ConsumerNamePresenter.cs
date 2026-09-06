using lazynats.Components;
using NATS.Client.JetStream.Models;

namespace lazynats.Streams;

internal sealed class ConsumerNamePresenter: IValuePresenter<ConsumerInfo>
{
    public string Format(ConsumerInfo value) => value.Name ?? "(unnamed)";
}
