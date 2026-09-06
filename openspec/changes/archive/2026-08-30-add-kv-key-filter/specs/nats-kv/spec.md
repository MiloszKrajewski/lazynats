## MODIFIED Requirements

### Requirement: Key List
The system SHALL list the names of keys in the currently drilled-into bucket, fetched fresh every
time the user descends into that level. If a server-side key filter is active for that bucket
(see "Server-Side Key Filter"), the fetch SHALL be scoped to keys matching that filter's pattern
rather than every key in the bucket.

#### Scenario: Descending fetches the key list
- **WHEN** the user descends into a bucket via Enter
- **THEN** the system fetches the current set of key names for that bucket from the server and
  displays them

#### Scenario: Re-descending re-fetches
- **WHEN** the user ascends from a bucket's key list and then descends into the same bucket again
- **THEN** the system fetches the key list again rather than reusing the previous result

#### Scenario: Ascending does not re-fetch the bucket list
- **WHEN** the user ascends from a bucket's key list back to the bucket list
- **THEN** the bucket list is not re-fetched from the server; it shows whatever it last held

#### Scenario: Descending with an active filter fetches only matching keys
- **WHEN** the user descends into a bucket for which a server-side key filter pattern is
  currently active
- **THEN** the system fetches only the keys matching that pattern, rather than every key in the
  bucket

### Requirement: Manual Key List Refresh
The system SHALL NOT automatically refresh the key list on a timer. The system SHALL allow the
user to refresh the key list on demand via Ctrl+R while at the key level, re-fetching the set of
keys from the server. If a server-side key filter is active, the refresh fetch SHALL remain scoped
to that filter (see "Server-Side Key Filter") rather than reverting to fetching every key. If the
previously-highlighted key is still present in the refreshed list, it SHALL remain highlighted;
otherwise the first item in the refreshed list SHALL become highlighted.

#### Scenario: The key list does not change on its own
- **WHEN** the key level is shown and a key is created or deleted on the server via another
  client, without the user pressing Ctrl+R
- **THEN** the key list shown in the app does not change

#### Scenario: Ctrl+R re-fetches the key list
- **WHEN** the user presses Ctrl+R while the key level holds focus
- **THEN** the key list is re-fetched from the server and the displayed list reflects any keys
  created or deleted since the last fetch

#### Scenario: Ctrl+R with an active filter stays scoped
- **WHEN** the user presses Ctrl+R while the key level holds focus and a server-side key filter is
  currently active
- **THEN** the key list is re-fetched scoped to that filter's pattern, not every key in the bucket

## ADDED Requirements

### Requirement: Server-Side Key Filter
The system SHALL allow the user, while at the key level, to set a NATS subject-wildcard filter
pattern via Ctrl+F that scopes every subsequent key-list fetch for the currently drilled-into
bucket — descend, Ctrl+R, and any create/edit-triggered refresh — to keys matching that pattern,
evaluated server-side, until the filter is cleared or the user ascends out of the bucket. This is
independent of the existing in-memory quick-search (`/`): the server-side filter narrows what is
fetched from the server, the quick-search narrows what is displayed from whatever was fetched, and
both may be active at once.

#### Scenario: Ctrl+F opens the filter dialog
- **WHEN** the user presses Ctrl+F while the key-level list holds focus
- **THEN** a modal dialog opens with an editable pattern field, seeded with the currently active
  filter pattern, or empty if no filter is active

#### Scenario: Confirming a non-empty pattern sets the filter and refreshes
- **WHEN** the user enters a non-empty pattern and confirms
- **THEN** that pattern becomes the active filter for the currently drilled-into bucket, and the
  key list is immediately re-fetched scoped to it

#### Scenario: Confirming an empty pattern clears the filter
- **WHEN** a filter is currently active, the user opens the dialog, clears the pattern field to
  empty, and confirms
- **THEN** the active filter is cleared, and the key list is immediately re-fetched unscoped
  (every key in the bucket)

#### Scenario: Cancelling the dialog leaves the filter unchanged
- **WHEN** the user opens the filter dialog and cancels (Esc) instead of confirming
- **THEN** the active filter (or lack of one) is unchanged, and no re-fetch occurs

#### Scenario: The active filter persists across a plain refresh
- **WHEN** a filter is active and the user presses Ctrl+R, or performs a create or edit that
  triggers a key-list refresh
- **THEN** the resulting fetch remains scoped to the active filter, rather than reverting to
  fetching every key in the bucket

#### Scenario: The active filter resets on ascend
- **WHEN** a filter is active for the currently drilled-into bucket and the user ascends
  (Esc/Backspace) back to the bucket list
- **THEN** the filter is cleared, so a later descent into any bucket starts unfiltered

#### Scenario: The active filter resets on re-descend, even into the same bucket
- **WHEN** the user ascends out of a bucket that had an active filter and then descends into that
  same bucket again
- **THEN** the key list is fetched unfiltered (every key), not scoped to the previously active
  filter

#### Scenario: An active filter is shown in the key list's title
- **WHEN** a filter is active for the currently drilled-into bucket
- **THEN** the key list's title reflects that a filter is applied, distinguishing the view from
  "showing every key"

#### Scenario: No filter active leaves the title as today
- **WHEN** no filter is active for the currently drilled-into bucket
- **THEN** the key list's title matches its existing unfiltered form

#### Scenario: A server-side filter fetch failure is reported
- **WHEN** the filtered (or newly unfiltered) key-list fetch fails — including an invalid
  pattern rejected by the server
- **THEN** the system reports the failure the same way any other key-list fetch failure is
  reported, and the key list is left showing whatever it last held
