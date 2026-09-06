## ADDED Requirements

### Requirement: Bucket List Filter
The system SHALL allow the user, while at the bucket level of the Values tab, to set a filter
pattern via Ctrl+F that narrows the currently-loaded bucket list to names matching that pattern,
using the same `* ? >` filter-expression grammar as every other list's Ctrl+F filter (see
`list-filter-affordance`). Since the bucket list is always fetched in full (see "Bucket List"),
this filter narrows only what is displayed, never what is fetched, and persists across a Ctrl+R
refresh until cleared or explicitly changed. This is distinct from the key level's existing
server-side key filter ("Server-Side Key Filter"), which scopes a fetch rather than only narrowing
a display — the bucket level has no server-side fetch to scope.

#### Scenario: Confirming a valid pattern narrows the bucket list
- **WHEN** the user presses Ctrl+F while the bucket-level list holds focus, enters a valid,
  non-empty pattern, and confirms
- **THEN** only currently-loaded bucket names matching that pattern remain shown

#### Scenario: The bucket filter persists across a refresh
- **WHEN** a bucket-list filter is active and the user presses Ctrl+R
- **THEN** the refreshed bucket list is immediately narrowed by the still-active filter
