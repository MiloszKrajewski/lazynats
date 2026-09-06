## MODIFIED Requirements

### Requirement: Tab-Owned Dispatch
A management tab that hosts one or more drillable-list/list-editor-based lists SHALL own dispatch
of N, D, R, E, and (where at least one hosted list supports it) F — by querying its currently
active list's advertised operations for a match, not by hardcoding which of these keys it forwards.
None of the tab's hosted list views SHALL bind these keys themselves.

#### Scenario: A list no longer binds the shared keys itself
- **WHEN** a drillable-list or list-editor-based list is hosted within a management tab
- **THEN** pressing N, D, R, or E while only that list view (and none of its ancestors up to and
  including the tab) has a matching key binding has no effect

### Requirement: Dispatch Targets Only the Active List
For a tab with more than one list level (e.g. a top-level list and a drilled-into list, toggled by
descend/ascend), pressing one of the tab-owned keys SHALL invoke the corresponding operation only
on the tab's currently active/visible list. The level that is not currently visible SHALL NOT
respond, even though it remains constructed and alive.

#### Scenario: Only the visible level responds
- **WHEN** a tab has descended into its second list level (the first level's list is no longer
  visible) and the user presses N
- **THEN** the create operation is invoked on the second-level list only; the first-level list's
  create operation (if any) is not invoked

#### Scenario: Ascending changes which level responds
- **WHEN** the user ascends back to a tab's first list level and then presses D
- **THEN** the delete operation is invoked on the first-level list, not the second-level list

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
  pressed E

#### Scenario: A shortcut is never advertised for an unsupported operation
- **WHEN** the currently active list does not support a given operation
- **THEN** no hint for that operation's key is included among the tab's advertised shortcuts,
  regardless of whether some other level of the same tab supports it

#### Scenario: A shortcut is always advertised when dispatch would actually invoke it
- **WHEN** the currently active list supports a given operation
- **THEN** a hint for that operation's key is included among the tab's advertised shortcuts
