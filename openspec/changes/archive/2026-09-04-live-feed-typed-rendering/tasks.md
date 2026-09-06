## 1. FeedEnvelope caching

- [x] 1.1 Add `CachedContentKind` (`PayloadContentKind?`) and `CachedRowText` (`string?`) mutable
      properties to `FeedEnvelope` (`src/lazynats/LiveFeed/FeedEnvelope.cs`).

## 2. Single-line presentation rendering

- [x] 2.1 Add `RenderSingleLine(byte[] data, PayloadContentKind kind, int maxLength)` to
      `src/lazynats/Payloads/PayloadPresentation.cs`:
      - `Json`: parse and re-serialize without indentation (minified), prefix `(json) `.
      - `Utf8Text`: decode UTF-8, collapse every maximal run of whitespace/control characters to a
        single space, prefix `(text) `.
      - `Binary`: hex-encode at most `maxLength / 2` leading bytes (no separators), prefix
        `(blob) ` — do not encode the full payload and truncate afterward.
      - Cap the body (post-prefix) at `maxLength` characters for the `Json`/`Utf8Text` cases too.
- [x] 2.2 Add unit-level sanity checks (or exercise via `AotProbe`/manual run if no test project
      applies) for: minified JSON output, whitespace-collapsed text, unspaced hex, the byte-budget
      behavior for oversized binary payloads, and the 1024-char cap applying to the body only.

## 3. Wire the live feed row through classification + caching

- [x] 3.1 Update `FeedRowFormatter.Format` (`src/lazynats/LiveFeed/FeedRowFormatter.cs`) to:
      - Return `envelope.CachedRowText` if already populated.
      - Otherwise classify via `PayloadContentProbe.Classify`, cache the result on
        `CachedContentKind`, render via `PayloadPresentation.RenderSingleLine(data, kind, 1024)`,
        cache the result on `CachedRowText`, and use it.
      - Remove the old unconditional `Encoding.UTF8.GetString(data)` call.

## 4. Message Detail dialog reuses the cached classification

- [x] 4.1 Update `MessageDetailDialog` (`src/lazynats/LiveFeed/MessageDetailDialog.cs`) to read
      `envelope.CachedContentKind ??= PayloadContentProbe.Classify(_payloadData)` instead of
      calling `Classify` unconditionally, since the dialog is always constructed from a
      `FeedEnvelope` whose row has already been rendered (and thus classified) at least once.

## 5. Verification

- [x] 5.1 Run the app (`dotnet run --project src/lazynats`) against a local NATS server and, via
      `nats pub`/the app's own Publish tab, send a JSON message, a plain-text message, and a
      binary payload; confirm the live feed rows show `(json)`/`(text)`/`(blob)` prefixed content
      as specified, and that opening the Message Detail dialog for each still works unchanged.
- [x] 5.2 Confirm a message with a payload larger than 1024 rendered characters (e.g. a large JSON
      array or long text line) is capped correctly in the feed row, with the type prefix intact.
- [x] 5.3 Confirm scrolling the feed does not visibly reclassify/re-render rows already computed
      (no functional test needed beyond code review of the cache-check-first logic in 3.1, given
      there's no automated test project — see `CLAUDE.md`).
