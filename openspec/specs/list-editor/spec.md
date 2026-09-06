# list-editor Specification

## Purpose
TBD - created by archiving change add-list-editor-shortcuts. Update Purpose after archive.
## Requirements
### Requirement: List-Only Layout
The list editor SHALL present a selectable list of items and no other permanently visible editable
control, where the list displays each item's text representation as produced by an injected
presenter.

#### Scenario: Only the list is present
- **WHEN** the list editor is displayed with existing items
- **THEN** the list shows each item's presenter-formatted text and no other input control is
  present within the list editor

### Requirement: Create via Modal Callback
The list editor SHALL, when the user presses Ctrl+N, invoke an overridable create callback that
reports success as a boolean and hands back the new value as an output parameter; if the callback
reports success, the returned value SHALL be committed via an overridable add operation (whose
default behavior appends it to the item collection), and if it reports failure (e.g. the user
cancelled), the item collection SHALL remain unchanged.

#### Scenario: Ctrl+N invokes the create callback and commits on success
- **WHEN** the user presses Ctrl+N and the create callback reports success with a value
- **THEN** that value is passed to the add operation, whose default behavior appends it to the item
  collection

#### Scenario: Cancelling create leaves the item collection unchanged
- **WHEN** the user presses Ctrl+N and the create callback reports failure (e.g. the user cancelled
  a modal dialog)
- **THEN** the item collection is unchanged

### Requirement: Edit via Modal Callback
The list editor SHALL, when the user selects an existing item and presses Ctrl+E, invoke an
overridable edit callback with that item's current value, which reports success as a boolean and
hands back the edited value as an output parameter; if the callback reports success, the returned
value SHALL be committed via an overridable replace operation (whose default behavior replaces the
item at that item's original position in the item collection), and if it reports failure (e.g. the
user cancelled), the item collection SHALL remain unchanged.

#### Scenario: Ctrl+E invokes the edit callback with the selected item's value
- **WHEN** the user selects an existing item and presses Ctrl+E
- **THEN** the edit callback is invoked with that item's current value

#### Scenario: Successful edit replaces the item in place
- **WHEN** the user has pressed Ctrl+E on a selected item and the edit callback reports success
  with a new value
- **THEN** that value is passed to the replace operation, whose default behavior replaces the item
  collection's entry at that item's original position, and no new item is appended

#### Scenario: Cancelling edit leaves the item collection unchanged
- **WHEN** the user has pressed Ctrl+E on a selected item and the edit callback reports failure
  (e.g. the user cancelled a modal dialog)
- **THEN** the item collection is unchanged

### Requirement: Delete Removes the Selected Item
The list editor SHALL allow the user to remove the selected item from the item collection via
Ctrl+D.

#### Scenario: Ctrl+D removes the selected item
- **WHEN** the user selects an existing item in the list and presses Ctrl+D
- **THEN** that item is removed from the item collection

### Requirement: Key Bindings Work Regardless of Focused Child
The list editor's Ctrl+D, Ctrl+E, and Ctrl+N key bindings SHALL take effect regardless of which
child view currently holds keyboard focus, as long as focus is somewhere within the list editor.

#### Scenario: Ctrl+D deletes while the list holds focus
- **WHEN** the list holds keyboard focus, an item is selected, and the user presses Ctrl+D
- **THEN** the selected item is removed from the item collection

### Requirement: Presenter-Driven Conversion
The list editor SHALL delegate formatting an item's value into its list-row text representation to
an injected presenter, supporting any item type without requiring a subclass of the list editor
solely to change that formatting.

#### Scenario: Two list editors with different item types share the same editor type
- **WHEN** one list editor is constructed with a presenter for one item type and another is
  constructed with a presenter for a different item type
- **THEN** both behave identically with respect to list-row rendering and create/edit/delete
  mechanics, differing only in how items are formatted for display and in what their create/edit
  callbacks do

### Requirement: Overridable Core Operations
The list editor's create and edit callbacks SHALL be abstract, requiring every subclass to define
how New and Edit obtain a value (e.g. by running a modal dialog appropriate to the item type). The
add, replace, and delete operations that commit those values SHALL be independently overridable
(each with a default that mutates the item collection directly), so a derived component whose true
source of truth lives elsewhere (e.g. redirecting into a registry rather than the item collection)
can commit there instead, without needing to change how items are formatted for display or how
their values are obtained.

#### Scenario: A derived component defines its own create and edit behavior
- **WHEN** a derived list editor implements its create and edit callbacks to run a modal specific
  to its item type
- **THEN** pressing Ctrl+N or Ctrl+E invokes that modal, while list-row formatting and the add/
  replace/delete operations remain governed independently of it

#### Scenario: A derived component redirects commits elsewhere
- **WHEN** a derived list editor overrides its add and replace operations to act on something other
  than the item collection (e.g. a backing registry) and that other source of truth updates the
  item collection asynchronously on its own
- **THEN** the list editor does not also mutate the item collection directly for that create/edit,
  avoiding a duplicate entry

### Requirement: Selection Recovers When the Item Collection Changes
The list editor SHALL ensure its list has a valid selected item whenever its item collection is
non-empty. If the underlying list view's selection is absent or refers to an index no longer
present after the item collection changes, the list editor SHALL select the first item.

#### Scenario: The first item added to an empty list becomes selected
- **WHEN** the item collection transitions from empty to containing one or more items and the list
  view has no selection
- **THEN** the first item becomes selected

#### Scenario: A collection rebuild that discards the prior selection re-selects the first item
- **WHEN** the item collection is replaced wholesale (e.g. cleared and repopulated by a subclass
  whose true source of truth lives elsewhere) and the list view's selection becomes invalid as a
  result
- **THEN** the first item in the rebuilt collection becomes selected, provided the collection is
  non-empty

#### Scenario: An existing valid selection is left unchanged
- **WHEN** the item collection changes but the list view's current selection still refers to a
  valid item
- **THEN** the selection is not altered

### Requirement: Empty-State Hint
The list editor SHALL, while its item collection is empty, display a dim, non-interactive hint
line in place of the (otherwise blank) list content. The hint SHALL be hidden as soon as the item
collection contains at least one item, and SHALL reappear if the collection becomes empty again.

#### Scenario: Empty collection shows the hint
- **WHEN** the list editor is displayed and its item collection is empty
- **THEN** a hint line is shown in place of the list content, and no other list content is visible

#### Scenario: Adding the first item hides the hint
- **WHEN** the item collection transitions from empty to containing one item (e.g. via a
  successful Ctrl+N create)
- **THEN** the hint is hidden and the list shows that item

#### Scenario: Removing the last item shows the hint again
- **WHEN** the item collection transitions from containing items to empty (e.g. via Ctrl+D on the
  last remaining item)
- **THEN** the hint is shown again in place of the now-empty list content

#### Scenario: The hint is not part of list navigation or selection
- **WHEN** the item collection is empty and the hint is displayed
- **THEN** the hint cannot be selected, and Ctrl+N/E/D behave exactly as they do for any other
  empty collection (Ctrl+N invokes the create callback as usual; Ctrl+E and Ctrl+D are no-ops
  because there is no selected item)

### Requirement: Per-Subclass Hint Text
The list editor SHALL obtain its empty-state hint text from an overridable source, so each
subclass can supply wording appropriate to its item type; subclasses that do not override it SHALL
still display a generic, non-empty hint rather than no hint at all.

#### Scenario: A subclass overrides the hint text
- **WHEN** a derived list editor overrides its hint-text source with a message specific to its item
  type
- **THEN** the empty-state hint displays that message

#### Scenario: A subclass does not override the hint text
- **WHEN** a derived list editor does not override its hint-text source
- **THEN** the empty-state hint displays a generic, non-empty default message

### Requirement: Configurable Background
The list editor SHALL expose an explicit background color, independent of any implicitly
inherited scheme, so it can be visually paired consistently with other edit controls (e.g. when
wrapped in a padded `EditFrame`).

#### Scenario: Setting a background applies it to the list's fill
- **WHEN** a background color is set on the list editor
- **THEN** the list's fill is rendered using that color rather than an inherited scheme color

#### Scenario: Background left unset falls back to inherited behavior
- **WHEN** no explicit background is set on the list editor
- **THEN** the list editor renders using its previously inherited scheme background, unchanged
  from prior behavior
