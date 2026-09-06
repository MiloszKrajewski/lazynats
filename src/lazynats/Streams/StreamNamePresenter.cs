using lazynats.Components;
using NATS.Client.JetStream.Models;

namespace lazynats.Streams;

internal sealed class StreamNamePresenter: IValuePresenter<StreamInfo>
{
    public string Format(StreamInfo value) => value.Config.Name ?? "(unnamed)";
}
