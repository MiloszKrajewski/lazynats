## REMOVED Requirements

### Requirement: Aggregated Shortcuts Are Rendered in the Status Bar
**Reason**: The status bar no longer displays per-view aggregated shortcuts directly — it shows
only a fixed, hardcoded top-level shortcut set. The same focus-chain aggregation is instead
surfaced on demand by the `shortcut-picker` capability's `Alt+K` dialog.
**Migration**: No consumer action needed; this is an in-app UI behavior change. Any expectation
of finding a view's shortcuts printed directly in the status bar is replaced by pressing `Alt+K`
to open the shortcut picker.

### Requirement: Available Shortcuts Update on Focus Change
**Reason**: This requirement existed to keep the status bar's continuously-rendered dynamic tail
in sync as focus moved. With that renderer removed, there is no longer a continuously-updated
consumer of the aggregation — the `shortcut-picker` capability computes the aggregated set once,
on demand, at the moment the picker opens, rather than the system maintaining a live-updated set.
**Migration**: No consumer action needed; `ShortcutAggregator.Collect` (the underlying focus-chain
walk) is still used, just called on demand instead of pushed on every focus change.
