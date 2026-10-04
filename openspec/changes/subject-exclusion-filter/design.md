## Context

`SubscriptionRegistry.RunAsync` currently computes two booleans per subscription from its raw
pattern (`excludeSystem = !pattern.StartsWith('$')`, `excludeInbox = !pattern.StartsWith("_INBOX.")`)
and drops matching subjects before building a `FeedEnvelope`. The rule is hardcoded, invisible in
the UI, and not extensible. This change replaces it with one user-editable regex shared by all
subscriptions, edited from the bottom of the Subscribe tab and applied explicitly.

Threading: the registry's reader loops run on background tasks (one per subscription); the Subscribe
tab's input is edited on the UI thread. The filter is therefore written from one thread and read
concurrently from many.

## Goals / Non-Goals

**Goals:**
- One global exclusion regex, default `^(\$|_INBOX\.)`, applied per received message.
- Explicit apply (Enter / Apply button), so half-typed expressions never take effect.
- An always-visible Invalid / Pending / Applied state on the input, readable without color too.
- Validate/apply/match and state classification live in `lazynats.Core` and are unit-tested there.

**Non-Goals:**
- Multiple independent rules / a rule list (use `|`).
- Per-subscription exclusion or reviving the pattern-prefix opt-in.
- Persisting the expression across launches.
- Retroactively filtering rows already in the live feed.
- Filtering `$TRACE` rows - `TraceFeedListener` writes to the feed directly, bypassing the
  registry, so the default `^\$` never hides them (and must not).

## Decisions

### 1. A small `SubjectExclusionFilter` type in `lazynats.Core`
Pure class, no Terminal.Gui/NATS references:
- `const string DefaultPattern = @"^(\$|_INBOX\.)"`
- `string Pattern` - the text of the expression in effect.
- `static bool IsValid(string pattern)` - does it compile (empty is valid).
- `bool TryApply(string pattern)` - compiles; on success swaps the active regex, updates `Pattern`,
  returns `true`; on failure changes nothing and returns `false`.
- `bool IsExcluded(string subject)` - `false` for an empty expression, otherwise `regex.IsMatch`.
- `ExclusionEditState Classify(string text)` - `Invalid` if `!IsValid(text)`, else `Applied` if
  `text == Pattern` (ordinal), else `Pending`. Invalid takes precedence, per spec. Keeping the
  three-way decision here (rather than inline in the tab) makes it unit-testable; the tab only
  maps the state to colors/enablement.

The active `Regex?` is held in a single field (`null` = empty expression = exclude nothing) and
swapped by reference assignment, marked `volatile` so reader tasks see the latest value without
locking. Reference assignment is atomic; readers take one snapshot per message, so there's no torn
state. This keeps the registry's "no locking, UI-thread-only mutation" posture. `Pattern` is only
read on the UI thread.

*Alternatives:* an `IObservable<Regex>` pipe into the registry (more machinery for a single mutable
value); putting the check in the feed pipeline (`FeedReaderLoop`) instead of the registry (would
also swallow `$TRACE` rows, and spends dedup/batching work on messages about to be dropped);
apply-on-every-keystroke with last-valid fallback (the original draft - rejected because
intermediate valid text such as a bare `^` matches every subject and briefly hides everything).

### 2. Regex options: `CultureInvariant`, case-sensitive, not `Compiled`, with a match timeout
NATS subjects are case-sensitive, so no `IgnoreCase`. Plain (non-`Compiled`) regex is already the
project's verified AOT-safe choice (see `RegexExtensions`). A short match timeout (e.g. 50 ms)
guards the reader loop against a pathological user expression; a `RegexMatchTimeoutException`
is treated as "not excluded" (fail open - showing a message is safer than silently hiding it, and
never faults the subscription).

### 3. Registry consults the filter; `SubscriptionRegistry` takes it via constructor
`Program.cs` constructs one `SubjectExclusionFilter`, passes it to `SubscriptionRegistry` and
registers it as a singleton so `MainWindow` can hand it to `SubscribeTab`. In `RunAsync` the two
prefix booleans are deleted and replaced by `if (_exclusion.IsExcluded(message.Subject)) continue;`
ahead of the client-side pattern match (where the old check lived).

### 4. Subscribe tab layout
```
Subscriptions
[EditFrame: SubscriptionsView, Height = Dim.Fill(4)]
Excluded Subjects (regex) *                   <- "*" only while Pending/Invalid
[EditFrame: TextField, Height = 3] [ Apply ]  <- anchored at bottom
```
The label and 3-row frame (same single-line `EditFrame` height as `PublishDialog`'s Subject field)
are positioned with `Pos.AnchorEnd(...)`; the list frame's height fills down to them. The frame's
width is `Dim.Fill()` minus the button's width; the button sits right of it on the frame's middle
(text) row. `TextField` is pre-filled with `filter.Pattern`.

### 5. State rendering
One `UpdateExclusionState()` runs on `ValueChanged` and after apply/revert:
`state = filter.Classify(text)`, then
- field scheme: Invalid → red on `EditableBackground` (same pattern as `PatternDialog`/
  `HeaderDialog`); Pending → new `Theme.PendingEditColor` (BrightYellow) on `EditableBackground`;
  Applied → `SetScheme(null)` (inherited default).
- `applyButton.Enabled = state == Pending`.
- label text gets a trailing ` *` unless `Applied`.

The `*` marker plus the button's enabled state carry the same information as the color, so the
state is readable on colorless terminals and in `tmux capture-pane` text. `PendingEditColor` is a
new `Theme` constant rather than a reuse of the existing BrightYellow accents, per `Theme.cs`'s
one-constant-per-usage-site convention.

### 6. Apply / revert key handling
- **Enter** in the field: handle the `TextField`'s `Accepting` and mark it handled (so it doesn't
  bubble up the view hierarchy); if `state == Pending`, `TryApply(text)`; Invalid/Applied are
  no-ops.
- **Apply button**: `Accepting` → same apply. Applying disables the button, and a disabled view
  can't hold focus, so focus is moved back to the field explicitly afterwards rather than left to
  Terminal.Gui's fallback.
- **Esc** in the field: a `KeyDown` handler on the field for `Key.Esc` resets `Text = filter.Pattern`
  and marks the key handled. Only handled when the field isn't already Applied, so an Esc with
  nothing to revert keeps whatever app-level meaning it has.
- Focus leaving the field does nothing (spec: pending edit is kept).

The apply action is wrapped in one private domain-named method (e.g. `ApplyExclusion()`), since
both Enter and the button call it.

### 7. Focus order (found during tmux verification)
- `Enabled = false` alone does not remove a `Button` from the Tab order in Terminal.Gui v2, so
  Tab dead-ended on the disabled Apply. The button's `CanFocus` is toggled together with `Enabled`.
- As a plain `TabStop`, the Subscribe tab let Tab from the field leave for the tab header, and Tab
  from the header re-entered the content at its most recently focused subview (the field), so the
  list became unreachable by Tab after the first visit to the field. This is the first tab whose
  content has more than one permanent Tab stop, which is why it hadn't come up before - but it's a
  property of how tabs are hosted, not of this tab, so the fix is general: `TabbedView.AddTab` sets
  `content.TabStop = TabBehavior.TabGroup` for every tab, keeping Tab cycling within the selected
  tab's own controls (tab-navigation's new "Tab Cycles Within the Selected Tab's Content"). The
  header stays reachable via Up. *Alternatives:* setting it per tab (opt-in, easy to forget on the
  next multi-stop tab) or in a shared tab base class (there is none - every tab derives straight
  from `View`; `AddTab` is the one path they all share). Side effect: Tab on a single-stop tab
  (Streams/Values/Objects/Templates lists) now stays put instead of bouncing to the header.
  `ManagementTabs`' FilterBox Tab-exit special case runs before the base advance and is unaffected
  (verified on Values and Objects).
- Tab and Shift+Tab both advance forward (`TabbedView.AdvanceWithinContent` is
  direction-agnostic), so "Shift+Tab goes back" isn't a thing here; with at most three stops the
  cycle is short either way.

### 8. Shortcut scoping
`SubscribeTab` currently forwards any unhandled key matching a `TabOperations` entry and reports
those operations via `IShortcutSource` (surfaced in the `?` Shortcuts picker) unconditionally. With a text input and button on the same
tab, both are gated on `_subscriptionsView.HasFocus`: `Shortcuts` yields nothing and
`OnKeyDownNotHandled` doesn't dispatch while the exclusion field or Apply button is focused.

## Risks / Trade-offs

- [Users who subscribed to `$SYS.>` / `_INBOX.>` silently see nothing after upgrading] → Spec
  documents the migration; the filter is visible directly under the list, which is the point of
  the change. Worth a mention in release notes.
- [Forgotten pending edit - user types, tabs away, and assumes it's live] → Yellow text, `*` label
  marker, and an enabled Apply button all persist while the edit is pending.
- [Every keystroke compiles a regex for validation] → Negligible cost for a single short
  expression; no debounce needed.
- [Esc handling in the field conflicting with an app-level Esc binding] → Handled only when there's
  something to revert; verify in tmux that Esc still behaves as before when the field is Applied.
- [Catastrophic backtracking] → Match timeout, fail open (Decision 2).
