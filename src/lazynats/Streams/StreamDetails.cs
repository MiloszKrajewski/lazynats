using System.Reactive.Linq;
using lazynats.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Streams;

// Read-only readout of a single stream's config + state, per nats-streams' "Stream Detail Panel"
// requirement - never focusable, never edits anything. Hand-drawn rather than a Label: labels are
// right-aligned into a shared ':' column and rendered in the ambient (dimmer) foreground, while
// values are bright white, so the two visually separate at a glance - neither is achievable with
// a single-Scheme Label.
//
// Owns its own poll pipeline (see openspec/changes/add-consumer-drilldown/design.md decision 2-3)
// rather than being polled from outside: the timer runs for the view's whole lifetime, and
// SetActive(false) (driven by StreamsTab, covering both "is the consumer level hidden" and "is
// the tab itself not focused") makes each tick a no-op rather than stopping/restarting the timer.
internal sealed class StreamDetails: View
{
    private static readonly Color ValueColor = new(255, 255, 255);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    private readonly INatsJSContext _jetStream;
    private (string Label, string Value)[] _rows = [];
    private string? _target;
    private bool _active;
    private IDisposable? _subscription;

    public event Action<string>? Error;

    public StreamDetails(INatsJSContext jetStream)
    {
        CanFocus = false;
        _jetStream = jetStream;
    }

    // Null (no stream highlighted, e.g. an empty list) clears the panel rather than leaving
    // stale content - per "No stream highlighted" scenario.
    public void Show(StreamInfo? info)
    {
        _rows = info is null ? [] : BuildRows(info);
        SetNeedsDraw();
    }

    // Re-points which stream the poll pipeline targets; does not itself trigger a fetch or
    // touch the currently-shown rows - callers pair this with an immediate Show() for instant
    // feedback on highlight change, same as before this was split out of StreamsTab.
    public void SetTarget(string? name) => _target = name;

    // Gates the poll pipeline's Where(...) - false means every tick is a genuine no-op (no NATS
    // call issued at all), not just a discarded result. The underlying subscription is created
    // once, lazily, the first time this is called with true (by which point App is guaranteed to
    // be available, since StreamsTab only calls this once the tab itself has focus) and is never
    // recreated - only Dispose() tears it down.
    public void SetActive(bool active)
    {
        _active = active;
        _subscription ??= StartPolling();
    }

    private IDisposable StartPolling() =>
        Observable.Interval(PollInterval)
            .Where(_ => _active && _target is not null)
            .SelectAsync(_ => FetchAsync(_target!))
            .Where(info => info is not null)
            .ObserveOnApp(App!)
            .Subscribe(info => Show(info));

    // Catches internally rather than via a downstream Catch operator: an OnError here would
    // propagate through SelectAsync's Concat and terminate the whole pipeline (including the
    // Interval timer) permanently after a single failure - see
    // openspec/changes/add-consumer-drilldown for the write-up. Returning null and filtering it
    // out keeps the pipeline alive for the next tick.
    private async Task<StreamInfo?> FetchAsync(string name)
    {
        try {
            return (await _jetStream.GetStreamAsync(name)).Info;
        } catch (Exception ex) {
            Error?.Invoke(ex.Message);
            return null;
        }
    }

    private static (string Label, string Value)[] BuildRows(StreamInfo info)
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
