## 1. `payload-presentation` capability

- [x] 1.1 Add a static component (e.g. `PayloadPresentation`) under `src/lazynats/Payloads/`,
      reusing the existing `PayloadType` enum (no new enum — see design.md Decision 1) with:
      - `AllowedTypes(PayloadContentKind) -> PayloadType[]` per design.md Decision 2 (`Json` ->
        all four; `Utf8Text` -> `Text`/`Hex`/`Base64`; `Binary` -> `Hex`/`Base64`).
      - `DefaultType(PayloadContentKind) -> PayloadType` (`Json`->`Json`, `Utf8Text`->`Text`,
        `Binary`->`Hex`).
      - `Render(byte[] data, PayloadType type) -> string`, moving `FormatJson`'s
        indent-reserialize logic and `FormatHex`'s 16-bytes-per-row hex dump out of
        `MessageDetailDialog` unchanged, adding the plain UTF-8 decode for `Text` and a new
        `Base64` case (`Convert.ToBase64String`).

## 2. `MessageDetailDialog` selector wiring

- [x] 2.1 Compute the message's `PayloadContentKind` once (already done via
      `PayloadContentProbe.Classify`), then the allowed types and default type via the new
      `PayloadPresentation` component.
- [x] 2.2 Replace `FormatPayload`'s inline switch with a call into `PayloadPresentation.Render`,
      keyed by the currently selected type (starting at the default).
- [x] 2.3 Add a `DropDownList` (non-generic, `ReadOnly = true`, per design.md Decision 3 — not
      `DropDownList<PayloadType>`, which can't be restricted to a subset) next to the Payload
      label, with `Source = new ListWrapper<PayloadType>(new(allowedTypes))` and initial selection
      set to the default type's index.
      - Skip adding the selector at all when `allowedTypes` has fewer than 2 entries (nothing to
        switch between) — confirm against spec scenarios whether this is in scope; if the spec's
        "always offers Hex and Base64" scenarios require the control to always render even at 2
        entries, always add it.
      - Note: `GetCurrentSelectedIndex`/`SelectItemAtIndex` on the non-generic `DropDownList` are
        `private` (confirmed via reflection on the installed 2.4.10 package), unlike design.md's
        assumption. Selection is instead read back via the dropdown's `Text` (the selected item's
        `PayloadType.ToString()`, since `ListWrapper<T>` renders via `ToString()`), parsed back
        with `Enum.Parse<PayloadType>` - same architectural intent (no typed `Value`), different
        mechanism (`Text` instead of index). `allowedTypes` never has fewer than 2 entries per
        Decision 2 (Hex/Base64 always available), so the selector is always added whenever the
        payload is non-empty; only the empty-payload case (pre-existing "(empty payload)" special
        case, outside this proposal's scope) skips it.
- [x] 2.4 Wire the selector's selection-changed event to: re-run `PayloadPresentation.Render` with
      the newly selected type, update the payload `Label.Text`, recompute `CountLines`, and
      re-run/tear-down `WireScrolling` depending on whether the new text overflows
      `MaxPayloadVisibleLines` (design.md Decision 4).
- [x] 2.5 Confirm the payload frame's fixed height (sized from the default type at construction,
      per design.md Decision 4) doesn't need to change for this feature, and that switching
      selection only swaps content, never resizes the frame or dialog.
- [x] 2.6 Confirm changing the selector never touches `envelope.Message` (headers/subject/raw
      payload bytes) — it only affects the `Label`'s displayed text.

## 2a. Selector focus/discoverability (post-review UX fix)

- [x] 2a.1 Stop the presentation selector from claiming keyboard focus when the dialog opens
      (`CanFocus = false` at construction - confirmed via tmux that `TabStop = NoStop` alone is
      insufficient, since it only opts out of Tab-key cycling, not Terminal.Gui's separate
      activation-time "nothing focused yet, fall back to the only focusable descendant"
      behavior). See design.md Decision 5.
- [x] 2a.2 Bind a dedicated key (`V`) on the dialog that flips the selector's `CanFocus` back to
      `true`, focuses it, and opens its popover in one step (`InvokeCommand(Command.Toggle)` -
      the version-safe equivalent of `ToggleDropDown()`/`OpenDropDown()`, which are public only
      in a newer Terminal.Gui than this project's 2.4.10).
- [x] 2a.3 Make the dialog implement `IShortcutSource` (`V`/"Presentation" hint whenever a
      selector exists, empty otherwise) and bind the dialog's own `?` shortcut picker - a modal
      `Dialog` is a separate top-level `MainWindow`'s own `?` handler never sees, confirmed via
      tmux. Required two follow-up fixes, also tmux-confirmed: bind `?` via raw `KeyDown` (not
      `AddCommand`/`KeyBindings(Command.Context)`, which never reached a custom handler), and
      collect shortcuts via `ShortcutAggregator.Collect(this)` (not `MostFocused`, which resolves
      to non-descendant adornment scaffolding in this dialog's default no-focus state).
- [x] 2a.4 Verify via tmux: fresh dialog open -> `Down` does not change the presentation (proves
      no auto-focus); `?` on a fresh dialog shows "Presentation (v)"; `V` then `Down`/`Enter`
      changes the presentation; `Esc` closes the dialog both untouched and after a presentation
      change; an empty-payload message's `?` shows "No shortcuts for this view".
- [x] 2a.5 Return focus to the dialog and drop the selector's `CanFocus` back to `false` once a
      value is selected (`OnPresentationChanged`, mirroring `OpenPresentationSelector` in
      reverse - `SetFocus()` before `CanFocus = false`, not after, since the dropdown still holds
      focus when `ValueChanged` fires) - a selection is one-shot, not the start of an editing
      session. Verified via tmux: `V` -> `Down` -> `Enter` to select, then a bare `Down`
      afterward no longer re-cycles the value, and `Esc` still closes the dialog immediately.
- [x] 2a.6 Right-align the selector to the dialog's content area (`X = Pos.AnchorEnd()`, not
      `Pos.Right(payloadLabel) + 2`) rather than immediately after the "Payload" label. Verified
      via tmux.

## 3. Verification

- [x] 3.1 Manually exercise (or via `tmux`, per CLAUDE.md) all three content classifications:
      - A JSON message: confirm selector offers all four kinds, defaults to `Json`, and switching
        to `Text`/`Hex`/`Base64` renders correctly.
      - A plain-text message: confirm selector offers `Text`/`Hex`/`Base64` only (no `Json`).
      - A binary message (e.g. publish raw non-UTF8 bytes via the `nats` CLI in `.bin/`): confirm
        selector offers only `Hex`/`Base64`.
      - Verified live via `tmux` against a real `nats-server`, all three classifications and all
        four render paths, plus round-tripping the selection back to a shorter/taller kind.
      - Found and fixed a real crash during this pass: confirming the dropdown's selection (Enter)
        bubbled up as an unhandled `Accept` and closed the whole dialog (`Dialog`'s default
        "unhandled Accept -> RequestStop") - fixed with `OnAccepting` override (see 2.4's commit).
      - Found and fixed a second crash: re-running the scroll-wiring `AddCommand`/`KeyBindings.Add`
        calls on every presentation switch threw `InvalidOperationException: A binding for
        CursorUp exists` on the second switch - `KeyBindings.Add` is NOT safe to call twice for
        the same key/command pair (contrary to the installed package's own XML doc wording).
        Fixed by binding scroll keys exactly once in the constructor (`BindScrollKeys`) and having
        `RefreshPayloadScrollState` only toggle content height / the scrollbar flag on switches.
- [x] 3.2 Confirm a payload whose selected presentation overflows `MaxPayloadVisibleLines` remains
      scrollable (arrow keys / PageUp/PageDown), including after switching from a
      shorter-rendering kind to a taller one.
      - Verified via `tmux`: `PageDown` scrolls the payload even while the presentation dropdown
        holds focus. Note: plain Up/Down arrow keys, while the dropdown has focus, are consumed by
        the dropdown itself to cycle the presentation value (a real, working feature of
        `DropDownList` - Up/Down "when closed" changes selection) rather than reaching the
        dialog's scroll bindings; PageUp/PageDown remain unclaimed by the dropdown and always
        scroll. This matches the task's "arrow keys / PageUp/PageDown" phrasing as alternatives -
        at least one keyboard path to scroll remains available at all times.
- [x] 3.3 Confirm `Esc` still closes the dialog immediately regardless of which presentation kind
      is selected.
      - Verified via `tmux`, including immediately after a `PageDown` scroll and from each of the
        four presentation kinds.
- [x] 3.4 `dotnet build src/lazynats.sln` succeeds with no new warnings.

## 4. Spec sync

- [ ] 4.1 Run the openspec sync/archive workflow to merge this change's delta specs
      (`payload-presentation` new capability, `message-detail-dialog` modified requirements) into
      `openspec/specs/` once implementation is verified.
