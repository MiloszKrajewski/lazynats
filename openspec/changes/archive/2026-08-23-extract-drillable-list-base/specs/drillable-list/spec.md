## ADDED Requirements

### Requirement: Presenter-Driven List Rendering
A drillable list SHALL delegate formatting an item's value into its list-row text representation
to an injected presenter, and SHALL delegate producing an item's identity (used to preserve
highlight across a refresh) to an overridable accessor, so a new item type can reuse the same
base without the base needing to know its shape.

#### Scenario: Two drillable lists with different item types share the same base
- **WHEN** one drillable list is constructed for one item type and another for a different item
  type, each with its own presenter and identity accessor
- **THEN** both behave identically with respect to list-row rendering, refresh, and empty-state
  handling, differing only in how items are formatted, identified, and in what navigation
  commands each additionally binds

### Requirement: Read-Only Presentation
A drillable list SHALL NOT itself provide any means to create, edit, or delete an item. Any
mutation affordance belongs to a different component.

#### Scenario: No mutation affordance is present
- **WHEN** a drillable list is displayed
- **THEN** no keybinding, button, or other control for creating, editing, or deleting an item is
  present on the base list itself

### Requirement: Manual Refresh via Ctrl+R
A drillable list SHALL allow the user to request a refresh via Ctrl+R, raising an event that the
owning component (which performs the actual fetch) subscribes to. The list SHALL NOT refresh
itself on a timer.

#### Scenario: Ctrl+R raises a refresh request
- **WHEN** the user presses Ctrl+R while a drillable list holds focus
- **THEN** the list raises its refresh-requested event and does not itself alter its contents

#### Scenario: The list does not change on its own
- **WHEN** time passes without the user pressing Ctrl+R and without the owning component calling
  `ReplaceItems`
- **THEN** the list's displayed contents are unchanged

### Requirement: Identity-Preserving Replace
A drillable list SHALL expose an operation that replaces its entire item collection wholesale and
restores the highlight to the item whose identity (per the overridable identity accessor) matches
the previously-highlighted item, if that identity is still present in the new collection;
otherwise the first item in the new collection SHALL become highlighted, or no item if the new
collection is empty.

#### Scenario: Replace preserves the highlight when possible
- **WHEN** the item collection is replaced and an item with the same identity as the
  previously-highlighted item is present in the new collection
- **THEN** that item becomes highlighted

#### Scenario: Replace falls back to the first item
- **WHEN** the item collection is replaced and no item in the new collection shares the
  previously-highlighted item's identity
- **THEN** the first item in the new collection becomes highlighted, provided it is non-empty

#### Scenario: Replace with an empty collection clears the highlight
- **WHEN** the item collection is replaced with an empty collection
- **THEN** no item is highlighted

### Requirement: Empty-State Hint
A drillable list SHALL, while its item collection is empty, display a non-interactive hint line
in place of the (otherwise blank) list content, using text supplied by each subclass. The hint's
visual style SHALL reflect whether the list currently holds keyboard focus, matching the focused/
unfocused distinction used elsewhere in the app's list components.

#### Scenario: Empty collection shows the subclass's hint text
- **WHEN** a drillable list is displayed and its item collection is empty
- **THEN** a hint line supplied by the subclass is shown in place of the list content

#### Scenario: A non-empty collection hides the hint
- **WHEN** the item collection is non-empty
- **THEN** no hint line is shown and the list's items are visible instead

### Requirement: Configurable Background
A drillable list SHALL expose an explicit background color, independent of any implicitly
inherited scheme, applied consistently to both the list's fill and the empty-state hint overlay.

#### Scenario: Setting a background applies it to the list and the hint overlay
- **WHEN** a background color is set on a drillable list
- **THEN** both the list's fill and the empty-state hint (when shown) render using that color

### Requirement: Subclass-Defined Navigation Commands
A drillable list's base SHALL bind no key beyond Ctrl+R. Each subclass SHALL independently define
whatever additional navigation or level-specific commands it needs (e.g. Enter to descend, Esc/
Backspace to ascend), without requiring changes to the base or to any other subclass.

#### Scenario: One subclass's extra binding does not affect another
- **WHEN** one drillable list subclass binds Enter to a descend action and a different subclass
  binds Esc/Backspace to an ascend action
- **THEN** each subclass responds only to the bindings it defined itself, and neither affects the
  other's behavior or the base class
