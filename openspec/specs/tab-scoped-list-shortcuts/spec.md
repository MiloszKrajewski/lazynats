# tab-scoped-list-shortcuts Specification

## Purpose
TBD - created by syncing change tab-scoped-list-shortcuts. Update Purpose after archive.

## Requirements

### Requirement: Tab-Owned Dispatch
A management tab that hosts one or more drillable-list/list-editor-based lists SHALL own dispatch
of Ctrl+N, Ctrl+D, Ctrl+R, Ctrl+E, and (where at least one hosted list supports it) Ctrl+F — by
querying its currently active list's advertised operations for a match, not by hardcoding which of
these keys it forwards. None of the tab's hosted list views SHALL bind these keys themselves.

#### Scenario: A list no longer binds the shared keys itself
- **WHEN** a drillable-list or list-editor-based list is hosted within a management tab
- **THEN** pressing Ctrl+N, Ctrl+D, Ctrl+R, or Ctrl+E while only that list view (and none of its
  ancestors up to and including the tab) has a matching key binding has no effect

### Requirement: Dispatch Targets Only the Active List
For a tab with more than one list level (e.g. a top-level list and a drilled-into list, toggled by
descend/ascend), pressing one of the tab-owned keys SHALL invoke the corresponding operation only
on the tab's currently active/visible list. The level that is not currently visible SHALL NOT
respond, even though it remains constructed and alive.

#### Scenario: Only the visible level responds
- **WHEN** a tab has descended into its second list level (the first level's list is no longer
  visible) and the user presses Ctrl+N
- **THEN** the create operation is invoked on the second-level list only; the first-level list's
  create operation (if any) is not invoked

#### Scenario: Ascending changes which level responds
- **WHEN** the user ascends back to a tab's first list level and then presses Ctrl+D
- **THEN** the delete operation is invoked on the first-level list, not the second-level list

### Requirement: Dispatch Only When the Active List Supports the Operation
The tab SHALL invoke a tab-owned key's operation only if the currently active list actually
supports that operation (per its opt-in wiring — see `drillable-list`'s Shared Create/Delete/Edit
Wiring and `list-editor`'s create/edit/delete requirements). If the active list does not support
the operation, the key press SHALL be treated as unhandled by the tab, not silently swallowed.

#### Scenario: Pressing an unsupported operation's key falls through
- **WHEN** the currently active list has not activated the shared wiring for an operation (e.g. it
  has no delete affordance) and the user presses that operation's key
- **THEN** the tab does not invoke anything for that key and does not report the key as handled

### Requirement: Advertised Shortcuts Match What Dispatch Would Do
A tab that owns these key bindings SHALL implement the shortcut-source contract (per
`keyboard-shortcut-discovery`), advertising exactly the tab-owned operations its *currently active*
list supports right now — using the same check used to decide whether dispatch invokes an
operation — rather than a static union of everything any level of the tab could ever support. This
check SHALL happen before a shortcut is advertised, not only before it is invoked.

#### Scenario: Advertised shortcuts change when the active list changes
- **WHEN** a tab descends from a level that supports Create/Delete/Edit to a level that supports
  only Create/Delete
- **THEN** the tab's advertised shortcuts no longer include an Edit hint, without the user having
  pressed Ctrl+E

#### Scenario: A shortcut is never advertised for an unsupported operation
- **WHEN** the currently active list does not support a given operation
- **THEN** no hint for that operation's key is included among the tab's advertised shortcuts,
  regardless of whether some other level of the same tab supports it

#### Scenario: A shortcut is always advertised when dispatch would actually invoke it
- **WHEN** the currently active list supports a given operation
- **THEN** a hint for that operation's key is included among the tab's advertised shortcuts

### Requirement: List-Local Operations Stay List-Owned
Ascend (Esc/Backspace) and quick-search (`/`) SHALL be excluded from tab-owned dispatch. They
SHALL remain bound directly on the list itself (or its attached search field), and SHALL be
reachable only while that list or its search field holds keyboard focus, unaffected by this
capability.

#### Scenario: Esc while focus is elsewhere in the tab does not ascend
- **WHEN** keyboard focus is on a view within the tab other than the active list or its attached
  search field, and the user presses Esc
- **THEN** the tab does not invoke ascend on the active list
