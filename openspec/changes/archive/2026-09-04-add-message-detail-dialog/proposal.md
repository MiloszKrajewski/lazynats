## Why

Selecting a message in the Live Feed currently opens a bare `MessageBox.Query` that shows only
the subject (`MainWindow.cs`) — headers and payload aren't visible at all, so inspecting a
message means reading it off the truncated feed row instead. Payload content also varies per
message (pretty JSON, plain UTF-8 text, or arbitrary binary), and rendering it usefully requires
knowing which of those it is before choosing how to display it.

## What Changes

- Add a reusable payload-content probe: given raw payload bytes, classify them as `Json` (valid
  UTF-8 JSON), `Utf8Text` (valid UTF-8, not JSON), or `Binary` (not valid UTF-8), so any feature
  that displays an arbitrary payload can decide how to render it without re-implementing the
  detection. Lives under `src/lazynats/Payloads/` alongside the existing outbound
  `PayloadType`/`PayloadValidation`/`PayloadEncoding` trio, as its own component (not folded into
  those) so it can be reused independently of the outbound payload-composition types.
- Add a proper Message Detail dialog replacing the `MessageBox.Query` stub: shows the message's
  subject, its headers (one per line, or an explicit empty-state if none), and its payload
  rendered according to the probe's classification (pretty-printed JSON, plain text, or a
  binary/hex indication for non-UTF-8 payloads) in a scrollable, read-only view.
- Wire `LiveUpdatesView.ItemSelected` in `MainWindow.cs` to open the new dialog instead of the
  `MessageBox.Query` stub.

## Capabilities

### New Capabilities
- `payload-content-probe`: classifies raw payload bytes as JSON / UTF-8 text / binary for display
  purposes, independent of and reusable beyond the Live Feed.
- `message-detail-dialog`: a read-only dialog presenting a message's subject, headers, and
  probe-classified payload.

### Modified Capabilities
- `live-feed`: selecting a message (`Enter`/`Accepted` on the feed list) now opens the Message
  Detail dialog instead of the current subject-only `MessageBox.Query` stub.

## Impact

- `src/lazynats/MainWindow.cs`: replace the `ItemSelected` handler's `MessageBox.Query` call.
- `src/lazynats/Payloads/`: new probe component alongside the existing payload-type files.
- New `src/lazynats/LiveFeed/` (or `Components/`) dialog type for the message detail view.
- No changes to `FeedEnvelope`, `SubscriptionRegistry`, or the feed pipeline itself.
