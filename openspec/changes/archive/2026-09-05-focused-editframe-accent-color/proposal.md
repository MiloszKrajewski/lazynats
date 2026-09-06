## Why

`EditFrame`'s edge accent (`TL`/`BL`/`LM`) is always the same white, regardless of whether the
wrapped child currently has focus, and `color-theme` deliberately keeps the inner
background/editable color constant across focus states too. That leaves keyboard-driven users
with no visual cue for *which* `EditFrame`-wrapped field currently has focus, other than a
`TextField`'s blinking cursor — which doesn't exist at all for a non-`TextField` child (e.g. a
`ListEditorView<T>`-wrapped frame). Since no caller currently overrides `EdgeAccent`, giving the
focused state its own distinct accent color is a one-place default change that fixes this for
every `EditFrame` in the app at once.

## What Changes

- `EditFrame` gets a second, focus-state-aware accent color: the edge accent (`TL`/`BL`/`LM`)
  uses a distinct color while the wrapped child has focus, and reverts to the existing default
  (white) when it doesn't.
- The new focused-accent color is `ColorName16.BrightBlue` ("light blue") — a distinct hue from
  `ColorName16.Cyan`/`BrightCyan`, and not already used by any existing `Theme.cs` constant.
- `EditFrame` exposes this as a new caller-settable property (following the existing
  `InnerBackgroundNormal`/`InnerBackgroundFocused` pattern), defaulting to the new color so no
  call site needs to change to pick it up; a caller may still override it, and the existing
  `EdgeAccent` property continues to control the unfocused-state color.

## Capabilities

### Modified Capabilities
- `edit-frame`: the Edge Accent Color requirement changes from a single static accent color to a
  focus-state-aware pair (normal vs. focused), mirroring the existing
  `InnerBackgroundNormal`/`InnerBackgroundFocused` split.

## Impact

- `src/lazynats/Components/EditFrame.cs`: new `EdgeAccentFocused` property, focus-driven accent
  selection in `OnDrawingContent`.
- `src/lazynats/Theme.cs`: new centrally-declared constant for the focused accent color.
- No call sites need to change — every existing `EditFrame` picks up the new focused-state accent
  automatically since none currently set `EdgeAccent`/`EdgeAccentFocused` explicitly.
