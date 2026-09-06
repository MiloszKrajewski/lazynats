## ADDED Requirements

### Requirement: Header List Quick-Search
The system SHALL allow the user to narrow the headers list live, in memory, via the same shared
quick-search shape (`/`, case-insensitive fuzzy-subsequence matching against each header pair's
`"Key: Value"` text representation) that other lists in the app offer, with no effect on which
headers are actually sent with the message.

#### Scenario: Typing a query narrows the displayed headers
- **WHEN** the user presses `/` and types a query matching some, but not all, header pairs' `"Key:
  Value"` text as a case-insensitive subsequence
- **THEN** only the matching header pairs remain shown, and every header (shown or not) is still
  sent with the message

#### Scenario: Clearing the query restores the full list
- **WHEN** the search field's text is cleared to empty
- **THEN** every header pair is shown again

### Requirement: Header List Filter
The system SHALL allow the user to set a filter pattern via Ctrl+F that narrows the headers list to
pairs whose `"Key: Value"` text representation matches it, using the same `* ? >`
filter-expression grammar as every other list's Ctrl+F filter (see `list-filter-affordance`). Since
the headers list has no server-side fetch to scope, this filter narrows only what is displayed,
with no effect on which headers are sent. This is independent of quick-search (`/`); both may be
active at once.

#### Scenario: Confirming a valid pattern narrows the header list
- **WHEN** the user presses Ctrl+F, enters a valid, non-empty pattern, and confirms
- **THEN** only header pairs whose `"Key: Value"` text matches the filter expression remain shown,
  and every header (shown or not) is still sent with the message

#### Scenario: An invalid pattern cannot be confirmed
- **WHEN** the entered pattern contains an empty token (a leading, trailing, or doubled `.`)
- **THEN** the dialog does not confirm on Enter

#### Scenario: Confirming an empty pattern clears the filter
- **WHEN** a filter is currently active, the user opens the dialog, clears the pattern field to
  empty, and confirms
- **THEN** the active filter is cleared and every header pair is shown again

#### Scenario: Adding a header while filtered may leave it hidden
- **WHEN** a filter is active and the user adds a new header pair whose `"Key: Value"` text does
  not match it
- **THEN** the new header pair is added and will still be sent with the message, but does not
  appear in the currently-filtered list
