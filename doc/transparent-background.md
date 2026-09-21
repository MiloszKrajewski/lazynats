# Transparent (terminal-default) background

Status: implemented (2026-09-21), pending real-terminal visual verification. User is
contemplating reverting this - kept here so the reasoning and the exact revert are easy to find
either way. Full OpenSpec trail: `openspec/changes/transparent-background/` (not yet archived).

## The ask

Running lazynats in a terminal configured with a background that's dark-but-not-pure-black (or
one with acrylic/opacity effects), the app rendered as a flat, literal black rectangle that
didn't match the terminal around it. Question: can the app's background be "transparent" so the
terminal's own background/opacity shows through instead?

## The one-line change

`src/lazynats/Program.cs`, `ApplyColorTheme()`:

```diff
- var normal = new Attribute(ColorName16.Gray, ColorName16.Black);
+ var normal = new Attribute(ColorName16.Gray, Color.None);
```

This `normal` value backs the `Normal` role on both the `"Base"` and `"Dialog"` `SchemeManager`
overrides, so it's the one edit needed for both plain windows/panes and dialogs/`MessageBox`.

### Revert

Change `Color.None` back to `ColorName16.Black` on that one line. Nothing else in the codebase
depends on this (see "What's untouched" below), so no cleanup is required beyond that.

## Mechanism

Terminal.Gui v2 ships `Color.None` (alpha = 0) specifically for this. When an `Attribute`'s
foreground or background is `Color.None`, the driver (`OutputBase.AppendOrWriteAttribute`) emits
an ANSI **reset** sequence instead of an explicit color - `CSI 39m` for foreground, `CSI 49m` for
background - so the terminal fills that cell with whatever it would anyway (its own configured
default, including any acrylic/opacity blending it applies specifically to default-background
cells). This is unconditional at render time; it does not depend on detecting terminal
capabilities.

(Terminal.Gui does separately use an OSC 10/11 query at startup to resolve `Color.None` to a
*concrete* color, but only for internal color-math - deriving a brightened/dimmed variant of a
scheme role. That resolution falls back to White/Black if the query fails, and has no bearing on
the reset-sequence behavior above.)

Matching the terminal's actual background RGB value explicitly (instead of using `Color.None`)
was considered and rejected: some emulators (Windows Terminal included) only apply acrylic/opacity
blending to cells drawn with the *default* background specifically, not to a solid color that
happens to match it visually. Only `Color.None` gets genuine transparency, not just color parity.

## What's untouched

Only `Normal`'s background component changed. Left as-is, deliberately:

- `Theme.EditableBackground` (the near-black truecolor constant used by every editable
  control) - stays opaque, for legibility against field text.
- `Dialog`'s `Focus` attribute (inverted black-on-white highlight bar) - stays opaque.
- Every other Scheme role (`Disabled`, `HotNormal`, etc.) on `"Base"`/`"Dialog"` - `Program.cs`'s
  `ApplyColorTheme()` only ever assigned `Normal`/`Editable` (and `Dialog`'s explicit `Focus`);
  everything else is copied verbatim from Terminal.Gui's stock scheme via the
  `new Scheme(SchemeManager.GetScheme(...))` copy-constructor, not derived from `Normal` - so
  nothing else picked up `Color.None` as a side effect.
- `Components/TabbedView.cs`'s `GetAmbientBackground()` already reads
  `GetAttributeForRole(VisualRole.Normal).Background` live rather than hardcoding a color (prior
  art for this exact class of fix, at smaller scope - see its own comment for the "hardcoded
  Color.Black didn't match the actual app background" bug that motivated it). It picks up
  `Color.None` automatically, no code change needed there.

## Verification status

- Build: clean.
- Smoke test (tmux launch): app starts and renders normally, no crash/layout regression.
- **Not yet confirmed**: whether the background actually reads as "transparent"/matching the
  terminal in a real terminal window. `tmux capture-pane` strips all color/attribute information,
  so it cannot confirm this - this genuinely needs eyes on a real terminal, which is also why this
  is documented here rather than just shipped silently.

## Spec

`openspec/specs/color-theme/spec.md`'s "App-wide base color theme" and "Dialogs match the app's
base color theme" requirements previously mandated `ColorName16.Black` for the base/dialog
`Normal` background "regardless of the host terminal's own default background setting" - the
delta at `openspec/changes/transparent-background/specs/color-theme/spec.md` updates those two
requirements to expect `Color.None` instead. Not yet synced into the main spec (the change hasn't
been archived) - if this gets reverted, discard that change directory instead of syncing it.
