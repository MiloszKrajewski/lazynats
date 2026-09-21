## 1. PublishDialog seeding

- [x] 1.1 Add `internal readonly record struct PublishSeed(string Subject, IReadOnlyDictionary<string, string> Headers, PayloadType PayloadType, byte[] PayloadBytes)` to `src/lazynats/Publish/` (e.g. a new `PublishSeed.cs`, following the codebase's one-type-per-file convention where practical).
- [x] 1.2 Add an optional `PublishSeed? seed = null` parameter to `PublishDialog`'s constructor.
- [x] 1.3 Seed `_subjectField.Text` from `seed?.Subject` (empty when `seed` is null, unchanged from today).
- [x] 1.4 Seed `_headers` from `seed.Value.Headers.Select(pair => new HeaderPair(pair.Key, pair.Value))` when `seed` is given (mirrors `TemplateDialog`'s own `_headers` seeding).
- [x] 1.5 Construct `_payloadSection` with `seed?.PayloadType ?? PayloadType.Text` (Payload text left empty either way - the actual bytes-rendered seeding happens in 1.6).
- [x] 1.6 When `seed` is given, after all controls (including `_sendButton`) are constructed, call `Layout()` then `_payloadSection.SeedFromBytes(seed.Value.PayloadBytes, seed.Value.PayloadType)`, matching `TemplateDialog`'s existing `seedBytes` sequencing exactly.
- [x] 1.7 Confirm `UpdateValidity()` (already called at the end of the constructor) correctly enables Send when a seeded Subject/Payload are valid.

## 2. Templates -> Publish wiring

- [x] 2.1 Add a `NatsConnection connection` constructor parameter to `TemplatesTab`, stored in a field.
- [x] 2.2 Add `OpenPublishDialog()` to `TemplatesTab`: return early if `_listView.SelectedTemplate is not { } template`; otherwise build a `PublishSeed` from that template (Subject, Headers, PayloadType, and `PayloadEncoding.ToBytes(template.PayloadType, template.Payload)` for the bytes - the same call `OpenEditDialog()` already makes) and `App!.Run(new PublishDialog(_connection, seed))`.
- [x] 2.3 Append `new ShortcutHint(Key.P, "Publish", OpenPublishDialog, Group: ExtraOperationsGroup)` to `_extraOperations`, alongside the existing Export/Import entries.

## 3. Call site

- [x] 3.1 In `MainWindow.cs`, pass `connection` as the new argument when constructing `templatesTab`.

## 4. Verification

- [x] 4.1 Build (`dotnet build src/lazynats.sln`) and run `dotnet test src/lazynats.sln` to confirm nothing else broke.
- [x] 4.2 Via tmux (see CLAUDE.md's build/run section): create or select a template with non-trivial Headers and a `Json` Payload with minified content, press `P` on it, and confirm the Publish dialog opens with Subject/Headers/Payload Type filled in and the Payload field showing pretty-printed Json.
- [x] 4.3 Via tmux: confirm `P` on an empty Templates list does nothing (no dialog opens), and that Alt+P still opens an empty Publish dialog regardless of Templates tab state.
- [x] 4.4 Via tmux: from a pre-populated dialog, edit a field and Cancel; reopen the same template's detail panel and confirm the stored template is unchanged.
