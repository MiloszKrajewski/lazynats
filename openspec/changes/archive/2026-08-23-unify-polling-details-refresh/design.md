## Context

`PollingDetailsView<TTarget, TInfo>` (`src/lazynats/Components/PollingDetailsView.cs`) backs
`StreamDetails`, `ConsumerDetails`, `BucketDetails`, and `KeyDetails`. Today it has two disjoint
refresh mechanisms:

- A lazily-started `Observable.Interval` poll pipeline (`SelectAsync` = `Select`+`Concat`),
  gated by `_active && _hasTarget`, reading the current `_target` field at each tick.
- A one-off `RefreshNow()`/`FetchAndShowAsync()` escape hatch, added only for `KeyDetails`
  (whose paired key list has no cached per-row data to `Show()` instantly on highlight change),
  with its own hand-rolled `EqualityComparer<TTarget>` staleness check.

This asymmetry means `StreamDetails`/`ConsumerDetails`/`BucketDetails` only refresh on target
change if the poll interval happens to land, while `KeyDetails` refreshes immediately — an
inconsistency with no behavioral justification (cached list data can be just as stale for any of
them). It also leaves a latent gap: an in-flight poll-tick fetch for a since-abandoned target has
no staleness guard at all, unlike `RefreshNow`'s ad hoc one.

See `proposal.md` for the motivating problem and `openspec/specs/polling-details/spec.md`'s
"Immediate Fetch On Demand" requirement (currently scoped to the no-cached-data case) for the
delta this change makes to the modified capability.

## Goals / Non-Goals

**Goals:**
- Every `PollingDetailsView` subclass fetches its current target immediately on target change,
  unconditionally — not only when there's nothing cached to show.
- Rapid successive target changes (e.g. holding an arrow key through a list) coalesce into a
  single fetch for the final target, via a fixed, easily-tunable debounce window.
- One flow-control mechanism decides "is this result still relevant" for *both* the
  target-change trigger and the poll-interval trigger, replacing two separate, inconsistent
  staleness stories with one.
- `RefreshNow()`/`FetchAndShowAsync()` and their manual staleness check are deleted; no subclass
  or `*Tab` caller needs to know about an "immediate fetch" special case any more.

**Non-Goals:**
- Changing `PollInterval` (stays a `virtual TimeSpan?`, per-subclass, unchanged).
- Making the new debounce window configurable per subclass — it is a single shared constant
  (explicit choice, see Decisions).
- Changing `AsyncExtensions.SelectAsync`'s public shape or behavior for other/future consumers —
  it simply becomes unused by `PollingDetailsView` (its own comment already frames it as generic,
  reusable plumbing, not owned by this view).
- Changing `Show`/`BuildRows`/`BuildBody`/`OnDrawingContent` — the rendering contract is untouched.
- Adding a "refreshing..." UI affordance — no requirement calls for one.

## Decisions

**1. Model target state as a `BehaviorSubject<(bool HasTarget, TTarget Target)>`, not a plain
`Subject`.**
The poll subscription is started lazily (`_subscription ??= StartPolling()`, first call to
`SetActive(true)`). A plain `Subject` drops any `OnNext` pushed before a subscriber exists — if a
`*Tab` ever calls `SetPollTarget` before its first `SetActive(true)` (e.g. a future call site
that doesn't mirror `KvTab`'s current focus-then-load ordering), that target change would be
silently lost. A `BehaviorSubject` always has a current value and replays it to the first
subscriber, removing that ordering fragility entirely — and means entering a tab whose target was
already set before focus arrived also gets an immediate refresh, rather than waiting on the next
poll tick. Seeded with `(false, default!)`.

**2. `(bool HasTarget, TTarget Target)` tuple, not `Subject<TTarget?>`.**
The file's existing top-of-class comment already explains why: an unconstrained `TTarget?`
doesn't erase to `Nullable<TTarget>` for a value-type `TTarget` (e.g. `ConsumerDetails`' and
`KeyDetails`' tuple targets) the way a concrete nullable field would. The tuple sidesteps that the
same way `_hasTarget`/`_target` already do, just moved onto the Subject's element type.

**3. Retire the `_target`/`_hasTarget` fields; `_targetChanges.Value` becomes the single source
of truth.**
With `RefreshNow` gone, the only other reader of "current target" was the poll-interval branch.
It reads `_targetChanges.Value` directly instead. One less place for the two to drift out of
sync.

**4. Throttle only target-set changes; a clear bypasses the throttle and cancels in-flight work
via `Observable.Empty`, not `Observable.Return(null)`.**
```
var toTarget = _targetChanges.Where(t => t.HasTarget).Throttle(SwitchDebounce);
var toClear  = _targetChanges.Where(t => !t.HasTarget);
var toPoll   = Observable.Interval(interval).Where(_ => _active && _targetChanges.Value.HasTarget)
                   .Select(_ => _targetChanges.Value);

Observable.Merge(toTarget, toClear, toPoll)
    .Select(t => t.HasTarget
        ? Observable.FromAsync(() => FetchInternalAsync(t.Target))
        : Observable.Empty<TInfo?>())
    .Switch()
    .ObserveOnApp(App!)
    .Subscribe(Show)
```
Two deliberate asymmetries here:
- Poll ticks are already rate-limited by `PollInterval` (typically 3s) and must not be debounced
  away; only the high-frequency target-*set* stream needs smoothing.
- A clear (`ClearPollTarget`) is **not** throttled and maps to `Observable.Empty`, not
  `Observable.Return(null)`. Debouncing a clear would let a stale in-flight fetch from the
  *previous* target resolve and flash back onto an already-cleared panel before the throttle
  window elapses and `Switch()` finally cancels it. `Observable.Empty` reaches `Switch()`
  immediately, cancelling/discarding whatever fetch was in flight, without itself emitting a
  value — so it does *not* call `Show(null)`. Clearing the visible display stays the caller's
  synchronous `Show(null)` responsibility, exactly as today (see "Show and Clear" — unchanged).

**5. `SwitchDebounce` is a single `private static readonly TimeSpan` (100ms), not a per-subclass
`virtual` like `PollInterval`.**
Explicit simplicity-over-flexibility choice: one shared, easily-adjusted constant. Can become
`virtual` later if a specific panel ever needs a different window — no current subclass does.

**6. `Select(...).Switch()` replaces both `SelectAsync`/`Concat` (poll pipeline) and
`RefreshNow`'s manual `EqualityComparer` check.**
`Switch()` unsubscribes the previous inner fetch the instant a newer one is projected, so a
superseded result (from either trigger) is never observed downstream — no equality check needed.
Alternative considered: keep two separate pipelines (poll: unchanged `SelectAsync`/`Concat`;
target-change: new `Throttle`+`Switch`, added alongside). Rejected — it keeps the split-contract
problem alive in a different shape (two disjoint fetch mechanisms racing to call `Show()`) and
duplicates the staleness story instead of unifying it, which was the point of this change.

## Risks / Trade-offs

- **A merged pipeline changes poll-tick/target-change interleaving relative to today's strictly
  ordered `Concat`.** → Not a real risk: `Switch()`'s "latest wins" semantics are exactly what's
  wanted here, and no requirement depends on poll-triggered fetches being ordered relative to
  target changes.
- **Removing `RefreshNow`/`FetchAndShowAsync` changes `PollingDetailsView`'s internal member
  surface.** → Low blast radius: both are `internal`/used only within this project, and
  `KvTab.OnKeyHighlightChanged` is the only call site (per proposal.md's Impact section).
- **`BehaviorSubject` retains the last-pushed target for the view's lifetime, including while
  inactive.** → Same lifetime as today's `_target`/`_hasTarget` fields; `Dispose(bool)` must also
  dispose `_targetChanges` alongside `_subscription` (it currently only disposes `_subscription`).
- **Fixed, non-per-subclass debounce window.** → Deliberate (Decision 5); revisit only if a
  concrete panel needs a different value.
- **Clear bypassing the throttle while target-set changes are throttled is an asymmetry a future
  reader could "simplify" away, reintroducing the stale-flash-after-clear bug.** → Documented
  inline at the `Merge` call site (Decision 4) and here; the two branches are combined explicitly
  rather than via one uniform `Throttle`, so removing the asymmetry requires touching visible,
  commented code rather than an easy oversight.

## Migration Plan

No data model or persisted state is involved — this is a same-repo, uncommitted-work refactor of
`PollingDetailsView` plus one caller update in `KvTab`. Suggested edit order:

1. Rewrite `PollingDetailsView.cs`'s target-state fields and pipeline; remove `RefreshNow`/
   `FetchAndShowAsync`.
2. Update `KvTab.OnKeyHighlightChanged` to drop the now-unnecessary explicit `RefreshNow()` call.
3. Manually verify via the tmux-driven flow (`doc/UI.md`/CLAUDE.md's testing approach) across the
   Streams, Consumers, and KV tabs: highlight-change now shows fresh data promptly on all of them,
   and rapid arrow-key scrolling doesn't visibly stutter or flood the server.

No rollback concerns beyond reverting the commit — no schema, no external state.

## Open Questions

None blocking. Whether `SwitchDebounce` should ever become per-subclass `virtual` is deferred
until a concrete panel needs it (see Decision 5 / Non-Goals).
