## 1. Extend PollingDetailsView with an optional body region

- [x] 1.1 Add `protected virtual string? BuildBody(TInfo info) => null;` to
      `src/lazynats/Components/PollingDetailsView.cs`.
- [x] 1.2 Update `OnDrawingContent` to, after drawing the existing header rows, call `BuildBody`
      (only when there's a currently-shown value — reuse whatever state `Show`/`_rows` already
      tracks) and render its text starting one row below the header rows (blank separator line),
      clipped to the view's remaining width/height, no scrolling.
- [x] 1.3 Confirm `Show(null)` clears the body along with the rows (no separate body state that
      could outlive a clear).
- [x] 1.4 Sanity check `StreamDetails`/`ConsumerDetails` render unchanged (no override added there,
      default `BuildBody` returns null).

## 2. KV domain wiring

- [x] 2.1 In `src/lazynats/Program.cs`, add `var kv = jetStream.CreateKeyValueStoreContext();`
      after the existing `jetStream` creation, and `services.AddSingleton(kv);` alongside the
      other singleton registrations.

## 3. Bucket level (`src/lazynats/Kv/`)

- [x] 3.1 `BucketNamePresenter: IValuePresenter<NatsKVStatus>` — `Format` returns `value.Bucket`.
- [x] 3.2 `BucketListView: DrillableListView<NatsKVStatus>` — mirrors `StreamListView`: presenter
      above, `GetIdentity` returns `item.Bucket`, empty hint text (e.g. "No buckets — Ctrl+R to
      refresh"), `DescendRequested` event re-raised from the inner `ListView.Accepted`.
- [x] 3.3 `BucketDetails: PollingDetailsView<string, NatsKVStatus>` — `SetTarget(string? bucket)`
      wraps `SetPollTarget`/`ClearPollTarget`; `FetchAsync(bucket)` calls
      `(await _kv.GetStoreAsync(bucket)).GetStatusAsync()`; `BuildRows` per design.md's "Row
      content" (Bucket, Compressed, TTL Limit, blank separator, Entries from `Info.State.Messages`,
      Bytes, History from `Info.Config.MaxMsgsPerSubject`, Max Age from `Info.Config.MaxAge`).
- [x] 3.4 (Found during live verification) `GetStatusesAsync()` returns every stream, not just KV
      buckets, and its `Bucket` field is unreliable (raw, unstripped stream name) — added
      `src/lazynats/Kv/BucketName.cs` (`IsKvStream`/`From`, `KV_` prefix convention) and used it to
      filter `KvTab.RefreshListAsync` and to derive the bare bucket name everywhere `.Bucket` was
      previously read directly (`BucketNamePresenter`, `BucketListView.GetIdentity`,
      `BucketDetails.BuildRows`, `KvTab.OnBucketHighlightChanged`/`Descend`). See design.md's
      "Bucket list filtering" decision.

## 4. Key level (`src/lazynats/Kv/`)

- [x] 4.1 `KeyNamePresenter: IValuePresenter<string>` — `Format` returns the value unchanged.
- [x] 4.2 `KeyListView: DrillableListView<string>` — mirrors `ConsumerListView`: presenter above,
      `GetIdentity` returns the item itself, empty hint text (e.g. "No keys — Ctrl+R to refresh"),
      `AscendRequested` event bound to `Command.Cancel` on `Esc`/`Backspace` (same pattern as
      `ConsumerListView`), `Shortcuts` override appending the "Back" hint.
- [x] 4.3 `KeyDetails: PollingDetailsView<(string Bucket, string Key), KeyDetails.Entry>` — TInfo is
      a small private record class (`Entry`), not `NatsKVEntry<byte[]>` directly, since that struct
      breaks the base's unconstrained `TInfo?` erasure (CS0508) — see design.md. `SetTarget(string?
      bucket, string? key)` wraps `SetPollTarget`/`ClearPollTarget`; `FetchAsync` calls
      `TryGetEntryAsync<byte[]>(key)` and returns `null` whenever the result is an error (key gone
      for any reason — no distinction between not-found/deleted/purged); `BuildRows` returns
      Key/Revision/Created/Operation/Size rows; `BuildBody` returns
      `Encoding.UTF8.GetString(entry.Value)`.

## 5. Tab composition

- [x] 5.1 `KvTab: View` in `src/lazynats/Kv/KvTab.cs` — mirrors `StreamsTab.cs` structure exactly:
      two levels (bucket/key) sharing one screen region, both built once and toggled via
      `Visible`, `_currentBucket` null at bucket level / holds the drilled-into bucket name at key
      level, `OnHasFocusChanged` gates the one-time initial `GetStatusesAsync` load and each level's
      `SetActive`, `Descend`/`Ascend` methods swap frame/label visibility and re-fetch the key list
      on descend, `RefreshBucketListAsync`/`RefreshKeyListAsync` methods parallel
      `RefreshListAsync`/`RefreshConsumerListAsync`, `StatusChanged` event for error reporting.
- [x] 5.2 Constructor takes `INatsKVContext kv` (resolved via DI, matching how `StreamsTab` takes
      `INatsJSContext jetStream`).

## 6. Registration

- [x] 6.1 In `src/lazynats/MainWindow.cs`, construct `KvTab` (resolving `INatsKVContext` from
      `Services.Root`) and register it with `ManagementTabs` as the 4th tab, titled `"4:KV"`,
      wired to the `StatusChanged` -> status bar path the same way `StreamsTab` is.
- [x] 6.2 Confirm `Alt+4` reaches it for free via the existing numeric-shortcut mechanism
      (`openspec/specs/tab-navigation/spec.md` / tab-numeric-shortcuts) — no new shortcut code
      expected, just verify.

## 7. Verification

- [x] 7.1 `dotnet build src/lazynats.sln` succeeds.
- [x] 7.2 Against a local `nats-server`, created bucket `TESTBUCKET` + 3 keys via `.bin/nats.exe`,
      drove the app via `tmux`: bucket list showed only real KV buckets with correct bare names
      (`TESTBUCKET`, `saga-demo-state`) after the 3.4 fix; bucket stats (Entries/Bytes/History/Max
      Age) correct; Enter descended to key list (`foo.bar`/`some.json`/`soon.deleted`); key details
      showed Key/Revision/Created/Operation/Size header + UTF-8 body filling remaining space for
      both a plain-text value ("hello world") and a JSON value (`{"id":42,"name":"widget"}`,
      rendered as raw text, no pretty-printing, per scope); Esc ascended back to the bucket list.
      Deleted `soon.deleted` via the CLI while highlighted in the app — next poll tick (~3s) blanked
      the details panel with no error in the status bar, confirming the "deleted renders as no
      selection" behavior. Cleaned up (`kv rm TESTBUCKET`) after verification.
- [x] 7.3 Verified live: switched to the Streams tab after the KV changes, `StreamDetails` (stream
      level) and `ConsumerDetails` (descended into a stream's consumers) both render their
      label:value rows exactly as before — no regression from the `PollingDetailsView` body
      extension.

## 8. Instant key-detail refresh on highlight change (found via dogfooding)

- [x] 8.1 (Found via visual testing against a persistent demo bucket) Key details stayed blank
      until the next ~3s poll tick after switching keys, unlike Bucket/Stream/Consumer details
      which update instantly — because the key list only carries bare names, with nothing to
      `Show()` synchronously. Added `PollingDetailsView.RefreshNow()`: fetches the current target
      immediately (bypassing the active-gate and poll interval), applies the result via the
      existing `Show()` path, with a staleness guard against rapid highlight changes. See
      design.md's "Instant refresh on key highlight change" decision.
- [x] 8.2 `KvTab.OnKeyHighlightChanged` calls `SetTarget` → `Show(null)` (clear, no stale flash) →
      `RefreshNow()` (immediate fetch) instead of just `SetTarget` → `Show(null)`.
- [x] 8.3 Verified live against `LAZYNATS_DEMO` (persistent visual-testing bucket): descending into
      a bucket and switching between keys (`greeting` → `config.json`) now populates the details
      panel within roughly a network round trip, not up to 3 seconds.
