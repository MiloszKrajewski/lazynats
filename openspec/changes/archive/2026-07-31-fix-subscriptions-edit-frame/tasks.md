## 1. Wire up the labeled band, frame, and background

- [x] 1.1 In `src/lazynats/MainWindow.cs`, add a "Subscriptions" `Label` and wrap `subscriptionsView`
      in a new `EditFrame` (`subscriptionsFrame`), positioned below the label, matching the shape
      of `PublishView`'s `headersBand` (`headersLabel` + `headerFrame`).
- [x] 1.2 Introduce a wrapping band `View` (`subscriptionsBand`) containing the label and frame,
      moving `Title = " Subscribe "` and `Padding = { Thickness = new Thickness(1) }` from
      `subscriptionsView` onto `subscriptionsBand`.
- [x] 1.3 Set `InnerBackgroundNormal`/`InnerBackgroundFocused` on `subscriptionsFrame` and
      `subscriptionsView.Background` (the `ListEditorView<T>` background property) both to
      `Theme.EditableBackground`.
- [x] 1.4 Update `tabs.Add(...)`, `tabs.Value = ...`, and `subscribeTabShortcut.Action` in
      `MainWindow.cs` to use `subscriptionsBand` instead of `subscriptionsView` as the actual
      `Tabs` SubView / selection target.

## 2. Verify

- [x] 2.1 Run the app (`dotnet run --project src/lazynats`) and confirm the Subscribe tab shows a
      "Subscriptions" label above a padded edit frame around the list, matching the Publish tab's
      labeled fields, with the correct (non-default) background.
- [x] 2.2 Manually verify Alt+B/Alt+P tab switching, Tab-key focus reaching the list, Up/Down
      between the tab header and list content, and Ctrl+N/E/D (add/edit/delete subscription) all
      still work on the Subscribe tab.
- [x] 2.3 `dotnet build src/lazynats.sln` succeeds with no new warnings.
