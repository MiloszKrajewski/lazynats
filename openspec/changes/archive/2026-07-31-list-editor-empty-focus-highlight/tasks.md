## 1. Focus tracking on ListEditorView<T>

- [x] 1.1 Confirm the current Terminal.Gui v2 mechanism for observing a `View`'s focus gained/lost
      (`doc/terminal-gui-howto.md`; verify against the actual API, not v1 `Enter`/`Leave`).
- [x] 1.2 Wire that mechanism up on `ListEditorView<T>` so it's notified when the component gains
      or loses keyboard focus.

## 2. Empty-hint scheme

- [x] 2.1 Add a private method that computes `_emptyHintLabel`'s `Scheme` from current focus state
      and the existing `_background` override (focused → highlighted/focused-row style; unfocused
      → today's `Disabled`-role style, background-adjusted as it already is).
- [x] 2.2 Call that method from construction, from the focus-changed handler added in 1.2, and from
      the `Background` setter, so all three inputs stay in sync.
- [x] 2.3 Verify the hint remains `CanFocus = false` and outside list selection/navigation — only
      its rendered style changes.
- [x] 2.4 Also recompute the scheme from `UpdateEmptyHintVisibility` (item-collection changes), not
      only from the focus handler: a modal opened via `TryCreate`/`TryEdit` doesn't reliably re-raise
      `OnHasFocusChanged` on close, so without this, deleting back down to empty right after an
      add-via-modal left the hint showing stale unfocused styling despite focus still being there.

## 3. Verification

- [x] 3.1 Manually exercise an empty `SubscriptionsView` (or Publish's empty header list): confirm
      the hint is dim when unfocused and highlighted when the list editor holds focus, and reverts
      correctly when focus moves away.
- [x] 3.2 Confirm behavior is unchanged for the non-empty case (list row focus highlighting as
      today) and for Ctrl+N/E/D semantics while empty.
- [x] 3.3 Check both a default-scheme list editor and one with an explicit `Background` set (e.g.
      inside `EditFrame`) to confirm both hint styles honor the configured background.
