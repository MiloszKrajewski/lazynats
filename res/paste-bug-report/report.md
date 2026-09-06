## Describe the bug

Right after a `Dialog` becomes modal, `Application.Navigation`'s tracked focused view is left
pointing one level short of the field that's actually focused - whenever that field sits behind a
thin "pass-through" wrapper `View` (`CanFocus = true`, no focusable content of its own, relying on
normal focus drill-down to reach the real field; not when the field is added directly as the
dialog's own child). Two independent things read that stale value instead of the correctly-set
`HasFocus` chain, and both misbehave as a result, until any subsequent Tab/Shift+Tab "fixes" it
permanently for the rest of that dialog's lifetime:

1. **No terminal cursor is drawn on the initially-focused field.** `ApplicationNavigation
   .UpdateCursor()` fetches its cursor from `Application.Navigation`'s tracked focused view once
   per main-loop iteration - i.e. the wrapper, not the field - and the wrapper has no `Cursor` set,
   so nothing is drawn. Tab away and back (or just Tab forward once) and the cursor appears and
   blinks normally from then on. This is the easiest way to observe the bug: no clipboard needed,
   and it's directly visible/GIF-able (see "Screenshots" below).
2. **The very first bracketed paste (what a real terminal sends for a physical Ctrl+V) into that
   field is silently dropped.** Ordinary typing into the same field works fine, since key input
   walks the `HasFocus` chain rather than `Application.Navigation`. Only this first paste, before
   any focus change, is lost; every paste afterwards works.

**Root cause** (source-verified against v2.4.10 and current v2.4.17 - present, unfixed, in both):
`Runnable.RaiseIsModalChangedEvent` in `Terminal.Gui/Views/Runnable/Runnable.cs` does, in order:

```csharp
SetFocus ();
App?.Navigation?.SetFocused (Focused);
```

`SetFocus()` correctly cascades `HasFocus` all the way down to the deepest focusable descendant
(the actual `TextField`). But the second line re-points `Application.Navigation`'s own tracked
focused view using `Focused` - which `View` defines as only *one level deep* ("the currently
focused SubView of this view") - instead of `MostFocused` (the true deepest leaf). So immediately
after a `Dialog` becomes modal, `Application.Navigation.GetFocused()` resolves to the wrapper
view, not the field inside it that's actually focused.

Both `ApplicationNavigation.UpdateCursor()`'s cursor lookup and bracketed paste's dispatch
(`ApplicationImpl.RaisePasteEvent`) read that same stale `Application.Navigation.GetFocused()`
value directly, rather than walking the (correctly-set) `HasFocus` chain the way ordinary typing
does - so both land on the wrapper's default (nothing: no `Cursor`, and `OnPaste` is the base
`View` no-op) instead of the field. A real Tab/Shift+Tab afterwards fixes both permanently, because
`AdvanceFocus` re-syncs `Application.Navigation` through a different, correct code path
(`View.Navigation.cs`'s `RaiseFocusChanging`) that this bug doesn't touch.

No upstream issue found for this as of 2026-09-03.

## To Reproduce

Steps to reproduce the behavior:

1. Run the minimal repro project included alongside this report (`repro/` - see its `README.md`):
   ```bash
   dotnet run --project repro
   ```
   Press "Open Dialog" on the main window to open a `Dialog` containing two fields
   ("Field A", "Field B"), each wrapped in a `SimpleFrame` - a stripped-down stand-in for a common
   "pass-through" wrapper `View` (`CanFocus = true`, no focusable content of its own, single child
   added via `Add()`; see the comment on `SimpleFrame` in `repro/Program.cs`). Field A receives the
   dialog's default initial focus (no explicit `SetFocus()` call).

   The essential shape, if inlining it directly:
   ```csharp
   internal sealed class SimpleFrame: View
   {
       public SimpleFrame(View child)
       {
           CanFocus = true; // required for a descendant to be focusable at all
           child.X = 0; child.Y = 0;
           child.Width = Dim.Fill(); child.Height = Dim.Fill();
           Add(child);
       }
   }

   var field = new TextField();
   var dialog = new Dialog();
   dialog.Add(new SimpleFrame(field) { Width = 40, Height = 1 });
   app.Run(dialog); // field gets default initial focus - it's the dialog's only focusable content
   ```

2. Expected behavior: with the dialog open, Field A shows a blinking terminal cursor immediately
   (it's already focused), and pasting (real terminal Ctrl+V) into it inserts the clipboard text
   immediately.

3. Actual behavior:
   - **Cursor**: Field A shows no cursor at all when the dialog first opens, even though it is
     focused and typing into it works. Press Tab (moves to Field B) and Shift+Tab (back to Field
     A) and the cursor now appears and blinks normally - permanently, for the rest of the dialog's
     lifetime.
   - **Paste**: the first paste into Field A is silently dropped - no text appears, no error, no
     event. Typing the same characters via the keyboard works fine. Pasting into Field B works
     immediately (it was never focused at modal-activation time). After the Tab/Shift+Tab above,
     pasting into Field A works too, from then on.

**Note on paste specifically:** it cannot be reproduced through a terminal multiplexer's synthetic
bracketed-paste injection (e.g. `tmux send-keys` / `paste-buffer`) - only a real terminal's own
Ctrl+V reproduces it. The cursor symptom above does not have this limitation - any real terminal
running the repro shows it, no clipboard interaction needed. See "Additional context" below.

## Environment

### OS Information

**Output:**
```
OS: Microsoft Windows 11 Pro 10.0.26100
```

### Terminal Information

**Windows Terminal:**
```
Terminal: Windows Terminal 1.24.11911.0
```

### PowerShell Version

**Output:**
```
Major  Minor  Patch  PreReleaseLabel BuildLabel
-----  -----  -----  --------------- ----------
7      6      5
```

### .NET Information

**Output:**
```
.NET SDK: 10.0.204
Host: 10.0.11 (x64)
OS: Windows 10.0.26100 (win-x64)
```

### Terminal.Gui Version

**Version:**
```
2.4.10 (NuGet package, this project's pinned dependency)
```

Also source-verified (reading the code, not run) against current `develop` at v2.4.17 - the same
`Focused`-vs-`MostFocused` bug is present there too, unchanged.

## Screenshots, GIFs, or Terminal Output

Not included here, but easy to capture and worth attaching: an animated GIF of the repro's Field A
showing no cursor at launch, then Tab/Shift+Tab, then the cursor blinking - no clipboard
interaction needed, so this symptom (unlike the paste one) is straightforward to record.

## Additional context

- **Consistent, not intermittent**: both symptoms reproduce every time a dialog opens, as long as
  the initially-focused field is behind a `CanFocus = true` pass-through wrapper `View`. Neither
  reproduces when the field is added directly as the dialog's own child (no wrapper) - presumably
  because in that case `Focused` and `MostFocused` already agree.
- **Cursor symptom confirmed via Terminal.Gui's own docs**, not just by reading `Runnable.cs`:
  https://gui-cs.github.io/Terminal.Gui/docs/cursor.html and
  https://gui-cs.github.io/Terminal.Gui/docs/navigation.html both describe
  `ApplicationNavigation.UpdateCursor()` as fetching the *tracked* focused view's cursor once per
  main-loop iteration, and the "Troubleshooting" section on the first page lists "is the most
  focused view in the chain" (i.e. `Application.Navigation`'s tracked value, not just `HasFocus`)
  as a precondition for the cursor to show at all - exactly the value this bug leaves stale.
- **Real-terminal-only for the paste symptom**: confirmed by hand against a real Windows Terminal
  Ctrl+V. A terminal multiplexer's synthetic bracketed-paste injection does not reproduce it, so
  this specific symptom cannot be covered by any test harness that only injects paste sequences
  that way. The cursor symptom has no such limitation.
- **Suggested fix**: in `Runnable.RaiseIsModalChangedEvent`, change
  `App?.Navigation?.SetFocused (Focused);` to use `MostFocused` instead of `Focused`, matching what
  `SetFocus()` on the line above already correctly cascades to.
- Found while building a terminal UI app on Terminal.Gui v2.4.10; worked around locally by
  re-triggering `AdvanceFocus`'s correct re-sync path (blur + refocus the wrapped child) once the
  containing `Runnable`'s `IsModalChanged` fires - see `repro/README.md` for the shape of that
  workaround, kept out of the repro project itself since it isn't part of the bug.

## For Maintainers

**Set Project & Milestone:** If you have access, please don't forget to set the right Project and
Milestone.
