## ADDED Requirements

### Requirement: Self-Contained Tab Content
Each management tab's content SHALL be implemented as a single component that owns its complete
internal layout — labels, `EditFrame` wrapping, and any sub-band composition — independent of
`MainWindow` or any other tab's content.

#### Scenario: Subscribe tab content is self-contained
- **WHEN** the Subscribe tab's content component is constructed
- **THEN** it produces its own labeled, framed list presentation without requiring any caller to
  assemble a label, `EditFrame`, or band around it

#### Scenario: Publish tab content is self-contained
- **WHEN** the Publish tab's content component is constructed
- **THEN** it produces its own Subject/Headers/Payload layout without requiring any caller to
  assemble that layout around it

### Requirement: MainWindow Performs Registration Only
`MainWindow` SHALL, for each management tab, only resolve that tab's dependencies, construct its
content component, and register the constructed component with `ManagementTabs`. `MainWindow`
SHALL NOT assemble any tab-specific layout (labels, `EditFrame` wrapping, band composition) of
its own on behalf of a tab's content.

#### Scenario: MainWindow adds tab content without further assembly
- **WHEN** `MainWindow` builds the management tabs
- **THEN** each tab's content is added to `ManagementTabs` as a single already-complete
  component, with `MainWindow` performing no label, frame, or band construction for that
  content itself

### Requirement: Tab Content Naming Convention
A component that is directly registered with `ManagementTabs` as a tab's content SHALL be named
with a `Tab` suffix (e.g. `SubscribeTab`, `PublishTab`). A component used only as a piece inside
such a tab's content (e.g. a list editor or header editor) SHALL NOT use the `Tab` suffix, so the
suffix unambiguously marks the outermost, directly-registered component.

#### Scenario: Tab-level components use the Tab suffix
- **WHEN** a new component is registered directly with `ManagementTabs` as a tab's content
- **THEN** its type name ends in `Tab`

#### Scenario: Inner components do not use the Tab suffix
- **WHEN** a component is used only as a sub-piece within a tab's content, not registered
  directly with `ManagementTabs`
- **THEN** its type name does not end in `Tab`
