## Why

The Message Detail dialog (`message-detail-dialog`) currently auto-classifies a payload via
`payload-content-probe` and renders it exactly one way: `Json` payloads are always pretty-printed,
`Utf8Text` always shown as decoded text, `Binary` always shown as hex. There is no way to see a
JSON payload's raw (undecoded) text, to see a text payload's bytes as hex, or to compare hex vs.
base64 for a binary payload — the probe's guess is final. A user inspecting an unfamiliar payload
needs to switch presentations without leaving the dialog.

## What Changes

- Add a presentation selector (`DropDownList`) to the Message Detail dialog's Payload section,
  defaulting to the presentation implied by the payload's probed `PayloadContentKind` (`Json` →
  formatted JSON, `Utf8Text` → raw text, `Binary` → hex).
- The selector's available options are gated by what the payload can actually be rendered as, not
  a fixed enum: `Json (formatted)` is offered only when the payload probes as `Json`; `Text (as
  is)` is offered only when the payload is renderable as text (`Json` or `Utf8Text`); `Hex` and
  `Base64` are always offered, since any byte sequence can be shown either way.
- Changing the selection re-renders the payload section in place (no dialog re-open), reusing the
  same scrollable frame and scroll wiring already in place for long payloads.
- Replace the payload-formatting logic embedded in `MessageDetailDialog` (`FormatJson`,
  `FormatHex`, the raw `Encoding.UTF8.GetString` case) with a new shared `payload-presentation`
  capability that maps a `(bytes, PayloadType)` pair to display text, and separately exposes which
  `PayloadType` values are valid for a given probed `PayloadContentKind` — so the dialog only
  wires selection UI to it rather than owning the formatting rules itself. Reuses the existing
  `PayloadType` enum (`Json`/`Text`/`Base64`/`Hex`, already defined by `payload-types` for the
  outbound compose/send path) rather than introducing a second enum with the same four values —
  the label ("this is JSON"/"this is hex") means the same thing whether it's being validated
  before sending or picked for display.
- Uses the framework's non-generic `DropDownList` bound to an explicit `Source` (a
  `ListWrapper<PayloadType>` over the allowed subset), not the generic `DropDownList<TEnum>` used
  elsewhere (e.g. `PublishDialog`'s own Payload Type field) — that generic form always populates
  every enum value from the type itself in its constructor and has no hook to restrict the list,
  which is exactly the constraint this feature needs to work around.

## Capabilities

### New Capabilities
- `payload-presentation`: defines, in terms of the existing `PayloadType` enum, which values are
  valid for rendering a payload given its probed `PayloadContentKind`, which value is the default,
  and how to render payload bytes under a selected value — the display-side counterpart to
  `payload-types`' compose/validate/encode, reusing its enum rather than duplicating it.

### Modified Capabilities
- `message-detail-dialog`: the payload section gains a presentation selector control; the
  previously-fixed "one probed kind, one rendering" requirement is replaced by "probe picks the
  default, user can switch among the `PayloadType` values valid for this payload."

## Impact

- `src/lazynats/LiveFeed/MessageDetailDialog.cs`: add the selector control and re-render-on-change
  wiring; delegate formatting to the new `payload-presentation` component instead of its own
  `FormatJson`/`FormatHex`/inline text-decode logic.
- `src/lazynats/Payloads/`: new static component (naming finalized in design.md) for
  `PayloadType`-availability-per-classification and byte-to-text rendering, built on the existing
  `PayloadType` enum; `PayloadContentProbe`/`PayloadContentKind` are unchanged and continue to
  supply the classification the new component keys off. No new enum is added.
- No change to `PayloadType` itself, `PayloadValidation`/`PayloadEncoding` (outbound
  Publish/Templates concept), or to `PublishDialog`'s `DropDownList<PayloadType>` — this
  proposal's `DropDownList` usage change is scoped to the read-only Message Detail dialog only.
