## ADDED Requirements

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

## MODIFIED Requirements

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

## REMOVED Requirements

### Requirement: Text Input and List Layout
**Reason**: The list editor no longer has a permanently visible text input; New and Edit are now
invoked via a modal callback instead of an inline field. Superseded by "List-Only Layout".
**Migration**: Subclasses that displayed or relied on the shared input field must instead implement
`TryCreate`/`TryEdit` to present their own modal.

### Requirement: Append on Commit
**Reason**: There is no inline input to commit via Enter; creation now goes through the
`TryCreate` modal callback. Superseded by "Create via Modal Callback".
**Migration**: Move append-time logic (e.g. parsing, side effects beyond the collection) into the
subclass's `TryCreate` implementation.

### Requirement: Edit Loads an Item for In-Place Update
**Reason**: There is no inline input to load a value into; editing now goes through the `TryEdit`
modal callback, which receives the item's current value directly as a parameter instead of via a
formatted string loaded into a shared field. Superseded by "Edit via Modal Callback".
**Migration**: Move edit-time logic into the subclass's `TryEdit` implementation, using the
`original` parameter instead of presenter-formatted input text.

### Requirement: Clear Cancels Input and Any In-Progress Edit
**Reason**: There is no shared input or in-progress-edit state at the list-editor level to clear or
cancel; a modal dialog's own Cancel action fills this role, scoped to that dialog.
**Migration**: None needed at the list-editor level; a subclass's modal is responsible for its own
cancel affordance (e.g. a Cancel button).

### Requirement: Live Validation Feedback
**Reason**: There is no inline input to validate live; validating what the user is entering is now
the responsibility of whatever modal a subclass's `TryCreate`/`TryEdit` presents.
**Migration**: Subclasses needing live validation feedback implement it within their own modal
dialog.

### Requirement: Parse Error on Commit
**Reason**: There is no free-text commit path at the list-editor level to fail to parse; a modal
either returns a valid value (success) or is cancelled (failure) — there is no third "invalid
commit attempt" state visible to the list editor.
**Migration**: Subclasses needing to surface a parse/validation error do so within their own modal
dialog before allowing it to report success.

### Requirement: Up at Top of List Focuses the Text Input
**Reason**: There is no text input within the list editor to move focus to; the list is the only
child, so there is nothing left within the component for Up at the top row to focus.
**Migration**: None needed; Up at the top of the list is simply left unhandled by the list editor,
same as any other key it doesn't bind.
