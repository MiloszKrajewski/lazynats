## Context

`DrillableListView<T>` (`src/lazynats/Components/DrillableListView.cs`) already centralizes list
rendering, empty-state, background, and Ctrl+R/`ReplaceItems` mechanics for six subclasses across
three tabs. What it does *not* centralize is the three recurring navigation shapes each subclass
layers on top in its own constructor — descend-on-Enter, ascend-on-Esc/Backspace, and
create/delete-on-Ctrl+N/Ctrl+D — each duplicated identically three times (see proposal.md), plus
a fourth duplication: `StreamsTab`/`KvTab` each hand-roll an index scan ("what's the neighboring
item's identity, for refocus after delete") against a collection the base view already manages
internally via `GetIdentity`/`IndexOfIdentity`.

## Goals / Non-Goals

**Goals:**
- Eliminate the three per-subclass wiring duplications and the neighbor-lookup duplication.
- Preserve every current keybinding, shortcut hint, and event-firing behavior exactly — this is a
  pure move, not a behavior change.
- Keep the three navigation shapes independently composable per subclass (no shape implies or
  excludes another), so this doesn't regress into the same "collapses at a 3rd axis" problem a
  hierarchy-level (parent/child tab) abstraction would have.

**Non-Goals:**
- Any tab-level (`StreamsTab`/`KvTab`/`ObjTab`) scaffold extraction — rejected this round (see
  proposal.md's "explicitly out of scope").
- Touching the create-dialog retry loop, the delete-confirm shell, or the `Refresh*ListAsync`
  shell — logged as future goals only.
- Enforcing mutual exclusivity between `EnableDescend`/`EnableAscend` at runtime (see Decisions).

## Decisions

### 1. Opt-in imperative helpers (`EnableDescend()` etc.), not declarative virtual flags

Considered instead: a `protected virtual bool SupportsCreateDelete => true` style flag the base
constructor checks. Rejected — `DrillableListView<T>`'s own header comment already documents why
abstract getters can't be trusted at base-constructor time ("this getter runs from the base
constructor, before a derived class's own field initializers have run" — see `Presenter`'s
static-instance workaround). An explicitly-called method avoids that ordering hazard entirely and
matches the existing pattern of subclasses configuring themselves imperatively inside their own
constructor, after `base(items)` has returned.

### 2. `Shortcuts` becomes base-composed; subclasses stop overriding it

Each `Enable*` helper needs to contribute its hint (`"Back"`, `"New"`, `"Delete"`) to `Shortcuts`.
Rather than keep the current `public override IEnumerable<ShortcutHint> Shortcuts => base.Shortcuts
.Append(...)` pattern per subclass, the base tracks which helpers were enabled (three private
bools) and composes the full list itself. This is a bonus dedup beyond what the proposal called
out: all six subclasses currently override `Shortcuts` *only* to append one of these three shapes,
so after this change none of them need the override anymore. `Shortcuts` stays `virtual` (not
sealed) so a future subclass with a genuinely novel shortcut can still override it as today.

### 3. `EnableDescend`/`EnableAscend` are not mutually exclusized at runtime

No current subclass calls both, and enforcing it (e.g. throwing if both are called) would be
speculative validation against a mistake nobody has made. If a future subclass ever needs both
bound simultaneously, that's a real, informed decision to unblock then — not a guard to add now.

### 4. `NeighborIdentity` returns `string?` (an identity), not `T?` (an item)

Matches `GetIdentity`'s existing contract and lets call sites pass the result straight into
`ReplaceItems(items, selectIdentity)`'s existing `string?` parameter with no conversion step —
`StreamsTab`/`KvTab`'s delete handlers already do exactly this today with their own
`Neighbor*Name` methods, so the call-site shape is unchanged, only the implementation moves.

## Risks / Trade-offs

- **[Risk]** Removing `ListView.KeyBindings.Remove(Key.N.WithCtrl)` from three separate
  constructors into one shared `EnableCreateDelete()` could silently change behavior if a
  subclass's own constructor ordering relative to the `Enable*` call mattered.
  → **Mitigation**: this removal only affects the Ctrl+N key on the *inner* `ListView`, which is
  private to the base and untouched by anything else in a subclass constructor — call-order
  relative to other `Enable*` calls is irrelevant since each touches disjoint state (different
  keys, different events).
- **[Risk]** A silent behavior drift (e.g. a dropped `ShortcutHint`, a keybinding on the wrong
  key) wouldn't show up in a build — Terminal.Gui key/command wiring is runtime-only.
  → **Mitigation**: after each subclass is migrated, drive it via `tmux` (per CLAUDE.md's testing
  guidance) to confirm Enter/Esc/Backspace/Ctrl+N/Ctrl+D and the status-bar shortcut hints are
  unchanged, and confirm a delete still refocuses the same neighbor it did before.

## Migration Plan

Mechanical, one piece at a time, each independently buildable/testable:
1. Add `NeighborIdentity` and the three `Enable*` helpers (plus the backing `Shortcuts`
   composition) to `DrillableListView<T>`, additive only — no existing subclass affected yet.
2. Migrate the six subclasses one at a time to call the relevant `Enable*` helper(s) and drop
   their hand-rolled wiring and `Shortcuts` override; build after each.
3. Update `StreamsTab`/`KvTab`'s three delete handlers to call `NeighborIdentity` and delete the
   three now-dead `Neighbor*Name` methods.
4. `tmux`-drive each of the three tabs to confirm no observable change (see Risks).

No server-side or data migration — this is UI-layer-only, no persisted state involved.

## Open Questions

None outstanding — scope, boundary, and risk mitigations above are considered sufficient to
proceed to tasks.
