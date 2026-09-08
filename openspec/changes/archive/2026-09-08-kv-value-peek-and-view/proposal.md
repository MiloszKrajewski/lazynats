## Why

The Values tab's Key Detail panel always decodes a KV entry's value as raw UTF-8 text, so a
binary value renders as garbage (or nothing) and a JSON value shows as an unformatted blob. There
is also no way to inspect a value beyond whatever few lines fit in the panel's fixed, clipped,
non-scrolling space. NATS messages in the Live Feed already solved both problems (content
classification via `payload-content-probe`, a switchable `Json`/`Text`/`Hex`/`Base64` presentation
via `payload-presentation`, and a full scrollable `MessageDetailDialog`) - KV values deserve the
same treatment.

## What Changes

- The Key Detail panel's value peek becomes content-aware: it classifies the value via
  `payload-content-probe` and renders it accordingly - `Json` pretty-printed, `Utf8Text` as
  decoded text (today's behavior), `Binary` as a hex dump at a fixed 16 bytes per row - still
  clipped to the panel's available lines with no scrolling, unchanged from today's "clip, don't
  scroll" contract.
- A new `V` shortcut at the key level opens a read-only KV Value Detail dialog, mirroring
  `message-detail-dialog`: shows the bucket, key, revision, created time, and operation, plus the
  value rendered under a switchable presentation (`Json`/`Text`/`Hex`/`Base64`, limited to the
  values valid for the value's content classification per `payload-presentation`), with adaptive
  width and a scrollable payload section. `Esc` closes it.
- Internal: the payload-section mechanics currently embedded in `MessageDetailDialog` (adaptive
  width computed once at open, the presentation dropdown, scroll key bindings, the `V`-shortcut
  wiring) are extracted into a shared component so the new KV Value Detail dialog reuses them
  instead of duplicating that logic.

## Capabilities

### New Capabilities
- `kv-value-detail-dialog`: a read-only dialog, opened via `V` from the key-level list, showing a
  KV entry's metadata (bucket, key, revision, created, operation) and its value rendered under a
  switchable `Json`/`Text`/`Hex`/`Base64` presentation, reusing `payload-content-probe` for
  classification and `payload-presentation` for rendering.

### Modified Capabilities
- `nats-kv`: the "Key Detail Panel" requirement changes from always rendering the value as UTF-8
  text to rendering it per its `payload-content-probe` classification (`Json` pretty-printed,
  `Utf8Text` as decoded text, `Binary` as a fixed 16-bytes-per-row hex dump); a new requirement is
  added for the `V` shortcut that opens the `kv-value-detail-dialog` for the highlighted key.

## Impact

- `src/lazynats/Values/KeyDetails.cs` - content-aware peek rendering instead of unconditional
  UTF-8 decode.
- `src/lazynats/Values/KeyListView.cs` / `src/lazynats/Values/ValuesTab.cs` - `V` shortcut wiring
  and shortcut-list discoverability at the key level.
- New `src/lazynats/Values/ValueDetailDialog.cs` (or similarly named) - the new dialog.
- `src/lazynats/LiveFeed/MessageDetailDialog.cs` - refactored to compose the extracted shared
  payload-section component instead of implementing it inline.
- New shared component under `src/lazynats/Components/` for the extracted payload-section
  mechanics (adaptive width, presentation selector, scroll bindings, `V`-shortcut wiring).
- No changes expected to `src/lazynats/Payloads/PayloadContentProbe.cs` or
  `PayloadPresentation.cs` - both are reused as-is, aside from possibly a small addition for the
  fixed 16-bytes-per-row hex peek (distinct from `PayloadPresentation`'s adaptive-width `Hex`
  rendering, which the new dialog still uses for its own `Hex` presentation value).
