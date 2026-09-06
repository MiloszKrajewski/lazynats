## 1. Verify existing implementation against the new requirement

- [x] 1.1 Confirm `EditFrame.WireInitialPasteFocusFix` (`src/lazynats/Components/EditFrame.cs`,
      currently uncommitted) matches the ADDED requirement's scenarios: paste-ready focus with no
      prior Tab/Shift+Tab, and no effect on `EditFrame`s whose child isn't the one focused.
      Confirmed: the `IsModalChanged` handler is gated on `_child.HasFocus`, so only the
      genuinely focused `EditFrame` in a dialog ever blurs/refocuses its child.
- [x] 1.2 Confirm the in-code comment still accurately names the Terminal.Gui versions checked
      (v2.4.10, v2.4.17), the exact upstream fix (`RaiseIsModalChangedEvent`, `Focused` →
      `MostFocused`), and that no upstream issue has since been filed — update the comment if any
      of that has changed since it was written.
      Confirmed against GitHub source for both `tui-cs/Terminal.Gui` tags `v2.4.10` and `v2.4.17`:
      `RaiseIsModalChangedEvent` in `Terminal.Gui/Views/Runnable/Runnable.cs` calls `SetFocus();`
      then `App?.Navigation?.SetFocused(Focused);` in both, unchanged. Searched
      `tui-cs/Terminal.Gui` issues for paste/modal/focus — no matching upstream issue found.

## 2. Land the documentation

- [x] 2.1 Commit the working-tree fix in `EditFrame.cs` together with this change's spec delta
      (no other source changes expected). Committed as `0dc62f9`.
- [x] 2.2 Manually re-verify the fix against a real terminal (not tmux — bracketed paste isn't
      reproducible through tmux's paste injection): open a dialog with an `EditFrame`-wrapped
      field focused by default (e.g. `PublishDialog`'s Subject field) and paste immediately.
      Confirmed working by the user.
