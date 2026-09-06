## Why

Pressing Down on a focused tab header sometimes lands keyboard focus on an invisible,
non-interactive view instead of anything the user can see or act on. Concretely: `Tabs.Value` (a
tab's content root, e.g. `SubscriptionsView`/`PublishView`) is itself `CanFocus = true` while also
containing its own focusable descendants (`ListEditorView<T>`'s `_listView`, `PublishView`'s
`_subjectField`/band containers). `ManagementTabs.FocusOwnContent()` enters content via
`Value.SetFocus()`, which - per its own code comment - "always lands on the content's default
focus target" rather than a specific remembered one; in practice that can resolve to `Value`
itself rather than descending into a genuinely interactive leaf. From the keyboard, this reads as
"pressing Down made focus disappear" - climbing back Up to the header and pressing Down again
happens to land correctly, because by then something else (unrelated to intent) has changed.

## What Changes

- `ManagementTabs.FocusOwnContent()` (the Down-from-header handler) explicitly finds the first
  focusable descendant leaf within the tab's content and focuses that, instead of calling
  `Value.SetFocus()` and trusting Terminal.Gui to resolve an unambiguous target on its own.
- This is a single, centralized fix in the class that owns the `tab-navigation` capability - no
  changes to `ListEditorView<T>`, `SubscriptionsView`, or `PublishView`, and no per-tab-content
  special-casing.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `tab-navigation`: clarifies that "Down returns focus from a header to its tab's content" means
  focus lands on an actual interactive descendant of that content, not merely somewhere within the
  content subtree (which can include non-interactive container views).

## Impact

- `src/lazynats/ManagementTabs.cs` - `FocusOwnContent()` body, plus a new private helper to locate
  the first focusable descendant.
- No changes to any tab's own content view; both `SubscriptionsView` (via `ListEditorView<T>`) and
  `PublishView` are structured the same way (a `CanFocus = true` root wrapping its own focusable
  children) and both benefit from the fix without modification.
