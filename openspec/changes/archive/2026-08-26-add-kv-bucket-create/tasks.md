## 1. NewBucketOptions and field parsing helpers

- [x] 1.1 Create `KVStore/NewBucketOptions.cs`: `internal sealed record NewBucketOptions(string
      Name, NatsKVStorageType Storage, TimeSpan? MaxAge, TimeSpan? LimitMarkerTTL)` — no History
      field; History is deferred to Advanced (see design.md's Non-Goals) and always created with
      an explicit value, not exposed here (**superseded by 5.1**: History was later promoted to a
      fifth field on this record — its final shape is `NewBucketOptions(string Name,
      NatsKVStorageType Storage, int? History, TimeSpan? MaxAge, TimeSpan? LimitMarkerTTL)`)
- [x] 1.2 Add `ToNatsKVConfig()` on/for `NewBucketOptions` (same file) that maps to
      `NATS.Client.KeyValueStore.NatsKVConfig`: `Bucket = Name`; `Storage = Storage` (passes
      straight through — `Storage` is non-nullable and always carries an explicit selection, so
      no substitution logic is needed); `History = 1` (unconditional, not derived from any field
      on `NewBucketOptions` — comment explaining the CLR-default-`0`-means-"keep nothing"
      landmine, per design.md's "explicit safe defaults" decision) (**superseded by 5.3**: once
      History became a field, this maps `History = History ?? 1` instead); `MaxAge = MaxAge ??
      TimeSpan.Zero`; `LimitMarkerTTL = LimitMarkerTTL ?? TimeSpan.Zero` (unset means markers
      disabled, a legitimate default per its own doc comment — not a landmine like `History`);
      `NumberOfReplicas = 1` (explicit, same CLR-default-`0` reasoning as `History`)
- [x] 1.3 Add `TryParseMaxAge(string text, out TimeSpan? result) -> bool`, same shape as
      `Streams/NewStreamOptions.TryParseMaxAge`: empty/whitespace input returns `true` with
      `result = null`; non-empty input returns `true` with the parsed `TimeSpan` on success or
      `false` (unparseable) otherwise
- [x] 1.4 Add `TryParseLimitMarkerTTL(string text, out TimeSpan? result) -> bool`: same
      empty-means-null/`TimeSpan.Parse`-otherwise shape as `TryParseMaxAge`, kept as its own
      method rather than a call to `TryParseMaxAge` (see design.md's "conceptually distinct
      fields" decision)

## 2. CreateBucketDialog

- [x] 2.1 Create `KVStore/CreateBucketDialog.cs` as `Dialog<NewBucketOptions>` with labeled
      fields: Name (`TextField`), Storage (`DropDownList<NatsKVStorageType>`, defaulted to
      `File`), Max Age (`TextField`), Limit Marker TTL (`TextField`); constructor takes an
      optional `NewBucketOptions? initial = null` to pre-fill all fields (used on retry after a
      failed create) — mirror `Streams/CreateStreamDialog.cs`'s layout/`WrapField` helper,
      including its Retention `DropDownList` pattern for Storage
- [x] 2.2 Wire per-field validity (reuse `PatternDialog`'s red-text-on-`EditableBackground`
      convention via the same `InvalidAttribute`/`SetFieldValidity` shape as
      `CreateStreamDialog`) for Name (non-empty), Max Age (`TryParseMaxAge` returns `true`),
      Limit Marker TTL (`TryParseLimitMarkerTTL` returns `true`) — Storage needs no validity
      wiring, it always has a valid selection
- [x] 2.3 Add a single `Create` button, disabled/inert while any field is invalid, plus a
      mnemonic-less `Cancel` button for mouse users (Esc already cancels); override `OnAccepting`
      to swallow Enter pressed on a field (a no-op, not a submit or a cancel) so Tab stays the
      only way to move between fields — same as `CreateStreamDialog`
- [x] 2.4 On `Create`, build `Result` as a `NewBucketOptions`: `Name` (trimmed), `Storage` from
      the dropdown's `.Value`, `MaxAge` from the `out` value of `TryParseMaxAge`,
      `LimitMarkerTTL` from the `out` value of `TryParseLimitMarkerTTL` — the dialog never
      constructs or references `NatsKVConfig` directly
- [x] 2.5 Set `Result` and `RequestStop()` on `Create`; leave `Result` unset on Esc/Cancel

## 3. Wiring into the bucket list and tab

- [x] 3.1 Add `Command.New` + `Key.N.WithCtrl` binding to `KVStore/BucketListView.cs` (mirroring
      `Streams/StreamListView.cs`, including removing the inner `ListView`'s own Ctrl+N-to-Down
      alias via `ListView.KeyBindings.Remove(Key.N.WithCtrl)` before adding the new binding, and
      appending the shortcut to `IShortcutSource.Shortcuts`), raising a `CreateRequested` event
- [x] 3.2 In `KVStore/KvTab.cs`, wire `_listView.CreateRequested += () =>
      OpenCreateBucketDialog(null);` and add `OpenCreateBucketDialog(NewBucketOptions? seed)` /
      `TryCreateBucketAsync(NewBucketOptions options)`, mirroring
      `StreamsTab.OpenCreateStreamDialog`/`TryCreateStreamAsync` exactly: run
      `CreateBucketDialog` via `App!.Run(...)`, on a non-null `Result` call
      `_kv.CreateStoreAsync(options.ToNatsKVConfig())`, and on success refresh the bucket list
      with the new bucket selected
- [x] 3.3 Add an optional `selectName` parameter to `KvTab.RefreshListAsync` (currently
      parameterless — see `KVStore/KvTab.cs`), mirroring `StreamsTab.RefreshListAsync(string?
      selectName = null)`, and pass it through to `_listView.ReplaceItems(buckets, selectName)`
      so `TryCreateBucketAsync` can highlight the newly created bucket via
      `RefreshListAsync(options.Name)`
- [x] 3.4 On `CreateStoreAsync` failure, catch the exception and call
      `MessageBox.ErrorQuery(App!, DialogText.Pad("Create Bucket Failed"),
      DialogText.Pad(ex.Message), "_Ok")`, then reopen `CreateBucketDialog` seeded with the
      just-entered `NewBucketOptions` — same shape as `TryCreateStreamAsync`'s catch block

## 4. Verification

- [x] 4.1 Manual pass via tmux against a real `nats-server`: Ctrl+N → fill valid Name (leave
      Storage at its File default, Max Age/Limit Marker TTL empty) → Create → confirm the bucket
      appears highlighted in the list and its detail panel shows History as a small positive
      number (not 0), no Max Age limit, and "(none)" for TTL Limit
      — verified: `test-bucket-1` created, highlighted, History: 1, Max Age: 00:00:00, TTL
      Limit: (none); confirmed server-side via `nats kv info` too.
- [x] 4.2 Manual pass: Ctrl+N → fill a valid Name, select Memory as Storage → Create → confirm
      via `nats kv info <bucket>` (or server-side stream info) that the created bucket's stream
      is memory-backed, not file-backed
      — verified: `test-bucket-mem` created; `nats kv info` shows `Storage: Memory`.
- [x] 4.3 Manual pass: leave Name empty, enter garbage Max Age/Limit Marker TTL text — confirm
      Create stays unavailable and invalid fields are flagged
      — verified: with Name empty and both TTL fields garbage, activating Create (Enter) left the
      dialog open (Create is inert while invalid), no bucket created.
- [x] 4.4 Manual pass: create with an explicit Max Age (e.g. `1:00:00`) and Limit Marker TTL
      (e.g. `0:10:00`) — confirm the detail panel reflects both values
      — verified: `test-bucket-ttl` created; detail panel showed Max Age: 01:00:00, TTL Limit:
      00:10:00.
- [x] 4.5 Manual pass: attempt to create a bucket with a name that already exists — confirm a
      `MessageBox.ErrorQuery` shows the server's error message and, after dismissing it, the
      create-bucket dialog reopens with the previously entered values still filled in
      — verified: a same-name-different-config create (existing `test-bucket-1`, File, vs.
      Memory) surfaced `MessageBox.ErrorQuery` "Create Bucket Failed" / "stream name already in
      use with a different configuration"; dismissing it reopened the dialog pre-filled with
      Name=test-bucket-1, Storage=Memory. (A same-name-*same*-config create is a no-op success
      per JetStream's idempotent AddStream semantics, not an error path — the mismatched-config
      case above is what actually exercises server-side rejection.)
- [x] 4.6 Confirm via `nats kv info <bucket>` (or the detail panel) that every bucket created via
      this dialog comes through with History `1`, not `0` — resolves design.md's open question
      about `NatsKVConfig`'s CLR-default behavior
      — verified: `nats kv info test-bucket-1` showed `History Kept: 1`; design.md's Open
      Questions section updated to record this.
- [x] 4.7 Check the test server's version (`nats server info` or equivalent) before 4.4's Limit
      Marker TTL check — if it's older than 2.11, confirm what actually happens when a non-zero
      value is sent (rejected outright vs. silently ignored) and note the finding in
      `NewBucketOptions.cs` next to the `LimitMarkerTTL` mapping, per design.md's risk on this
      field
      — verified: test server is 2.12.8 (via `/varz` monitoring endpoint), above the 2.11
      minimum, so the non-zero-on-old-server case wasn't reachable; finding noted as a comment
      next to `LimitMarkerTTL` in `NewBucketOptions.cs`.

## 5. History field (pivot — promoted from deferred-to-Advanced to a core field)

Sections 1-4 above shipped History hardcoded to `1`, never exposed as a field. This section
promotes it to a fifth core field alongside Name/Storage/Max Age/Limit Marker TTL, per the
updated proposal.md/design.md/spec.md — unset still defaults to `1`, preserving prior behavior.

- [x] 5.1 In `KVStore/NewBucketOptions.cs`, add `int? History` to the `NewBucketOptions` record
      (after `Storage`, before `MaxAge`, matching the dialog's field order)
- [x] 5.2 Add `TryParseHistory(string text, out int? result) -> bool`: empty/whitespace input
      returns `true` with `result = null`; non-empty input returns `true` with the parsed value
      when it parses as an `int` and is `>= 1`, `false` otherwise (0/negative/unparseable are all
      invalid — a "keep <1 revisions" bucket isn't usable)
- [x] 5.3 Update `ToNatsKVConfig()` to map `History = History ?? 1` instead of the unconditional
      `History = 1`, with a comment explaining why the substitution (not a straight pass-through)
      is still needed even though the field is now user-facing — `History`'s own CLR default of
      `0` still isn't a meaningful "unset" value the way `MaxAge`/`LimitMarkerTTL`'s
      `TimeSpan.Zero` is
- [x] 5.4 In `KVStore/CreateBucketDialog.cs`, add a History `TextField` between Storage and Max
      Age (labeled "History"), pre-filled from `initial?.History?.ToString() ?? string.Empty`,
      wired to `UpdateValidity()` on `ValueChanged` — mirror the existing Max Age/Limit Marker
      TTL field wiring exactly
- [x] 5.5 Wire History's validity into `UpdateValidity()`/`SetFieldValidity` (valid when
      `TryParseHistory` returns `true`) and into `_createButton.Enabled`
- [x] 5.6 In `Commit()`, parse History via `TryParseHistory` (bail out, same as the Max
      Age/Limit Marker TTL checks, if it fails) and pass the `out` value into the constructed
      `NewBucketOptions`
- [x] 5.7 Manual pass via tmux against a real `nats-server`: Ctrl+N → leave History empty →
      Create → confirm the detail panel shows History: 1 (unset still defaults correctly with
      the field now present)
      — verified: `test-bucket-hist-default` created; detail panel showed History: 1.
- [x] 5.8 Manual pass: Ctrl+N → set History to e.g. `3` → Create → confirm via the detail panel
      and/or `nats kv info <bucket>` that the created bucket's History is `3`, not `1` — resolves
      design.md's new open question about an explicit History value round-tripping
      — verified: `test-bucket-hist-3` created; detail panel showed History: 3, confirmed
      server-side via `nats kv info` (`History Kept: 3`).
- [x] 5.9 Manual pass: set History to `0` or `-1` or garbage text — confirm Create stays
      unavailable and the History field is flagged invalid
      — verified: with History="0" and separately History="abc", activating Create left the
      dialog open both times (Create inert while invalid), no bucket created in either case.
