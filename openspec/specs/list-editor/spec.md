# list-editor Specification

## Purpose
TBD - created by archiving change add-list-editor-shortcuts. Update Purpose after archive.
## Requirements
### Requirement: Text Input and List Layout
The list editor SHALL present a single-line editable text input above a selectable list of items,
where the text input is the only editable text control in the component and the list displays
each item's text representation as produced by an injected presenter.

#### Scenario: Input and list are independently visible
- **WHEN** the list editor is displayed with existing items
- **THEN** the text input shows its own current (possibly empty) content and the list shows each
  item's presenter-formatted text, independently of each other

### Requirement: Append on Commit
The list editor SHALL, when the user presses Enter in the text input while not editing an existing
item, parse the input's current text via the injected presenter and, if parsing succeeds, append
the resulting value to the item collection and clear the input.

#### Scenario: Enter appends a new item
- **WHEN** the user types text that the presenter can parse successfully into the input, without
  having loaded an existing item for editing, and presses Enter
- **THEN** the parsed value is appended to the item collection and the text input is cleared

### Requirement: Edit Loads an Item for In-Place Update
The list editor SHALL allow the user to load a selected item back into the text input for editing
via Ctrl+E, formatting the item's current value via the presenter and moving focus to the text
input; on a subsequent successful Enter, the item at that original position SHALL be replaced by
the newly parsed value rather than a new item being appended.

#### Scenario: Ctrl+E loads the selected item for editing
- **WHEN** the user selects an existing item in the list and presses Ctrl+E
- **THEN** the text input is populated with that item's presenter-formatted text and receives
  focus

#### Scenario: Enter commits an edit in place
- **WHEN** the user has loaded an item via Ctrl+E, changes the input's text to something the
  presenter can parse successfully, and presses Enter
- **THEN** the item collection's entry at that item's original position is replaced with the newly
  parsed value, and no new item is appended

### Requirement: Delete Removes the Selected Item
The list editor SHALL allow the user to remove the selected item from the item collection via
Ctrl+D.

#### Scenario: Ctrl+D removes the selected item
- **WHEN** the user selects an existing item in the list and presses Ctrl+D
- **THEN** that item is removed from the item collection

### Requirement: Clear Cancels Input and Any In-Progress Edit
The list editor SHALL allow the user to clear the text input and discard any in-progress edit via
Ctrl+N, refocusing the text input, without modifying the item collection.

#### Scenario: Ctrl+N clears the input and cancels an edit
- **WHEN** the user has loaded an item via Ctrl+E and then presses Ctrl+N before pressing Enter
- **THEN** the text input is cleared, the loaded item is no longer tracked as being edited, the
  text input receives focus, and the item collection is unchanged

### Requirement: Key Bindings Work Regardless of Focused Child
The list editor's Ctrl+D, Ctrl+E, and Ctrl+N key bindings SHALL take effect regardless of whether
the text input or the list currently holds keyboard focus, as long as focus is somewhere within
the list editor.

#### Scenario: Ctrl+D deletes while the text input has focus
- **WHEN** the text input has keyboard focus, an item is selected in the list, and the user
  presses Ctrl+D
- **THEN** the selected item is removed from the item collection

### Requirement: Live Validation Feedback
The list editor SHALL, on every change to the text input's content, attempt to parse the current
text via the injected presenter and visually flag the text input as invalid whenever parsing would
fail, without displaying any popup or blocking further typing.

#### Scenario: Invalid text is visually flagged as the user types
- **WHEN** the user types text into the text input that the presenter cannot parse successfully
- **THEN** the text input is shown in its invalid visual state and the user can continue typing

#### Scenario: Correcting the text clears the invalid state
- **WHEN** the text input is showing its invalid visual state and the user edits the text so the
  presenter can now parse it successfully
- **THEN** the text input returns to its normal visual state

### Requirement: Parse Error on Commit
The list editor SHALL, when Enter is pressed and the presenter fails to parse the current input
text, leave the item collection unchanged, leave the text input's content in place, and invoke an
overridable parse-error hook carrying the presenter-supplied error message.

#### Scenario: Enter with invalid text does not append or edit
- **WHEN** the user presses Enter while the text input contains text the presenter cannot parse
- **THEN** no item is appended or replaced in the item collection, and the parse-error hook is
  invoked with the presenter's error message for that input

### Requirement: Presenter-Driven Conversion
The list editor SHALL delegate all conversion between an item's value and its text representation
to an injected presenter, supporting any item type without requiring a subclass of the list editor
solely to change that conversion.

#### Scenario: Two list editors with different item types share the same editor type
- **WHEN** one list editor is constructed with a presenter for one item type and another is
  constructed with a presenter for a different item type
- **THEN** both behave identically with respect to append/edit/delete/clear mechanics, differing
  only in how items are parsed and formatted

### Requirement: Overridable Core Operations
The list editor's append, edit, delete, and clear-input operations SHALL be overridable
independently of the injected presenter, so that a derived component can change what an action
does without needing to change how items are parsed or formatted.

#### Scenario: A derived component overrides the append operation
- **WHEN** a derived list editor overrides its append operation to perform behavior other than
  adding the parsed value to the item collection
- **THEN** pressing Enter with valid, non-editing input invokes the overridden behavior instead of
  the default append, while edit, delete, and clear-input behavior remain governed by the base
  implementation unless also overridden

### Requirement: Up at Top of List Focuses the Text Input
The list editor SHALL, when the list holds keyboard focus and the user presses Up while the first
item is selected (or the list is empty), move keyboard focus to the text input above the list,
rather than leaving the key unhandled.

#### Scenario: Up at the first item moves focus to the input
- **WHEN** the list holds keyboard focus, its first item is selected, and the user presses Up
- **THEN** keyboard focus moves to the text input above the list

#### Scenario: Up in an empty list moves focus to the input
- **WHEN** the list holds keyboard focus, contains no items, and the user presses Up
- **THEN** keyboard focus moves to the text input above the list

#### Scenario: Up while the text input already has focus is left unhandled by the list editor
- **WHEN** the text input holds keyboard focus and the user presses Up
- **THEN** the list editor does not act on the key or move focus itself, leaving it unhandled so an
  ancestor view outside the list editor may act on it instead

