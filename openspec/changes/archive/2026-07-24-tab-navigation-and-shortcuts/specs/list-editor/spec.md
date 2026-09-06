## ADDED Requirements

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
