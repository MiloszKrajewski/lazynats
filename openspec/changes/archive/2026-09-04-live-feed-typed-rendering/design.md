## Context

`FeedRowFormatter.Format` (`src/lazynats/LiveFeed/FeedRowFormatter.cs`) currently builds each live
feed row with `Encoding.UTF8.GetString(data)` — the lossy, non-throwing `Encoding.UTF8` instance,
with no validity check, no classification, and no length cap. `LiveLogDataSource.Render` calls
`Format` once per *visible* row per redraw (not once per buffered message — `MaxItemLength` is
deliberately `0` to avoid an O(n²) rescan, per the existing comment in that file), and the feed
view buffers up to `MaximumFeedLength = 10_000` envelopes.

The Message Detail dialog already solves payload classification and rendering for its own
purposes: `PayloadContentProbe.Classify` (strict UTF-8 decode → control-character check → JSON
parse attempt) produces a `PayloadContentKind`, and `PayloadPresentation` renders bytes under a
selected `PayloadType` for that dialog's width-wrapped, multi-line display. Neither is reused by
the feed today.

## Goals / Non-Goals

**Goals:**
- Replace the feed row's raw-UTF8 assumption with the same classification already used for the
  Message Detail dialog.
- Render `Json` compactly (minified), `Utf8Text` collapsed to one line, `Binary` as unspaced hex —
  each prefixed with its kind so the type is legible even when truncated.
- Cap rendered row text at 1024 characters (excluding the prefix), and for `Binary` payloads avoid
  ever hex-encoding more than the bytes that could possibly be shown.
- Keep the cost of classifying/rendering bounded by *what is actually scrolled into view*, not by
  total feed throughput or buffer size.

**Non-Goals:**
- Changing `PayloadContentProbe`'s classification rules (the strict-UTF8 / control-character /
  JSON-parse boundary stays exactly as `payload-content-probe` specifies it today).
- Changing the Message Detail dialog's existing presentation selector or its `PayloadPresentation`
  renderers — only where it sources its `PayloadContentKind` from changes (see below), not what it
  does with it.
- Bounding the cost of parsing/serializing a large `Json`- or `Utf8Text`-classified payload before
  the 1024-char cap is applied (see Risks below) — only `Binary`/hex gets a pre-encode byte budget.

## Decisions

### Cache the classification and rendered row text as mutable fields on `FeedEnvelope`

`FeedEnvelope` becomes:
```csharp
internal sealed record FeedEnvelope(
    DateTimeOffset ReceivedAt,
    Guid SubscriptionId,
    NatsMsg<byte[]> Message
)
{
    public PayloadContentKind? CachedContentKind { get; set; }
    public string? CachedRowText { get; set; }
}
```
`FeedRowFormatter.Format` checks `CachedRowText` first; on a miss it classifies (caching the
`PayloadContentKind` too), renders, caches, and returns.

**Alternative considered**: a side table (`ConditionalWeakTable<FeedEnvelope, ...>` or a
`Dictionary` keyed by envelope identity) owned by `LiveLogDataSource`. Rejected — it needs its own
eviction logic to avoid growing unbounded, duplicating what the existing 10,000-entry ring buffer
already does for free when the cache lives on the envelope itself; the envelope is evicted, the
cache goes with it.

**Equality note**: `FeedEnvelope` is a record, so adding public settable properties adds them to
the compiler-generated `Equals`/`GetHashCode`. This is safe here because nothing in the codebase
compares `FeedEnvelope` instances for equality or hashes them into a collection keyed by value
(confirmed: no `Equals`/`Contains`/`Dictionary<FeedEnvelope,_>`/`HashSet<FeedEnvelope>` usage
anywhere) — dedup keys off the inner `NatsMsg` fields via `MessageDeduplicator`, and eviction is
index-based (`RemoveAt`), not value-based.

### No thread-safety needed on the cache fields

`FeedEnvelope`'s immutable fields are set on a subscription's background reader task
(`SubscriptionRegistry`), but the cache fields are only ever read or written from
`LiveLogDataSource.Render`, which Terminal.Gui only invokes on the UI thread. By the time a UI
redraw can observe an envelope at all, it has already crossed the batched dispatch in
`LiveUpdatesView` onto the UI thread (per `live-feed`'s "Batched Main-Thread Dispatch"
requirement), so there is no concurrent writer to guard against — consistent with the existing
"UI-thread-only by design, don't add locking" posture documented for `SubscriptionRegistry`.

### Classification is reused as-is; rendering is a new, separate code path

`RenderSingleLine(byte[] data, PayloadContentKind kind, int maxLength)` is added to
`PayloadPresentation.cs` alongside the existing dialog-oriented renderers, rather than
parameterizing the existing `RenderJson`/`RenderText`/`RenderHex` to also handle the compact case.
Those existing renderers are shaped around width-wrapped multi-line output for the dialog; compact
single-line rendering has different rules (minify instead of indent, collapse instead of wrap,
prefix, hard length cap) that don't share meaningfully more than "take bytes, produce text" with
the dialog renderers.

### Binary gets a pre-encode byte budget; Json/Text do not

Hex has no notion of "content" beyond the raw bytes, so the row cap can be enforced *before*
encoding: with a 1024-character cap and no separator spaces, at most `1024 / 2 = 512` bytes are
ever hex-encoded, regardless of payload size — a 64KB binary payload only ever touches its first
512 bytes.

`Json` and `Utf8Text` don't get the same treatment: correctness requires the *whole* payload to be
decoded/parsed first (truncating raw bytes before decoding UTF-8 risks cutting a multi-byte
codepoint; truncating before JSON-parsing breaks the parse and there's no such thing as "the first
512 bytes" of a minified re-serialization computed without seeing the rest of the document). The
cap is therefore applied to the *output* string after the full payload has been processed. This is
accepted per the caching design below — the cost is paid at most once per message, only for
messages that are actually scrolled into view, never on the ingestion path.

### Message Detail dialog reuses the envelope's cached classification too

`MessageDetailDialog` is constructed directly from a `FeedEnvelope` (`MainWindow`'s
`liveUpdates.ItemSelected` handler), and by the time a row can be selected to open it, that
envelope's row has necessarily already been rendered at least once — so `CachedContentKind` is
already populated. The dialog now reads `envelope.CachedContentKind ??= PayloadContentProbe
.Classify(_payloadData)` instead of calling `Classify` unconditionally, avoiding a second probe
pass (including its own JSON-parse attempt) over bytes already classified for the feed row. This
doesn't change the dialog's presentation selector or renderers - only where the `PayloadContentKind`
it feeds them comes from.

### Redundant JSON parse between classification and rendering is accepted

`PayloadContentProbe.Classify` already parses candidate JSON once (via `JsonDocument.Parse`) to
decide `Json` vs `Utf8Text`, then discards the result. `RenderSingleLine` parses again to minify.
This duplicate parse is accepted rather than changing `Classify`'s contract to return the parsed
document (out of scope per Non-Goals — `payload-content-probe` is unchanged) or plumbing the
`JsonDocument` through a new API shape. Since both classification and rendering are cached
together and computed at most once per message, the duplicate parse cost is paid once, not
repeatedly.

### Empty payload is not special-cased

`PayloadContentProbe.Classify` already classifies a zero-length payload as `Utf8Text` (per
`payload-content-probe`'s "Empty payload classifies as Utf8Text" scenario). The feed row lets this
fall through the normal `Utf8Text` path: decoded text is empty, collapsing whitespace on an empty
string is a no-op, and the row shows just the `(text) ` prefix. This differs from the Message
Detail dialog, which special-cases an empty payload as the literal string `(empty payload)` — that
special case is not carried over here, since it's dialog-specific UI text or unnecessary at
single-row granularity, not a probe-level concept.

## Risks / Trade-offs

- **A very large `Json`/`Utf8Text` payload still pays full parse/serialize/decode cost once** →
  Mitigated by caching: this cost is paid at most once per message, only for messages actually
  scrolled into view (not on ingestion, not on every redraw). If profiling later shows this is
  hot for pathologically large text/JSON messages, a bounded/early-exit writer could be added —
  out of scope for now.
- **`FeedEnvelope` gaining mutable fields on an otherwise-immutable-looking record is a slightly
  surprising shape** → Mitigated by the equality-safety note above and by keeping the fields
  clearly named `Cached*` and documented as lazily populated, UI-thread-only.

## Open Questions

None outstanding — see `proposal.md` for the resolved discussion (empty-payload handling, cap
scope, hex byte-budgeting) that fed these decisions.
