using System.Reactive.Disposables;
using System.Reactive.Linq;
using lazynats.Core;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Components;

// Shared plumbing behind StreamDetails/ConsumerDetails (and the future KV/OBJ detail panes):
// Show/SetActive wiring, the lazily-started active-gated poll pipeline, and the label:value
// OnDrawingContent renderer. FetchAsync/BuildRows stay abstract - see
// openspec/changes/extract-drillable-list-base/design.md.
//
// Target presence is tracked via a separate bool rather than a nullable TTarget: an unconstrained
// TTarget? doesn't erase to Nullable<TTarget> for a value-type TTarget (e.g. ConsumerDetails'
// (string, string) tuple) the way a concrete `(string, string)?` field would, so SetPollTarget/
// ClearPollTarget stand in for what a single nullable-target setter can't express generically.
// Each subclass's public SetTarget (whose shape differs - a single name vs. a stream/consumer
// pair) translates into these.
internal abstract class PollingDetailsView<TTarget, TInfo>: View
{
    private static readonly Color ValueColor = new(255, 255, 255);

    private (string Label, string Value)[] _rows = [];
    private string? _body;
    private TTarget _target = default!;
    private bool _hasTarget;
    private bool _active;
    private IDisposable? _subscription;

    public event Action<string>? Error;

    protected PollingDetailsView() => CanFocus = false;

    // Both existing subclasses already use 3s. Null means polling is disabled entirely - no
    // Observable.Interval is ever started, so a subclass that always returns null issues no
    // outbound calls ever, while still getting the label:value rendering and Show/SetActive
    // wiring for free.
    protected virtual TimeSpan? PollInterval => TimeSpan.FromSeconds(3);

    protected abstract Task<TInfo?> FetchAsync(TTarget target);
    protected abstract (string Label, string Value)[] BuildRows(TInfo info);

    // Optional large body rendered below the header rows, filling the rest of the pane - e.g. a
    // KV key's decoded value. Default is no body at all, so subclasses that don't override this
    // (StreamDetails, ConsumerDetails) render exactly as before.
    protected virtual string? BuildBody(TInfo info) => null;

    // Null (nothing highlighted, e.g. an empty list) clears the panel rather than leaving stale
    // content - per "Show and Clear".
    public void Show(TInfo? info)
    {
        _rows = info is null ? [] : BuildRows(info);
        _body = info is null ? null : BuildBody(info);
        SetNeedsDraw();
    }

    // Re-points which target the poll pipeline refetches; does not itself trigger a fetch or
    // touch the currently-shown rows - callers pair this with an immediate Show() for instant
    // feedback on highlight change.
    protected void SetPollTarget(TTarget target)
    {
        _target = target;
        _hasTarget = true;
    }

    protected void ClearPollTarget()
    {
        _target = default!;
        _hasTarget = false;
    }

    // Fetches the current target immediately, bypassing both the active gate and the poll
    // interval - for a subclass whose paired list has nothing to Show() instantly on a highlight
    // change (e.g. KeyDetails - the key list only carries bare names, unlike Stream/Consumer/
    // Bucket, which already have full info cached in their list item). No-op with no target set.
    public void RefreshNow()
    {
        if (_hasTarget) _ = FetchAndShowAsync(_target);
    }

    private async Task FetchAndShowAsync(TTarget target)
    {
        if (await FetchInternalAsync(target) is not { } info) return;

        // The target may have moved on while this was in flight (e.g. rapid highlight changes) -
        // only apply a result that's still current, same staleness guard KvTab's list refreshes
        // use.
        if (_hasTarget && EqualityComparer<TTarget>.Default.Equals(_target, target)) Show(info);
    }

    // Gates the poll pipeline's Where(...) - false means every tick is a genuine no-op (no fetch
    // issued at all), not just a discarded result. The underlying subscription is created once,
    // lazily, the first time this is called with true (by which point App is guaranteed to be
    // available) and is never recreated - only Dispose() tears it down.
    public void SetActive(bool active)
    {
        _active = active;
        _subscription ??= StartPolling();
    }

    private IDisposable StartPolling()
    {
        if (PollInterval is not { } interval) return Disposable.Empty;

        return Observable.Interval(interval)
            .Where(_ => _active && _hasTarget)
            .SelectAsync(_ => FetchInternalAsync(_target))
            .Where(info => info is not null)
            .ObserveOnApp(App!)
            .Subscribe(Show);
    }

    // Catches internally rather than via a downstream Catch operator: an OnError here would
    // propagate through SelectAsync's Concat and terminate the whole pipeline (including the
    // Interval timer) permanently after a single failure - see
    // openspec/changes/add-consumer-drilldown for the write-up. Returning null and filtering it
    // out keeps the pipeline alive for the next tick.
    private async Task<TInfo?> FetchInternalAsync(TTarget target)
    {
        try {
            return await FetchAsync(target);
        } catch (Exception ex) {
            Error?.Invoke(ex.Message);
            return default;
        }
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        if (_rows.Length == 0 && _body is null) return true;

        var labelAttribute = GetAttributeForRole(VisualRole.Normal);
        var valueAttribute = new Attribute(ValueColor, labelAttribute.Background);

        if (_rows.Length > 0) {
            var labelWidth = _rows.Where(row => row.Label.Length > 0).Max(row => row.Label.Length);

            for (var row = 0; row < _rows.Length; row++) {
                var (label, value) = _rows[row];
                if (label.Length == 0) continue;

                Move(0, row);
                SetAttribute(labelAttribute);
                AddStr($"{label.PadLeft(labelWidth)}: ");
                SetAttribute(valueAttribute);
                AddStr(value);
            }
        }

        // Body starts one row below the header rows (blank separator), or at row 0 if there are
        // no header rows at all - clipped to whatever height remains, never scrolled.
        if (_body is not null) {
            var bodyStartRow = _rows.Length == 0 ? 0 : _rows.Length + 1;
            SetAttribute(valueAttribute);
            var lines = _body.Split('\n');
            for (var i = 0; i < lines.Length; i++) {
                var row = bodyStartRow + i;
                if (row >= Viewport.Height) break;
                Move(0, row);
                AddStr(lines[i].TrimEnd('\r'));
            }
        }

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _subscription?.Dispose();
        base.Dispose(disposing);
    }
}
