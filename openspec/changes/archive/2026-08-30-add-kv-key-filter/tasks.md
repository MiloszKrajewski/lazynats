## 1. PatternDialog: allow empty confirmation

- [x] 1.1 Add an `allowEmpty` constructor parameter to `PatternDialog` (default `false`), used by
      `UpdateValidity` and the `Accepting` handler's empty-text guard instead of the current
      hard-coded non-empty check.
- [x] 1.2 Confirm `SubscriptionsView`'s existing `PatternDialog` call sites are unaffected
      (still default to `allowEmpty: false`, still refuse empty patterns).

## 2. KeyListView: Ctrl+F binding

- [x] 2.1 In `KeyListView`'s constructor, bind `Key.F.WithCtrl` to a repurposed `Command.Open`
      (unused elsewhere on this view) that raises a new no-arg `FilterRequested` event, matching
      the `CreateRequested`/`DeleteRequested`/`EditRequested` shape.
- [x] 2.2 Override `Shortcuts` on `KeyListView` to append a `Ctrl+F` "Filter" hint via
      `base.Shortcuts.Append(...)`.

## 3. ValuesTab: filter state, dialog, and scoped fetch

- [x] 3.1 Add `_currentKeyFilter` (nullable `string`) state to `ValuesTab`, reset to `null` in
      both `Descend()` and `Ascend()`.
- [x] 3.2 Subscribe to `_keyListView.FilterRequested`, opening
      `new PatternDialog("Filter Keys", _currentKeyFilter ?? "", allowEmpty: true)`; on a non-null
      `Result`, normalize empty to `null`, set `_currentKeyFilter`, and call
      `RefreshKeyListAsync()`. A cancelled dialog (`Result is null`) leaves `_currentKeyFilter`
      and the list unchanged.
- [x] 3.3 Update `RefreshKeyListAsync` to call `store.GetKeysAsync([_currentKeyFilter])` when
      `_currentKeyFilter` is set, `store.GetKeysAsync()` otherwise — no new method parameter, it
      reads `_currentKeyFilter` directly so Ctrl+R and post-create/edit refreshes stay scoped for
      free.
- [x] 3.4 Update the key-level `_listLabel.Text` assignment(s) to include the active filter
      pattern when `_currentKeyFilter` is set (e.g. `$"Keys of {name} (filter: {pattern})"`),
      recomputed in `Descend()` and after a filter change.

## 4. Verification

- [x] 4.1 Manual check against a NATS server with a KV bucket containing keys under multiple
      prefixes: Ctrl+F with a pattern narrows the list; Ctrl+R afterward stays narrowed; Esc/
      Backspace back to the bucket list and re-descending shows the full key list again.
      Confirmed interactively by a human (tmux Ctrl+letter delivery blocker from earlier in this
      session was environment/tooling-specific, not a defect in this change).
- [x] 4.2 Manual check that cancelling the filter dialog (Esc) changes nothing, and that
      confirming an empty pattern while a filter is active clears it. Confirmed interactively.
- [x] 4.3 Confirm the existing `/` quick-search and Ctrl+F server-side filter can be used together
      without interfering with each other. Confirmed interactively.

      **Reservation raised during manual testing, deferred for future consideration**: the
      server-side filter is a NATS subject-wildcard match (`INatsKVStore.GetKeysAsync`'s
      `filters`), so `*`/`>` operate per `.`-delimited token like any NATS subject. It's only as
      useful as the bucket's own key-naming convention — for keys that don't use `.` as a
      hierarchy delimiter, a single `*` already matches the whole key (no narrowing power beyond
      "has a value") and there's no filesystem-style substring/prefix wildcard equivalent to what
      `add-obj-name-filter` gives the Objects tab via `WildcardToRegex` (client-side, delimiter-
      agnostic). Left as-is for this change — revisit if flat (non-`.`-delimited) KV buckets turn
      out to be common enough that this native filter rarely narrows anything for them.
- [x] 4.4 `dotnet build src/lazynats.sln` succeeds.
