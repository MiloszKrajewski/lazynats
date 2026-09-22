## ADDED Requirements

### Requirement: Alt+Digit Shortcuts Share One Leading Status Bar Hint
The status bar SHALL advertise all of the Alt+digit focus shortcuts (the management tabs'
Alt+1..5 and the live feed's Alt+0) with one display-only hint, keyed `Alt-#` and labeled
"Jump", placed as the first entry of the status bar. It SHALL NOT show a separate hint for each
Alt+digit shortcut. Leaving these hints out SHALL NOT affect the shortcuts themselves, which stay
bound and work as before. Which digit goes to which target is shown by the `N:` prefix in each
tab's and the live feed's title.

#### Scenario: One Jump hint leads the status bar
- **WHEN** the application is running and no modal dialog is open
- **THEN** the first entry in the status bar is an `Alt-#` hint labeled "Jump"

#### Scenario: No per-digit hints are shown
- **WHEN** the application is running and no modal dialog is open
- **THEN** the status bar shows no hint keyed `Alt-1` through `Alt-5` or `Alt-0`

#### Scenario: Alt+digit shortcuts keep working without their own hints
- **WHEN** the user presses Alt+3 while focus is anywhere in the application
- **THEN** the third management tab becomes the selected tab, just as it did when the status bar
  had a separate hint for it

#### Scenario: The Jump hint does not act as a key
- **WHEN** the user presses the literal `#` key (with or without Alt)
- **THEN** no tab switch or focus change happens because of the Jump hint, since it is display-only
