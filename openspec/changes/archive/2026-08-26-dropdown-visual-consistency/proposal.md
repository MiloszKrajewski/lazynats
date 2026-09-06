## Why

`DropDownList<T>` (used for Ack Policy / Deliver Policy in `CreateConsumerDialog`, Retention in
`CreateStreamDialog`) doesn't match the app's established editor-control look: `TextField`s wrapped
in `EditFrame` show the app's gray `Theme.EditableBackground`, but the dropdown's closed control
and its expanded popup both render on the plain black `Normal` background instead, so it visually
reads as a different kind of control. Separately, the expanded popup's width is sized to its
longest item's text rather than to the dropdown control's own width, which looks inconsistent with
the rest of the field layout. Decompiling the installed `Terminal.Gui 2.4.10` package confirms
concrete root causes for both (see `design.md`), but the exact fix for the popup (both its
background inheritance and its width) needs to be confirmed against the running app before
locking in an implementation, since it depends on Terminal.Gui's runtime focus/scheme-resolution
behavior that isn't fully pinned down by static analysis alone.

## What Changes

- Give `DropDownList<T>`'s closed control the same `Theme.EditableBackground` gray that
  `TextField`s get, so it reads as the same kind of editable control.
- Make the expanded popup's background consistent with the same gray (currently confirmed to
  inherit from whatever view was focused when it opens, rather than from `Theme.EditableBackground`
  directly).
- Constrain the expanded popup's width to the dropdown control's own width instead of its longest
  item's text width.
- Investigation spike (tracked in `tasks.md`) to confirm, against the running app via `tmux`, the
  exact mechanism and fix for the popup's background and width, since the popover's attribute
  resolution and sizing are wired through `Terminal.Gui` internals not fully observable from
  reading the decompiled source alone.

## Capabilities

### New Capabilities
- `dropdown-popup-width`: the expanded popup of a `DropDownList<T>` sizes its width to match the
  anchor control's width rather than to its longest item's text.

### Modified Capabilities
- `color-theme`: extends the existing "Editor controls use a distinct background" requirement to
  explicitly cover `DropDownList<T>` (both its closed control and its expanded popup), since
  `DropDownList` internally redirects `VisualRole.Editable` attribute lookups to `Normal`/`Focus`
  in its default (read-only) mode, which the existing requirement's scenarios don't account for.

## Impact

- `src/lazynats/Streams/CreateConsumerDialog.cs`, `src/lazynats/Streams/CreateStreamDialog.cs` —
  the only current call sites constructing `DropDownList<T>`.
- `src/lazynats/Theme.cs` — possibly gains a small helper/scheme for applying
  `EditableBackground` to a `DropDownList`, so future dropdowns pick it up without repeating
  per-call-site setup.
- `src/lazynats/Program.cs` (or another single app-wide wiring point) — likely gains a one-time
  hook into `Application.Popovers` to correct popup width/background for every `DropDownList`
  popover, rather than per-dialog code, per the investigation findings in `design.md`.
- No change to `NATS.Client.*` usage, stream/consumer semantics, or any other tab.
