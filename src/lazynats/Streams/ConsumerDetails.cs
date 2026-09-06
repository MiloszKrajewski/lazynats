using lazynats.Components;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace lazynats.Streams;

// Mirrors StreamDetails deliberately closely (see openspec/changes/add-consumer-drilldown/
// design.md decision 1 and 2) - same hand-drawn label/value layout, same lazily-started,
// always-running, SetActive-gated poll pipeline (both now via PollingDetailsView).
internal sealed class ConsumerDetails: PollingDetailsView<(string Stream, string Consumer), ConsumerInfo>
{
    private readonly INatsJSContext _jetStream;

    public ConsumerDetails(INatsJSContext jetStream) => _jetStream = jetStream;

    public void SetTarget(string? stream, string? consumer)
    {
        if (stream is not null && consumer is not null) SetPollTarget((stream, consumer));
        else ClearPollTarget();
    }

    protected override async Task<ConsumerInfo?> FetchAsync((string Stream, string Consumer) target) =>
        (await _jetStream.GetConsumerAsync(target.Stream, target.Consumer)).Info;

    protected override (string Label, string Value)[] BuildRows(ConsumerInfo info)
    {
        var config = info.Config;

        // Depending on how a consumer was created, the server populates the singular
        // FilterSubject, the plural FilterSubjects, or (observed via the nats CLI) only the
        // plural one even for a single filter - so neither field alone is reliable. Union both,
        // rather than preferring one, in case a consumer somehow has a distinct singular value
        // alongside the plural list.
        var filters = (config.FilterSubjects ?? [])
            .Append(config.FilterSubject)
            .OfType<string>()
            .Distinct()
            .ToArray();
        var filterSubject = filters.Length > 0 ? string.Join(", ", filters) : "(none)";

        return [
            ("Name", info.Name ?? "(unnamed)"),
            ("Filter Subject", filterSubject),
            ("Ack Policy", config.AckPolicy.ToString()),
            ("Deliver Policy", config.DeliverPolicy.ToString()),
            ("Max Deliver", config.MaxDeliver.ToString()),
            ("Max Ack Pending", config.MaxAckPending.ToString()),
            (string.Empty, string.Empty),
            ("Delivered", info.Delivered.StreamSeq.ToString()),
            ("Ack Floor", info.AckFloor.StreamSeq.ToString()),
            ("Ack Pending", info.NumAckPending.ToString()),
            ("Redelivered", info.NumRedelivered.ToString()),
            ("Waiting", info.NumWaiting.ToString()),
            ("Pending", info.NumPending.ToString()),
        ];
    }
}
