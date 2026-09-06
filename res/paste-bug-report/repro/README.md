# Bracketed-paste-after-modal repro

Minimal standalone Terminal.Gui v2 app reproducing the bug described in `../report.md`.

## Run

```bash
dotnet run --project repro
```

(needs a real terminal, not tmux/psmux or another headless/injecting harness - see below)

## Steps

Press "Open Dialog" on the main window to open the repro dialog. **Field A** gets that
dialog's default initial focus (no explicit `SetFocus()` call, same as most fields in the
reporting app).

### Paste symptom (needs a real terminal's own Ctrl+V - see note below)

1. Copy some text to your clipboard.
2. Paste it into **Field A** right away, with no prior Tab/Shift+Tab: nothing appears.
3. Tab forward to **Field B** and paste: it works immediately (Field B was never focused at the
   moment the dialog became modal, so it isn't affected).
4. Shift+Tab back to **Field A** and paste again: it now works too, permanently, for the rest of
   this dialog's lifetime.

Close the dialog and press "Open Dialog" to repeat either test with a fresh dialog
instance.

**Note:** the paste symptom cannot be reproduced through a terminal multiplexer's synthetic
bracketed-paste injection (e.g. `tmux send-keys` / `paste-buffer`) - only a real terminal's own
Ctrl+V reproduces it. The paste symptom comes from the stale tracked focus described in
`../report.md`.

`SimpleFrame` (in `Program.cs`) is a stand-in for the reporting app's own wrapper view, stripped
to the one piece of shape relevant to this bug: `CanFocus = true` with no focusable content of
its own, so focus always drills through to its single wrapped child. Wrapping `TextField` in
such a view is what exposes the bug: `Application.Navigation`'s tracked focused view ends up
pointing at the wrapper, not the field, immediately after the dialog becomes modal.

## Workaround (not included in this repro)

The reporting app works around this by re-triggering `AdvanceFocus`'s correct
`Application.Navigation` re-sync itself: once the wrapper view is attached to the tree, it walks
up to find its nearest ancestor `Runnable` (the `Dialog`) and subscribes to that `Runnable`'s
`IsModalChanged`. When that fires with the wrapped child still holding focus, the wrapper blurs
and immediately re-focuses the child (`child.HasFocus = false; child.SetFocus();`), which forces
`Application.Navigation` to resolve to the field itself. This isn't reproduced in `SimpleFrame`
above so the repro shows the bug in isolation; the real fix belongs in `Runnable`, not in
user code (see `../report.md`'s suggested fix).
