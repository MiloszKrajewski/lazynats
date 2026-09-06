## Context

`ManagementTabs` (`src/lazynats/ManagementTabs.cs`) overrides `Tabs`' arrow-key commands to
implement the tab/content focus model in `doc/ui-design.md` and the `tab-navigation` spec.
`FocusOwnContent()` (bound to Down while a header is focused) calls `Value?.SetFocus()`, where
`Value` is the currently selected tab's content root view. The class's own comment already flags
that this is a compromise: `RestoreFocus()` (which would remember and restore whatever had focus
before the header was entered) is `internal` and inaccessible from this assembly, so `SetFocus()` -
the same public method the base `Tabs.Value` setter itself uses - is substituted, "at the cost of
always landing on the content's default focus target."

That "default focus target" is ambiguous when the content root is itself `CanFocus = true` *and*
contains its own focusable descendants - exactly the shape of every tab's content today:
`ListEditorView<T>` (`CanFocus = true` in its constructor) wraps a `_listView`
(`Terminal.Gui.Views.ListView`, also focusable); `PublishView` (`CanFocus = true`) wraps several
`CanFocus = true` band containers, each wrapping its own focusable field. `SetFocus()` can resolve
to the root itself rather than descending - a view with no border, no highlight, and no visible
indication that it holds focus, which reads to the user as focus having vanished.

## Goals / Non-Goals

**Goals:**
- Make Down-from-header deterministically land keyboard focus on a real, interactive, visible
  descendant of the tab's content - never on an ambiguous intermediate container.
- Fix this once, in the class that owns the tab-navigation mechanism, rather than adjusting
  `CanFocus` flags across every current and future tab content view.

**Non-Goals:**
- Restoring whatever previously had focus within the content (the `RestoreFocus()` behavior this
  code already explicitly declines to reimplement). This change only fixes *which* deterministic
  target is chosen, not making it session-remembered.
- Changing what happens when a key is left unhandled at the edges of a tab's content (e.g. Down at
  the bottom of a single-item list still being absorbed rather than doing something else) - that's
  the class's existing, deliberate leak-prevention behavior (see its own comment) and is unrelated
  to which view gets focused on entry.

## Decisions

**Explicit first-focusable-descendant search, not a `CanFocus` change on content views.**
`FocusOwnContent()` walks `Value`'s `SubViews` depth-first, returning the first `Visible && Enabled
&& CanFocus` view found (preferring a deeper match over an ancestor's own `CanFocus`), and calls
`SetFocus()` on *that* view instead of on `Value`. Considered instead setting
`CanFocus = false` on `ListEditorView<T>` (and `PublishView`, and its band containers): rejected
because it would need to be repeated for every current and future tab content view (including the
still-unbuilt Streams/Consumers/KV/OBJ tabs), whereas the search lives once in the class that
already owns this responsibility. It also risks removing `CanFocus` needed for some other reason
(e.g. `PublishView`'s bands might rely on being a focus target for their own scheme/highlight
logic) that isn't obviously safe to touch without auditing every content view individually.

**Depth-first, prefer-the-deepest-match traversal.** For a subview, the search first recurses into
*its* children before considering the subview's own `CanFocus`, so `PublishView`'s
`_subjectField` (nested two levels down inside `subjectBand`) is found and focused directly,
rather than stopping at `subjectBand` itself. This matches the existing pattern of outer bands
being `CanFocus = true` purely as visual/grouping containers, with the real interactive control
nested inside.

**Fall back to `Value` itself if no focusable descendant exists.** If a future tab's content has no
focusable children at all, focusing `Value` (if it's `CanFocus`) is still strictly better than
focusing nothing.

## Risks / Trade-offs

- [A future tab content view intentionally wants its own root to be the Down-entry focus target,
  not a descendant] → Not true of any tab today (all current content roots exist only to host
  their own interactive children); if it becomes true, the search already falls back to focusing
  the root when it has no focusable descendants, and can be revisited then.
- [The search walks the full `SubViews` tree on every Down-from-header] → Negligible: tab content
  trees are small (a handful of views), and this only runs on an explicit header-to-content
  transition, not per keystroke within content.
