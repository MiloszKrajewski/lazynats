using lazynats.Components;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace lazynats.Streams;

// Read-only readout of a single stream's config + state, per nats-streams' "Stream Detail Panel"
// requirement - never focusable, never edits anything.
internal sealed class StreamDetails: PollingDetailsView<string, StreamInfo>
{
    private readonly INatsJSContext _jetStream;

    public StreamDetails(INatsJSContext jetStream) => _jetStream = jetStream;

    public void SetTarget(string? name)
    {
        if (name is not null) SetPollTarget(name); else ClearPollTarget();
    }

    protected override async Task<StreamInfo?> FetchAsync(string name) =>
        (await _jetStream.GetStreamAsync(name)).Info;

    protected override (string Label, string Value)[] BuildRows(StreamInfo info)
    {
        var config = info.Config;
        var state = info.State;
        var subjects = config.Subjects is { Count: > 0 } list ? string.Join(", ", list) : "(none)";

        return [
            ("Name", config.Name ?? "(unnamed)"),
            ("Subjects", subjects),
            ("Retention", config.Retention.ToString()),
            ("Max Messages", config.MaxMsgs.ToString()),
            ("Max Bytes", config.MaxBytes.ToString()),
            ("Max Age", config.MaxAge.ToString()),
            ("Replicas", config.NumReplicas.ToString()),
            (string.Empty, string.Empty),
            ("Messages", state.Messages.ToString()),
            ("Bytes", state.Bytes.ToString()),
            ("First Seq", state.FirstSeq.ToString()),
            ("Last Seq", state.LastSeq.ToString()),
            ("Consumers", state.ConsumerCount.ToString()),
        ];
    }
}
