## 1. NewBucketOptions and field parsing helper

- [x] 1.1 Create `ObjStore/NewBucketOptions.cs`: `internal sealed record NewBucketOptions(string
      Name, TimeSpan? MaxAge)`
- [x] 1.2 Add `ToNatsObjConfig()` on/for `NewBucketOptions` (same file) that maps to
      `NATS.Client.ObjectStore.NatsObjConfig`: `Bucket = Name`; `MaxAge = MaxAge ?? TimeSpan.Zero`
      (unset means unlimited — a genuine safe default per its doc comment, not a CLR-default
      landmine); `NumberOfReplicas = 1` (explicit — a bare `NatsObjConfig` leaves this at the CLR
      default of `0`, which isn't a meaningful request, same reasoning
      `KVStore/NewBucketOptions.ToNatsKVConfig()` applies to `NumberOfReplicas`); `Storage` is
      left unassigned (its CLR/documented default is `File`, a genuine safe default — this dialog
      offers no way to choose Memory)
- [x] 1.3 Add `TryParseMaxAge(string text, out TimeSpan? result) -> bool`, same shape as
      `KVStore/NewBucketOptions.TryParseMaxAge`: empty/whitespace input returns `true` with
      `result = null`; non-empty input returns `true` with the parsed `TimeSpan` on success or
      `false` (unparseable) otherwise

## 2. CreateBucketDialog

- [x] 2.1 Create `ObjStore/CreateBucketDialog.cs` as `Dialog<NewBucketOptions>` with labeled
      fields: Name (`TextField`), Max Age (`TextField`); constructor takes an optional
      `NewBucketOptions? initial = null` to pre-fill all fields (used on retry after a failed
      create) — mirror `KVStore/CreateBucketDialog.cs`'s layout/`WrapField` helper, dropping the
      Storage dropdown and History/Limit Marker TTL fields it has no analog for
- [x] 2.2 Wire per-field validity (reuse the shared red-text-on-`EditableBackground` convention
      via the same `InvalidAttribute`/`SetFieldValidity` shape as `KVStore/CreateBucketDialog`)
      for Name (non-empty) and Max Age (`TryParseMaxAge` returns `true`)
- [x] 2.3 Add a single `Create` button, disabled/inert while any field is invalid, plus a
      mnemonic-less `Cancel` button for mouse users (Esc already cancels); override `OnAccepting`
      to swallow Enter pressed on a field (a no-op, not a submit or a cancel) so Tab stays the
      only way to move between fields — same as `KVStore/CreateBucketDialog`
- [x] 2.4 On `Create`, build `Result` as a `NewBucketOptions`: `Name` (trimmed), `MaxAge` from the
      `out` value of `TryParseMaxAge` — the dialog never constructs or references `NatsObjConfig`
- [x] 2.5 Set `Result` and `RequestStop()` on `Create`; leave `Result` unset on Esc/Cancel

## 3. Wiring into the bucket list and tab

- [x] 3.1 Add `Command.New` + `Key.N.WithCtrl` binding to `ObjStore/BucketListView.cs` (mirroring
      `KVStore/BucketListView.cs`, including removing the inner `ListView`'s own Ctrl+N-to-Down
      alias via `ListView.KeyBindings.Remove(Key.N.WithCtrl)` before adding the new binding, and
      appending the shortcut to `IShortcutSource.Shortcuts`), raising a `CreateRequested` event
- [x] 3.2 In `ObjStore/ObjTab.cs`, wire `_listView.CreateRequested += () =>
      OpenCreateBucketDialog(null);` and add `OpenCreateBucketDialog(NewBucketOptions? seed)` /
      `TryCreateBucketAsync(NewBucketOptions options)`, mirroring
      `KvTab.OpenCreateBucketDialog`/`TryCreateBucketAsync` exactly: run `CreateBucketDialog` via
      `App!.Run(...)`, on a non-null `Result` call `_obj.CreateObjectStoreAsync(options.ToNatsObjConfig())`,
      and on success refresh the bucket list with the new bucket selected
- [x] 3.3 Add an optional `selectName` parameter to `ObjTab.RefreshListAsync` (currently
      parameterless — see `ObjStore/ObjTab.cs`), mirroring `KvTab.RefreshListAsync(string?
      selectName = null)`, and pass it through to `_listView.ReplaceItems(buckets, selectName)`
      so `TryCreateBucketAsync` can highlight the newly created bucket via
      `RefreshListAsync(options.Name)`
- [x] 3.4 On `CreateObjectStoreAsync` failure, catch the exception and call
      `MessageBox.ErrorQuery(App!, DialogText.Pad("Create Bucket Failed"),
      DialogText.Pad(ex.Message), "_Ok")`, then reopen `CreateBucketDialog` seeded with the
      just-entered `NewBucketOptions` — same shape as `KvTab.TryCreateBucketAsync`'s catch block

## 4. Verification

- [x] 4.1 Manual pass via tmux against a real `nats-server`: Ctrl+N → fill valid Name (Max Age
      empty) → Create → confirm the bucket appears highlighted in the list and its detail panel
      shows no max age limit
- [x] 4.2 Manual pass: confirm via `nats object info <bucket>` (or server-side stream info) that
      a bucket created via this dialog is file-backed
- [x] 4.3 Manual pass: leave Name empty, enter garbage Max Age text — confirm Create stays
      unavailable and invalid fields are flagged
- [x] 4.4 Manual pass: create with an explicit Max Age (e.g. `1:00:00`) — confirm the detail
      panel reflects the value
- [x] 4.5 Manual pass: attempt to create a bucket with a name that already exists (with a
      different config) — confirm a `MessageBox.ErrorQuery` shows the server's error message and,
      after dismissing it, the create-bucket dialog reopens with the previously entered values
      still filled in
