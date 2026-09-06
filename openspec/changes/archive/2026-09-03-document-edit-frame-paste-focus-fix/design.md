## Context

`EditFrame` wraps a single focusable child (a `TextField`/`TextView`, etc.) with no focus stop of
its own — see `openspec/specs/edit-frame/spec.md`'s Pass-Through Focus requirement. Several
dialogs (`PublishDialog`, `HeaderDialog`, `CreateBucketDialog`, `CreateKeyDialog`, ...) put a
field's initial focus on an `EditFrame`-wrapped child, either as the dialog's default initial
focus or by explicitly redirecting focus in an `isEdit` branch.

Terminal.Gui v2's `Runnable.RaiseIsModalChangedEvent` (`Runnable.cs`), called when a `Dialog`
becomes modal, does two things in sequence:

```csharp
SetFocus ();
App?.Navigation?.SetFocused (Focused);
```

`SetFocus()` correctly cascades `HasFocus` all the way to the deepest focusable descendant. But
`Focused` (as `View` defines it) is only the immediate focused SubView — one level deep — not
`MostFocused` (the true deepest leaf). So the second line re-points
`Application.Navigation`'s tracked focused view at the `EditFrame` itself, one level short of the
field actually holding focus.

Ordinary typing is unaffected because it walks the (correctly cascaded) `HasFocus` chain. But
bracketed paste — what a real terminal sends for a physical Ctrl+V —
(`ApplicationImpl.RaisePasteEvent`) reads `Application.Navigation.GetFocused()` directly. Right
after a dialog opens, that resolves to the `EditFrame`, whose `OnPaste` is the base `View` no-op,
so the first paste is silently swallowed. A subsequent real Tab/Shift+Tab "fixes" it permanently,
because `AdvanceFocus` re-syncs `Application.Navigation` through a different, correct code path
(`View.Navigation.cs`'s `RaiseFocusChanging`) that this bug doesn't touch.

This was confirmed by hand against a real terminal paste (PublishDialog's Subject field) — tmux's
bracketed-paste injection, used elsewhere in this project for driving the TUI non-interactively,
doesn't reproduce it, so it can't be used to regression-test this fix.

Source-verified against both Terminal.Gui v2.4.10 (this project's dependency) and current
v2.4.17: the bug is present, unfixed, and unreported (no upstream issue filed) in both.

## Goals / Non-Goals

**Goals:**
- Record the already-implemented fix (`EditFrame.WireInitialPasteFocusFix`) as a spec requirement
  of `edit-frame`, so the behavior is asserted rather than only living as a code comment.
- Make the workaround discoverable the same way `ViewPasteExtensions.FixPasteRedraw`'s Terminal.Gui
  workaround is: a clear comment plus a removal condition, now also reflected in `openspec/specs`.

**Non-Goals:**
- No source changes — `EditFrame.cs` already implements the fix; this change only documents it.
- Not attempting to fix the bug upstream (no issue is filed as of writing) or to generalize the
  workaround beyond `EditFrame`'s single-child case.

## Decisions

- **Document as a requirement on `edit-frame`, not a new capability.** The fix is intrinsic to
  what `EditFrame` promises (transparent pass-through focus, per the existing Pass-Through Focus
  requirement) — a reader relying on that requirement needs to know it also covers this
  modal/paste edge case, not just plain Tab navigation.
- **Keep the fix's mechanism (subscribing to the nearest ancestor `Runnable`'s `IsModalChanged`,
  blur+refocus on `_child`) in the code comment, not duplicated in prose in the spec.** The spec
  states the observable behavior (paste reaches the child immediately after the dialog becomes
  modal); the code comment remains the authoritative source for the Terminal.Gui internals,
  version numbers, and exact removal condition — consistent with how `ViewPasteExtensions.cs`'s
  redraw workaround is written up only in its own file.

## Risks / Trade-offs

- [The workaround silently stops mattering, or silently breaks, if Terminal.Gui changes
  `Runnable`'s modal-focus internals in a future version] → The code comment names the exact
  method and line to fix upstream (`RaiseIsModalChangedEvent`, `Focused` → `MostFocused`) as the
  removal trigger; no action needed here beyond keeping that comment accurate.
- [Real-terminal-only reproduction means this can't be covered by tmux-driven UI testing] →
  Documented explicitly in this design and in the code comment so a future contributor doesn't
  waste time trying to write a tmux-based regression test for it.
