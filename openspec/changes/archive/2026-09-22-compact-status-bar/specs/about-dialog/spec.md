## RENAMED Requirements

- FROM: `### Requirement: Global F10 shortcut opens the About dialog`
- TO: `### Requirement: Global Alt-A shortcut opens the About dialog`

- FROM: `### Requirement: F10 shortcut is visible in the status bar`
- TO: `### Requirement: Alt-A shortcut is visible in the status bar`

## MODIFIED Requirements

### Requirement: Global Alt-A shortcut opens the About dialog
The application SHALL bind `Alt-A` as a global shortcut, available from anywhere in the app (any
management tab, the live feed, or the status bar) except while another modal dialog is already
open, that opens a modal About dialog. `F10` SHALL NOT open the About dialog.

#### Scenario: Alt-A pressed from a management tab
- **WHEN** the user presses `Alt-A` while any management tab or the live feed has focus
- **THEN** the modal About dialog opens

#### Scenario: Alt-A pressed while another dialog is open
- **WHEN** the user presses `Alt-A` while a different modal dialog (e.g. Publish, the shortcut
  picker) is already open
- **THEN** `Alt-A` is not intercepted by the About shortcut and is handled (or ignored) by the
  already-open dialog instead, per normal modal key-routing

#### Scenario: F10 no longer opens About
- **WHEN** the user presses `F10` while any management tab or the live feed has focus
- **THEN** the About dialog does not open

### Requirement: Alt-A shortcut is visible in the status bar
The status bar SHALL display an `Alt-A`/"About" hint, following the same always-visible
presentation as the app's other global shortcuts (Quit, Publish, Jump).

#### Scenario: About hint appears without further action
- **WHEN** the application is running and no modal dialog is open
- **THEN** the status bar shows an `Alt-A` hint labeled "About" alongside the other global
  shortcut hints
