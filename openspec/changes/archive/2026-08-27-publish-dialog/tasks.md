## 1. PublishDialog

- [x] 1.1 Create `src/lazynats/Publish/PublishDialog.cs` as a plain `Dialog` (not `Dialog<T>`)
      taking `NatsConnection` in its constructor, titled `" Publish "` (leading/trailing space
      per convention), `Padding.Thickness` top value `1`.
- [x] 1.2 Add Subject (`TextField`), Headers (`HeaderEditorView`, reused unchanged), and Payload
      (`TextView` with `TabKeyAddsTab = false`) bands, each wrapped in an `EditFrame` at a
      fixed 76-column width (wider than the 43 columns other dialogs use, to fit long NATS
      subjects/JSON payloads) and explicit `Y` offsets, mirroring `CreateKeyDialog`'s
      `WrapField`/background-matching pattern. No Navigate/Edit gate, no `KeyDown` interceptor,
      no `IShortcutSource` implementation.
- [x] 1.3 Add a status `Label` above the button row for inline Send feedback (starts empty/
      hidden).
- [x] 1.4 Add Cancel button (added first) that calls `RequestStop()` without publishing, and
      `_Send` button (added last, so it's the Enter-activated default) that calls an internal
      `Send()` — mirroring `PublishTab.Send`/`PublishAsync`, publishing via the injected
      `NatsConnection` and updating the status `Label` on success/failure without closing the
      dialog or clearing fields.
- [x] 1.5 Wire Subject validity the same way `PublishTab` did (red text while empty, `_Send`
      disabled) via `UpdateValidity()`.
- [x] 1.6 Override `OnAccepting` to return `true` (same as `CreateBucketDialog`/`CreateKeyDialog`)
      so Enter on Subject or the Headers list doesn't fall through to `Dialog`'s default
      unhandled-Accept-closes-dialog behavior.
- [x] 1.7 Delete `src/lazynats/Publish/PublishTab.cs`.

## 2. MainWindow wiring

- [x] 2.1 Remove `publishTab` construction, its tab-strip `Add`, and `tabs.Value` fallback
      references in `src/lazynats/MainWindow.cs`.
- [x] 2.2 Remove `publishTabShortcut` (Alt+2/"Publish") and `publishStatusShortcut` (and the
      `PublishTab.StatusChanged` subscription) from the status bar wiring.
- [x] 2.3 Renumber the remaining tab titles and Alt+digit `Shortcut`s: Subscribe stays
      `" 1:Subscribe "`/Alt+1, Streams becomes `" 2:Streams "`/Alt+2, KV becomes `" 3:KV "`/Alt+3,
      OBJ becomes `" 4:OBJ "`/Alt+4.
- [x] 2.4 Add a global `publishShortcut` (`Text = "Publish"`, `Key = Key.P.WithAlt`,
      `BindKeyToApplication = true`), whose `Action` constructs a fresh
      `new PublishDialog(connection)` and runs it via `App!.Run(dialog)`.
- [x] 2.5 Update the `StatusBar` construction's shortcut list to match (drop the two removed
      shortcuts, add `publishShortcut`, keep static-shortcut-count bookkeeping correct).

## 3. Docs

- [x] 3.1 Update `doc/UI.md`'s tab table (drop the Publish row, renumber Streams/KV/OBJ to
      2/3/4 and Alt+2/3/4) and its "Tab shortcuts use Alt+digit..." paragraph if it references
      Publish's mnemonic button.
- [x] 3.2 Rewrite `doc/UI.md`'s "Sending messages" section to describe the Alt+P Publish dialog
      (Cancel/Send buttons, inline status, fields reset on next open) instead of a management
      tab.

## 4. Verification

- [x] 4.1 Build: `dotnet build src/lazynats.sln`.
- [x] 4.2 Drive the app via tmux per `CLAUDE.md`: confirm Alt+P opens the Publish dialog from
      both the Subscribe tab and a drilled-down Streams/KV/OBJ view, Tab cycles
      Subject → Headers → Payload → Send without inserting tab characters into Payload, typing
      into Payload works with no prior mode switch, Ctrl+N/E/D still work on the Headers list
      (including that the nested `HeaderDialog` opens/closes correctly on top of the running
      Publish dialog), Send with an empty Subject is disabled, Send with a valid message
      publishes and shows inline success/failure feedback while keeping fields populated, and
      Cancel/Esc closes the dialog without publishing and without retaining values on the next
      Alt+P. Verified end-to-end against a real `nats-server` with the `nats` CLI subscribed to
      the target subject. Found and fixed a re-entrancy bug during this pass (see design.md):
      `publishShortcut.Action` originally called `App!.Run(new PublishDialog(...))` directly from
      inside the Alt+P key dispatch, which caused the nested modal loop to re-observe that same
      in-flight keypress as unhandled and recursively stack new `PublishDialog` instances,
      masquerading as "Cancel/Esc don't close the dialog" and "fields go blank". Fixed by
      deferring the `Run` call via `App!.AddTimeout(TimeSpan.Zero, ...)`.
- [x] 4.3 Confirm Alt+1..4 select Subscribe/Streams/KV/OBJ in the new order and Alt+2 no longer
      does anything Publish-related.
