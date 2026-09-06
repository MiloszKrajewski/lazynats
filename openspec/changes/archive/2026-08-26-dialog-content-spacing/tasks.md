## 1. Shared padding helper

- [x] 1.1 Add `Components/DialogText.cs` with `internal static class DialogText { public static
      string Pad(string text) => $" {text} "; }`

## 2. Dialog<T> subclass titles

- [x] 2.1 `Streams/CreateStreamDialog.cs`: `Title = DialogText.Pad("New Stream");`
- [x] 2.2 `Streams/CreateConsumerDialog.cs`: `Title = DialogText.Pad("New Consumer");`
- [x] 2.3 `Subscriptions/PatternDialog.cs`: `Title = DialogText.Pad(title);` (leave
      `SubscriptionsView`'s `"New Subscription"`/`"Edit Subscription"` call-site strings
      unpadded)
- [x] 2.4 `Publish/HeaderDialog.cs`: `Title = DialogText.Pad(title);` (leave
      `HeaderEditorView`'s `"New Header"`/`"Edit Header"` call-site strings unpadded)

## 3. MessageBox call sites

- [x] 3.1 `Streams/StreamsTab.cs`: pad both arguments of the `"Create Stream Failed"` /
      `ex.Message` `ErrorQuery` call
- [x] 3.2 `Streams/StreamsTab.cs`: pad both arguments of the `"Create Consumer Failed"` /
      `ex.Message` `ErrorQuery` call
- [x] 3.3 `Streams/StreamsTab.cs`: pad both the title and message arguments of the
      `"Delete Stream"` / `$"Delete stream '{name}'? ..."` `Query` call (leave the `"_Delete"`/
      `"_Cancel"` button labels untouched)
- [x] 3.4 `Streams/StreamsTab.cs`: pad both arguments of the `"Delete Stream Failed"` /
      `ex.Message` `ErrorQuery` call
- [x] 3.5 `MainWindow.cs`: pad both arguments of the `"Selected"` / `envelope.Message.Subject`
      `Query` call
- [x] 3.6 `add-consumer-delete` had already landed, so its `"Delete Consumer"` `Query` call and
      `"Delete Consumer Failed"` `ErrorQuery` call in `Streams/StreamsTab.cs` got the same
      treatment

## 4. Verification

- [x] 4.1 Manual pass via tmux: open `CreateStreamDialog` (Ctrl+N on the stream list) — confirm
      the title reads with visible margin on both sides against the border corners
- [x] 4.2 Manual pass: open `PatternDialog` for both New and Edit (Subscriptions tab) — confirm
      both titles are padded
- [x] 4.3 Manual pass: open `HeaderDialog` for both New and Edit (Publish tab) — confirm both
      titles are padded
- [x] 4.4 Manual pass: trigger the "Delete Stream" confirmation (Ctrl+D on a highlighted stream)
      — confirm both title and message read with margin, and the box has visibly grown to
      accommodate the padded text rather than clipping it
- [x] 4.5 Manual pass: trigger a failure path (e.g. attempt to create a stream with a name that
      already exists) — confirm the `ErrorQuery` title and the exception-message body both show
      the same margin
- [x] 4.6 Confirm no existing dialog's fixed-width content (the `Width = 43` fields) is clipped
      or visually disrupted by the title-length change
