## Context

`LiveUpdatesView.ItemSelected` (raised from `Accepted` on the feed's `ListView`) is currently
wired in `MainWindow.cs` straight to `MessageBox.Query(App!, " Selected ", envelope.Message.Subject.Pad(), "_Ok")`
— a stub that shows only the subject. Headers and payload are invisible except as truncated text
in the feed row itself (`FeedRowFormatter.Format`, which already does a blind
`Encoding.UTF8.GetString` on the payload — fine for a one-line row, unsafe to lean on for a
faithful detail view since it silently replaces invalid bytes with U+FFFD instead of surfacing
that the payload isn't text at all).

Payloads arrive as `byte[]` with no self-describing content-type. NATS core messages carry no
`Content-Type` header convention the way, say, HTTP does, so nothing upstream tells the UI
whether a given payload is JSON, plain UTF-8 text, or arbitrary binary — the UI has to infer it
from the bytes themselves before it can decide how to render them.

## Goals / Non-Goals

**Goals:**
- A byte-classification component (`payload-content-probe`) that turns raw payload bytes into one
  of three kinds — `Json`, `Utf8Text`, `Binary` — reliably enough to pick a rendering strategy,
  reusable by any future feature that displays an arbitrary payload (not just the Live Feed).
- A Message Detail dialog that shows subject, headers, and the payload rendered per its probed
  kind (pretty-printed JSON, plain text, or a hex dump for binary), replacing the `MessageBox`
  stub for Live Feed selection.

**Non-Goals:**
- Perfect content-type detection. Bytes are inherently ambiguous — a binary blob can, by chance,
  also be well-formed UTF-8 (see Risks). The probe is a best-effort heuristic for display
  purposes, not a guarantee.
- Truncation, pagination, or lazy rendering for very large payloads. NATS core messages are
  already bounded by the server's `max_payload` (a few MB by default); the whole payload is
  already resident as `byte[]` before the dialog opens, so formatting and displaying all of it in
  one scrollable view is acceptable for this change.
- Editing, copying, or re-publishing the displayed message — this is a read-only viewer.
- Reworking `FeedRowFormatter`'s row text (still a blind UTF-8 decode for the one-line feed row) —
  out of scope; only the detail dialog changes.

## Decisions

### Decision 1: Probe classifies via strict UTF-8 decoding + a printable-text check, then a JSON parse attempt

`PayloadContentProbe.Classify(byte[] payload)` (in `src/lazynats/Payloads/`, alongside the
existing `PayloadType`/`PayloadValidation`/`PayloadEncoding` trio, as its own file/type — kept
separate rather than added to those, since it solves the inverse problem: inferring a kind from
bytes rather than validating/encoding text against an already-chosen `PayloadType`) works in two
steps:

1. **Strict UTF-8 validity**: decode with a UTF-8 `Encoding` configured with
   `EncoderFallback.ExceptionFallback`/`DecoderFallback.ExceptionFallback` (not
   `Encoding.UTF8.GetString`, which silently substitutes U+FFFD for invalid sequences instead of
   failing). A decode failure means `Binary`, full stop — this is what makes emoji and other
   multi-byte-but-well-formed UTF-8 classify correctly as text rather than binary: well-formedness
   is the criterion, not "looks ASCII-ish".
2. **Printable-content check**: a successfully decoded string is still rejected to `Binary` if it
   contains C0/C1 control characters other than `\t`, `\n`, `\r`. This exists because arbitrary
   binary data (protobuf, compressed blobs, etc.) can, by chance, be valid UTF-8 while still being
   obviously not text — without this check such payloads would render as text full of NUL/control
   noise. This is the deliberate heuristic layer flagged as the hard part in the proposal
   discussion: strict UTF-8 alone under-rejects; requiring pure ASCII over-rejects legitimate
   non-Latin/emoji text. Rejecting only genuine control characters (not letters, digits,
   punctuation, or any non-ASCII Unicode text) sits between those two failure modes.
3. Only once a payload passes both checks does the probe attempt `JsonDocument.Parse` on the
   decoded text (mirroring `PayloadValidation.IsValidJson`'s own parse-and-catch shape) to
   distinguish `Json` from plain `Utf8Text`.

Empty payloads classify as `Utf8Text` (an empty string is trivially valid UTF-8, non-JSON),
matching `PayloadValidation`'s existing "empty is valid `Text`" stance for the outbound side.

**Alternatives considered:**
- *Manual byte-level UTF-8 state machine* — rejected: reimplements what the BCL's strict
  `Encoding` fallback already does correctly, with more surface for subtle bugs.
- *Heuristic-only "mostly printable ASCII" check without real UTF-8 decoding* — rejected: this is
  exactly the failure mode the proposal called out (misclassifying legitimate UTF-8 text
  containing emoji/non-Latin scripts as binary).
- *Treat any valid-UTF-8 payload as text, skip the printable-content check* — rejected: too
  permissive, binary payloads that happen to decode validly would render as garbled "text" with
  no indication they're actually binary.

### Decision 2: Dialog renders three content-sized `EditFrame` sections, not one scrollable view

`MessageDetailDialog` (in `src/lazynats/LiveFeed/`, its only caller for now) lays out Subject,
Headers, and Payload as three separate sections, each a labeled `EditFrame` (`Components/`)
wrapping a read-only `Label` — mirroring `PublishDialog`'s own Subject/Headers/Payload shape, just
non-editable. Each frame is sized to its own content instead of the dialog rendering one
fixed-height scrollable block for everything: Subject is always exactly one line (height 3
including the frame's border), Headers grows to fit however many `Key: Value` lines it has (or the
explicit "(no headers)" line), and Payload grows up to `MaxPayloadVisibleLines` (16) before capping
and scrolling internally - so a short JSON object or a handful of headers no longer forces the
same tall dialog a multi-KB payload would. `Dialog.Height` is left unset (Terminal.Gui's own
`Dim.Auto` default), so the dialog itself grows to fit the stacked sections and only the payload
frame - the one section with genuinely unbounded size - needs its own cap and scrollbar.
`Padding.Thickness` is `(1, 1, 1, 0)`, restoring the one-blank-row-above-content convention
button-dialogs use (`CreateBucketDialog`/`CreateKeyDialog`/...) even though this dialog is
buttonless. `Width` prefers 132 columns (payloads are often JSON, which reads better with room)
but is capped to `IApplication.Screen.Width - 4` via `Dim.Func` - a small function `Dim` that
Terminal.Gui re-evaluates on every layout pass, rather than a fixed number computed once at
construction - so the dialog still fits (and reflows live if the terminal is resized) on a
narrower terminal instead of clipping or throwing. `IApplication.Screen`, resolved from
`Services.Root.GetRequiredService<IApplication>()` (the `app` instance registered as a singleton
in `Program.cs`), is used rather than the static `Application.Screen` accessor, which is obsolete
in this Terminal.Gui version. `Height` has no equivalent cap - left to `Dim.Auto`, it already
sizes from content, and this dialog's content (Subject/Headers + a capped Payload frame) is
already bounded independently of terminal width.

Each section's `Label` also has `HotKeySpecifier` set to `(Rune)0xffff` (disabled) before `Text`
is assigned - `Label` defaults `HotKeySpecifier` to `'_'` (unlike the plain `View` base, which
defaults to disabled), and message content is arbitrary text (NATS subjects, JSON, header values
routinely contain `_`), so the default would silently consume an underscore and underline the
following character instead of rendering the content verbatim. It must be set before `Text`,
since `Label` parses the specifier out of `Text` at assignment time using whatever
`HotKeySpecifier` is current then.

Each section's `Label` has `TextFormatter.MultiLine` opted in and `WordWrap` left off (so the hex
dump's fixed 16-bytes-per-row layout isn't re-flowed), uses `Theme.ApplyEditableScheme` for the
same white-on-`Theme.EditableBackground` grey every `TextField`/`TextView` field elsewhere in the
app uses (a `Label` doesn't pick this up automatically the way `TextField` does via
`Program.cs`'s `ApplyColorTheme`, so it's applied explicitly, and the wrapping `EditFrame`'s
`InnerBackgroundNormal`/`InnerBackgroundFocused` are set to the same color so the frame's fill and
the `Label`'s own per-character paint don't seam), and its wrapping `EditFrame` has
`CanFocus = false` - unlike every other `EditFrame` in this codebase, which wraps a focusable
`TextField`/`TextView`, these wrap a non-focusable `Label`, so leaving the frame focusable would
pull Tab focus (and the payload frame's own scroll key bindings, bound on the Dialog) onto an
otherwise-inert frame instead of keeping it on the Dialog. Subject's `Label` additionally overrides
its `Scheme` to a cyan foreground (still on the same grey background) so the message's identifying
line stands out from the plain white Headers/Payload text below it. Only the payload's `Label` gets
scroll wiring (`SetContentHeight` + `ViewportSettingsFlags.HasVerticalScrollBar` +
`Command.ScrollUp`/`ScrollDown`/`PageUp`/`PageDown` key bindings on the Dialog, since `AddCommand`
is protected and the `Label` is never focused - same reasoning as `ObjectFileDialog`'s F2/Browse
binding), and only when its content actually exceeds the cap. No buttons: `Esc` closes via
`Dialog`'s own cancellation convention, matching the existing compact-dialog shape used by
`ShortcutPickerDialog`/`PatternDialog` rather than introducing a `MessageBox`-style `_Ok` button
for a dialog with nothing to commit.

An empty payload (`message.Data` null or zero-length) short-circuits to the literal text
`(empty payload)` before the probe ever runs - `PayloadContentProbe.Classify` still reports
`Utf8Text` for an empty array (see Decision 1), but that classification is for callers that want
a rendering *strategy*, not a display string, so the dialog special-cases the empty case itself
rather than showing a blank payload frame with no indication there's nothing there.

The `Json` case re-serializes via `JsonElement.WriteTo(Utf8JsonWriter)` rather than
`JsonSerializer.Serialize(document.RootElement, options)` - both parse once with `JsonDocument`,
but the latter is reflection-based (`RequiresUnreferencedCode`/`RequiresDynamicCode`) and unsafe
under `PublishAot` trimming per CLAUDE.md's stack guidance, where `WriteTo` needs neither.

**Alternatives considered:**
- *One pre-formatted string in a single scrollable `Label`/`TextView` (the original shape of this
  decision)* — rejected on redesign: a single block sizes the whole dialog to its largest section
  (typically the payload) even when the message has one header and a two-line payload, and
  scrolling to reach headers past a long subject/payload mixes unrelated content in one scroll
  region. Splitting into three independently-sized sections, only capping the one that's genuinely
  unbounded, keeps small messages compact and only reaches for scrolling where it's needed.
- *Plain black `Normal` role background instead of the grey `Editable` look (the dialog's original
  choice, before a follow-up design pass asked for the grey to match input fields elsewhere)* —
  superseded: the concern was that a grey, `TextField`-like background would read as editable
  despite the dialog being read-only, but in practice the `Label`'s lack of a cursor and this
  dialog's overall read-only framing (no buttons, `Esc`-only) already carries that distinction,
  and matching the app's other input-shaped fields reads as more consistent.
- *`TextView` (`ReadOnly = true`)* instead of `Label` — rejected: `TextView` carries editing
  affordances (a visible cursor, insertion-point navigation) that read as editable regardless of
  `ReadOnly`, where a `Label` has none of that by construction; scrolling is instead wired manually
  onto the payload section specifically (see above).
- *Reuse `MessageBox` with a longer string* — rejected: `MessageBox` wraps/centers text for short
  confirmational messages: it's not built for scrolling through a multi-KB payload.

### Decision 3: Binary rendering is a fixed-width hex dump, offset-free

Non-text payloads render as hex bytes, 16 per row, space-separated, no ASCII gutter and no offset
column — enough to eyeball structure/length without building a full hex-editor widget. If this
proves too thin in practice (e.g. users want offsets or an ASCII gutter to spot embedded text),
that's a follow-up, not blocking this change.

## Risks / Trade-offs

- **[Risk]** A binary payload can coincidentally be valid UTF-8 with no stray control characters
  (e.g. a short payload of printable-range bytes) and would misclassify as `Utf8Text`. →
  **Mitigation**: none needed beyond documenting it as an inherent limitation of content-sniffing
  without an explicit content-type; the probe is a display heuristic, not a correctness
  guarantee, and this failure mode only affects how the payload is *rendered*, not what bytes are
  underneath.
- **[Risk]** Large payloads (multi-MB, near NATS's max_payload ceiling) formatted in full into one
  `TextView` string could be slow to build/render. → **Mitigation**: accepted as a non-goal for
  this change (see Non-Goals); revisit only if it's observed to actually matter.

## Open Questions

- None — scope is intentionally narrow (Live Feed's own message selection); broader reuse of the
  probe is left for whichever future feature needs it.
