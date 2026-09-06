using System.Reactive.Linq;
using lazynats.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Streams;

// Mirrors StreamDetails deliberately closely (see openspec/changes/add-consumer-drilldown/
// design.md decision 1 and 2) - same hand-drawn label/value layout, same lazily-started,
// always-running, SetActive-gated poll pipeline.
internal sealed class ConsumerDetails: View
{
    private static readonly Color ValueColor = new(255, 255, 255);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    private readonly INatsJSContext _jetStream;
    private (string Label, string Value)[] _rows = [];
    private (string Stream, string Consumer)? _target;
    private bool _active;
    private IDisposable? _subscription;

    public event Action<string>? Error;

    public ConsumerDetails(INatsJSContext jetStream)
    {
        CanFocus = false;
        _jetStream = jetStream;
    }

    // Null (no consumer highlighted, e.g. an empty list) clears the panel rather than leaving
    // stale content - per "No consumer highlighted" scenario.
    public void Show(ConsumerInfo? info)
    {
        _rows = info is null ? [] : BuildRows(info);
        SetNeedsDraw();
    }

    public void SetTarget(string? stream, string? consumer) =>
        _target = stream is not null && consumer is not null ? (stream, consumer) : null;

    public void SetActive(bool active)
    {
        _active = active;
        _subscription ??= StartPolling();
    }

    private IDisposable StartPolling() =>
        Observable.Interval(PollInterval)
            .Where(_ => _active && _target is not null)
            .SelectAsync(_ => FetchAsync(_target!.Value))
            .Where(info => info is not null)
            .ObserveOnApp(App!)
            .Subscribe(info => Show(info));

    // Catches internally rather than via a downstream Catch operator: an OnError here would
    // propagate through SelectAsync's Concat and terminate the whole pipeline (including the
    // Interval timer) permanently after a single failure - see
    // openspec/changes/add-consumer-drilldown for the write-up. Returning null and filtering it
    // out keeps the pipeline alive for the next tick.
    private async Task<ConsumerInfo?> FetchAsync((string Stream, string Consumer) target)
    {
        try {
            return (await _jetStream.GetConsumerAsync(target.Stream, target.Consumer)).Info;
        } catch (Exception ex) {
            Error?.Invoke(ex.Message);
            return null;
        }
    }

    private static (string Label, string Value)[] BuildRows(ConsumerInfo info)
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

    protected override bool OnDrawingContent(DrawContext? context)
    {
        if (_rows.Length == 0) return true;

        var labelWidth = _rows.Where(row => row.Label.Length > 0).Max(row => row.Label.Length);
        var labelAttribute = GetAttributeForRole(VisualRole.Normal);
        var valueAttribute = new Attribute(ValueColor, labelAttribute.Background);

        for (var row = 0; row < _rows.Length; row++) {
            var (label, value) = _rows[row];
            if (label.Length == 0) continue;

            Move(0, row);
            SetAttribute(labelAttribute);
            AddStr($"{label.PadLeft(labelWidth)}: ");
            SetAttribute(valueAttribute);
            AddStr(value);
        }

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _subscription?.Dispose();
        base.Dispose(disposing);
    }
}
