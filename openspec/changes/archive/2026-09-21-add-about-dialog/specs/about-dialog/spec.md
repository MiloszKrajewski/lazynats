## ADDED Requirements

### Requirement: Global F10 shortcut opens the About dialog
The application SHALL bind `F10` as a global shortcut, available from anywhere in the app (any
management tab, the live feed, or the status bar) except while another modal dialog is already
open, that opens a modal About dialog.

#### Scenario: F10 pressed from a management tab
- **WHEN** the user presses `F10` while any management tab or the live feed has focus
- **THEN** the modal About dialog opens

#### Scenario: F10 pressed while another dialog is open
- **WHEN** the user presses `F10` while a different modal dialog (e.g. Publish, the shortcut
  picker) is already open
- **THEN** `F10` is not intercepted by the About shortcut and is handled (or ignored) by the
  already-open dialog instead, per normal modal key-routing

### Requirement: F10 shortcut is visible in the status bar
The status bar SHALL display an `F10`/"About" hint, following the same always-visible
presentation as the app's other global shortcuts (Quit, Publish, tab switches).

#### Scenario: About hint appears without further action
- **WHEN** the application is running and no modal dialog is open
- **THEN** the status bar shows an `F10` hint labeled "About" alongside the other global
  shortcut hints

### Requirement: About dialog is fixed-width with scrollable content
The About dialog SHALL render at a fixed width regardless of terminal size, and SHALL present its
text, framed in the same `EditFrame` styling used by every other read-only text block in the app,
in a scrollable, read-only view when the text is taller than the dialog's visible area.

#### Scenario: Dialog width does not change with terminal size
- **WHEN** the About dialog is opened in terminals of different widths
- **THEN** the dialog renders at the same fixed width in each case

#### Scenario: Content taller than the visible area scrolls
- **WHEN** the embedded About text has more lines than the dialog's text area can show at once
- **THEN** the extra lines are reachable by scrolling (e.g. arrow/page keys) rather than being
  clipped or expanding the dialog beyond its bounds

#### Scenario: Content fits without scrolling
- **WHEN** the embedded About text has fewer lines than the dialog's text area can show
- **THEN** no scrollbar or scroll affordance is shown

### Requirement: About dialog closes on Esc
The About dialog SHALL be read-only (no editable fields) and SHALL close when the user presses
`Esc`.

#### Scenario: Esc closes the dialog
- **WHEN** the About dialog is open and the user presses `Esc`
- **THEN** the dialog closes and focus returns to whatever had focus before it opened

### Requirement: About text is loaded from an embedded resource
The About dialog's displayed text SHALL be loaded from a text file embedded as a resource in the
application assembly, not hardcoded as a C# string literal.

#### Scenario: Text originates from the embedded resource
- **WHEN** the About dialog is opened
- **THEN** the text it displays is read from the embedded resource text file bundled in the
  assembly

### Requirement: About text supports literal token replacement
The About dialog SHALL support replacing literal `{token}` placeholders in the embedded text with
runtime values via plain string substitution, before displaying it.

#### Scenario: {version} token is replaced with the running assembly's version
- **WHEN** the embedded About text contains the literal token `{version}`
- **THEN** the displayed text has that token replaced with the application's version, as reported
  by the running assembly's version/product metadata

#### Scenario: {product}/{author}/{description} tokens are replaced from assembly metadata
- **WHEN** the embedded About text contains the literal tokens `{product}`, `{author}`, or
  `{description}`
- **THEN** each is replaced with the running assembly's `AssemblyProductAttribute`,
  `AssemblyCompanyAttribute`, or `AssemblyDescriptionAttribute` value respectively - all generated
  at build time from `PublicAssembly.props`' `Product`/`Company`/`Description` MSBuild properties,
  not hand-maintained constants

#### Scenario: Unrecognized tokens are left as-is
- **WHEN** the embedded About text contains a `{...}`-shaped token that the dialog does not
  recognize
- **THEN** that token is displayed verbatim, unmodified
