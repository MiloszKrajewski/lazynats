## Why

The Subscribe tab's subscription list is the only `ListEditorView<T>` in the app that isn't
wrapped in an `EditFrame`, and the only edit-capable field with no field-name label above it.
`PublishView`'s bands each pair a `Label` ("Subject"/"Headers"/"Payload") with a padded, explicitly
colored `EditFrame`; `SubscriptionsView` has neither — it renders with no edit-frame border, no
label, and falls back to whatever background its `ListView` inherits from the tab content area —
visually inconsistent with the rest of the app and, on the current color scheme, the wrong
background color for an editable list.

## What Changes

- Wrap `SubscriptionsView`'s list in an `EditFrame`, matching the pattern already used for
  `HeaderEditorView` in `PublishView`.
- Set `SubscriptionsView.Background` (the `ListEditorView<T>` background already exposed for this
  purpose) to `Theme.EditableBackground`, so the list's fill matches the other editable controls
  in the app instead of an inherited scheme color.
- Add a "Subscriptions" `Label` above the framed list, matching `PublishView`'s
  "Subject"/"Headers"/"Payload" labels above their own fields — the tab caption itself stays
  " Subscribe " (the action), same as the Publish tab's caption stays "Publish" while its fields
  are separately labeled by name.
- No behavioral change to subscribing/editing/deleting subscriptions — this only corrects how the
  existing list is framed, colored, and labeled.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `nats-subscriptions`: adds a presentation requirement — the Subscribe tab's list is now
  presented with a "Subscriptions" label above a padded `EditFrame` with the shared editable
  background, mirroring the `nats-publish` capability's own equivalent requirement added by
  `add-edit-frame`. `edit-frame` and `list-editor` themselves are unchanged — both already support
  this composition; `SubscriptionsView` simply wasn't using it.

## Impact

- `src/lazynats/MainWindow.cs`: construction site for `SubscriptionsView` — introduces a wrapping
  band `View` (label + `EditFrame`), the same shape as `PublishView`'s `headersBand`, and moves the
  `Title`/`Padding` currently set on `SubscriptionsView` onto that band.
- No changes to `src/lazynats/Subscriptions/SubscriptionsView.cs` beyond setting `Background` —
  its own class/inheritance is untouched.
- No changes to `src/lazynats/Components/EditFrame.cs` or
  `src/lazynats/Components/ListEditorView.cs` — both already expose everything this needs.
