## 1. NewKeyOptions

- [x] 1.1 Add `PayloadType PayloadType` to `NewKeyOptions` (`src/lazynats/Values/NewKeyOptions.cs`),
      ordered `(string Name, PayloadType PayloadType, string Value)`.

## 2. CreateKeyDialog

- [x] 2.1 Add a `DropDownList<PayloadType>` field (`_payloadTypeDropDown`), constructed and themed
      the same way `TemplateDialog`/`PublishDialog` do (`Theme.ApplyEditableScheme`, `ValueChanged`
      -> `UpdateValidity`), defaulted from `initial?.PayloadType ?? PayloadType.Text`.
- [x] 2.2 Insert the Payload Type label + `WrapField`-wrapped dropdown between Name and Value: Name
      stays at Y0 (height 3), Payload Type label at Y4 / frame at Y5 (height 3), Value label moves
      to Y8 / frame to Y9. Bump `ReservedChromeRows` from 14 to 18.
- [x] 2.3 In `UpdateValidity`, add `PayloadValidation.IsValid(payloadType, _valueView.Text)` to the
      gate alongside the existing Name check, flag `_valueView` invalid the same way `_payloadView`
      is flagged in `PublishDialog`/`TemplateDialog`, and set
      `_valueView.WordWrap = payloadType != PayloadType.Json` (mirroring those dialogs' identical
      line).
- [x] 2.4 In `Commit()`, include `_payloadTypeDropDown.Value ?? PayloadType.Text` in the returned
      `NewKeyOptions`.
- [x] 2.5 Add a `SeedValueWidth` (or similarly named) private static helper computing the Value
      field's current inner width from the dialog's own responsive width math (`dialogWidth - 5`,
      per design.md's "Render width for the Edit seed" decision), for use by `ValuesTab` when
      building the Edit seed (see 3.2) - or expose the computed `dialogWidth` some other way
      `ValuesTab` can reach before constructing the dialog; pick whichever keeps `ValuesTab` from
      duplicating `CreateKeyDialog`'s own width-clamping logic.

## 3. ValuesTab - Edit seeding

- [x] 3.1 Remove the `ValueText.TryDecode` guard from `TryOpenEditKeyDialogAsync`: after a
      successful `TryGetEntryAsync`, always proceed to open the dialog.
- [x] 3.2 Classify the fetched bytes via `PayloadContentProbe.Classify`, derive the default type via
      `PayloadPresentation.DefaultType(kind)`, and render the seed text via a `SeedValueText`
      helper: `PayloadPresentation.Render(bytes, type, width)` (width per task 2.5) for
      Json/Hex/Base64, but the plain `Encoding.UTF8.GetString(bytes)` decode for Text - see
      design.md's "Why Text is the one exception" for why Render's fixed-width chop
      double-wraps once fed into `_valueView`'s own word-wrapping `TextView`. Then call
      `OpenEditKeyDialog(bucket, key, new NewKeyOptions(key, type, text))`.
- [x] 3.3 Update the reopen-after-failure path in `TryEditKeyAsync`'s catch block (and
      `OpenCreateKeyDialog`'s equivalent) - no logic change needed there since both already just
      pass the existing `NewKeyOptions` back into the dialog, but confirm the seeded Payload Type
      survives the round-trip.

## 4. ValuesTab / CreateKeyDialog - encoding

- [x] 4.1 In `TryCreateKeyAsync`, replace `Encoding.UTF8.GetBytes(options.Value)` with
      `PayloadEncoding.ToBytes(options.PayloadType, options.Value)`.
- [x] 4.2 In `TryEditKeyAsync`, replace `Encoding.UTF8.GetBytes(edited.Value)` with
      `PayloadEncoding.ToBytes(edited.PayloadType, edited.Value)`.
- [x] 4.3 Add the `using lazynats.Core.Payloads;` import to `ValuesTab.cs` and `CreateKeyDialog.cs`.

## 5. Cleanup

- [x] 5.1 Delete `src/lazynats/Values/ValueText.cs` (no longer referenced).

## 6. Verification

- [x] 6.1 `dotnet build src/lazynats.sln` succeeds with no new warnings.
- [x] 6.2 Via tmux (per CLAUDE.md's UI-testing guidance) against a real NATS server: create a key
      with each Payload Type (Text/Json/Base64/Hex), confirm invalid text blocks Create and flags
      the Value field, and confirm the created entry's bytes match the selected type's encoding.
- [x] 6.3 Via tmux: write a binary value into a bucket (e.g. via the `nats` CLI in `.bin/`), then
      press E on that key in the app and confirm the dialog opens seeded as `Hex` (no refusal),
      edit it, save, and confirm the KV Value Detail dialog (`V`) shows the updated bytes.
- [x] 6.4 Via tmux: press E on a JSON-classified and a plain-text-classified key and confirm they
      seed as `Json` (pretty-printed) and `Text` respectively.
- [x] 6.5 Sync the `nats-kv` delta spec into `openspec/specs/nats-kv/spec.md` (or via
      `/opsx:sync`/archive) once implementation matches the spec deltas.
