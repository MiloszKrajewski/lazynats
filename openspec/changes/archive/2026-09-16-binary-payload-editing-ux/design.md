## Context

`PayloadType` (`Json`/`Text`/`Base64`/`Hex`), `PayloadValidation.IsValid`, and
`PayloadEncoding.ToBytes` (`src/lazynats/Payloads/`) already exist and are already live —
wired into `PublishDialog`/`TemplateDialog`'s per-keystroke validation, `PublishDialog`'s actual
Send-time encoding, and `TemplatesTab`'s import validation. This change doesn't introduce the
payload-type concept; it makes the `Hex`/`Base64` half of it usable for anything beyond a short,
unbroken, whitespace-free string, and gives the payload field a wrap behavior it currently has
none of at all (`TextView.WordWrap` is never set in either dialog today).

`PayloadPresentation.Render` (display-side, used by the read-only Message Detail view) already
renders `Hex` as space-grouped byte pairs and `Base64` wrapped at 4-char quanta — so the app
already produces exactly the kind of formatted text this change needs to *accept* as input.

## Goals / Non-Goals

**Goals:**
- Let `Hex`/`Base64` payload text carry whitespace for readability (typed or pasted) without it
  affecting validity or the encoded bytes.
- Wrap the payload field so long `Hex`/`Base64`/`Text` content doesn't require horizontal
  scrolling to edit.
- Keep `Json`/`Text` payload bytes exactly what the user typed — no silent content changes.
- Avoid allocating an intermediate stripped string for every keystroke's validation pass.
- Store templates' `Hex`/`Base64` payloads in a canonical (whitespace-free) form, and make
  reopening one for editing start from a nicely-formatted rendering rather than a raw blob.

**Non-Goals:**
- No true hex-editor (offsets, byte grid, ASCII gutter) — this is still a text field, just a more
  forgiving one for two of its four interpretations.
- No live-resize-responsive wrap width — the payload field's width is fixed for the dialog's
  lifetime (it already is, via each dialog's `FieldWidth` constant), so "adaptive width" here means
  reading that constant once at construction, not reacting to terminal resizes.
- No change to `Json` handling, and no change to `PayloadPresentation`/`PayloadContentProbe`
  (display-side rendering) beyond reusing `Render` as-is.

## Decisions

### Wrap is on for Hex/Base64/Text, off for Json
`TextView.WordWrap = true` whenever Payload Type is `Hex`, `Base64`, or `Text`; `false` for `Json`.
Alternative considered: wrap unconditionally for all four types. Rejected — pretty-printed JSON
(`RenderJson`'s own indentation, or whatever the user typed) has deliberate line structure; an
added soft-wrap on top of that fights it visually. `Text` has no such built-in structure to
protect, so it gets the same treatment as the two binary types. This needs to react live to the
Payload Type dropdown's existing `ValueChanged` handler (which already drives `UpdateValidity` in
both dialogs), not just be set once at construction — switching modes without closing the dialog
must update the wrap state too.

### Whitespace is significant for Json/Text, insignificant for Hex/Base64
This follows directly from how each type encodes, not from a UX preference: `Json`/`Text` encode
as the literal UTF-8 bytes of the typed text (`PayloadEncoding.ToBytes`'s `_ =>
Encoding.UTF8.GetBytes(payload)` branch), so any whitespace the user types **is** payload content —
stripping it would silently change what gets sent. `Hex`/`Base64` encode as the *decoded* byte
sequence; whitespace in the encoded representation has no effect on the decoded bytes once removed.
Alternative considered: strip whitespace uniformly across all four types before validating.
Rejected outright — it would corrupt `Json`/`Text` payloads.

### Whitespace must fall on encoding-unit boundaries, not just "anywhere"
Whitespace is only valid between complete units — a 2-hex-digit byte pair for `Hex`, a 4-character
quantum for `Base64` — matching how `PayloadPresentation.RenderHex`/`RenderBase64` already group
these types for display. Whitespace that splits a unit (e.g. a space between the two digits of one
byte) makes the payload invalid, even though blind stripping would still often decode to *some*
byte sequence. Alternative considered: strip all whitespace unconditionally and decode whatever's
left. Rejected — it can't distinguish deliberately-formatted input from a garbled paste that
happens to still decode to an even digit count; boundary checking catches that case as invalid
instead of silently accepting mangled input. This also happens to be what makes the
allocation-free implementation possible (next decision) rather than being pure extra strictness for
its own sake.

### Boundary validation and decoding via spans, no intermediate string
Scan the payload text as a `ReadOnlySpan<char>`, locate whitespace runs with a `SearchValues<char>`
built from a small fixed set (space, tab, CR, LF — not the full `char.IsWhiteSpace` Unicode
category, since `SearchValues.Create` needs concrete values, not a predicate), and split into
non-whitespace fragments. Each fragment's length must be a multiple of the type's unit size (2 for
`Hex`, 4 for `Base64` — the final `Base64` quantum's padding `=` characters keep this true even for
a partial final group). Decode each fragment directly via the span-based `Convert` overloads
(`Convert.FromHexString(ReadOnlySpan<char>)`, `Convert.TryFromBase64Chars`) into a single pre-sized
output buffer. Alternative considered: `new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray())`
then decode normally. Rejected — allocates on every validation pass (i.e. every keystroke) and
throws away the boundary information needed for the previous decision.

### One shared decode routine backs both validation and encoding
`PayloadValidation.IsValidHex`/`IsValidBase64` and `PayloadEncoding.ToBytes`'s `Hex`/`Base64`
branches both route through the same internal fragment-scan-and-decode routine — validation checks
whether it succeeds, encoding uses its result — rather than each reimplementing the boundary/scan
logic independently. Keeps them from drifting out of sync (validation accepting something encoding
then can't actually decode).

### Templates store the normalized form; editing re-renders from bytes
`TemplateDialog.Commit()` stores the canonical (whitespace-stripped) `Hex`/`Base64` payload text,
not whatever whitespace the user typed or pasted. `TemplateDialog(initial)` decodes that stored
text back to bytes via `PayloadEncoding.ToBytes` and repopulates the payload field via
`PayloadPresentation.Render(bytes, type, FieldWidth)` instead of dropping in the raw stored string —
reusing the same rendering the read-only Message Detail view already uses, at the dialog's existing
`FieldWidth` constant (no new width value introduced). `PublishDialog` is unaffected by either
half of this — it always starts empty (existing "Each open starts empty" behavior) and never
persists anything.

## Risks / Trade-offs

- **[Risk]** The fixed whitespace set (space/tab/CR/LF) doesn't cover every character
  `char.IsWhiteSpace` would (e.g. some Unicode space separators) → **Mitigation**: this change is
  strictly more lenient than today's behavior (which accepts none of them), never less — anything
  outside the fixed set was already invalid before this change and remains so; no regression, just
  a bounded scope of leniency.
- **[Risk]** Re-rendering a template's payload on edit-open replaces whatever whitespace the user
  originally typed with the app's own grouping, which could read as an unexpected change →
  **Mitigation**: intentional per the storage decision above, and the grouping matches what
  `PayloadPresentation` already shows for the same bytes in the Message Detail view, so it should
  look familiar rather than arbitrary.
- **[Risk]** `WordWrap` toggling live off the Payload Type dropdown, on top of the existing
  `ValueChanged` → `UpdateValidity` wiring, is an easy spot to under-wire (e.g. set at construction
  but not on subsequent changes) → **Mitigation**: fold it into the same handler that already
  drives `UpdateValidity` in both dialogs, not a separate one.

## Migration Plan

No data migration needed. The new validation is a strict superset of the old one (whitespace-free
`Hex`/`Base64` text — the only kind previously accepted — still validates and decodes identically),
so every template already stored today remains valid and unchanged. Existing `Hex`/`Base64`
templates opportunistically pick up the normalized storage form the next time they're edited and
saved, the same "upgraded on next write" pattern `nats-templates`' marker-TTL requirement already
uses.

## Open Questions

- Exact whitespace character set for the `SearchValues<char>` (space/tab/CR/LF as listed above, or
  a slightly broader set) — low-stakes, can be settled during implementation.
