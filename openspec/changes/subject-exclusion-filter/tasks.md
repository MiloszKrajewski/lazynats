## 1. Core filter type

- [x] 1.1 Add `SubjectExclusionFilter` and `ExclusionEditState` (`Invalid`/`Pending`/`Applied`) to `src/lazynats.Core` (e.g. `Subjects/`): `DefaultPattern = @"^(\$|_INBOX\.)"`, `Pattern`, `IsValid`, `TryApply`, `IsExcluded`, `Classify`; volatile active-`Regex?` field, `CultureInvariant` + match timeout, timeout treated as not excluded (design Decisions 1-2)
- [x] 1.2 Add xunit tests in `src/lazynats.Core.Tests`: default excludes `$SYS.ACCOUNT.PING` and `_INBOX.abc.1`, keeps `invoices.paid` and `orders.$draft`; empty excludes nothing; `^` excludes everything; invalid `TryApply` returns false and leaves `Pattern`/matching unchanged; alternation `^(\$|_INBOX\.)|\.heartbeat$`; case-sensitivity; `Classify` returns Applied for the current pattern, Pending for other valid text, Invalid for non-compiling text (including when it differs from the pattern)
- [x] 1.3 `dotnet test src/lazynats.sln` passes

## 2. Registry wiring

- [x] 2.1 `SubscriptionRegistry`: take a `SubjectExclusionFilter` in the constructor; in `RunAsync` delete `excludeSystem`/`excludeInbox` and replace them with a single `IsExcluded(message.Subject)` check
- [x] 2.2 `Program.cs`: construct the filter, pass it to the registry, register it as a DI singleton
- [x] 2.3 Update any remaining comments/docs referencing the implicit `$`/`_INBOX.` rule (grep for `_INBOX`)

## 3. Subscribe tab UI

- [x] 3.1 Add `Theme.PendingEditColor` (BrightYellow) with a comment naming its usage site
- [x] 3.2 `SubscribeTab`: accept the filter (resolved in `MainWindow`); add bottom-anchored label, 3-row `EditFrame`-wrapped `TextField` pre-filled with `filter.Pattern`, and an `[ Apply ]` button on the same row; list frame height fills down to them (design Decision 4)
- [x] 3.3 `UpdateExclusionState()` on `ValueChanged` and after apply/revert: field scheme per state (red / `PendingEditColor` / default), Apply enabled only when Pending, label ` *` marker unless Applied (design Decision 5)
- [x] 3.4 `ApplyExclusion()` wired to the field's `Accepting` (handled, no-op unless Pending) and the button's `Accepting`; after a button apply, move focus back to the field (design Decision 6)
- [x] 3.5 Esc in the field reverts `Text` to `filter.Pattern` and is marked handled, only when not already Applied
- [x] 3.6 Gate `Shortcuts` and `OnKeyDownNotHandled` dispatch on `_subscriptionsView.HasFocus` (design Decision 7)
- [x] 3.7 `dotnet build src/lazynats.sln` is clean (no new warnings, incl. AOT/trim analyzers)

## 4. Verification

- [x] 4.1 tmux: Subscribe tab shows label (no `*`), input with the default, and Apply at the bottom; Tab cycles list ↔ input within the tab (Apply skipped while disabled, header never in the cycle); `?` picker drops N/E/D while the input is focused
- [x] 4.2 tmux: typing makes the label show `*` and Tab reaches Apply; editing back to the original text clears `*`; Esc reverts and clears `*`; Esc with nothing to revert behaves as before; tabbing away keeps the `*`
- [x] 4.3 tmux + `.bin/` nats CLI: with default, `$SYS...`/`_INBOX...` publishes are hidden and ordinary subjects show; typing `^` without applying still shows ordinary subjects; an unbalanced `(` + Enter keeps the old filter in effect; applying an empty expression (Enter and, separately, the Apply button) lets `$`-subjects through for a `>` subscription
- [x] 4.4 Update `doc/UI.md` if it describes the Subscribe tab's contents

## 5. Tab containment by default

- [x] 5.1 Move `TabStop = TabBehavior.TabGroup` from `SubscribeTab` into `TabbedView.AddTab`, applying to every tab's content
- [x] 5.2 Add `tab-navigation` spec delta: "Tab Cycles Within the Selected Tab's Content"
- [x] 5.3 tmux: on all five tabs, Tab/Shift+Tab keeps focus in content and Up still reaches the header; Subscribe list ↔ field cycle still works; FilterBox Tab-exit on Values/Objects still returns to the list
