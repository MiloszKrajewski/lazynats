## MODIFIED Requirements

### Requirement: Key Bindings Work Regardless of Focused Child
When the list editor is used standalone — not hosted within a management tab (e.g.
`HeaderEditorView` inside the `PublishDialog` modal) — its Ctrl+D, Ctrl+E, and Ctrl+N key bindings
SHALL take effect regardless of which child view currently holds keyboard focus, as long as focus
is somewhere within the list editor itself.

When the list editor is hosted within a management tab, it SHALL NOT bind Ctrl+D, Ctrl+E, or
Ctrl+N itself; instead it exposes create/edit/delete operations for its owning tab to dispatch to,
per `tab-scoped-list-shortcuts`. In that case the effective key bindings SHALL take effect
regardless of which view holds keyboard focus anywhere within the owning tab — not only within the
list editor's own bounds.

#### Scenario: Ctrl+D deletes while the list holds focus (standalone usage)
- **WHEN** a standalone list editor holds keyboard focus, an item is selected, and the user
  presses Ctrl+D
- **THEN** the selected item is removed from the item collection

#### Scenario: Tab-hosted list editor responds regardless of focus elsewhere in the tab
- **WHEN** a list editor is hosted within a management tab, keyboard focus is on a different view
  within that same tab (not the list editor itself), an item is selected in the list editor, and
  the user presses Ctrl+D
- **THEN** the selected item is removed from the item collection, via the owning tab's dispatch
