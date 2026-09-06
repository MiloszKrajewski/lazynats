## Context

The KV tab (`src/lazynats/KVStore/`) is a two-level `DrillableListView` tab: bucket list ↔ key
list, exactly like `StreamsTab`. The bucket level already has full CRUD (`CreateBucketDialog`,
`KvTab.OpenCreateBucketDialog`/`OpenEditBucketDialog`/`TryDeleteBucketAsync`), all wired through
the shared `DrillableListView.EnableCreate`/`EnableEdit`/`EnableDelete` shapes. The key level
(`KeyListView`/`KeyDetails`) is currently read-only by design — `nats-kv`'s "Read-Only Tab"
requirement — with `KeyDetails` only ever polling and rendering a key's decoded UTF-8 value, never
writing.

This change brings the key level up to the same CRUD shape as the bucket level, scoped to plain
string values. `NatsKVEntry<T>.Value` is `byte[]`, and a KV value can be arbitrary binary — a
general editor would need a text/hex toggle. That's out of scope here; instead, editing a key
whose current value isn't printable text is refused outright, with the door left open for a
"smart" editor later.

## Goals / Non-Goals

**Goals:**
- Create, edit, and delete a key/value entry from the key level, mirroring the bucket level's
  Ctrl+N/Ctrl+E/Ctrl+D shape and dialog conventions exactly.
- Support multi-line string values (JSON, etc.) without a heavyweight editing mode.
- Never silently corrupt or truncate a binary value — refuse to edit it instead.

**Non-Goals:**
- A text/hex "smart" editor for binary values (mentioned in the proposal as future work).
- Optimistic concurrency (revision-based CAS) on edit — same simplification Edit Bucket already
  makes.
- Renaming a key (Name is locked in edit mode, same as Name/Storage in Edit Bucket).
- Any cap or special handling for very large values beyond what already exists (read-only
  clipping in `KeyDetails`).

## Decisions

### 1. Dialog shape mirrors `CreateBucketDialog`, not `ListEditorView`

A new `CreateKeyDialog: Dialog<NewKeyOptions>` follows `CreateBucketDialog`'s exact shape: Tab
between fields, Enter-on-a-field inert (`OnAccepting` override returns `true`), a single
Create/Save button plus a plain Cancel, `isEdit` locking the Name field, reopen-with-values-
preserved on a server-side failure.

`ListEditorView<T>` (the other candidate, used by `SubscriptionsView`/`PublishTab`'s header
editor) was considered and rejected: it generalizes a *flat* list's own New/Edit/Delete UI, but
the key level is already a `DrillableListView` page inside a two-level tab, matching the bucket
level's shape — introducing a second, differently-styled editing mechanism on the same tab would
read as inconsistent for no benefit.

### 2. Value field is a multi-line `TextView` with `TabKeyAddsTab = false`

Unlike every other field in `CreateBucketDialog` (all `TextField`), Value uses a `TextView` so a
JSON-like or multi-line value can be typed/pasted directly, matching what `KeyDetails` already
renders (UTF-8 text, not a single line).

Confirmed from `Terminal.Gui`'s source
(`Views/TextInput/TextView/TextView.Commands.cs`, `ProcessTab`): `Key.Tab`/`Key.Tab.WithShift` are
bound unconditionally to `Command.NextTabStop`/`PreviousTabStop`, but the handler
(`ProcessTab`) starts with `if (!TabKeyAddsTab || _isReadOnly) return false;` — with
`TabKeyAddsTab = false`, both directions return unhandled, and the key bubbles up to ordinary
focus navigation exactly as it would for a `TextField`. This means the dialog needs none of
`PublishTab`'s Navigate/Edit toggle (that toggle also had to solve arrow-key trapping across
multiple bands in a full tab — not a concern inside a 2-field modal, where arrows aren't a field-
navigation key to begin with). There's no built-in binding to insert a literal tab once
`TabKeyAddsTab` is false (no Ctrl+Tab fallback), which is acceptable — KV values aren't source
code needing indentation.

`Enter` is left at its `TextView` default (`EnterKeyAddsLine = true`, inserts a newline) — that's
exactly what a multi-line value editor should do with Enter, and it doesn't conflict with the
dialog's own submit convention since Create/Save has always been an explicit button, never
Enter-to-submit, even for the existing `TextField`-only dialogs.

### 3. Printable-text guard is a fresh fetch + a UTF-8/control-character check, gating Edit only

A new static helper, `ValueText.TryDecode(byte[] value, out string text)`, returns `false` when
`value` isn't valid UTF-8 (`System.Text.Unicode.Utf8.IsValid`) or decodes to any `Rune` that's a
control character other than `\t`/`\n`/`\r`. `true` + the decoded `text` otherwise.

`KvTab`'s `EditRequested` handler for the key level does its own fresh
`store.TryGetEntryAsync<byte[]>(key)` fetch (same call `KeyDetails.FetchAsync` already makes) —
not a reuse of whatever `KeyDetails` last polled — then runs `ValueText.TryDecode` on the result:
- success → open `CreateKeyDialog` in edit mode seeded with the decoded text.
- failure (missing key, or binary value) → report via `StatusChanged` (e.g. `"KV: Cannot edit —
  value is not printable text"` or a not-found message) and never open the dialog.

Fetching fresh (rather than trusting `KeyDetails`'s last poll) mirrors the existing precedent that
the key level never caches entries (`nats-kv`'s "Key List" requirement: descending always
re-fetches), and narrows the staleness window before an edit.

Create is unaffected by this guard — a value typed into `CreateKeyDialog`'s `TextView` is always
valid text by construction, so there's nothing to reject.

### 4. Delete uses `DeleteAsync` (soft delete/tombstone), not `PurgeAsync`

Matches the default, reversible-in-principle semantics `nats kv del` uses (a delete marker,
history preserved subject to the bucket's own History/Limit-Marker-TTL settings) rather than a
hard, unrecoverable purge. The confirm prompt still warns "This cannot be undone" (from the app's
point of view, once confirmed there's no in-app undo), matching Delete Bucket's wording.

### 5. Edit writes via plain `PutAsync`, no CAS

Same simplification "Edit Bucket" already makes (merges onto a freshly-fetched config, but never
checks the fetch is still current at write time). A concurrent external write between the
edit-dialog's fetch and the user's Save silently loses that concurrent write — accepted for this
change; revision-based CAS (`UpdateAsync(key, value, expectedRevision, ...)`) is future work if
this turns out to matter in practice.

### 6. List refresh after mutation mirrors the bucket level exactly

Create/Edit → `RefreshKeyListAsync` (re-fetch of the whole key list — the key level has no
per-item caching to patch) with the affected key passed as `selectName` so it ends up highlighted.
Delete → `RefreshKeyListAsync` using `_keyListView.NeighborIdentity(key)` for the post-delete
highlight, same as `TryDeleteBucketAsync`.

## Risks / Trade-offs

- **No CAS on edit** → a concurrent external write can be silently clobbered. Mitigation:
  edit always re-fetches immediately before opening the dialog, narrowing (not eliminating) the
  window; documented as accepted, matching Edit Bucket's existing precedent.
- **Printable check is a heuristic, not a format guarantee** → a byte sequence that happens to be
  valid UTF-8 but was never intended as text (e.g. structured binary that decodes without error)
  is treated as editable. Not a correctness risk: the app only ever writes back exactly the text
  it rendered, so nothing is corrupted — worst case is a value some other tool treats as opaque
  gets a text-shaped round-trip through this UI, which is inherent to string-only KV editing and
  called out as a non-goal (hex editing) in the proposal.
- **No size cap on the Value `TextView`** → editing a very large value is unwieldy but not capped;
  consistent with not adding scope beyond "name and value, nothing fancy."

## Migration Plan

Purely additive UI + one removed spec requirement ("Read-Only Tab", replaced by the new Create/
Edit/Delete Key requirements). No persisted state, no server-side migration, no rollback concern
beyond reverting the change.
