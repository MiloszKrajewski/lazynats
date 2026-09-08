using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using lazynats.Core;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Components;

// Shared plumbing behind StreamDetails/ConsumerDetails/BucketDetails/KeyDetails: Show/SetActive
// wiring, the lazily-started active-gated poll+target-change pipeline, and the label:value
// OnDrawingContent renderer. FetchAsync/BuildRows stay abstract - see
// openspec/changes/extract-drillable-list-base/design.md.
//
// Target state lives in a BehaviorSubject<(bool HasTarget, TTarget Target)> rather than a
// nullable TTarget: an unconstrained TTarget? doesn't erase to Nullable<TTarget> for a value-type
// TTarget (e.g. ConsumerDetails'/KeyDetails' tuple targets) the way a concrete nullable field
// would, so the tuple's HasTarget flag stands in for what a single nullable-target field can't
// express generically. BehaviorSubject (not a plain Subject) so a target pushed before the
// lazily-started pipeline has a subscriber is never silently dropped - see
// openspec/changes/unify-polling-details-refresh/design.md decision 1. Each subclass's public
// SetTarget (whose shape differs - a single name vs. a stream/consumer pair) translates into
// SetPollTarget/ClearPollTarget.
internal abstract class PollingDetailsView<TTarget, TInfo>: View
{
    private static readonly Color ValueColor = new(255, 255, 255);

    // Debounces rapid successive target changes (e.g. holding an arrow key through a list) so
    // only the target still current once things settle triggers a fetch. Fixed for every
    // subclass, deliberately not per-subclass virtual like PollInterval - see design.md decision 5.
    private static readonly TimeSpan SwitchDebounce = TimeSpan.FromMilliseconds(100);

    private readonly BehaviorSubject<(bool HasTarget, TTarget Target)> _targetChanges = new((false, default!));

    private (string Label, string Value)[] _rows = [];
    private string? _body;
    private bool _active;
    private IDisposable? _subscription;

    public event Action<string>? Error;

    protected PollingDetailsView() => CanFocus = false;

    // Both existing subclasses already use 3s. Null means polling (and, per "Polling Can Be
    // Disabled Entirely", the target-change fetch below) is disabled entirely - no pipeline is
    // ever started, so a subclass that always returns null issues no outbound calls ever, while
    // still getting the label:value rendering and Show/SetActive wiring for free.
    protected virtual TimeSpan? PollInterval => TimeSpan.FromSeconds(3);

    protected abstract Task<TInfo?> FetchAsync(TTarget target);
    protected abstract (string Label, string Value)[] BuildRows(TInfo info);

    // Optional large body rendered below the header rows, filling the rest of the pane - e.g. a
    // KV key's decoded value. Default is no body at all, so subclasses that don't override this
    // (StreamDetails, ConsumerDetails) render exactly as before.
    protected virtual string? BuildBody(TInfo info) => null;

    // Null (nothing highlighted, e.g. an empty list) clears the panel rather than leaving stale
    // content - per "Show and Clear". Virtual so a subclass (KeyDetails) can track the
    // last-shown info itself (e.g. for a "peek deeper" dialog to reuse without a fresh fetch - see
    // openspec/changes/kv-value-peek-and-view/design.md Decision 5) without this base class
    // needing to know that concept exists.
    public virtual void Show(TInfo? info)
    {
        _rows = info is null ? [] : BuildRows(info);
        _body = info is null ? null : BuildBody(info);
        SetNeedsDraw();
    }

    // Re-points which target the pipeline tracks. Doesn't itself synchronously fetch or touch
    // currently-shown rows - callers still pair this with an immediate Show() for instant
    // feedback on highlight change - but it does schedule a debounced fetch of the new target,
    // per "Immediate Fetch On Demand".
    protected void SetPollTarget(TTarget target) => _targetChanges.OnNext((true, target));

    // Cancels any pending or in-flight fetch for the previous target immediately (not debounced -
    // see design.md decision 4) without displaying anything itself; clearing the visible content
    // stays the caller's own Show(null) responsibility, same as today.
    protected void ClearPollTarget() => _targetChanges.OnNext((false, default!));

    // Gates the pipeline's poll-tick branch - false means every tick is a genuine no-op (no fetch
    // issued at all), not just a discarded result. Target-change fetches are NOT gated by this,
    // matching the old RefreshNow's "independent of the active-gate" contract. The underlying
    // subscription is created once, lazily, the first time this is called with true (by which
    // point App is guaranteed to be available) and is never recreated - only Dispose() tears it
    // down.
    public void SetActive(bool active)
    {
        _active = active;
        _subscription ??= StartPolling();
    }

    private IDisposable StartPolling()
    {
        if (PollInterval is not { } interval) return Disposable.Empty;

        // toTarget is throttled (debounced); toClear deliberately isn't - see design.md decision
        // 4 for why debouncing a clear would let a stale in-flight fetch flash back onto an
        // already-cleared panel before the throttle window elapsed.
        var toTarget = _targetChanges.Where(t => t.HasTarget).Throttle(SwitchDebounce);
        var toClear = _targetChanges.Where(t => !t.HasTarget);
        var toPoll = Observable.Interval(interval)
            .Where(_ => _active && _targetChanges.Value.HasTarget)
            .Select(_ => _targetChanges.Value);

        return Observable.Merge(toTarget, toClear, toPoll)
            // Empty (not a Return) for the clear case: it reaches Switch() immediately,
            // cancelling/discarding whatever fetch was in flight, without itself emitting a value
            // - so no Show(null) happens here (callers already Show(null) themselves on clear).
            .Select(t => t.HasTarget
                ? Observable.FromAsync(() => FetchInternalAsync(t.Target))
                : Observable.Empty<(bool Success, TInfo? Info)>())
            .Switch()
            // Only a genuine fetch result reaches Show - including a legitimate "not found" null,
            // which must still reach Show(null) per "A Deleted Key/Object Renders As No
            // Selection". An error result (Success: false) is dropped entirely instead, so the
            // panel keeps showing whatever it last held - see FetchInternalAsync.
            .Where(result => result.Success)
            .Select(result => result.Info)
            .ObserveOnApp(App!)
            .Subscribe(Show);
    }

    // Catches internally rather than via a downstream Catch operator: an OnError here would
    // propagate through Switch and terminate the whole pipeline (including the Interval timer)
    // permanently after a single failure - see openspec/changes/add-consumer-drilldown for the
    // write-up. Success: false keeps the pipeline alive for the next tick without touching the
    // panel; Success: true (even with a null Info) is a real result and must still reach Show.
    private async Task<(bool Success, TInfo? Info)> FetchInternalAsync(TTarget target)
    {
        try {
            return (true, await FetchAsync(target));
        } catch (Exception ex) {
            Error?.Invoke(ex.Message);
            return (false, default);
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
        // no header rows at all - clipped to whatever height remains, never scrolled. Each source
        // line is character-wrapped (not word-wrapped) to the viewport's width first, so a long
        // line spills onto the following visual row(s) instead of being clipped mid-line - see
        // openspec/changes/kv-value-peek-and-view/design.md Decision 1's peek-wrap addendum. Still
        // no scrolling: once wrapped rows exhaust the remaining height, the rest is simply not drawn.
        if (_body is not null) {
            var bodyStartRow = _rows.Length == 0 ? 0 : _rows.Length + 1;
            SetAttribute(valueAttribute);
            var width = Math.Max(1, Viewport.Width);
            var row = bodyStartRow;
            foreach (var rawLine in _body.Split('\n')) {
                var line = rawLine.TrimEnd('\r');
                var offset = 0;
                do {
                    if (row >= Viewport.Height) return true;
                    var chunkLength = Math.Min(width, line.Length - offset);
                    Move(0, row);
                    AddStr(line.Substring(offset, chunkLength));
                    row++;
                    offset += chunkLength;
                } while (offset < line.Length);
            }
        }

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) {
            _subscription?.Dispose();
            _targetChanges.Dispose();
        }
        base.Dispose(disposing);
    }
}
