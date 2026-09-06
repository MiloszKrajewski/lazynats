## Context

`src/lazynats/Streams/` has two nearly-identical pairs of views built for the stream/consumer
drill-down (`openspec/changes/archive/2026-08-21-add-consumer-drilldown`):

- `StreamListView` / `ConsumerListView` — same `PresenterListDataSource` wiring, same empty-hint
  label mechanics, same `Background` scheme handling, same Ctrl+R refresh binding, same
  identity-preserving `ReplaceItems` shape. They differ in item type, presenter, empty-hint text,
  the identity accessor used for highlight preservation, and which extra key bindings each level
  adds (`StreamListView` binds Enter→Descend; `ConsumerListView` binds Esc/Backspace→Ascend).
- `StreamDetails` / `ConsumerDetails` — same `Show`/`SetTarget`/`SetActive` wiring, same
  lazily-started `Observable.Interval`-based active-gated poll pipeline (built on the
  `reactive-polling` capability's `SelectAsync`/`ObserveOnApp` operators), same label:value
  `OnDrawingContent` renderer. They differ in the target key shape (a single stream name vs. a
  `(stream, consumer)` tuple), the fetch call, and the row content.

`doc/UI.md` plans two more management tabs — KV stores (bucket → keys) and OBJ stores (bucket →
files) — that need the same two-level, list-plus-polling-details shape. Refactoring now, while
only one pair exists, is cheaper than doing it after three more copies exist.

`Components/ListEditorView.cs` has a similar-looking foundation (empty-hint label, `Background`,
`PresenterListDataSource`) but bakes in Ctrl+N/E/D create/edit/delete machinery the drillable
lists explicitly don't want. Unifying it with the new base is deliberately out of scope here.

## Goals / Non-Goals

**Goals:**
- Extract the byte-identical plumbing from `StreamListView`/`ConsumerListView` into an abstract
  `DrillableListView<T>`, and from `StreamDetails`/`ConsumerDetails` into an abstract
  `PollingDetailsView<TTarget, TInfo>`.
- Keep all per-level behavior (which keys navigate, which fetch call runs, which rows render) in
  the concrete subclass — list behavior is driven by list *type*, not item type.
- Preserve `nats-streams`'s existing behavior exactly; this is a refactor, not a feature change.
- Leave a `PollInterval => null` extension point so a future non-polling details pane (e.g. a "go
  in" on a single live-feed message, mentioned in `doc/UI.md`) can reuse the label:value rendering
  shape without inheriting a poll timer it would never use.

**Non-Goals:**
- No KV or OBJ tabs in this change — this only prepares the foundation they'll build on.
- No polymorphic-item / stack-based navigator (an item type driving its own children and detail
  rendering). Explicitly deferred — current behavior differs by list *level*, not by item, and
  going further now would be speculative.
- No unification with `ListEditorView<T>`. It keeps its own separate plumbing.
- No change to `StreamsTab`'s two-slot orchestration (`Descend`/`Ascend`, visibility toggling,
  `RefreshListAsync`/`RefreshConsumerListAsync`) — still hand-rolled per tab.

## Decisions

**Base classes live in `Components/`, not `Streams/`.** They're not stream-specific — KV/OBJ will
import them from the same shared location as `EditFrame`/`ListEditorView`/`IValuePresenter`/
`PresenterListDataSource`.

**`DrillableListView<T>` owns only what's identical today; navigation commands stay in the
subclass.** The base binds Ctrl+R → `Command.Refresh` → `RefreshRequested` (identical across both
existing views) and nothing else. `StreamListView` adds its own Enter→Descend binding;
`ConsumerListView` adds its own Esc/Backspace→Ascend bindings. This is deliberate, per the
"list-level-driven behavior" decision: a future KV-key list binding its own Ctrl+N, for instance,
should not require touching the base class.

**Identity is a required override, not a constructor-injected delegate.** `GetIdentity(T item)` is
abstract (`Config.Name` for `StreamInfo`, `Name` for `ConsumerInfo`) rather than a `Func<T, string>`
passed into the constructor — consistent with how `Presenter` and `EmptyHintText` are also
overrides rather than constructor parameters, keeping the construction signature uniform across
subclasses.

**`EmptyHintText` is abstract, not virtual-with-default.** Unlike `ListEditorView.EmptyHint`
(which defaults to a generic message), both existing drillable lists already define a specific,
non-generic hint (`"No streams — Ctrl+R to refresh"` / `"No consumers — Ctrl+R to refresh"`).
There's no observed case of "forgot to override, fell back to generic," so nothing is gained by a
default here, and an abstract member makes a missing override a compile error instead of a silent
generic string.

**`PollingDetailsView<TTarget, TInfo>.PollInterval` is virtual, typed `TimeSpan?`, with a `3s`
default.** Both existing subclasses already use `3s` — making it the default rather than requiring
every subclass to restate it. A `null` return disables polling entirely: `StartPolling` skips
creating the `Observable.Interval` altogether rather than starting an idle timer that never fires
a fetch, so a subclass that always returns `null` issues no outbound calls and starts no timer,
while still getting the label:value rendering and `Show`/`SetTarget` wiring for free.

**`FetchAsync`/`BuildRows` stay abstract; the `try/catch`-and-report-via-`Error` shape stays in the
base.** Both existing `FetchAsync` implementations catch, call `Error?.Invoke(ex.Message)`, and
return `null` — for the same reason documented in both files (an unhandled `OnError` would
terminate the whole Rx pipeline via `SelectAsync`'s `Concat`). Moving that catch into the base
around the abstract fetch call removes the duplicated try/catch from both subclasses while keeping
the actual server call (which differs) abstract.

## Risks / Trade-offs

- [Risk] Generic base classes over-fit to today's two call sites and need reshaping once KV/OBJ
  arrive → Mitigation: both bases only claim what's *already* byte-identical between the existing
  two subclasses; nothing speculative is added on the bet that KV/OBJ will need it.
- [Risk] Moving the try/catch into `PollingDetailsView` changes where `Error` is raised from →
  Mitigation: behavior (message content, timing, pipeline continuation) is unchanged; only the
  code's location moves, verified by the existing `nats-streams` spec scenarios still holding
  after refactor.

## Migration Plan

1. Add `Components/DrillableListView.cs`; refactor `StreamListView` then `ConsumerListView` onto
   it, keeping each one's existing public surface (events, `SelectedStream`/`SelectedConsumer`,
   `Shortcuts`) unchanged so `StreamsTab` needs no changes.
2. Add `Components/PollingDetailsView.cs`; refactor `StreamDetails` then `ConsumerDetails` onto
   it, same constraint — `StreamsTab`'s calls to `Show`/`SetTarget`/`SetActive` and the `Error`
   event stay as-is.
3. Run the app (tmux, per `CLAUDE.md`) against a real NATS server and manually walk the
   `nats-streams` spec's scenarios (list, drill down, drill back up, Ctrl+R at both levels, detail
   polling at both levels) to confirm no observable behavior changed.
4. No rollback complexity beyond a normal revert — no data migration, no persisted state, no
   public API consumed outside this project.

## Open Questions

None outstanding — scope, naming, and placement were confirmed in exploration before this
proposal was written.
