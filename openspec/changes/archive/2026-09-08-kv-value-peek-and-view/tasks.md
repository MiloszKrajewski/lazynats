## 1. Extract shared read-only frame helper (no behavior change)

- [x] 1.1 Add a static `EditFrame.CreateReadOnly(string text, int y, int height, out Label view)`
      factory to `src/lazynats/Components/EditFrame.cs`, moving `MessageDetailDialog.WrapText`'s
      body (HotKeySpecifier disabled before Text assignment, multi-line/no-wrap TextFormatter,
      `Theme.ApplyEditableScheme`, non-focusable frame) there verbatim.
- [x] 1.2 Update `MessageDetailDialog`'s Subject/Headers frame construction to call
      `EditFrame.CreateReadOnly` instead of its own `WrapText`; remove the now-unused private
      `WrapText` method.
- [x] 1.3 Confirm via tmux that the Message Detail dialog (Subject/Headers section) renders
      identically to before (see `openspec/specs/message-detail-dialog/spec.md`).

## 2. Extract `PayloadDetailSection` shared component

- [x] 2.1 Create `src/lazynats/Components/PayloadDetailSection.cs`: a `View, IShortcutSource`
      taking `(byte[] data, string label, string emptyText)`, moving in
      `MaxPayloadVisibleLines`/`ScrollbarWidth`/`ScrollbarGap`/`PresentationDropDownWidth` and the
      classification (`PayloadContentProbe`/`PayloadPresentation.AllowedTypes`/`DefaultType`)
      currently inline in `MessageDetailDialog`'s constructor.
- [x] 2.2 Move `OpenPresentationSelector` (renamed `OpenSelector`, made public),
      `OnPresentationChanged`, `RefreshPayloadScrollState`, `BindScrollKeys`, and the
      `IShortcutSource.Shortcuts` payload-selector hint into `PayloadDetailSection`, using
      `EditFrame.CreateReadOnly` (task 1.1) for its own label/frame.
- [x] 2.3 Add `PayloadDetailSection.MeasureAndRender()`, called by the owner right after the
      owner's own initial `Layout()` — resolves the label's `Viewport.Width`, renders the default
      presentation, sets the frame height, adds the presentation dropdown (skipped when `data` is
      empty), and calls `BindScrollKeys` — reproducing `MessageDetailDialog`'s current sequence
      exactly (see design.md Decision 2).
- [x] 2.4 Update `MessageDetailDialog` to construct a `PayloadDetailSection` for its payload
      instead of the inline fields/logic being removed; keep its own `PreferredDialogWidth`/
      `TerminalWidthMargin`, its own `OnAccepting` override, and its own `Key.V` binding that
      calls `_payloadSection.OpenSelector()`; change its `Shortcuts` property to delegate to
      `_payloadSection.Shortcuts`.
- [x] 2.5 Confirm via tmux that every `message-detail-dialog` spec scenario still holds: default
      presentation per content kind, width computed once and stable across resize, scrolling,
      presentation switching in place, `V` summons the selector without initial focus, Esc closes,
      read-only.

## 3. Content-aware Key Detail panel peek

- [x] 3.1 Add `src/lazynats/Values/ValuePeek.cs`: `internal static string Render(byte[] value)`
      that classifies via `PayloadContentProbe.Classify`, then renders `Json` via
      `PayloadPresentation.Render(value, PayloadType.Json, _)` (width-independent), `Utf8Text` via
      `Encoding.UTF8.GetString(value)`, and `Binary` as a fixed-16-bytes-per-row hex dump (new
      small local helper — see design.md Decision 1, not `PayloadPresentation`'s adaptive `Hex`).
- [x] 3.2 Update `KeyDetails.BuildBody` to call `ValuePeek.Render(entry.Value)` instead of
      unconditional `Encoding.UTF8.GetString`.
- [x] 3.3 Confirm via tmux with a JSON value, a plain-text value, and a binary value (e.g. written
      via the `nats` CLI in `.bin/`) that the Key Details panel shows each per
      `openspec/specs/nats-kv/spec.md`'s updated "Key Detail Panel" scenarios, still clipped
      (not scrolled) when taller than the panel.

## 4. KV Value Detail dialog

- [x] 4.1 Add `KeyDetails.CurrentEntry` (the last `Entry` passed to `Show`, `null` when cleared),
      per design.md Decision 5.
- [x] 4.2 Create `src/lazynats/Values/ValueDetailDialog.cs`: a buttonless `Dialog` taking a
      `(string Bucket, KeyDetails.Entry Entry)` (or equivalent), with a "Details" frame (Bucket,
      Key, Revision, Created, Operation as `Label: Value` lines via `EditFrame.CreateReadOnly`)
      followed by a `PayloadDetailSection` labeled "Value" over `Entry.Value`; wire its own
      `Key.V` binding to `OpenSelector()`, `OnAccepting` override, and `ShortcutPickerLauncher.BindKey`
      — mirroring `MessageDetailDialog`'s post-extraction shape. Deliberately does NOT implement
      `IShortcutSource`/delegate to `_payloadSection.Shortcuts` itself — confirmed via tmux (see
      task 2.5) that doing so on `MessageDetailDialog` double-listed the same hint, since
      `ShortcutAggregator`'s walk already passes through `PayloadDetailSection` (this dialog's sole
      `CanFocus=true` descendant) on its way up to the dialog.
- [x] 4.3 Wire `V` at the key level: add a `ViewValueRequested` (or similarly named) event on
      `KeyListView` bound to `V` (alongside its existing Ctrl+N/E/D/F bindings), raised only when
      a key is highlighted; `ValuesTab` handles it by opening `ValueDetailDialog` with
      `_keyDetails.CurrentEntry` when non-null (no-op otherwise, per
      `openspec/specs/nats-kv/spec.md`'s new "Open KV Value Detail Dialog" requirement).
- [x] 4.4 Confirm the new dialog is discoverable via the key list's shortcut hints (`?` picker /
      status bar) per `keyboard-shortcut-discovery`.
- [x] 4.5 Confirm via tmux: `V` with no key highlighted does nothing; `V` right after highlighting
      a key (before its fetch lands) does nothing; `V` with a loaded entry opens the dialog showing
      bucket/key/revision/created/operation and the value under its default presentation;
      switching presentation and scrolling behave per `kv-value-detail-dialog` spec; Esc closes it.

## 5. Final validation

- [x] 5.1 `dotnet build src/lazynats.sln` succeeds with no new warnings.
- [x] 5.2 Re-run the Live Feed's Message Detail dialog by hand (tmux) once more end-to-end to
      confirm the extraction in section 2 introduced no regressions.
- [x] 5.3 `openspec validate --changes kv-value-peek-and-view --strict` passes.

## 6. Post-review refinements

- [x] 6.1 `PollingDetailsView.OnDrawingContent`'s body rendering now character-wraps each source
      line to the panel's width instead of hard-clipping it - see design.md Decision 1's addendum.
- [x] 6.2 `ValueDetailDialog` restructured from one combined "Details" frame into three sections
      (Key/Metadata/Value) mirroring `MessageDetailDialog`'s Subject/Headers/Payload shape, with
      the Key section colored `Theme.SubjectColor` (cyan) and the Metadata section colored
      `Theme.HeaderColor` (LimeGreen) - see design.md Decision 4's revision.
- [x] 6.3 Confirmed via tmux: a long plain-text value's peek now wraps onto additional rows
      instead of being clipped mid-line; the KV Value Detail dialog shows Key/Metadata/Value as
      three distinct sections for both a `Utf8Text` and a `Json` value.
