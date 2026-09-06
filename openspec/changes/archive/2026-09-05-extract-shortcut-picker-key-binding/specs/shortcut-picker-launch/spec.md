## ADDED Requirements

### Requirement: Caller-Supplied Picker Start View
The system SHALL provide a reusable mechanism for opening the shortcut picker that accepts the
focus-chain start view as a lazily-evaluated function, resolved at the moment the picker is
actually opened rather than at the moment the mechanism was wired up. A caller MAY supply this
function explicitly when its correct start view isn't expressible in terms of its own current
focus state (e.g. an app-wide "wherever focus currently is" policy); when a caller omits it, the
mechanism SHALL resolve a start view automatically from the calling view's own current focus
state, without requiring the caller to hand-reason a fixed policy.

#### Scenario: A caller with no natural "owner" view supplies its own start-view policy
- **WHEN** a caller has no single view whose focus subtree it is scoped to (e.g. it needs the
  application's current app-wide focused view, wherever that is) and supplies a function
  resolving to that
- **THEN** the mechanism collects shortcuts starting from that supplied view, and this does not
  affect any other caller's resolved start view

#### Scenario: A caller omitting the start view gets one resolved from its own focus state
- **WHEN** a caller has a single owning view and omits the start-view function
- **THEN** the mechanism resolves the start view automatically: if that view's currently most-
  focused descendant's focus chain leads back to the owning view, that descendant is used;
  otherwise the owning view itself is used

#### Scenario: Start view is resolved at open time, not at wiring time
- **WHEN** the mechanism is wired up once (e.g. at application startup) but focus changes multiple
  times before the picker is subsequently opened
- **THEN** each time the picker opens, the start view reflects focus as of that opening, not focus
  as of when the mechanism was wired up

### Requirement: Reusable Action and Direct Key-Binding Forms
The system SHALL expose the picker-opening sequence (deferred aggregation, dialog construction and
run, and invocation of the selected entry's action) both as a plain reusable action a caller can
attach to its own dispatch mechanism, and as a convenience that binds that action directly to the
picker's key on a given view — implemented such that the direct-binding form does not duplicate
the underlying sequence.

#### Scenario: Reusable action form integrates with an existing dispatch list
- **WHEN** a caller already dispatches its own key-to-action bindings from a list (e.g. to also
  drive a status-bar widget) and adds the picker-opening action to that list
- **THEN** pressing the picker's key runs the same picker-opening sequence as the direct-binding
  form would

#### Scenario: Direct key-binding form requires no caller-side dispatch code
- **WHEN** a caller has no existing key-to-action dispatch mechanism of its own and uses the
  direct-binding form on a view, supplying only that view (and, optionally, an explicit start-view
  function where the automatic resolution described above doesn't apply)
- **THEN** pressing the picker's key while that view is part of the active key-dispatch chain
  opens the picker, with no additional wiring from the caller

### Requirement: Single Definition of the Picker's Trigger Key
The system SHALL define the picker's trigger key in exactly one place, used by both the reusable
action/direct-binding mechanism and any caller that needs to reference the same key for its own
display purposes (e.g. a status-bar widget).

#### Scenario: A caller displaying the key uses the same definition the mechanism binds
- **WHEN** a caller builds a user-visible representation of the picker's trigger key (such as a
  status-bar widget) alongside using the direct-binding or reusable-action mechanism
- **THEN** both the displayed key and the key the mechanism actually binds come from the same
  single definition
