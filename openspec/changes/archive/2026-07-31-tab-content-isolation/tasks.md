## 1. Rename PublishView to PublishTab

- [x] 1.1 Rename `src/lazynats/PublishView.cs` to `src/lazynats/PublishTab.cs`, renaming the
      class `PublishView` → `PublishTab` (no other changes).
- [x] 1.2 Update the reference in `MainWindow.cs` (`new PublishView(...)` → `new PublishTab(...)`)
      and grep the repo for any other `PublishView` references (docs/comments included).

## 2. Extract SubscribeTab

- [x] 2.1 Create `src/lazynats/Subscriptions/SubscribeTab.cs`: a `View` taking
      `SubscriptionRegistry` in its constructor, moving the `subscriptionsLabel` +
      `subscriptionsFrame` (`EditFrame`-wrapped `SubscriptionsView`) construction currently inline
      in `MainWindow` (lines ~22-32) into this new type verbatim, preserving `X`/`Y`/`Width`/
      `Height`/`Add` ordering exactly.
- [x] 2.2 In `MainWindow`, replace the inline `subscriptionsView`/`subscriptionsLabel`/
      `subscriptionsFrame`/`subscriptionsBand` construction with
      `new SubscribeTab(registry) { Title = " Subscribe ", Padding = { Thickness = new Thickness(1) } }`,
      matching how `PublishTab` is constructed.
- [x] 2.3 Update `tabs.Add(...)` / `tabs.Value = ...` / the Alt+B shortcut's target to reference
      the new `SubscribeTab` instance in place of the old `subscriptionsBand`.

## 3. Verify no behavior change

- [x] 3.1 `dotnet build src/lazynats.sln` succeeds with no warnings from the rename/extraction.
- [x] 3.2 Run the app against a local NATS server; confirm the Subscribe tab's visual layout
      (label, framed list, title, padding) and Ctrl+N/E/D behavior are unchanged.
- [x] 3.3 Confirm Alt+B/Alt+P tab switching, Up/Down/Left/Right tab-navigation behavior
      (`tab-navigation` spec scenarios), and the Publish tab's send/status-bar behavior are all
      unchanged.

## 4. Documentation

- [x] 4.1 Update `doc/ui-design.md`'s Management Tabs section to document the `*Tab`
      naming/structure convention (self-contained content, `MainWindow` registration-only) for
      future tabs (Streams, Consumers, KV, OBJ).
