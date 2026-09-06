## MODIFIED Requirements

### Requirement: Object List
The system SHALL list the names of all non-deleted objects in the currently drilled-into bucket,
fetched fresh every time the user descends into that level. Unlike the KV key list's server-side
filter, this fetch always retrieves every non-deleted object name from the server; if a
post-fetch name filter is active for that bucket (see "Post-Fetch Object Name Filter"), the
fetched names SHALL be filtered down to matches before being shown, but the fetch itself is not
scoped.

#### Scenario: Descending fetches the object list
- **WHEN** the user descends into a bucket via Enter
- **THEN** the system fetches the current set of object names for that bucket from the server and
  displays them

#### Scenario: Re-descending re-fetches
- **WHEN** the user ascends from a bucket's object list and then descends into the same bucket
  again
- **THEN** the system fetches the object list again rather than reusing the previous result

#### Scenario: Ascending does not re-fetch the bucket list
- **WHEN** the user ascends from a bucket's object list back to the bucket list
- **THEN** the bucket list is not re-fetched from the server; it shows whatever it last held

#### Scenario: Deleted objects are excluded
- **WHEN** a bucket contains an object that has been deleted (tombstoned) on the server
- **THEN** that object does not appear in the object list

#### Scenario: Descending with an active filter still fetches every name, then narrows
- **WHEN** the user descends into a bucket for which a post-fetch name filter is currently active
- **THEN** the system fetches every non-deleted object name from the server as usual, then
  displays only the names matching the active filter

### Requirement: Manual Object List Refresh
The system SHALL NOT automatically refresh the object list on a timer. The system SHALL allow the
user to refresh the object list on demand via Ctrl+R while at the object level, re-fetching the
set of objects from the server. If a post-fetch name filter is active, the refreshed list SHALL
remain filtered to that pattern (see "Post-Fetch Object Name Filter") rather than reverting to
showing every fetched name. If the previously-highlighted object is still present in the refreshed
(and, if applicable, filtered) list, it SHALL remain highlighted; otherwise the first item in the
refreshed list SHALL become highlighted.

#### Scenario: The object list does not change on its own
- **WHEN** the object level is shown and an object is added or deleted on the server via another
  client, without the user pressing Ctrl+R
- **THEN** the object list shown in the app does not change

#### Scenario: Ctrl+R re-fetches the object list
- **WHEN** the user presses Ctrl+R while the object level holds focus
- **THEN** the object list is re-fetched from the server and the displayed list reflects any
  objects added or deleted since the last fetch

#### Scenario: Ctrl+R with an active filter stays narrowed
- **WHEN** the user presses Ctrl+R while the object level holds focus and a post-fetch name filter
  is currently active
- **THEN** every object name is re-fetched as usual, but the displayed list remains narrowed to
  names matching the active filter

## ADDED Requirements

### Requirement: Post-Fetch Object Name Filter
The system SHALL allow the user, while at the object level, to set a name filter pattern via
Ctrl+F that narrows every subsequent object-list refresh for the currently drilled-into bucket —
descend, Ctrl+R, and any upload/delete-triggered refresh — to names matching that pattern, applied
to the full set of names fetched from the server (the fetch itself is never scoped; see "Object
List"), until the filter is cleared or the user ascends out of the bucket. Matching SHALL use
case-insensitive filesystem-style wildcards: `*` matches any run of characters (including none),
`?` matches exactly one character, and the pattern is matched against the full object name (not a
substring search) — e.g. `foo*` matches names starting with `foo`, `*foo*` matches names
containing `foo`, `*.json` matches names ending in `.json`. This is distinct from both the KV key
filter's NATS subject-wildcard notation (object names have no token/dot-hierarchy structure for
that to apply to) and the existing live quick-search's (`/`) fuzzy-subsequence matching. This
filter is independent of `/`: the post-fetch filter narrows what is retained after every fetch,
`/` further narrows what is displayed from whatever the post-fetch filter left, and both may be
active at once.

#### Scenario: Ctrl+F opens the filter dialog
- **WHEN** the user presses Ctrl+F while the object-level list holds focus
- **THEN** a modal dialog opens with an editable pattern field, seeded with the currently active
  filter pattern, or empty if no filter is active

#### Scenario: Confirming a non-empty pattern sets the filter and refreshes
- **WHEN** the user enters a non-empty pattern and confirms
- **THEN** that pattern becomes the active filter for the currently drilled-into bucket, and the
  object list is immediately re-fetched (every name) and re-narrowed to matches

#### Scenario: Confirming an empty pattern clears the filter
- **WHEN** a filter is currently active, the user opens the dialog, clears the pattern field to
  empty, and confirms
- **THEN** the active filter is cleared, and the object list is immediately re-fetched and shown
  unfiltered (every name)

#### Scenario: Cancelling the dialog leaves the filter unchanged
- **WHEN** the user opens the filter dialog and cancels (Esc) instead of confirming
- **THEN** the active filter (or lack of one) is unchanged, and no re-fetch occurs

#### Scenario: A wildcard pattern matches names case-insensitively
- **WHEN** the active filter pattern contains `*` and/or `?` wildcards and an object's name
  matches that pattern against its full length, case-insensitively
- **THEN** that object's name is retained after the post-fetch filter is applied

#### Scenario: A pattern without wildcards requires an exact (case-insensitive) name match
- **WHEN** the active filter pattern contains no `*` or `?` characters
- **THEN** only an object whose full name equals the pattern, case-insensitively, is retained —
  unlike `/`'s fuzzy-subsequence search, a plain substring is not enough on its own without a
  leading and/or trailing `*`

#### Scenario: A leading and/or trailing `*` matches a substring
- **WHEN** the active filter pattern is `*foo*`
- **THEN** any object name containing `foo` anywhere is retained, matching the common
  "substring search" wildcard convention

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
