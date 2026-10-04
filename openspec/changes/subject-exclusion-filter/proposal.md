## Why

Subscriptions currently hide `$...` and `_INBOX....` traffic through a hardcoded, per-pattern rule
("excluded unless your own pattern starts with the same prefix"). That rule is invisible, can't be
turned off or extended, and there's no way to silence any other noisy subject family (heartbeats,
telemetry, ...) short of narrowing every subscription pattern. An explicit, user-editable exclusion
regex makes the filtering visible and puts the user in control of it.

## What Changes

- Add a single, global **subject exclusion filter**: one .NET regular expression, matched against
  every received message's subject; a match drops the message before it becomes a `FeedEnvelope`.
  Only one expression is supported - multiple rules are combined by the user with `|`.
- The expression defaults to `^(\$|_INBOX\.)`, which excludes exactly what the current implicit
  rule excludes for an ordinary pattern.
- An empty expression is valid and excludes nothing.
- Add a one-line, labeled input for the expression at the bottom of the Subscribe tab, below the
  subscription list, with an `[ Apply ]` button on the same line.
- Edits are **not** applied while typing (so half-typed expressions like a bare `^` never take
  effect); they are applied explicitly via Enter in the field or the Apply button. The field
  shows its state at all times: red = doesn't compile (can't apply), yellow = valid but not yet
  applied, default = exactly the filter in effect. The Apply button is enabled only in the
  yellow state, and the label carries a `*` marker while an edit is pending. Esc in the field
  reverts it to the applied expression.
- An applied change takes effect for all active subscriptions immediately, for messages received
  from that point on; rows already in the live feed are not retroactively removed.
- **BREAKING**: remove the implicit per-subscription `$`/`_INBOX.` opt-in. A subscription whose
  pattern starts with `$` or `_INBOX.` no longer bypasses the exclusion automatically - to watch
  such subjects the user edits (or clears) the exclusion filter.

## Capabilities

### New Capabilities

<!-- none -->

### Modified Capabilities

- `nats-subscriptions`: replaces the "Implicit System and Inbox Subject Filtering" requirement with
  a user-editable "Subject Exclusion Filter" requirement (default, matching semantics), adds
  "Editing and Applying the Exclusion Filter" (explicit apply, invalid/pending/applied states,
  revert), and extends "Framed List Presentation" to cover the new bottom input row.
- `tab-navigation`: adds "Tab Cycles Within the Selected Tab's Content" - Tab/Shift+Tab stay within
  the selected tab's content for every tab by default. The Subscribe tab is the first with more
  than one permanent Tab stop, which exposed that Tab previously left for the header and, on
  re-entry, stranded earlier stops. Visible side effect elsewhere: Tab on a single-stop tab no
  longer bounces to the header (Up still does).

## Impact

- `src/lazynats/Subscriptions/SubscriptionRegistry.cs`: drop the `excludeSystem`/`excludeInbox`
  logic; consult the shared exclusion filter per message instead.
- New pure, unit-testable exclusion-filter type in `src/lazynats.Core` (validate, apply, match),
  with tests in `src/lazynats.Core.Tests`.
- `src/lazynats/Subscriptions/SubscribeTab.cs`: new bottom label + `EditFrame`-wrapped `TextField`
  + Apply button, list frame shrinks to make room; shortcut hints/forwarding gated to the list's
  focus.
- `Theme.cs`: new pending-edit (yellow) color constant.
- `src/lazynats/Components/TabbedView.cs`: `AddTab` makes every tab's content a Tab group.
- `Program.cs`: construct the filter and hand it to `SubscriptionRegistry` / `SubscribeTab`.
- No effect on `$TRACE` rows (`TraceFeedListener` writes to the feed directly, bypassing the
  registry) and no persistence - the filter resets to the default on each launch.
