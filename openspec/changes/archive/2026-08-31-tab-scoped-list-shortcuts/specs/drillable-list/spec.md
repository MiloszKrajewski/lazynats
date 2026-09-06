## MODIFIED Requirements

### Requirement: Manual Refresh via Ctrl+R
A drillable list SHALL expose a refresh affordance — raising a refresh-requested event that the
owning component (which performs the actual fetch) subscribes to — without itself binding Ctrl+R
as a `KeyBindings` entry. When hosted within a management tab, the owning tab binds Ctrl+R and
invokes this affordance only while this list is the tab's currently active list, per
`tab-scoped-list-shortcuts`. The list SHALL NOT refresh itself on a timer.

#### Scenario: Ctrl+R raises a refresh request
- **WHEN** the user presses Ctrl+R while a drillable list is the currently active list within its
  owning tab
- **THEN** the list's refresh-requested event is raised and the list does not itself alter its
  contents

#### Scenario: The list does not change on its own
- **WHEN** time passes without the user pressing Ctrl+R and without the owning component calling
  `ReplaceItems`
- **THEN** the list's displayed contents are unchanged

### Requirement: Subclass-Defined Navigation Commands
A drillable list's base SHALL bind no key on its own initiative for the shared shapes forwarded by
an owning tab (Refresh, Create, Delete, Edit, and, where a subclass adds it, a filter operation) —
see `tab-scoped-list-shortcuts`. The base MAY supply shared, opt-in implementations of common
navigation shapes (descend, ascend, create/delete/edit) that a subclass activates explicitly from
its own constructor, each still exposing whether it is enabled and an invocable action for the
owning tab to dispatch to; a subclass MAY also define whatever additional navigation or
level-specific commands it needs beyond those shared shapes, without requiring changes to the base
or to any other subclass. Regardless of whether a shape comes from a shared opt-in implementation
or a subclass's own code, one subclass's activated bindings SHALL have no effect on another
subclass's behavior or on the base's default (no-key-bound) shape.

#### Scenario: One subclass's extra binding does not affect another
- **WHEN** one drillable list subclass binds Enter to a descend action and a different subclass
  binds Esc/Backspace to an ascend action
- **THEN** each subclass responds only to the bindings it defined itself, and neither affects the
  other's behavior or the base class

#### Scenario: Opting into a shared navigation shape does not affect a sibling that didn't opt in
- **WHEN** one drillable list subclass activates a shared opt-in navigation shape (descend,
  ascend, or create/delete) from its constructor, and a different subclass does not activate that
  shape
- **THEN** only the subclass that activated it exposes the corresponding operation to its owning
  tab, and the non-activating subclass's behavior and the base's default shape are unaffected

### Requirement: Shared Create Wiring
A drillable list SHALL offer a shared, opt-in implementation of a "create" affordance: when
activated by a subclass, the list exposes a create operation — raising a create-requested event
when invoked, with a "New" label — among the operations its owning tab can dispatch Ctrl+N to per
`tab-scoped-list-shortcuts`, without the subclass needing to bind the key, the event, or the
operation's own advertisement itself. This wiring SHALL be activatable independently of the shared
delete wiring — a subclass MAY activate create without delete, delete without create, both, or
neither.

#### Scenario: Activating create wiring raises the event on Ctrl+N
- **WHEN** a subclass activates the shared create wiring and the user presses Ctrl+N while this
  list is the active list within its owning tab
- **THEN** the create-requested event is raised

#### Scenario: Activating create wiring surfaces a New operation
- **WHEN** a subclass activates the shared create wiring
- **THEN** a "New" operation is present among the operations this list exposes to its owning tab

#### Scenario: Create wiring without delete wiring has no delete affordance
- **WHEN** a subclass activates the shared create wiring but not the shared delete wiring
- **THEN** Ctrl+D has no effect while this list is the active list, and no "Delete" operation is
  present among the operations this list exposes to its owning tab

### Requirement: Shared Delete Wiring
A drillable list SHALL offer a shared, opt-in implementation of a "delete" affordance: when
activated by a subclass, the list exposes a delete operation — raising a delete-requested event
when invoked, with a "Delete" label — among the operations its owning tab can dispatch Ctrl+D to
per `tab-scoped-list-shortcuts`, without the subclass needing to bind the key, the event, or the
operation's own advertisement itself. This wiring SHALL be activatable independently of the shared
create wiring — a subclass MAY activate delete without create, create without delete, both, or
neither.

#### Scenario: Activating delete wiring raises the event on Ctrl+D
- **WHEN** a subclass activates the shared delete wiring and the user presses Ctrl+D while this
  list is the active list within its owning tab
- **THEN** the delete-requested event is raised

#### Scenario: Activating delete wiring surfaces a Delete operation
- **WHEN** a subclass activates the shared delete wiring
- **THEN** a "Delete" operation is present among the operations this list exposes to its owning
  tab

#### Scenario: Delete wiring without create wiring has no create affordance
- **WHEN** a subclass activates the shared delete wiring but not the shared create wiring
- **THEN** Ctrl+N has no effect while this list is the active list, and no "New" operation is
  present among the operations this list exposes to its owning tab

#### Scenario: A subclass that activates neither wiring has neither affordance
- **WHEN** a subclass activates neither the shared create wiring nor the shared delete wiring
- **THEN** Ctrl+N and Ctrl+D have no effect while this list is the active list, and no "New"/
  "Delete" operations are present among the operations this list exposes to its owning tab

### Requirement: Shared Edit Wiring
A drillable list SHALL offer a shared, opt-in implementation of an "edit" affordance: when
activated by a subclass, the list exposes an edit operation — raising an edit-requested event when
invoked, with an "Edit" label — among the operations its owning tab can dispatch Ctrl+E to per
`tab-scoped-list-shortcuts`, without the subclass needing to bind the key, the event, or the
operation's own advertisement itself. This wiring SHALL be activatable independently of the shared
create and delete wirings — a subclass MAY activate edit alone, alongside either or both of
create/delete, or none at all.

#### Scenario: Activating edit wiring raises the event on Ctrl+E
- **WHEN** a subclass activates the shared edit wiring and the user presses Ctrl+E while this list
  is the active list within its owning tab
- **THEN** the edit-requested event is raised

#### Scenario: Activating edit wiring surfaces an Edit operation
- **WHEN** a subclass activates the shared edit wiring
- **THEN** an "Edit" operation is present among the operations this list exposes to its owning tab

#### Scenario: Edit wiring is independent of create/delete wiring
- **WHEN** a subclass activates the shared edit wiring but activates neither the shared create nor
  the shared delete wiring
- **THEN** Ctrl+N and Ctrl+D have no effect while this list is the active list and no "New"/
  "Delete" operations are exposed, while Ctrl+E and its "Edit" operation behave normally
