## Why

`SubscriptionsView` still uses the ad hoc "text input + editable list" wiring that `ListEditorView<T>`
was built to replace — including the exact parallel-`List<Guid>`-bookkeeping smell called out as the
motivating example when that component was designed. Now that `ListEditorView<T>` exists and is
verified, `SubscriptionsView` is its first real consumer, and doing so also lets subscription patterns
gain a keyboard-only edit convenience (Ctrl+E) that was previously unavailable.

## What Changes

- `SubscriptionsView` is rebuilt on `ListEditorView<SubscriptionInfo>` instead of a hand-rolled
  `TextField` + `Button` + `ListView`, using `SubscriptionInfo`'s existing `Id` field in place of the
  separate `List<Guid> _ids` mirror it previously needed.
- Adds a small additive `protected int? EditingIndex` read accessor to `ListEditorView<T>`
  (`src/lazynats/ListEditorView.cs`) so a subclass whose commit path has side effects beyond direct
  collection mutation (like starting/stopping a real NATS subscription) can tell whether the current
  commit is a fresh add or an edit-in-place, without duplicating the base class's private bookkeeping.
- Adds Ctrl+E as a convenience for changing an existing subscription's pattern: it loads the pattern into
  the input for editing, and on commit removes the old subscription and adds a new one with the edited
  pattern — the same underlying operation as delete-then-add, just reachable via one edit-and-commit
  gesture instead of two separate actions. **The subscription's identity (`Guid`) always changes**, since
  a NATS subscription cannot be altered in place; this is a UI convenience, not a new domain capability.
- Delete moves from the bare `Delete` key to `Ctrl+D`, and clearing/canceling in-progress input gains an
  explicit `Ctrl+N`, both inherited for free from `ListEditorView<T>`.
- Removes the "_Add" button, which was always redundant with pressing Enter in the pattern field; matches
  the Enter-only, no-button convention `ListEditorView<T>` already established for `PublishView`'s header
  editor and the "no mouse-only affordances" rule in `doc/UI.md`.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `nats-subscriptions`: the "Modifying a Subscription Pattern" requirement changes from disclaiming any
  edit action to describing the Ctrl+E convenience — still implemented as delete-then-add underneath
  (new subscription identity), not true in-place mutation.

## Impact

- `src/lazynats/SubscriptionsView.cs`: rewritten to subclass `ListEditorView<SubscriptionInfo>`.
- `src/lazynats/ListEditorView.cs`: one additive `protected` accessor, no behavior change for existing
  consumers.
- No changes to `PublishView.cs`, `HeaderListDataSource.cs`, `LiveUpdatesView.cs`, `MainWindow.cs`, or
  `SubscriptionRegistry.cs` — those remain out of scope for this change.
