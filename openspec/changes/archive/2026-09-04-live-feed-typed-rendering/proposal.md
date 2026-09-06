## Why

The live feed renders every message's payload with an unconditional, lossy `Encoding.UTF8.GetString(data)` — there is no check that the bytes are actually text, no truncation, and JSON is shown exactly as received (which may be multi-line or padded) rather than compacted to fit a single feed row. Binary payloads currently render as UTF-8 replacement-character noise instead of something legible. The app already has a payload content probe and per-type renderers built for the Message Detail dialog (`payload-content-probe`, `payload-presentation`); the live feed should reuse that classification rather than assuming text, while respecting that a feed row has limited horizontal space and high message throughput, so the cost of classifying/rendering a payload must not scale with total feed volume.

## What Changes

- The live feed row for a message's payload is now type-aware instead of raw-UTF8:
  - `Json`-classified payloads render minified (no indentation), prefixed `(json) `.
  - `Utf8Text`-classified payloads render as decoded text with runs of whitespace/control characters collapsed to a single space, prefixed `(text) `.
  - `Binary`-classified payloads render as unspaced hex, prefixed `(blob) `.
- Row text (prefix excluded) is capped at 1024 characters; for `Binary` payloads this cap is enforced *before* hex-encoding (only the first 512 bytes are ever encoded), not by encoding the full payload and truncating the string after.
- Classification (`PayloadContentProbe.Classify`, unchanged) and the rendered row text are each computed at most once per message and cached on its `FeedEnvelope`, computed lazily the first time that row is actually drawn — not when the message is received — so feed throughput and buffer size do not drive classification/rendering cost; only what actually gets scrolled into view does.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `payload-presentation`: adds a compact, single-line rendering mode (type-prefixed, length-capped, unspaced hex, minified JSON, whitespace-collapsed text) alongside the existing width-wrapped `Json`/`Text`/`Hex`/`Base64` presentation values used by the Message Detail dialog.
- `live-feed`: `FeedEnvelope` gains a classification result and rendered row text that are computed lazily on first render and cached for the envelope's lifetime, replacing `FeedRowFormatter`'s current unconditional raw-UTF8 decode.

## Impact

- `src/lazynats/LiveFeed/FeedEnvelope.cs`: add mutable, lazily-populated cache fields for classification and rendered row text.
- `src/lazynats/LiveFeed/FeedRowFormatter.cs`: replace the raw `Encoding.UTF8.GetString` call with cache-checked classify-then-render, populating the cache on first use.
- `src/lazynats/Payloads/PayloadPresentation.cs`: add the new single-line rendering mode (compact JSON, collapsed text, unspaced/byte-budgeted hex, type prefixes, 1024-char cap).
- No change to `PayloadContentProbe`, `PayloadContentKind`, `PayloadType`, or the Message Detail dialog's existing presentation selector/renderers — this reuses classification as-is and adds a rendering mode alongside the existing ones.
- `src/lazynats/LiveFeed/MessageDetailDialog.cs`: reuses `envelope.CachedContentKind` (populated by `FeedRowFormatter`) instead of calling `PayloadContentProbe.Classify` unconditionally on open.
