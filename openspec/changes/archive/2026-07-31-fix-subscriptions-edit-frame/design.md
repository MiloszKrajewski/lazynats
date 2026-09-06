## Context

`ListEditorView<T>` already exposes a `Background` property specifically so it can be "visually
paired with other edit controls (e.g. when wrapped in a padded `EditFrame`)" (see
`openspec/specs/list-editor/spec.md`). `PublishView` uses exactly this pairing for its
`HeaderEditorView`: each band pairs a `Label` naming the field ("Subject", "Headers", "Payload")
with a padded `EditFrame`, both keyed to the same background color.

`SubscriptionsView` (also a `ListEditorView<T>`, over `SubscriptionInfo`) never adopted this
pairing. Per the original `add-edit-frame` change's own proposal, this was a deliberate scope cut
("Wire `EditFrame` into `PublishView`'s three edit surfaces" — the Subscribe tab was never
listed), not an oversight in that change. In `MainWindow.cs` today, `SubscriptionsView` is added
straight to `ManagementTabs` as a tab's content, with only `Title = " Subscribe "`/outer
`Padding.Thickness(1)` set on it directly — no field-name label, no `EditFrame`, no `Background`.
Its list renders with whatever it inherits from the tab content area, inconsistent with
`PublishView` and, per the user's report, the wrong background color and no framing.

`EditFrame` only requires its wrapped child to be a `View`; it doesn't care whether that child is
itself a `ListEditorView<T>` subclass (`SubscriptionsView`) or a plain `View` composing one
(`PublishView`'s relationship to `HeaderEditorView`). So `SubscriptionsView` can be wrapped as-is,
with no change to its own class or its `ListEditorView<T>` inheritance.

## Goals / Non-Goals

**Goals:**
- Give `SubscriptionsView`'s list the same "labeled band + padded `EditFrame`" treatment
  `PublishView`'s bands already use, with a "Subscriptions" label and `Theme.EditableBackground`
  as the shared background.
- Keep the tab's existing `Title`/outer-padding presentation and all existing focus/shortcut
  behavior (Ctrl+N/E/D, Tab navigation, Alt+B tab switch) working exactly as before.

**Non-Goals:**
- No change to subscription add/edit/delete behavior, keybindings, or the `PatternDialog` modal.
- No change to `EditFrame` or `ListEditorView<T>` themselves, or to `SubscriptionsView`'s class
  hierarchy — both already support this composition as-is.

## Decisions

- **Wrap at the `MainWindow.cs` call site, not inside `SubscriptionsView` itself**, using the same
  "band" shape `PublishView` already uses per field: a `Label` at the top, an `EditFrame` filling
  the rest. `SubscriptionsView` itself gains no internal structure — only a new wrapping band
  `View` is introduced at its single construction site. Concretely:

  ```csharp
  var subscriptionsView = new SubscriptionsView(registry) { Background = Theme.EditableBackground };
  var subscriptionsLabel = new Label { Text = "Subscriptions", X = 0, Y = 0 };
  var subscriptionsFrame = new EditFrame(subscriptionsView) {
      X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(),
      InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
  };
  var subscriptionsBand = new View {
      X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true,
      Title = " Subscribe ", Padding = { Thickness = new Thickness(1) },
  };
  subscriptionsBand.Add(subscriptionsLabel, subscriptionsFrame);
  ```

  `subscriptionsBand` (not `subscriptionsView`) becomes the `Tabs` child: `Title` and
  `Padding.Thickness(1)` move from `subscriptionsView` onto it, since `Tabs` reads whichever `View`
  it was given for its tab caption and border — mirroring `PublishView`'s own top-level shape
  (a plain `View` containing labeled bands) one level up, rather than `PublishView`'s per-band
  internals (which have no outer `Title`/`Padding` of their own, since `PublishView` itself already
  carries those).
- **`tabs.Value` and `subscribeTabShortcut` must target `subscriptionsBand`, not
  `subscriptionsView`.** `ManagementTabs` (`FocusOwnHeader`/`FocusOwnContent`/`SwitchTab`) reads
  `Value.Border`/`Value.HasFocus` directly on whatever `Tabs.Value` currently is — that must be the
  actual `Tabs` SubView (`subscriptionsBand`), matching how `Tabs` already treats any added view.
  `FocusOwnContent`'s `FindFirstFocusableDescendant` walk resolves through `subscriptionsBand` then
  through `EditFrame` (which has no focusable content of its own) straight to `subscriptionsView`,
  so Down-from-header behavior is unaffected.
- **Label text is "Subscriptions" (the content), tab caption stays " Subscribe " (the action)** —
  matching `PublishView`, whose tab caption is "Publish" while its own field labels name what's in
  each field ("Subject"/"Headers"/"Payload"), not the tab's action.
- **Background value**: `Theme.EditableBackground` directly, both for
  `SubscriptionsView.Background` and the frame's inner colors — the same constant
  `PublishView`/`HeaderDialog`/`PatternDialog` already treat as the single source of truth for
  editable-control backgrounds. Unlike `PublishView`'s field frames, there's no `TextField` here to
  read `VisualRole.Editable` off of, so this reads `Theme.EditableBackground` directly instead.

## Risks / Trade-offs

- [Swapping which view is `Tabs`' actual SubView could silently break tab-switching or focus
  drill-down] → Manually verify, after the change: Alt+B/Alt+P tab switching, Tab-key focus
  reaching the list, Up/Down between tab header and list content, and Ctrl+N/E/D all still work on
  the Subscribe tab.
- [`EditFrame` reserves 2 left / 1 right / 1 top / 1 bottom cell beyond its child, stacked on top
  of the existing outer `Padding.Thickness(1)` and the new label row] → Confirm visually that the
  list still has enough height/width for the longest expected pattern text and the empty-hint
  message, and that this reads consistent with (not more cramped than) `PublishView`'s own bands.

## Open Questions

- None — implementation approach confirmed by direct comparison with `PublishView`'s existing
  labeled-band + `EditFrame` usage and `ManagementTabs`'/`Tabs`' documented `Value`/`Border`
  semantics (`doc/terminal-gui-howto.md`).
