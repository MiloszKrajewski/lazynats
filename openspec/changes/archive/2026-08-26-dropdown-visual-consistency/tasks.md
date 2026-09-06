## 1. Investigation spike (run against the real app via `tmux`, per `CLAUDE.md`)

- [x] 1.1 Launch `lazynats` in a detached `tmux` session against a real NATS server, open
      `CreateConsumerDialog` (or `CreateStreamDialog`), and `tmux capture-pane` the Ack
      Policy/Retention dropdown in its closed unfocused, closed focused, and expanded states, to
      confirm today's actual mismatch (status-bar/dialog text as a focus proxy, since colors don't
      come through `capture-pane`) — use as the "before" baseline to compare fixes against.
- [x] 1.2 Add a temporary diagnostic (e.g. a one-off `Console.Error.WriteLine`/log line, or a
      breakpoint via a debug build) at the point where `DropDownList`'s popup's
      `GettingAttributeForRole` handler resolves `App?.TopRunnableView?.MostFocused`, to determine
      what view that actually is while the popup is open. Resolves the design.md Open Question on
      whether the popup needs its own explicit background fix or already inherits one once the
      closed-control `Scheme` fix (task 2) is applied.
- [x] 1.3 Add a temporary diagnostic in a `PopoverRegistered` handler to confirm it fires (a)
      before the popup's first `MakeVisible()`, and (b) on every subsequent open, not just the
      first — needed for task 3's width fix to be reliable rather than accidentally-working-once.
- [x] 1.4 Remove all temporary diagnostics added in 1.2/1.3 once their answers are captured; fold
      the findings into `design.md`'s Open Questions section (resolving or updating them) before
      continuing.

## 2. Closed-control background fix

- [x] 2.1 Add a small shared helper (e.g. `Theme.ApplyEditableScheme(View)` in `Theme.cs`, or
      similar — exact shape per design.md's Decision 1) that sets a `Scheme` with `Normal` and
      `Focus` both equal to `new Attribute(ColorName16.White, Theme.EditableBackground)` (matching
      `Program.cs`'s existing `editable` attribute) on a given view via `SetScheme`.
- [x] 2.2 Apply that helper to `_ackPolicyDropDown` and `_deliverPolicyDropDown` in
      `CreateConsumerDialog.cs`, and to `_retentionDropDown` in `CreateStreamDialog.cs`.
- [x] 2.3 Verify via `tmux capture-pane` (status-bar/dialog-appearance proxy, per `CLAUDE.md`) that
      the closed dropdown's `EditFrame` rule now matches a `TextField`'s in both focused and
      unfocused states — WrapField's existing `GetAttributeForRole(VisualRole.Editable)` read
      should now resolve correctly with no changes to `WrapField` itself.

## 3. Popup background and width fix

- [x] 3.1 Based on task 1.2's finding: if the popup's `Normal` role does not already resolve to
      `Theme.EditableBackground` once task 2 is applied, add a `Scheme` (or direct
      `ContentView.SetScheme` call) to the popup's `ContentView` fixing its background too;
      otherwise, confirm no extra work is needed here and note that in `design.md`. **Confirmed no
      extra work needed** — see design.md's Open Questions.
- [x] 3.2 Add a `PopoverRegistered` handler (in `Program.cs`, alongside `ApplyColorTheme()`, or
      another single app-wide wiring point — per design.md's Decision 2) that, when the registered
      popover is a `Popover<ListView, string?>` with a non-null `Anchor`, sets `ContentView.Width`
      from `Anchor()?.Width`.
- [x] 3.3 Verify via `tmux capture-pane` that the expanded popup's width now matches the closed
      control's width for all three current dropdowns, and that a long enum member name (if any
      exceeds the control's width) clips rather than widening the popup.

## 4. Spec/documentation cleanup

- [x] 4.1 Confirm the final implementation matches every scenario in
      `openspec/changes/dropdown-visual-consistency/specs/color-theme/spec.md` and
      `specs/dropdown-popup-width/spec.md`; adjust either the implementation or (if reality turned
      out differently after the spike) the spec text to match, before archiving.
- [x] 4.2 Update `design.md`'s Open Questions/Risks sections with the spike's actual findings if
      they weren't already folded in during task 1.4.
