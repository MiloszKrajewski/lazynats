## MODIFIED Requirements

### Requirement: Key Bindings Work Regardless of Focused Child
When the list editor is used standalone — not hosted within a management tab (e.g.
`HeaderEditorView` inside the `PublishDialog` modal) — its D, E, and N key bindings SHALL take
effect regardless of which child view currently holds keyboard focus, as long as focus is
somewhere within the list editor itself. Standalone usage SHALL use the same bare D, E, and N keys
as tab-hosted usage — no Ctrl modifier — since a standalone list editor's own focus subtree has no
other control competing for those letters (item creation/editing happens via a separate modal
dialog, not in place).

When the list editor is hosted within a management tab, it SHALL NOT bind D, E, or N itself;
instead it exposes create/edit/delete operations for its owning tab to dispatch to, per
`tab-scoped-list-shortcuts`, which dispatches them as bare D, E, and N. In that case the effective
key bindings SHALL take effect regardless of which view holds keyboard focus anywhere within the
owning tab — not only within the list editor's own bounds.

In both usage modes, a Filter key binding (bare F) SHALL be registered if and only if the subclass
has opted into the shared filter wiring (per "Shared Filter Wiring"); a subclass that does not opt
in SHALL have no Filter key binding at all, standalone or tab-hosted.

#### Scenario: Bare D deletes while the list holds focus (standalone usage)
- **WHEN** a standalone list editor holds keyboard focus, an item is selected, and the user
  presses D
- **THEN** the selected item is removed from the item collection

#### Scenario: Tab-hosted list editor responds regardless of focus elsewhere in the tab
- **WHEN** a list editor is hosted within a management tab, keyboard focus is on a different view
  within that same tab (not the list editor itself), an item is selected in the list editor, and
  the user presses D
- **THEN** the selected item is removed from the item collection, via the owning tab's dispatch

#### Scenario: A standalone list editor that has not opted into filtering has no Filter binding
- **WHEN** a standalone list editor's subclass has not called the shared filter wiring's opt-in
  (e.g. `HeaderEditorView`)
- **THEN** pressing F while it holds focus has no effect, and no "Filter" hint is advertised for it
