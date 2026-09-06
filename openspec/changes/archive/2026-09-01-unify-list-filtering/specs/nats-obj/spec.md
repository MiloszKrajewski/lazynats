## MODIFIED Requirements

### Requirement: Post-Fetch Object Name Filter
The system SHALL allow the user, while at the object level, to set a name filter pattern via
Ctrl+F that narrows every subsequent object-list refresh for the currently drilled-into bucket —
descend, Ctrl+R, and any upload/delete-triggered refresh — to names matching that pattern, applied
to the full set of names fetched from the server (the fetch itself is never scoped; see "Object
List"), until the filter is cleared or the user ascends out of the bucket. Matching SHALL use the
same `* ? >` filter-expression grammar as every other list's Ctrl+F filter (see
`list-filter-affordance` and the `kv-filter-expression` capability), matched case-sensitively —
`*` matches any run of characters within one `.`-separated token, `?` matches exactly one non-`.`
character, and `>` matches one or more remaining tokens as a bare final segment (or an arbitrary
run of characters, including `.`, elsewhere). This is distinct from the existing live quick-search's
(`/`) fuzzy-subsequence matching. This filter is independent of `/`: the post-fetch filter narrows
what is retained after every fetch, `/` further narrows what is displayed from whatever the
post-fetch filter left, and both may be active at once.

#### Scenario: Ctrl+F opens the filter dialog
- **WHEN** the user presses Ctrl+F while the object-level list holds focus
- **THEN** a modal dialog opens with an editable pattern field, seeded with the currently active
  filter pattern, or empty if no filter is active

#### Scenario: Confirming a valid, non-empty pattern sets the filter and refreshes
- **WHEN** the user enters a valid, non-empty pattern and confirms
- **THEN** that pattern becomes the active filter for the currently drilled-into bucket, and the
  object list is immediately re-fetched (every name) and re-narrowed to matches

#### Scenario: An invalid pattern cannot be confirmed
- **WHEN** the entered pattern contains an empty token (a leading, trailing, or doubled `.`)
- **THEN** the dialog does not confirm on Enter

#### Scenario: Confirming an empty pattern clears the filter
- **WHEN** a filter is currently active, the user opens the dialog, clears the pattern field to
  empty, and confirms
- **THEN** the active filter is cleared, and the object list is immediately re-fetched and shown
  unfiltered (every name)

#### Scenario: Cancelling the dialog leaves the filter unchanged
- **WHEN** the user opens the filter dialog and cancels (Esc) instead of confirming
- **THEN** the active filter (or lack of one) is unchanged, and no re-fetch occurs

#### Scenario: A wildcard pattern matches names case-sensitively
- **WHEN** the active filter pattern contains `*`, `?`, and/or `>` and an object's name matches
  that pattern per the filter-expression grammar
- **THEN** that object's name is retained after the post-fetch filter is applied

#### Scenario: A pattern without wildcards requires an exact, case-sensitive name match
- **WHEN** the active filter pattern contains no `*`, `?`, or `>` characters
- **THEN** only an object whose full name equals the pattern exactly, case-sensitively, is
  retained — unlike `/`'s fuzzy-subsequence search, a plain substring is not enough on its own

#### Scenario: A bare leading and/or trailing `*` matches within a token
- **WHEN** the active filter pattern is `*foo*`
- **THEN** any object name containing `foo` within a single `.`-free run is retained, per the same
  token-scoped `*` semantics every other list's filter uses

#### Scenario: The active filter persists across a plain refresh
- **WHEN** a filter is active and the user presses Ctrl+R, or performs an upload or delete that
  triggers an object-list refresh
- **THEN** the resulting fetch is re-narrowed by the active filter, rather than reverting to
  showing every fetched name

#### Scenario: The active filter resets on ascend
- **WHEN** a filter is active for the currently drilled-into bucket and the user ascends
  (Esc/Backspace) back to the bucket list
- **THEN** the filter is cleared, so a later descent into any bucket starts unfiltered

#### Scenario: The active filter resets on re-descend, even into the same bucket
- **WHEN** the user ascends out of a bucket that had an active filter and then descends into that
  same bucket again
- **THEN** the object list is fetched and shown unfiltered, not scoped to the previously active
  filter

#### Scenario: An active filter is shown in the object list's title
- **WHEN** a filter is active for the currently drilled-into bucket
- **THEN** the object list's title reflects that a filter is applied, distinguishing the view from
  "showing every object"

#### Scenario: No filter active leaves the title as today
- **WHEN** no filter is active for the currently drilled-into bucket
- **THEN** the object list's title matches its existing unfiltered form

#### Scenario: This filter never reduces what is fetched from the server
- **WHEN** any filter (active or not) is applied via this requirement
- **THEN** the underlying fetch from the server retrieves every non-deleted object name in the
  bucket regardless — the filter only affects what is retained/displayed afterward, never what is
  requested

## ADDED Requirements

### Requirement: Bucket List Filter
The system SHALL allow the user, while at the bucket level of the Objects tab, to set a filter
pattern via Ctrl+F that narrows the currently-loaded bucket list to names matching that pattern,
using the same `* ? >` filter-expression grammar as every other list's Ctrl+F filter (see
`list-filter-affordance`). Since the bucket list is always fetched in full (see "Bucket List"), this
filter narrows only what is displayed, never what is fetched, and persists across a Ctrl+R refresh
until cleared or explicitly changed.

#### Scenario: Confirming a valid pattern narrows the bucket list
- **WHEN** the user presses Ctrl+F while the bucket-level list holds focus, enters a valid,
  non-empty pattern, and confirms
- **THEN** only currently-loaded bucket names matching that pattern remain shown

#### Scenario: The bucket filter persists across a refresh
- **WHEN** a bucket-list filter is active and the user presses Ctrl+R
- **THEN** the refreshed bucket list is immediately narrowed by the still-active filter
