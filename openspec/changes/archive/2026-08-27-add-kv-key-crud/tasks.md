## 1. Printable-text helper

- [x] 1.1 Add `ValueText` (e.g. `src/lazynats/KVStore/ValueText.cs`) with a
      `TryDecode(byte[] value, out string text)` that returns `false` for invalid UTF-8
      (`System.Text.Unicode.Utf8.IsValid`) or any decoded control `Rune` other than `\t`/`\n`/`\r`,
      and `true` + the decoded text otherwise.

## 2. Create/Edit dialog

- [x] 2.1 Add `NewKeyOptions` (Name, Value) alongside a `TryParseName`-style non-empty check,
      mirroring `NewBucketOptions`' shape.
- [x] 2.2 Add `CreateKeyDialog: Dialog<NewKeyOptions>` in `src/lazynats/KVStore/`, mirroring
      `CreateBucketDialog`: Name `TextField` (locked when `isEdit`), Value `TextView` with
      `TabKeyAddsTab = false`, Cancel + Create/Save buttons, `OnAccepting` override, Commit/
      UpdateValidity wiring, reopen-with-values-preserved on failure.
- [x] 2.3 Verify by hand (or via tmux) that Tab/Shift+Tab move focus out of the Value `TextView`
      into the surrounding fields/buttons rather than inserting a tab character, and that Enter
      inserts a newline inside Value without submitting the dialog.

## 3. KeyListView wiring

- [x] 3.1 Call `EnableCreate()`, `EnableEdit()`, `EnableDelete()` in `KeyListView`'s constructor,
      mirroring `BucketListView`.

## 4. KvTab wiring

- [x] 4.1 Wire `_keyListView.CreateRequested` to open `CreateKeyDialog` (create mode) for the
      current `_currentBucket`, and on confirmation `PutAsync` the new entry, then
      `RefreshKeyListAsync(selectName: name)`.
- [x] 4.2 Wire `_keyListView.EditRequested` to: fetch the highlighted key's entry fresh via
      `store.TryGetEntryAsync<byte[]>`, run it through `ValueText.TryDecode`; on success open
      `CreateKeyDialog` (edit mode) seeded with the decoded text; on failure (missing key, or
      `TryDecode` returns `false`) raise `StatusChanged` with an explanatory message and open
      nothing.
- [x] 4.3 On edit confirmation, `PutAsync` the edited value, then `RefreshKeyListAsync(selectName:
      name)`.
- [x] 4.4 Wire `_keyListView.DeleteRequested` to a confirm `MessageBox.Query` (matching
      `TryDeleteBucketAsync`'s wording/default-button convention), then `DeleteAsync` the key on
      confirmation and `RefreshKeyListAsync` using `_keyListView.NeighborIdentity(key)`.
- [x] 4.5 Report server-side create/edit/delete failures the same way the bucket-level handlers
      do: a modal `MessageBox.ErrorQuery` with the exception message, reopening the dialog with
      previously entered values for create/edit failures.

## 5. Spec/doc cleanup

- [x] 5.1 Update `KeyDetails.cs`'s class comment (currently "never focusable, never edits
      anything") to reflect that the key level now supports editing (via the list-level Ctrl+E,
      same as `BucketDetails` never itself becoming focusable or editable either).
- [x] 5.2 Run `openspec validate --changes add-kv-key-crud` and confirm it still passes after any
      wording tweaks made during implementation.

## 6. Manual verification

- [x] 6.1 Via tmux against a real NATS server: create a key, edit its value, delete it, and
      confirm the key list and detail panel reflect each step.
- [x] 6.2 Create or otherwise seed a key with a binary (non-UTF-8) value on the server (e.g. via
      the `nats` CLI) and confirm Ctrl+E on it reports a status message instead of opening the
      edit dialog, while Ctrl+D still deletes it normally.
