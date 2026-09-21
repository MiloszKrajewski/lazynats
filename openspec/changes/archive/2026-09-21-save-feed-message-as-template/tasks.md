## 1. TemplatesTab: pre-populated Create entry point

- [x] 1.1 Add `public void OpenCreateDialogFromMessage(string subject, NatsHeaders? headers, byte[] payloadBytes)` to `TemplatesTab`, building a `Template` seed (Name empty, Subject as given, Headers converted the same key/value way `MessageDetailDialog.FormatHeaders` does, Payload Type via `PayloadContentProbe.Classify(payloadBytes)` + `PayloadPresentation.DefaultType(...)` when `payloadBytes.Length > 0`, else `PayloadType.Text`, Payload left as a placeholder string).
- [x] 1.2 From that method, open `new TemplateDialog(seed, isEdit: false, seedBytes: payloadBytes)` and, on `dialog.Result`, call the existing private `WriteAsync(template, isEdit: false)` unchanged - mirror `OpenCreateDialog(Template? seed)`'s existing `App!.Run(...)` shape.
- [x] 1.3 Confirm no change is needed to `WriteAsync`, `EnsureBucketExistsAsync`, or `RefreshListAsync` - this path reuses them as-is.

## 2. LiveUpdatesView: T shortcut

- [x] 2.1 Check context7 (`websites/gui-cs_github_io_terminal_gui`) / `doc/terminal-gui-howto.md` for an appropriate existing `Command` enum member to reuse for this action (matching the codebase's existing pattern of repurposing a semantically-close `Command`, e.g. `Command.DeleteAll` for Clear, `Command.Toggle` for Follow/Pause).
- [x] 2.2 Add a new event `public event Action<FeedEnvelope>? SaveAsTemplateRequested;` on `LiveUpdatesView`.
- [x] 2.3 Add an `AddCommand`/`KeyBindings.Add(Key.T, ...)` pair (same pattern as the existing `Command.DeleteAll`/`Key.C` binding) that, when `_listView.SelectedItem` is a valid index into `_events` (same guard `OnAccepted` uses for `Enter`), raises `SaveAsTemplateRequested` with that envelope; no-op otherwise.
- [x] 2.4 Add `new(Key.T, "Save as Template", ...)` to `LiveUpdatesView.Shortcuts` (the `IShortcutSource` list already returning `Clear`/`Follow-Pause`).

## 3. MainWindow: wiring

- [x] 3.1 Subscribe to `liveUpdates.SaveAsTemplateRequested`, unpacking `envelope.Message.Subject`, `envelope.Message.Headers`, and `envelope.Message.Data ?? []`, and call `templatesTab.OpenCreateDialogFromMessage(...)` with them - same style as the existing `liveUpdates.ItemSelected += envelope => App!.Run(new MessageDetailDialog(envelope));` line.

## 4. Verification

- [x] 4.1 `dotnet build src/lazynats.sln` compiles cleanly.
- [x] 4.2 Via `tmux` against a real NATS server: publish a JSON message, select it in the Live Feed, press `T`, confirm the Create Template dialog opens with Subject/Headers/Payload pre-filled and Payload Type defaulted to `Json`, pretty-printed.
- [x] 4.3 Via `tmux`: repeat with a plain-text message and with a binary/non-UTF8 payload, confirming Payload Type defaults to `Text` / `Hex`|`Base64` respectively and the Payload field renders readably (not a raw byte dump).
- [x] 4.4 Via `tmux`: press `T` while the Live Feed is empty (no message selected) and confirm no dialog opens.
- [x] 4.5 Via `tmux`: press `T`, confirm the dialog, and verify (via the `nats` CLI in `.bin/`, or the Templates tab) that the new entry now exists in the `lazynats-templates` bucket.
- [x] 4.6 Via `tmux`: press `T` and then `Esc` (cancel) and confirm no template was created and the source message is unchanged in the feed.
- [x] 4.7 Via `tmux`: open the shortcut picker (`?`) while the Live Feed is focused and confirm `Save as Template` is listed.
