# Why "accent-color the focused frame/tab" isn't a simple change

Status: attempted and abandoned (2026-09). Kept here so a future attempt doesn't re-discover the
same dead ends. If Terminal.Gui's rendering internals change in a later version, re-verify
everything below live before trusting it - all of it was confirmed against the specific installed
version (2.4.17) at the time, not read off documentation alone.

## The ask

Alt+0..5 jumps keyboard focus straight to the Live Feed panel or a management tab, but nothing in
the UI showed which one currently has it. The goal was a color accent (border, then title text)
on whichever frame/tab currently holds keyboard focus - "a quick clue what is selected."

## Attempt 1: recolor the whole border

`View.Border.GetOrCreateView()` returns an `AdornmentView` (not the lightweight `Border` settings
object itself, which has no `SetScheme`/`GetAttributeForRole` in 2.4.17 - this was already a
surprise, since the design this started from assumed 2.4.10's `Border` was directly a `View`).
Setting `borderView.SetScheme(new Scheme(new Attribute(accentColor, bg)))` on focus and `null` on
blur genuinely does recolor the drawn border lines and corners, confirmed via `tmux
capture-pane -p -e` (reads the pane's actual rendered ANSI attributes, not just characters).

**This broke at shared corners/T-junctions between adjacent, differently-colored views** (e.g. an
accented tab next to an unaccented one). Root cause, found by reading
`Terminal.Gui/Drawing/LineCanvas/LineCanvas.cs` directly (fetched from the `gui-cs/Terminal.Gui`
GitHub repo at the `v2.4.17` tag - the installed package's XML docs don't include this):

```csharp
private Attribute? GetAttributeForIntersects (ReadOnlySpan<IntersectionDefinition> intersects) =>
    Fill?.GetAttribute (intersects [0].Point) ?? intersects [0].Line.Attribute;
```

At any cell where multiple lines cross, the attribute used is whichever line happens to be
`intersects[0]` - the first in an internally-ordered list of lines touching that cell. There is no
accent-aware or focus-aware tie-break. Confirmed live (via a diagnostic that also swapped
`LineStyle` between `Single`/`Heavy` alongside the color, so the *rune* itself differed and could
be read reliably through `tmux` even where color-only changes couldn't - see the next section) that
which line wins is **deterministic per intersection type, not per focus state**: a plain 2-line
corner and a 3-line T-junction resolved the ambiguity in opposite directions. This is an inherent
property of `LineCanvas`, not something a caller can fix by setting colors more carefully - it
would need a much deeper hook (see "If this gets picked up again" below).

## Testing gotcha: tmux/psmux under-reports color-only changes

Independently of the above, `tmux` (via the `psmux` Windows wrapper this project's tooling uses)
does not reliably re-send a cell to the attached terminal when only its **color** changes and its
**character** doesn't - even though `tmux capture-pane -p -e` (which reads tmux's own internal
screen buffer, not what was actually flushed to the live display) shows the correct color. This
initially looked exactly like "only the border corners recolor, straight runs don't" when tested
live in Windows Terminal over tmux/psmux - a red herring that cost real time before being
distinguished from the actual `LineCanvas` issue above. Confirmed as a tmux-specific display
artifact (not a `lazynats` or Terminal.Gui bug) by testing outside tmux, directly in Windows
Terminal, where border-line recoloring worked correctly and fully.

**Takeaway for future color-related UI verification**: `tmux capture-pane -p -e` is reliable for
confirming what Terminal.Gui *computed* (the logical correctness of a color change), but not for
confirming what a human would actually *see live* in that same tmux pane - for that, either test
outside tmux entirely, or make the change also swap something rune-level (a character, a
`LineStyle`) so tmux's client-refresh has a reason to re-send the cell.

## Attempt 2: title-text-only recoloring (border lines left untouched)

Given the whole-border approach was structurally unfixable, the fallback was to recolor only the
title *text*, which is drawn directly (`GetAttributeForRole(HasFocus ? VisualRole.Focus :
VisualRole.Normal)`) rather than through `LineCanvas` merging - so only `Scheme.Focus`/`HotFocus`
were overridden, leaving `Normal` (and therefore every line/corner) alone.

**This worked cleanly for a plain `FrameView`'s inline title** (e.g. the Live Feed panel, `"
0:Live Feed "`): setting `Focus`/`HotFocus` on `Border.GetOrCreateView()` recolors the title text
on focus and reverts on blur, with zero effect on the border lines - confirmed live, including
correctly reverting when a modal dialog opens on top of it.

**It did not work for a `ManagementTabs` tab's header text**, despite trying every plausible target
found by reading `BorderView.cs`/`TitleView.cs` directly:

1. The tab's outer `Border.GetOrCreateView()` (same object that worked for the plain `FrameView`
   case) - no effect on the tab header text.
2. `BorderView.TitleView` (a separate, lazily-created `View` - tabs render their header through a
   dedicated `TitleView` SubView, not directly through the tab's own `BorderView`) - no effect.
3. `TitleView.Border.GetOrCreateView()` (TitleView has its *own* nested border, per its own doc
   comment: "The view has its own border ... so its border lines auto-join with the View's content
   border via LineCanvas") - setting `Focus`/`HotFocus` alone had no effect; additionally setting
   `Normal` did visibly change *something*, but only the decorative `│` separator glyphs shared
   with the *neighboring* tab - not the title text itself - and reintroduced a smaller-scoped
   version of the exact `LineCanvas` boundary ambiguity from Attempt 1.
4. The tab's own content view (e.g. `subscribeTab` itself, as the plain-English "owning view" -
   docs describe TitleView as inheriting "color attributes from its owning View's scheme") - no
   effect.

None of these actually recolored the "1:Subscribe"-style text. Wherever that text's attribute
really comes from was not identified in the time spent. A relevant clue not followed up on: an
`hasFocus` local in `BorderView.DrawTabBorder` is computed via `IsFocusedOrLastTab()`
(`border.Parent?.HasFocus`, i.e. the *tab content view's* own focus - not `TitleView`'s), but that
variable is only ever used there for border **geometry** (whether to open a gap at the tab/content
seam), not for attribute selection - so it isn't the missing piece either.

## What's left in the code

Nothing - this attempt was fully reverted (`Theme.cs`, `MainWindow.cs` back to their prior
committed state; the `openspec/changes/focused-frame-accent-color/` planning docs deleted). The
Live Feed frame does not have an accent color; no tab does either. `TODO.md` still lists "different
frame color for active window" as a pending idea.

## If this gets picked up again

- Re-verify the installed Terminal.Gui version's behavior from scratch - don't trust this
  document's specifics if the package version has moved past 2.4.17, since the `Border`/`BorderView`
  split alone already invalidated the prior design's assumptions once.
- `BorderView.cs` has an inert `#if TAB_COLOR_PROTOTYPE` block wrapping an empty
  `OnGettingAttributeForRole` override, compiled out by default. It looks like an intentional but
  unfinished upstream hook for exactly this kind of per-tab visual-role coloring - worth checking
  whether a later Terminal.Gui release finishes or documents it before building another workaround.
- A completely different, non-color indicator (e.g. a leading glyph in the `Title` string itself,
  changed on focus) would sidestep all of the above - it doesn't touch `LineCanvas` or any nested
  `TitleView` attribute resolution at all, at the cost of not being "a color."
- Any renewed color attempt should keep the `App.Navigation.FocusedChanged`-driven design from this
  attempt (not each view's own `HasFocusChanged`): `HasFocusChanged` was confirmed live to never
  fire when a modal `Dialog` is pushed as a separate session/Toplevel on top of an already-focused
  frame, since `HasFocus` reflects position in a view's local `SuperView` ancestor chain, which a
  modal's disconnected session never touches. `App.Navigation.FocusedChanged` is global and does
  correctly fire in that case.
