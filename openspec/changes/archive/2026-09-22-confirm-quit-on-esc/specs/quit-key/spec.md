## ADDED Requirements

### Requirement: Top-level Esc does not quit
When Esc reaches the main window unhandled (no focused view, dialog, or drill-down level consumed
it as "back"/"cancel"), the app SHALL NOT quit. The key SHALL be consumed with no visible effect:
no dialog opens and focus stays where it was.

#### Scenario: Esc at the top level is a no-op
- **WHEN** the main window is active with no dialog open, focus is on a view that does not
  itself handle Esc, and the user presses Esc (once or repeatedly)
- **THEN** nothing happens and the app keeps running

#### Scenario: Esc used as "back" is unchanged
- **WHEN** focus is on a drill-down level that has an upper level (e.g. a stream's consumers),
  or on a filter box with text in it, and the user presses Esc
- **THEN** that view handles Esc as it does today (ascend, clear)

#### Scenario: Esc inside another dialog is unchanged
- **WHEN** any dialog (Publish, About, a create/edit dialog, a message detail dialog, ...) is
  open and the user presses Esc
- **THEN** only that dialog closes and the app keeps running

### Requirement: Alt+Q is the way to quit
The explicit Quit shortcut (Alt+Q) SHALL keep quitting immediately, with no confirmation.

#### Scenario: Alt+Q exits directly
- **WHEN** the main window is active with no dialog open and the user presses Alt+Q
- **THEN** the app exits
