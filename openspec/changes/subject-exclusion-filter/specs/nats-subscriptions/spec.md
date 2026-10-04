## ADDED Requirements

### Requirement: Subject Exclusion Filter
The system SHALL maintain a single, global subject exclusion filter: one .NET regular expression
(case-sensitive, unanchored unless the expression itself anchors) that applies to every active
subscription. Every received message whose subject matches the filter currently in effect SHALL be
excluded before it is wrapped into a `FeedEnvelope` or otherwise contributed to the shared feed
pipeline. Only one expression SHALL be supported; combining several rules is done within that one
expression (e.g. with `|`). The filter SHALL default to `^(\$|_INBOX\.)` on every launch. An empty
expression SHALL be valid and SHALL exclude nothing. The exclusion SHALL be applied independently
of, and in addition to, each subscription's own pattern matching (native scoping plus any
client-side filter), and SHALL NOT depend on the subscription's own pattern text.

#### Scenario: Default filter excludes system traffic
- **WHEN** the exclusion filter is at its default and a subscription for `>` is active, and a
  message is published on `$SYS.ACCOUNT.PING`
- **THEN** that message is received from NATS but excluded before becoming a `FeedEnvelope`, and
  does not appear in the feed

#### Scenario: Default filter excludes inbox traffic
- **WHEN** the exclusion filter is at its default and a subscription for `>` is active, and a
  message is published on `_INBOX.abc123.1`
- **THEN** that message is excluded and does not appear in the feed

#### Scenario: Default filter leaves ordinary subjects alone
- **WHEN** the exclusion filter is at its default and a message is published on a subject that
  starts with neither `$` nor `_INBOX.` (e.g. `invoices.paid`, or `orders.$draft`)
- **THEN** the exclusion filter does not exclude it, and existing pattern-matching behavior is
  unchanged

#### Scenario: A pattern starting with the excluded prefix does not bypass the filter
- **WHEN** the exclusion filter is at its default and a subscription for `$SYS.>` is active, and a
  message is published on `$SYS.ACCOUNT.PING`
- **THEN** that message is excluded, since the filter does not take the subscription's own pattern
  into account

#### Scenario: Clearing the filter lets everything through
- **WHEN** the exclusion filter is set to the empty expression and a subscription for `$SYS.>` is
  active, and a message is published on `$SYS.ACCOUNT.PING`
- **THEN** that message is not excluded and appears in the feed

#### Scenario: Multiple rules combined with alternation
- **WHEN** the exclusion filter is set to `^(\$|_INBOX\.)|\.heartbeat$` and messages are published
  on `svc.a.heartbeat`, `_INBOX.x`, and `svc.a.status`
- **THEN** `svc.a.heartbeat` and `_INBOX.x` are excluded, and `svc.a.status` appears in the feed

### Requirement: Editing and Applying the Exclusion Filter
The Subscribe tab SHALL NOT apply edits to the exclusion input while the user types. The input's
text SHALL become the filter in effect only when explicitly applied, either by pressing Enter in
the input or by activating the Apply button beside it. The input SHALL always be in exactly one of
three states, determined by comparing its current text with the expression in effect:
- **Invalid**: the text does not compile as a regular expression. The text SHALL be shown in red,
  the Apply button SHALL be disabled, and Enter SHALL be a no-op. This state takes precedence over
  Pending.
- **Pending**: the text compiles and differs from the expression in effect. The text SHALL be shown
  in yellow, the Apply button SHALL be enabled, and the input's label SHALL carry a `*` marker.
- **Applied**: the text is identical to the expression in effect. The text SHALL be shown in the
  default editable colors, the Apply button SHALL be disabled, and the label SHALL carry no marker.

The label SHALL also carry the `*` marker in the Invalid state, since the input's text then differs
from the expression in effect. Applying SHALL immediately make the text the filter in effect for
all active subscriptions and return the input to Applied. Pressing Esc in the input SHALL revert its
text to the expression in effect (returning it to Applied) without changing the filter. Moving focus
away from the input SHALL neither apply nor revert its edit. An applied change SHALL affect only
messages received after it; rows already in the live feed SHALL NOT be removed or re-evaluated.

#### Scenario: Typing does not change the filter in effect
- **WHEN** the filter in effect is `^(\$|_INBOX\.)` and the user edits the input to `^` without
  applying, and a message is published on `invoices.paid`
- **THEN** the input shows `^` as Pending (yellow text, Apply enabled, label marked `*`), and
  `invoices.paid` still appears in the feed

#### Scenario: Enter applies a pending edit
- **WHEN** the input is Pending with `^_INBOX\.` and the user presses Enter in it, and then a
  message is published on `$SYS.ACCOUNT.PING` matching an active subscription
- **THEN** the input returns to Applied (default colors, Apply disabled, no marker), and the
  message appears in the feed

#### Scenario: The Apply button applies a pending edit
- **WHEN** the input is Pending and the user moves focus to the Apply button and activates it
- **THEN** the input's text becomes the filter in effect and the input returns to Applied

#### Scenario: An invalid edit cannot be applied
- **WHEN** the filter in effect is `^(\$|_INBOX\.)` and the user edits the input to
  `^(\$|_INBOX\.` (unbalanced parenthesis) and presses Enter
- **THEN** the text is shown in red, the Apply button is disabled, the filter in effect remains
  `^(\$|_INBOX\.)`, and a message published on `_INBOX.abc` is still excluded

#### Scenario: Editing back to the applied text clears the pending state
- **WHEN** the input is Pending and the user edits its text back to exactly the expression in
  effect
- **THEN** the input returns to Applied without anything having been applied

#### Scenario: Esc reverts an unapplied edit
- **WHEN** the input is Pending or Invalid and the user presses Esc in it
- **THEN** its text is replaced by the expression in effect, it returns to Applied, and the filter
  in effect is unchanged

#### Scenario: Leaving the input keeps an unapplied edit
- **WHEN** the input is Pending and the user moves focus back to the subscription list
- **THEN** the input keeps its text and remains Pending, and the filter in effect is unchanged

#### Scenario: Existing feed rows are not retroactively filtered
- **WHEN** the feed already shows rows for `svc.a.heartbeat` and the user applies the filter
  `heartbeat`
- **THEN** the existing `svc.a.heartbeat` rows stay in the feed, and only subsequently received
  `...heartbeat` messages are excluded

## MODIFIED Requirements

### Requirement: Framed List Presentation
The Subscribe tab's subscription list SHALL be presented with a "Subscriptions" label above a
padded `EditFrame`, using the same shared editable-control background as the Publish tab's fields
and header editor, giving it visual breathing room, background consistency, and field-name
labeling matching the Publish tab's fields, without altering any of its existing add/edit/delete
behavior. Below the list, anchored to the bottom of the tab, the tab SHALL present the subject
exclusion filter input: a label naming it, above a single row holding a single-line text input
inside a padded `EditFrame` using the same shared editable background, followed by an Apply button.
The list SHALL fill the remaining height above it. The input SHALL be reachable by keyboard: Tab
(and Shift+Tab, which advances the same way, per the tab strip's direction-agnostic navigation)
SHALL cycle list, input, and - only while it is enabled - the Apply button, staying within the
tab's content rather than passing through the tab header (which remains reachable via Up). While
the input or the Apply button has focus, the list's New/Edit/Delete shortcuts SHALL NOT be
advertised among the focused view's shortcut hints (the `?` Shortcuts picker) nor dispatched from
keys the input leaves unhandled; they SHALL be advertised and dispatched as before while the list
has focus.

#### Scenario: Subscription list is visually framed and labeled
- **WHEN** the Subscribe tab is displayed
- **THEN** a "Subscriptions" label is shown above the subscription list, the list is presented
  inside a padded frame using the shared editable background color, and its existing
  New/Edit/Delete behavior is unchanged

#### Scenario: Exclusion input sits at the bottom with its own label
- **WHEN** the Subscribe tab is displayed
- **THEN** a labeled, single-line exclusion filter input with an Apply button beside it is shown at
  the bottom of the tab, the input framed like the list and pre-filled with the filter currently in
  effect (Applied state), and the list fills the height above it

#### Scenario: Keyboard reaches the exclusion input
- **WHEN** the subscription list has focus and the user presses Tab
- **THEN** focus moves to the exclusion filter input, and a further Tab returns it to the list
  while the Apply button is disabled

#### Scenario: Tab reaches the Apply button only while it is enabled
- **WHEN** the exclusion input is Pending, has focus, and the user presses Tab
- **THEN** focus moves to the Apply button, and a further Tab moves it to the list

#### Scenario: Tab never strands the list
- **WHEN** focus has cycled from the list to the exclusion input any number of times
- **THEN** a Tab from the input (or the Apply button) still reaches the list, never passing
  through the tab header

#### Scenario: List shortcuts are scoped to the list's focus
- **WHEN** the exclusion filter input has focus
- **THEN** the `?` Shortcuts picker does not list the list's New/Edit/Delete shortcuts, and typing
  `n`, `e`, or `d` edits the input text rather than acting on the list

## REMOVED Requirements

### Requirement: Implicit System and Inbox Subject Filtering
**Reason**: Replaced by the explicit, user-editable "Subject Exclusion Filter", whose default
`^(\$|_INBOX\.)` reproduces the old exclusion for ordinary patterns. The per-subscription opt-in
(a pattern starting with `$`/`_INBOX.` bypassing the exclusion) is dropped rather than carried over.
**Migration**: To watch `$...` or `_INBOX....` subjects, edit or clear the exclusion filter on the
Subscribe tab (e.g. `^_INBOX\.` to see `$` subjects while still hiding inboxes).
