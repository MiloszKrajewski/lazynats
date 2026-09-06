## Context

`ValuesTab.RefreshKeyListAsync` (`src/lazynats/Values/ValuesTab.cs:330`) is the single fetch path
for the key-level list — called on descend, Ctrl+R, and after every create/edit — and always does
`await foreach (var key in store.GetKeysAsync())`, pulling every key in the bucket. `KeyListView`
(`src/lazynats/Values/KeyListView.cs`) already has an in-memory fuzzy quick-search (`/`, via
`DrillableListView<T>.AttachFilterBox`) added by `add-drillable-list-search`, but that only
narrows what's already been fetched — it does nothing for a bucket too large to fetch
comfortably in the first place. That change's design.md explicitly reserved Ctrl+F for this
follow-up rather than colliding on the same key.

`INatsKVStore.GetKeysAsync(IEnumerable<string> filters, NatsKVWatchOpts opts, CancellationToken)`
(`NATS.Client.KeyValueStore` 2.8.2) evaluates one or more NATS subject-wildcard filters
server-side and streams back only matching key names — this is the native mechanism to narrow
the fetch itself, as opposed to filtering an already-fetched set.

## Goals / Non-Goals

**Goals:**
- Let the user scope the key-level fetch to a NATS subject-wildcard pattern before it happens,
  via Ctrl+F, so a bucket with far more keys than is practical to pull in full can still be
  browsed by narrowing to a known prefix/pattern.
- Keep the active filter applied across whatever already triggers a re-fetch today (Ctrl+R,
  post-create, post-edit) so the narrowing isn't silently undone by the next refresh — the whole
  point is to avoid re-pulling everything.
- Make the active filter visually obvious (list title) so the displayed set is never mistaken for
  "every key in the bucket".

**Non-Goals:**
- No fetch cap or truncation indicator for a bucket too large even under a filter — named as a
  further deferred follow-up in `add-drillable-list-search`'s proposal.md, unchanged here.
- No client-side validation of NATS subject-wildcard syntax beyond "non-empty" — an invalid
  pattern surfaces as whatever error (or empty result) the server/client library produces, via
  the same `StatusChanged` path `RefreshKeyListAsync` already uses for other fetch failures.
- No change to `DrillableListView<T>` (the shared base) or to any other subclass
  (`StreamListView`, `ConsumerListView`, the KV/OBJ `BucketListView`s, `ObjectListView`) — none of
  their fetches take a server-side filter today (`NATS.Client.ObjectStore` 2.8.2's
  `INatsObjStore.ListAsync` has no filter overload), so generalizing this into a new shared
  `Enable*` shape would be speculative.
- No change to the existing `/` in-memory quick-search behavior or its reset-on-refresh semantics
  — the two filters are independent and can be active simultaneously (server-side narrows the
  fetch; in-memory narrows the view further).

## Decisions

**1. `KeyListView` binds Ctrl+F directly, not via a new shared `DrillableListView<T>` opt-in.**
Every current `Enable*` shape (`EnableCreate`/`EnableDelete`/`EnableEdit`/...) is used by multiple
subclasses; a server-side pre-fetch filter is only meaningful for `KeyListView` today. Per the
base's own documented allowance ("a subclass MAY also define whatever additional navigation... it
needs beyond shared shapes, without requiring changes to the base"), this binds
`Key.F.WithCtrl` directly in `KeyListView`'s constructor, repurposing `Command.Open` (unused
elsewhere in this view; matches `ObjectListView`'s existing repurposing of `Command.Save` for
Ctrl+S download) and raises a new no-arg `FilterRequested` event, mirroring the
`CreateRequested`/`DeleteRequested`/`EditRequested` shape: the list raises the request, the owning
tab does the actual work (open the dialog, know the current filter, call the fetch). `KeyListView`
overrides `Shortcuts` to append a `Ctrl+F` "Filter" hint via `base.Shortcuts.Append(...)`, the same
pattern the drillable-list spec already documents for subclass-added commands.

**2. `PatternDialog` gains an `allowEmpty` constructor flag rather than a new dialog type.**
`PatternDialog` (`src/lazynats/Subscriptions/PatternDialog.cs`) is already the exact shape needed
— single `TextField`, Enter-to-confirm, Esc-to-cancel, `Dialog<string>` — except it currently
refuses to confirm on empty text, correct for a subscription pattern (which must never be empty)
but wrong here, where empty is a legitimate value meaning "clear the filter, show every key
again". Adding `allowEmpty` (default `false`, so `SubscriptionsView`'s existing calls are
untouched) changes only `UpdateValidity`'s check; `ValuesTab` calls
`new PatternDialog("Filter Keys", _currentKeyFilter ?? "", allowEmpty: true)`. Considered a
separate dialog class; rejected as needless duplication of an already-generic single-field modal.

**3. The active filter is tab-level state (`_currentKeyFilter`), scoped to the currently
drilled-into bucket, reset on ascend/descend.** `ValuesTab` already tracks `_currentBucket` the
same way; `_currentKeyFilter` (nullable `string`) sits alongside it, set from the pattern dialog's
result (empty string normalized to `null`), and reset to `null` in both `Descend()` and
`Ascend()` — a filter set for one bucket has no meaningful carry-over into a different bucket
entered later, or back at the bucket list. `RefreshKeyListAsync` reads `_currentKeyFilter`
directly (no new parameter) and calls `store.GetKeysAsync([_currentKeyFilter])` when set,
`store.GetKeysAsync()` when not — so Ctrl+R, post-create, and post-edit refreshes all
transparently stay scoped to the active filter without each call site needing to pass it through.
Considered resetting the filter on every refresh (matching the `/` quick-search's own
reset-on-refresh rule from `add-drillable-list-search`) for consistency; rejected because that
would defeat the entire purpose here — a plain Ctrl+R after narrowing a huge bucket down would
immediately re-fetch everything again. The two filters are allowed to diverge in this respect
because they solve different problems (one is presentation-only over an in-memory set with
nothing at stake in re-deriving it; the other exists specifically to avoid a large fetch).

**4. The key list's title reflects the active filter.** `_listLabel.Text` (currently set to
`$"Keys of {name}"` in `Descend()`) becomes `$"Keys of {name} (filter: {pattern})"` when
`_currentKeyFilter` is set, recomputed wherever it's currently set plus after a filter change.
This is the only UI surface used to distinguish "showing every key" from "showing a scoped
subset" — no separate indicator element, keeping this change to existing patterns
(`StreamsTab`/`ObjectsTab` already vary this label on descend/ascend).

## Risks / Trade-offs

- [A newly created or edited key that doesn't match the active filter won't appear in the
  post-create/edit refresh, which could read as "the create/edit silently failed"] → Accepted: the
  list title makes the active filter visible, and this only occurs when the user has deliberately
  narrowed the view; clearing the filter (Ctrl+F, confirm empty) immediately shows it. Calling
  this out explicitly since it's the main behavioral surprise this design accepts.
- [`GetKeysAsync`'s filter parameter takes `IEnumerable<string>`, i.e. multiple OR'd patterns, but
  the single-field dialog only ever produces one] → Accepted for this change; a multi-pattern
  filter UI is unneeded speculative scope — nothing in the request asks for it, and the same
  dialog can be revisited later if a real need for multiple simultaneous patterns shows up.
- [No client-side wildcard-syntax validation means a malformed pattern's failure mode is whatever
  the server/client library surfaces] → Accepted, consistent with how `RefreshKeyListAsync`
  already surfaces any other fetch failure via `StatusChanged` — no new error-handling path
  needed.

## Open Questions

- Should the empty-state hint (`KeyListView.EmptyHintText`, currently a static "No keys — Ctrl+R
  to refresh") distinguish "this bucket truly has no keys" from "no keys match the active
  filter"? Left as today's generic hint for this first cut — worth revisiting if it proves
  confusing in practice, same spirit as the deferred match-highlighting question in
  `add-drillable-list-search`.
