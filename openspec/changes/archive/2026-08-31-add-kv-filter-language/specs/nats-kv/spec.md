## MODIFIED Requirements

### Requirement: Server-Side Key Filter
The system SHALL allow the user, while at the key level, to set a filter expression (see the
`kv-filter-expression` capability for its grammar) via Ctrl+F that scopes every subsequent
key-list fetch for the currently drilled-into bucket — descend, Ctrl+R, and any create/edit-
triggered refresh — to keys matching that expression, until the filter is cleared or the user
ascends out of the bucket. Matching is resolved by compiling the expression into a native NATS
subject filter (used to scope the server-side fetch) plus, only when the expression needs it, a
client-side regex applied to the fetch's results — which phase(s) actually ran is not observable
from the key list beyond the final matching set it shows. This is independent of the existing
in-memory quick-search (`/`): the server-side filter narrows what is fetched from the server, the
quick-search narrows what is displayed from whatever was fetched, and both may be active at once.
A fetch scoped by an active filter SHALL stop after collecting 10,000 matching keys, regardless of
how many more the native-filter-scoped fetch could have returned; an unfiltered fetch (no active
filter) is not capped.

#### Scenario: Ctrl+F opens the filter dialog
- **WHEN** the user presses Ctrl+F while the key-level list holds focus
- **THEN** a modal dialog opens with an editable expression field, seeded with the currently active
  filter expression, or empty if no filter is active

#### Scenario: An invalid expression cannot be confirmed
- **WHEN** the user types a filter expression containing an empty token (a leading, trailing, or
  doubled `.`)
- **THEN** the dialog does not confirm on Enter

#### Scenario: Confirming a non-empty, valid expression sets the filter and refreshes
- **WHEN** the user enters a non-empty, valid filter expression and confirms
- **THEN** that expression becomes the active filter for the currently drilled-into bucket, and the
  key list is immediately re-fetched scoped to it

#### Scenario: Confirming an empty pattern clears the filter
- **WHEN** a filter is currently active, the user opens the dialog, clears the pattern field to
  empty, and confirms
- **THEN** the active filter is cleared, and the key list is immediately re-fetched unscoped
  (every key in the bucket)

#### Scenario: Cancelling the dialog leaves the filter unchanged
- **WHEN** the user opens the filter dialog and cancels (Esc) instead of confirming
- **THEN** the active filter (or lack of one) is unchanged, and no re-fetch occurs

#### Scenario: Only keys matching the full expression are shown
- **WHEN** the active filter expression requires a client-side regex phase (e.g. it contains `?`,
  or a `>` outside its native terminal position)
- **THEN** the key list shows only keys matching the expression exactly, not every key the
  native-filter-scoped fetch happened to return

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

#### Scenario: A filtered fetch stops at 10,000 matching keys
- **WHEN** an active filter's matching keys in the currently drilled-into bucket number more than
  10,000
- **THEN** the key-list fetch stops after collecting 10,000 matches, rather than continuing to
  collect every match in the bucket

#### Scenario: An unfiltered fetch is never capped
- **WHEN** no filter is active for the currently drilled-into bucket, even if the bucket holds more
  than 10,000 keys
- **THEN** the key-list fetch is not stopped early

#### Scenario: An active filter is shown in the key list's title
- **WHEN** a filter is active for the currently drilled-into bucket and its matches did not hit the
  10,000-key cap
- **THEN** the key list's title reflects that a filter is applied, distinguishing the view from
  "showing every key"

#### Scenario: A capped fetch is indicated in the key list's title
- **WHEN** an active filter's fetch stops at the 10,000-key cap
- **THEN** the key list's title indicates that the shown keys are a truncated subset of the
  filter's full matches, distinct from the plain "filter applied" indication

#### Scenario: No filter active leaves the title as today
- **WHEN** no filter is active for the currently drilled-into bucket
- **THEN** the key list's title matches its existing unfiltered form

#### Scenario: A server-side fetch failure is reported
- **WHEN** the filtered (or newly unfiltered) key-list fetch fails
- **THEN** the system reports the failure the same way any other key-list fetch failure is
  reported, and the key list is left showing whatever it last held
