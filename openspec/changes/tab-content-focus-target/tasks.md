## 1. Deterministic content-entry focus target

- [x] 1.1 Add a private helper in `ManagementTabs` that depth-first searches a view's `SubViews`
      for the first `Visible && Enabled && CanFocus` descendant, preferring a deeper match over an
      ancestor's own `CanFocus`.
- [x] 1.2 Change `FocusOwnContent()` to call `SetFocus()` on that helper's result (falling back to
      `Value` itself if no focusable descendant is found), instead of calling `Value.SetFocus()`
      directly.

## 2. Verification

- [x] 2.1 Run the app and reproduce the original bug on the Subscribe tab: focus the header, add a
      subscription (Ctrl+N), press Down, confirm the list is focused/highlighted on the *first*
      Down - no Up/Down bounce required.
- [x] 2.2 Confirm Down-from-header on the Publish tab lands on the Subject field (its first
      focusable descendant), not on the tab's outer content view.
- [x] 2.3 Confirm Up-from-content-to-header and Left/Right tab switching (existing `tab-navigation`
      scenarios) are unaffected.
