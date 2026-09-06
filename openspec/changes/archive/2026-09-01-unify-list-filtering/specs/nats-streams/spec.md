## ADDED Requirements

### Requirement: Stream List Filter
The system SHALL allow the user, while at the stream level, to set a filter pattern via Ctrl+F
that narrows the currently-loaded stream list to names matching that pattern, using the same
`* ? >` filter-expression grammar as every other list's Ctrl+F filter (see
`list-filter-affordance`). Since the stream list is always fetched in full (see "Stream List"),
this filter narrows only what is displayed, never what is fetched, and persists across a Ctrl+R
refresh until cleared or explicitly changed. This is independent of the existing in-memory
quick-search (`/`); both may be active at once.

#### Scenario: Ctrl+F opens the filter dialog
- **WHEN** the user presses Ctrl+F while the stream-level list holds focus
- **THEN** a modal dialog opens with an editable expression field, seeded with the currently active
  filter pattern, or empty if no filter is active

#### Scenario: Confirming a valid pattern narrows the stream list
- **WHEN** the user enters a valid, non-empty pattern and confirms
- **THEN** only currently-loaded stream names matching that pattern remain shown

#### Scenario: An invalid pattern cannot be confirmed
- **WHEN** the entered pattern contains an empty token (a leading, trailing, or doubled `.`)
- **THEN** the dialog does not confirm on Enter

#### Scenario: Confirming an empty pattern clears the filter
- **WHEN** a filter is currently active, the user opens the dialog, clears the pattern field to
  empty, and confirms
- **THEN** the active filter is cleared and every currently-loaded stream is shown again

#### Scenario: The filter persists across a refresh
- **WHEN** a filter is active and the user presses Ctrl+R
- **THEN** the refreshed stream list is immediately narrowed by the still-active filter

### Requirement: Consumer List Filter
The system SHALL allow the user, while at the consumer level, to set a filter pattern via Ctrl+F
that narrows the currently-loaded consumer list (for the currently drilled-into stream) to names
matching that pattern, using the same `* ? >` filter-expression grammar as every other list's
Ctrl+F filter (see `list-filter-affordance`). Since the consumer list is always fetched in full
(see "Consumer List"), this filter narrows only what is displayed, never what is fetched. The
active filter SHALL reset when the user ascends back to the stream list, so a later descent into
any stream (including the same one) starts unfiltered — mirroring how the consumer list itself is
always re-fetched fresh on descend. This is independent of the existing in-memory quick-search
(`/`); both may be active at once.

#### Scenario: Ctrl+F opens the filter dialog
- **WHEN** the user presses Ctrl+F while the consumer-level list holds focus
- **THEN** a modal dialog opens with an editable expression field, seeded with the currently active
  filter pattern, or empty if no filter is active

#### Scenario: Confirming a valid pattern narrows the consumer list
- **WHEN** the user enters a valid, non-empty pattern and confirms
- **THEN** only currently-loaded consumer names matching that pattern remain shown

#### Scenario: The filter persists across a consumer-list refresh
- **WHEN** a filter is active and the user presses Ctrl+R while at the consumer level
- **THEN** the refreshed consumer list is immediately narrowed by the still-active filter

#### Scenario: The filter resets on ascend
- **WHEN** a filter is active at the consumer level and the user ascends (Esc/Backspace) back to
  the stream list
- **THEN** the filter is cleared, so a later descent into any stream's consumers starts unfiltered
