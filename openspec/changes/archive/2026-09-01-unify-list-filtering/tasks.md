## 1. Shared filter-expression engine

- [x] 1.1 Generalize `Core/KeyFilterExpression.cs` (rename to reflect its broadened scope, e.g.
      `FilterExpression`) so `TryCompile` unconditionally returns an exact-match `Regex` alongside
      the existing native-filter derivation, plus a `NativeFilterIsExact` flag for the one
      native-scoped consumer's skip-regex optimization (design.md Decision 1).
- [x] 1.2 Update `ValuesTab.RefreshKeyListAsync` to the new result shape (native-scoped fetch +
      skip regex only when `NativeFilterIsExact`), verifying `nats-kv`'s "Server-Side Key Filter"
      behavior (grammar, cap, title indicator) is unchanged, and confirming the native-scoped
      fetch's own (server) result order still ends up alphabetically sorted via the existing
      `ReplaceItems` call — never displayed in raw fetch order (design.md Decision 6).
- [x] 1.3 Delete `RegexExtensions.WildcardToRegex` and its now-only call site once
      `ObjectListView`/`ObjectsTab` move onto the shared engine (task 3.3).

## 2. `DrillableListView<T>` shared Filter shape

- [x] 2.1 Add `EnableFilter()` to `DrillableListView<T>`: binds Ctrl+F, opens `PatternDialog`
      (validator = `FilterExpression.TryCompile(...) is not null`), maintains sticky filter state
      independent of quick-search's transient text, and re-derives `_filtered` as the AND of both
      predicates when both are active, by narrowing (never reordering) the already-sorted `_items`
      (design.md Decisions 2 and 6).
- [x] 2.2 Ensure the sticky filter state survives `ReplaceItems` (unlike quick-search's text, which
      already resets there) and add a `ClearFilter()` method for an owning tab to call on
      scope-changing navigation (descend/ascend).
- [x] 2.3 Expose the active filter pattern (for tab title/header use) and raise `FilterChanged`
      carrying the new pattern (or `null`) on every confirm/clear.
- [x] 2.4 Add a "Filter" `ShortcutHint` (Ctrl+F) to `TabOperations` when `EnableFilter()` is active,
      matching the existing Create/Delete/Edit hint pattern.

## 3. Wire `EnableFilter()` into existing lists

- [x] 3.1 Activate `EnableFilter()` on `StreamListView` and `ConsumerListView`; no `FilterChanged`
      subscription needed in `StreamsTab` (pure in-memory narrowing). Reset the consumer-level
      filter on ascend, per the `nats-streams` delta.
- [x] 3.2 Activate `EnableFilter()` on `BucketListView`. **Correction found during implementation**:
      `Values/BucketListView.cs` and `Objects/BucketListView.cs` are two separate, near-identical
      classes (same name, different namespace, different item type — `NatsKVStatus` vs.
      `StreamInfo`), not one shared component as the proposal/design assumed; both were updated
      independently. No `FilterChanged` subscription needed in either `ValuesTab` or `ObjectsTab`.
- [x] 3.3 Activate `EnableFilter()` on `ObjectListView`; remove its bespoke `FilterRequested` event
      and `ObjectsTab.OpenObjectFilterDialog`/`WildcardToRegex` call site, folding "Post-Fetch
      Object Name Filter" behavior (re-fetch-then-narrow on confirm, per `nats-obj`'s unchanged
      "Object List" scoping) into a `FilterChanged` subscription that re-triggers
      `RefreshObjectListAsync`.
- [x] 3.4 Update `KeyListView`/`ValuesTab` to use `EnableFilter()`'s `FilterChanged` event as the
      trigger for its existing server-scoped `RefreshKeyListAsync` logic, replacing its bespoke
      `FilterRequested` event while preserving the 10,000-match cap and truncation title behavior.

## 4. `ListEditorView<T>` filtering

- [x] 4.1 Add an `_filtered` `ObservableCollection<T>` to `ListEditorView<T>`, bound to the
      `ListView`, re-derived from `_items` on quick-search/filter changes and on every `_items`
      collection change, preserving insertion order (design.md Decision 4).
- [x] 4.2 Wire the same `FilterBox`/quick-search shape `DrillableListView<T>` uses, plus the new
      Ctrl+F `EnableFilter()`-equivalent, onto `ListEditorView<T>`.
- [x] 4.3 Update `TryEditItem`/`TryDeleteItem` to resolve the selected filtered-view item back to
      its position in `_items` before calling `Replace`/`Delete`; leave `TryCreateItem`'s `Add`
      unaffected.
- [x] 4.4 Update `Shortcuts`/`TabOperations` to include the new "Search"/"Filter" hints alongside
      existing New/Edit/Delete, for both the standalone (`bindSharedKeys: true`) and tab-hosted
      cases.

## 5. Activate filtering on Publish (Subscribe deliberately excluded)

- [x] 5.1 **Reverted after initial implementation.** Quick-search and Ctrl+F filter were wired onto
      `SubscriptionsView` and then removed: each subscription row already *is* a subject-pattern
      filter over the live feed, not a name to narrow a list by, so neither shape applies there
      (design.md Decision 5). The `nats-subscriptions` spec delta was deleted; `SubscriptionsView`/
      `SubscribeTab` are back to their pre-change shape (no `FilterBox`, no `EnableFilter()`).
- [x] 5.2 Wire quick-search and Ctrl+F filter onto `HeaderEditorView`, matching against each header
      pair's `"Key: Value"` text; verify filtering has no effect on which headers are sent
      (`nats-publish` delta), and that its standalone modal usage inside `PublishDialog`
      (`bindSharedKeys: true` — there is no tab-hosted usage, Publish is a modal reachable via
      Alt+P, not a tab) keeps working per `list-editor`'s existing "Key Bindings Work Regardless of
      Focused Child" requirement.

## 6. Verification

- [x] 6.1 Manually drive each tab via tmux (per CLAUDE.md's testing guidance) to confirm Ctrl+F and
      `/` both work on: Streams, Consumers, Values buckets, Values keys, Objects buckets, Objects,
      and Publish headers — and confirm Subscribe's list has neither (by design).
- [x] 6.2 Confirm the object-name filter grammar change (case-sensitive, token-aware `* ? >`)
      against a bucket with dotted/mixed-case object names, matching the updated `nats-obj`
      scenarios.
- [x] 6.3 Confirm KV key filtering behavior (cap, title, native+regex resolution) is bit-for-bit
      unchanged from before the refactor.
- [x] 6.4 Confirm every refreshed and/or filtered list (Streams, Consumers, Buckets ×2, Keys,
      Objects) displays alphabetically-ordered results even when the server returns matches in a
      different order (e.g. insertion order) — spot-check against a bucket/stream/stream-of-keys
      whose names are not already alphabetical on the server side (design.md Decision 6,
      `drillable-list`'s "Filtered Results Stay Alphabetically Ordered..." requirement).
- [x] 6.5 Run `dotnet build src/lazynats.sln` and fix any warnings introduced by the rename/refactor
      in task 1.1.
