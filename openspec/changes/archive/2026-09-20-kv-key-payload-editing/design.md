## Context

`CreateKeyDialog` (Values/CreateKeyDialog.cs) is today a plain Name + Value (TextView) dialog:
Value is always sent as `Encoding.UTF8.GetBytes(text)`, and `ValuesTab.OpenEditKeyDialog` gates
Edit on `ValueText.TryDecode` - a printable-UTF-8-only guard that refuses to open the dialog at
all for a binary value. `PublishDialog` and `TemplateDialog` already solve the general "compose a
payload of a chosen type" problem with a `DropDownList<PayloadType>` plus the shared
`lazynats.Core.Payloads` trio (`PayloadValidation.IsValid`, `PayloadEncoding.ToBytes`), and the KV
side already has a read-only counterpart for *display*: `ValueDetailDialog`'s `PayloadDetailSection`
classifies bytes via `PayloadContentProbe.Classify` and picks a default presentation via
`PayloadPresentation.DefaultType`/`Render`. This change makes `CreateKeyDialog` the same kind of
payload-composing dialog Publish/Templates already are, and reuses the read side's own
classification helpers to seed Edit instead of refusing to open it.

## Goals / Non-Goals

**Goals:**
- `CreateKeyDialog` gets a Payload Type dropdown identical in shape to `PublishDialog`/
  `TemplateDialog`'s, gating Create/Save the same way (`PayloadValidation.IsValid`) and encoding
  the same way (`PayloadEncoding.ToBytes`).
- Edit always opens (no printable-text refusal), seeding Payload Type and Value from the entry's
  `PayloadContentProbe.Classify` result, using the exact default-type/render logic
  `ValueDetailDialog`'s presentation selector already uses, so a binary value is now editable (as
  Hex/Base64) instead of unreachable.
- `NewKeyOptions` carries the selected `PayloadType` end-to-end so `ValuesTab` never has to
  re-derive it.

**Non-Goals:**
- No change to `ValueDetailDialog`/`PayloadDetailSection` (the read-only viewer) - it already does
  the right thing; this change only reuses its classification helpers.
- No change to `KeyDetails`/`ValuePeek` (the key-list detail panel's clipped preview).
- Not building a restricted, content-aware dropdown (à la `ValueDetailDialog`'s presentation
  selector, which only offers `AllowedTypes(kind)`). Create has no content to restrict against, and
  Edit should let the user deliberately reinterpret a value's bytes under a different type, so both
  modes offer the full, unrestricted four-type dropdown - matching `PublishDialog`/`TemplateDialog`,
  not `ValueDetailDialog`.

## Decisions

### Reuse the Publish/Template dropdown shape verbatim
`DropDownList<PayloadType>` + `Theme.ApplyEditableScheme` + a `ValueChanged` handler calling the
dialog's own `UpdateValidity`, placed as its own labeled row - same construction `PublishDialog`/
`TemplateDialog` already use. No new shared component: three call sites (Publish, Templates,
Values) repeating a five-line `DropDownList` setup doesn't clear the bar for extraction the way
`PayloadValidation`/`PayloadEncoding` themselves did (those are actual decision logic, not view
wiring).

### Layout: Payload Type row inserted between Name and Value
Name (Y 0-3) stays; a new Payload Type label+dropdown occupies Y 4-6 (label Y4, frame Y5 height 3,
mirroring Publish/Template's own label-then-frame spacing); Value's label/frame shift from Y4/Y5 to
Y8/Y9. `ReservedChromeRows` (used to clamp the responsive Value field height) increases from 14 to
18 to absorb the new row's label+frame height (4 rows), keeping the existing
`Math.Clamp(app.Screen.Height - ReservedChromeRows, MinValueHeight, MaxValueHeight)` margin
unchanged in spirit.

### Seeding Edit from content classification, not a printable-text guard
`ValuesTab.TryOpenEditKeyDialogAsync` drops `ValueText.TryDecode` entirely. It fetches the entry
(unchanged) and always proceeds:
```
var kind = PayloadContentProbe.Classify(bytes);
var type = PayloadPresentation.DefaultType(kind);      // Json->Json, Utf8Text->Text, Binary->Hex
var text = SeedValueText(bytes, type);
```
`SeedValueText` reuses `PayloadPresentation.Render` for Json/Hex/Base64 - exactly
`ValueDetailDialog`'s own default-presentation logic - but seeds `Text` from the plain
`Encoding.UTF8.GetString(bytes)` decode instead. `ValueText.cs` is deleted - nothing else
references it once the guard is gone.

**Why Text is the one exception:** `Render`'s Text path (`RenderText`/`ChunkFixedWidth`) chops the
decoded string into fixed-`width` lines with no regard for word boundaries - reasonable for the
read-only `PayloadDetailSection` Label it was built for (`WordWrap = false`, so nothing re-wraps
it). But `CreateKeyDialog`'s `_valueView` is a `TextView` with its own `WordWrap` on for every type
but Json (see `UpdateValidity`), which *does* wrap on word boundaries. Feeding it a string that's
already hard-chopped mid-word double-wraps it: the TextView disagrees with the chop and pushes each
trailing partial word down onto its own line, producing exactly the ragged, doubly-broken text this
was meant to avoid. Saving compounds it further - `PayloadEncoding.ToBytes` writes Text back as raw
UTF-8 with no whitespace stripping (unlike Hex/Base64's whitespace-tolerant decode), so an
unmodified save would literally persist the chop-point newlines into the stored value. Hex/Base64
don't hit this: their fixed-width rows have no partial "words" for `WordWrap` to disagree with (see
below), and Json's `WordWrap` is off, so its real indentation newlines render as typed.

### Render width for the Edit seed
`TemplateDialog.SeedPayloadText` renders Hex/Base64 at a fixed `FieldWidth = 76` because its Value
field is a fixed width. `CreateKeyDialog`'s Value field is already responsive
(`Dim.Func(_ => Math.Min(PreferredDialogWidth, app.Screen.Width - TerminalWidthMargin))` on the
dialog, `Dim.Fill()` down to the field). The seed instead computes the field's actual current inner
width the same way `EditFrame` derives it (`child.Width = Dim.Fill(1)` inside a frame whose own
content starts at `X = 2`): `valueWidth = dialogWidth - Padding(2) - EditFrame(2 + 1) = dialogWidth
- 5`. This only affects how Hex/Base64 bytes are initially grouped into rows/lines - `TextView`'s
own `WordWrap` (already on for everything but Json) still reflows if the terminal is resized after
opening, same as today.

### Payload Type is stored on `NewKeyOptions`, not re-derived
`NewKeyOptions` becomes `(string Name, PayloadType PayloadType, string Value)`. `TryCreateKeyAsync`/
`TryEditKeyAsync` call `PayloadEncoding.ToBytes(options.PayloadType, options.Value)` in place of
`Encoding.UTF8.GetBytes(options.Value)`. A reopen-after-server-failure (`OpenCreateKeyDialog`/
`OpenEditKeyDialog(bucket, key, edited)`) reseeds from the same `NewKeyOptions`, so the
previously-selected type and typed text survive a failed write unchanged - same convention as every
other field in this dialog.

## Risks / Trade-offs

- **Switching Payload Type without touching Value can silently reinterpret bytes** - e.g. a value
  seeded as `Hex` for a binary entry, then switched to `Text` without editing it, remains "valid"
  (Text accepts anything) and would save the *hex string itself* as UTF-8 bytes rather than the
  original binary. This is the same pre-existing behavior `PublishDialog`/`TemplateDialog` already
  have (Payload Type is user-selected, not locked to content) - not a new risk this change
  introduces, and out of scope to fix here.
- **BREAKING**: removing the printable-text guard means Edit is now always reachable, including for
  binary values that previously reported a refusal status message. No data migration needed - this
  is strictly more permissive than today.

## Migration Plan

No data migration. Existing keys are read exactly as before (`INatsKVStore.TryGetEntryAsync<byte[]>`
unchanged); only the Create/Edit dialog's fields and encoding path change.
