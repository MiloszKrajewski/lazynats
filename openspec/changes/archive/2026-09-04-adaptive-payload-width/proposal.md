## Why

`PayloadPresentation.Render`'s `Hex` and `Base64` output is width-blind: `Hex` always emits a
fixed 16 bytes/row, and `Base64` emits a single unbroken line via `Convert.ToBase64String`. The
Message Detail dialog's payload `Label` has `WordWrap = false` and no horizontal scrollbar (only
vertical, via `BindScrollKeys`), so a wide `Base64` payload is already unreachable past the visible
width today, and `Hex` wastes available width on a wide terminal (or, on a narrow one, could
already be too wide, though 16 bytes/row happens to fit most terminals in practice). Sizing both
to the payload section's actual available width fixes the `Base64` overflow bug and makes `Hex`
use the space it's given.

## What Changes

- `PayloadPresentation.Render` for `PayloadType.Hex` and `PayloadType.Base64` takes an available
  width and sizes its output to it, computed once when the Message Detail dialog opens (not
  live-reactive to a later terminal resize, matching how `Json`/`Text` rendering already behaves
  under resize). **BREAKING** (internal API only, single caller): `Render`'s signature changes to
  accept a width parameter.
- `Hex`: bytes/row is computed from the available width and snapped down to the nearest of a fixed
  candidate set (`8, 16, 24, 32, 48, 64`), clamped between 8 and 64.
- `Base64`: line width is computed from the available width, floored to the nearest multiple of 4,
  clamped between 24 and 144.
- New generic `NotLessThan`/`NotMoreThan` clamp extension methods (`Comparer<T>.Default`-based),
  added alongside the existing `Core/*Extensions.cs` files, used for both clamps above.

## Capabilities

### New Capabilities

(none - this reuses and extends the existing `payload-presentation` and `message-detail-dialog`
capabilities; the clamp extension methods are a generic utility with no user-facing behavior of
their own, not a capability requiring its own spec.)

### Modified Capabilities
- `payload-presentation`: `Hex` and `Base64` rendering now take an available-width parameter and
  size their row/line length to it (snapped/clamped per fixed rules), instead of `Hex`'s previous
  fixed 16 bytes/row and `Base64`'s previous single unbroken line.
- `message-detail-dialog`: the payload section computes its available width once when the dialog
  opens and supplies it to the presentation renderer, so `Hex` and `Base64` output fits the section
  without horizontal overflow, and does not change when the width is later recomputed to a
  different value.

## Impact

- `src/lazynats/Payloads/PayloadPresentation.cs`: `Render`/`RenderHex` signatures gain a width
  parameter; new `Base64` wrapping logic.
- `src/lazynats/LiveFeed/MessageDetailDialog.cs`: computes the payload label's available width
  once at construction and passes it to both `Render` call sites (initial render and
  `OnPresentationChanged`).
- New `Core/NumericExtensions.cs` (or similarly named file, following the existing
  `Core/*Extensions.cs` convention): generic `NotLessThan`/`NotMoreThan`.
- Single caller of `PayloadPresentation.Render` today (`MessageDetailDialog`), so the signature
  change has no other call sites to update.
