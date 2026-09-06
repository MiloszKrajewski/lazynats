## 1. Payload Content Probe

- [x] 1.1 Add `PayloadContentKind` enum (`Json`, `Utf8Text`, `Binary`) in
      `src/lazynats/Payloads/PayloadContentKind.cs`.
- [x] 1.2 Add `PayloadContentProbe.Classify(byte[] payload)` in
      `src/lazynats/Payloads/PayloadContentProbe.cs`: strict-UTF-8 decode (exception-fallback
      `Encoding`, not `Encoding.UTF8.GetString`) → `Binary` on decode failure; reject to `Binary`
      on any decoded C0/C1 control character other than `\t`/`\n`/`\r`; otherwise attempt
      `JsonDocument.Parse` on the decoded text → `Json` on success, `Utf8Text` otherwise. Empty
      payload classifies as `Utf8Text`.
- [x] 1.3 Manually verify classification against representative cases (empty payload, plain
      ASCII text, JSON object/array, JSON scalar e.g. `"42"`, UTF-8 text with emoji/non-Latin
      script, malformed UTF-8 bytes, well-formed-UTF-8-but-control-character-laden bytes) —
      no automated test project exists yet (see CLAUDE.md), so exercise this via a scratch
      console check or the AOT probe project rather than a unit test.

## 2. Message Detail Dialog

- [x] 2.1 Add `MessageDetailDialog` in `src/lazynats/LiveFeed/MessageDetailDialog.cs`: a
      buttonless, non-generic `Dialog` (Esc-cancels via the same inherited convention as
      `Dialog<T>`; plain `Dialog` chosen over `Dialog<T>` since nothing outside the dialog needs a
      result back, same reasoning as `PublishDialog`) taking a `FeedEnvelope`, laid out with
      `Padding.Thickness` breathing room per the project's bordered-container convention.
- [x] 2.2 Build the dialog's formatted content: subject line, a blank line, a headers section
      (`Key: Value` per line, or an explicit "(no headers)" line when `message.Headers` is
      empty/null), a blank line, and a payload section rendered per `PayloadContentProbe.Classify`
      — `Json` re-serialized indented (parse with `JsonDocument`, write with
      `Utf8JsonWriter`/`JsonSerializer` using indented options), `Utf8Text` as its decoded text,
      `Binary` as a 16-bytes-per-row hex dump (space-separated, no offset/ASCII gutter) — no
      `Subject:`/`Headers:`/`Payload:` labels.
- [x] 2.3 Render Subject/Headers/Payload as three separate `EditFrame`-wrapped read-only `Label`s
      (not `TextView`, which carries editing affordances - a cursor, insertion-point navigation -
      that read as editable regardless of `ReadOnly`), each sized to its own content
      (`TextFormatter.MultiLine = true`, `WordWrap = false`; `CanFocus = false` on each frame so
      Tab/scroll-key focus stays on the Dialog, not an inert frame). Each `Label` uses
      `Theme.ApplyEditableScheme` (white on `Theme.EditableBackground` grey, matching every other
      input field in the app) with the wrapping `EditFrame`'s `InnerBackgroundNormal`/
      `InnerBackgroundFocused` set to the same color so the fill and the `Label`'s own paint don't
      seam; Subject's `Label` additionally overrides its `Scheme` to a cyan foreground on that same
      background. Only the payload `Label` gets scroll wiring when its content exceeds its cap:
      `SetContentHeight` + `ViewportSettingsFlags.HasVerticalScrollBar` +
      `Command.ScrollUp`/`ScrollDown`/`PageUp`/`PageDown` key bindings on the dialog itself
      (`AddCommand` is protected, and the `Label` is never focused).
- [x] 2.4 Size the dialog sensibly for typical payloads: `Width` prefers 132 columns (payloads are
      often JSON, which reads better with room) but is capped to `IApplication.Screen.Width - 4`
      via `Dim.Func`, re-evaluated on every layout pass so the dialog still fits (and reflows live
      on resize) on a narrower terminal, with `Height` left to Terminal.Gui's own `Dim.Auto`
      default so the dialog grows to fit Subject/Headers, while the payload frame caps at
      `MaxPayloadVisibleLines` (16) and scrolls internally rather than growing the dialog
      unbounded.

## 3. Wire Up Live Feed Selection

- [x] 3.1 In `MainWindow.cs`, replace the `liveUpdates.ItemSelected` handler's
      `MessageBox.Query(...)` stub with opening `MessageDetailDialog` for the selected
      `FeedEnvelope` (via `App!.Run`/whatever mechanism other dialogs in `MainWindow.cs` use to
      show a modal `Dialog<T>`).

## 4. Verification

- [x] 4.1 `dotnet build src/lazynats.sln` succeeds.
- [x] 4.2 Drive the running app (tmux, per CLAUDE.md's UI-testing guidance) to publish a JSON
      message, a plain-text message, and a binary message on subscribed subjects, select each in
      the Live Feed, and confirm the dialog shows subject, headers, and correctly-classified
      payload rendering for each; confirm `Esc` closes the dialog.
