## Context

The Values tab's `KeyDetails` (a `PollingDetailsView<TTarget, TInfo>`) currently renders a
highlighted key's value with `Encoding.UTF8.GetString(entry.Value)` unconditionally
(`KeyDetails.BuildBody`), then `PollingDetailsView.OnDrawingContent` clips it to whatever lines
fit below the metadata rows - no wrapping, no scrolling, no content awareness. The Live Feed
already solved the equivalent problem for NATS messages via three existing capabilities:
`payload-content-probe` (classify raw bytes as `Json`/`Utf8Text`/`Binary`), `payload-presentation`
(render bytes under a selected `PayloadType`, with adaptive `Hex`/`Base64`/`Text` width), and
`MessageDetailDialog` (a modal that shows the classification's default presentation and lets the
user switch it via a `V`-summoned dropdown). This change brings the same capabilities to KV
values: a content-aware peek in the existing panel, and a new modal for full inspection.

## Goals / Non-Goals

**Goals:**
- Classify a KV value the same way a message payload is classified (`payload-content-probe`),
  and render the `KeyDetails` peek accordingly.
- Add a `V`-triggered KV Value Detail dialog with the same presentation-switching/adaptive-width/
  scrollable behavior as `MessageDetailDialog`, without duplicating that dialog's intricate
  payload-section logic.

**Non-Goals:**
- Object store (`ObjectsTab`) entries are out of scope - confirmed with the user. Objects are
  files with their own download/metadata flow, not a simple `byte[]` value.
- The `KeyDetails` peek panel itself does not become focusable, scrollable, or presentation-
  switchable - confirmed with the user. It keeps today's "clip, don't scroll" contract
  (`nats-kv`'s existing "A value taller than the available space is clipped, not scrolled"
  scenario is unaffected); only *what* gets rendered into that clipped space changes. Full
  inspection is exclusively the new dialog's job.
- No change to `PayloadContentProbe`/`PayloadPresentation`'s public contract or the
  `payload-content-probe`/`payload-presentation` specs themselves - both are reused as-is.

## Decisions

### Decision 1: Peek uses a fixed 16-bytes-per-row hex dump, not `PayloadPresentation`'s adaptive `Hex`
`PollingDetailsView<TTarget, TInfo>.BuildBody(TInfo)` takes no width parameter - only
`OnDrawingContent` knows the panel's `Viewport.Width`, and it isn't touched by this change (see
Non-Goals: no scrolling/switching added there). Threading an available width through
`BuildBody`'s abstract signature just for this one caller would widen a shared base class's
contract for every `PollingDetailsView` subclass. Since the peek is explicitly a lightweight,
non-adaptive display (per the Non-Goals above), a fixed 16-bytes-per-row hex dump is enough - this
also matches the plain, non-word-wrapped style `payload-presentation`'s own single-line rendering
already uses for compact contexts. A small internal helper (`Values/ValuePeek.cs`) implements
this, calling into `PayloadPresentation.Render(..., PayloadType.Json, _)` for the `Json` case
(width-independent already, per that capability's own contract) and doing today's plain
`Encoding.UTF8.GetString` for `Utf8Text`.

**Alternative considered**: widen `PollingDetailsView.BuildBody` to `BuildBody(TInfo, int width)`
and reuse `PayloadPresentation.Render(..., PayloadType.Hex, width)` for full adaptive sizing.
Rejected - `StreamDetails`/`ConsumerDetails`/`BucketDetails` don't need a width and would have to
either ignore the new parameter or be touched for no behavioral reason; not worth it for a value
that's clipped anyway.

**Addendum: character-wrap the body, not just clip it.** `PollingDetailsView.OnDrawingContent`'s
body-rendering loop originally drew each `\n`-split line at its own row and let Terminal.Gui's own
draw clipping cut off whatever ran past `Viewport.Width` - fine for short label:value rows, but a
long `Utf8Text` peek line (no `\n` at all) then rendered as a single hard-clipped row, hiding most
of the value. The loop now breaks each source line into `Viewport.Width`-sized chunks itself and
draws each chunk on its own row, still counting every chunk against `Viewport.Height` and simply
stopping once that's exhausted - character wrap, not word wrap (a mid-word break is fine; this is
a compact peek, not prose), and still no scrolling, consistent with the Non-Goals above.

### Decision 2: Extract `MessageDetailDialog`'s payload-section mechanics into a shared component
`MessageDetailDialog`'s constructor and private members between the payload placeholder frame and
`BindScrollKeys` (~150 lines: adaptive width computed once at open, the presentation
`DropDownList` with its `CanFocus` dance, `OpenPresentationSelector`/`OnPresentationChanged`,
`RefreshPayloadScrollState`, `BindScrollKeys`, the `IShortcutSource` hint) is lifted into a new
`Components/PayloadDetailSection.cs` (`View`), so the new KV Value Detail dialog composes it
instead of re-implementing the same width/focus/scroll invariants a second time.

Shape:
- `PayloadDetailSection(byte[] data, string label, string emptyText)` - builds its own label +
  placeholder `EditFrame`/`Label` as children (`Width = Dim.Fill()`, caller sets `Y`), classifying
  `data` via `PayloadContentProbe`/`PayloadPresentation` up front. `emptyText` lets a caller say
  `"(empty payload)"` vs. `"(empty value)"`.
- `MeasureAndRender()` - called by the owning dialog immediately after the owner's own initial
  `Layout()` call (same point `MessageDetailDialog` measures `_payloadLabelWidth` today): resolves
  the label's `Viewport.Width`, renders the default presentation, sets the frame height, adds the
  presentation dropdown (skipped when `data` is empty), and binds the section's own scroll keys.
  Mirrors today's exact sequence - no new Terminal.Gui behavior, just relocated.
- `public void OpenSelector()` (renamed from today's private `OpenPresentationSelector`) - the
  owning dialog's own `Key.V` binding calls this; the section is never itself focused (its
  dropdown starts `CanFocus = false`, same as today), so it cannot receive the keypress directly.
  Each owning dialog still does its own two-line `AddCommand(Command.Expand, ...)` /
  `KeyBindings.Add(Key.V, Command.Expand)` pointing at `section.OpenSelector` - trivial, and
  keeps `Key.V` an owner-dialog concern rather than something the section binds globally.
- `IShortcutSource.Shortcuts` - yields the `V`/"Presentation" hint iff the section has a payload;
  each owning dialog's own `Shortcuts` property becomes `_payloadSection.Shortcuts` (or `[]` when
  no section was built), same delegation `MessageDetailDialog` already does implicitly today.

`MessageDetailDialog` keeps its own `Subject`/`Headers` frames (unchanged - not part of the
extraction, they're simple single-purpose read-only text, not the adaptive/switchable piece) and
its own `OnAccepting` override (still needed per-dialog: it's what stops a dropdown-Enter bubbling
up and closing the dialog). `PreferredDialogWidth`/`TerminalWidthMargin` (the dialog's own overall
width) also stay per-dialog; `MaxPayloadVisibleLines`/`ScrollbarWidth`/`ScrollbarGap`/
`PresentationDropDownWidth` move into `PayloadDetailSection`, so both dialogs get identical
values for free instead of two copies of the same literals.

### Decision 3: Extract the read-only label+`EditFrame` builder (`WrapText`) as a static helper
`MessageDetailDialog.WrapText` (Label with `HotKeySpecifier` disabled, multi-line/no-wrap
`TextFormatter`, `Theme.ApplyEditableScheme`, wrapped in a non-focusable `EditFrame`) is needed
identically by: `MessageDetailDialog`'s `Subject`/`Headers` frames, the new KV Value Detail
dialog's metadata frame, and `PayloadDetailSection`'s own label. Rather than duplicate a ~15-line,
comment-heavy constructor three times, it becomes a static factory on `EditFrame` itself
(`EditFrame.CreateReadOnly(string text, int y, int height, out Label view)`), since `EditFrame` is
already the type it builds and already lives in `Components/`.

### Decision 4: KV Value Detail dialog mirrors Subject/Headers/Payload as Key/Metadata/Value
Revised from the original single combined "Details" frame (Bucket/Key/Revision/Created/Operation
as one `Label: Value` block) after seeing it in the running dialog: a KV entry does have a single
line that reads as "the subject" after all - its key - so the dialog now mirrors
`MessageDetailDialog`'s three-part shape directly instead of collapsing it to two:
- A "Key" frame (`EditFrame.CreateReadOnly`, Decision 3) holding just `entry.Key` verbatim, colored
  cyan (`Theme.SubjectColor`) exactly like `MessageDetailDialog`'s own `subjectView` - same role,
  same color, so a key reads the same way a subject does.
- A "Metadata" frame (`EditFrame.CreateReadOnly`) of the remaining `Label: Value` lines (Bucket,
  Revision, Created, Operation - Key excluded, it has its own section now), colored LimeGreen
  (`Theme.HeaderColor`) - the color the live feed already uses for a message's own header segments
  (`FeedRowFormatter`), reused here for the same "secondary, scannable metadata" role even though
  `MessageDetailDialog`'s own Headers frame doesn't apply that color to itself today.
- The `PayloadDetailSection` (Decision 2) labeled "Value", unchanged.

Layout math (`keyY`/`keyFrameHeight`/`metadataLabelY`/`metadataFrameY`/`payloadSectionY`) mirrors
`MessageDetailDialog`'s `subjectY`/`subjectFrameHeight`/`headersLabelY`/`headerFrameY`/
`payloadSectionY` computation exactly, just renamed.

### Decision 5: The dialog reuses `KeyDetails`' already-fetched entry, not a fresh fetch
`V` is only meaningful while a key is highlighted, and `KeyDetails` already holds (and keeps
current via its own polling) that key's last-fetched `Entry`. Rather than have the dialog trigger
its own fetch (extra async/loading-state complexity for a "peek deeper" action), `KeyDetails`
exposes its currently-shown entry via a small `CurrentEntry` accessor, and `ValuesTab`'s new `V`
handler at the key level opens the dialog with that entry directly - a no-op (or a status-bar
message) if it's `null` (nothing highlighted yet, or the initial fetch hasn't landed). This
mirrors `MessageDetailDialog`'s own reuse of `envelope.CachedContentKind` instead of
reclassifying from scratch.

## Risks / Trade-offs

- [The extracted `PayloadDetailSection` changes `MessageDetailDialog`'s internals significantly]
  → No behavior change is intended (message-detail-dialog spec is unmodified by this change); the
  existing `message-detail-dialog` spec's scenarios double as the regression check that the
  extraction preserved behavior exactly.
- [A key's value changing on the server between opening the dialog and it closing isn't reflected]
  → Acceptable and consistent with `MessageDetailDialog`, which also shows a point-in-time
  snapshot of a message that already happened; the dialog is read-only and explicitly not live.
