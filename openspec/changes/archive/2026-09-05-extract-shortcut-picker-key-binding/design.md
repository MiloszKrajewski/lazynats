## Context

`MainWindow` (`MainWindow.cs` ~L145-158) and `MessageDetailDialog` (`MessageDetailDialog.cs`
~L172-190) each independently implement "pressing `?` opens the aggregated shortcut picker for
whatever's currently relevant, and running the selected entry": defer via
`App.AddTimeout(TimeSpan.Zero, ...)` to escape the still-unwinding `KeyDown` dispatch, call
`ShortcutAggregator.Collect(startView)`, construct and `Run` a `ShortcutPickerDialog`, then invoke
`picker.Result?.Action()`. The code is near-identical except for one thing: what view they pass as
`startView` into `Collect`.

`Collect` only walks *upward* (`view.SuperView` chain) from the view it's given — it never
descends into children. That makes the choice of start view load-bearing, not stylistic:

- `MainWindow` passes `App.TopRunnableView?.MostFocused` — the actual keyboard focus, wherever it
  is app-wide (several levels deep inside whichever tab is active). MainWindow itself never
  implements `IShortcutSource`, so its own hardcoded top-level shortcuts are structurally excluded
  from the walk rather than filtered out after the fact.
- `MessageDetailDialog` passes `this` (itself) — confirmed via tmux that `this.MostFocused`, in
  this dialog's no-explicit-initial-focus state, resolves to internal adornment scaffolding that
  isn't even reachable back to `this` via `SuperView`, so `Collect` would come back empty. Passing
  `this` directly works only because the dialog's own `IShortcutSource` is the sole source
  anywhere in its subtree — it has no focusable `IShortcutSource`-implementing descendant whose
  shortcuts would otherwise be missed by not starting from a focused leaf.

A future dialog that embeds something like `ListEditorView` (which does implement
`IShortcutSource`) and sets real initial focus on a child would need `this.MostFocused`, not
`this` — the opposite of `MessageDetailDialog`'s choice. `PublishDialog` turned out to be exactly
this case: its `HeaderEditorView` (a `ListEditorView`) is a real, focusable `IShortcutSource`
descendant, so `PublishDialog` needs `this.MostFocused`, not `this`. So the extraction must keep
this a decision each call site *can* make — though see the `ResolveStartView` decision below for
why, once a third caller needing the opposite policy from `MessageDetailDialog` actually existed,
it stopped being worth hand-reasoning per call site.

## Goals / Non-Goals

**Goals:**
- One place owns the deferred-collect-run-invoke sequence and its re-entrancy reasoning.
- Each call site ends up with the correct picker start-view logic, whether by supplying its own
  (`MainWindow`) or by relying on the helper's runtime-resolved default (`MessageDetailDialog`,
  `PublishDialog`) — see the `ResolveStartView` decision below.
- `MainWindow`'s existing `ShortcutHint`/status-bar-widget/click-support wiring for `?` keeps
  working unchanged.
- A future `IShortcutSource`-implementing dialog can wire up `?` with a single call, no copied
  comment block — confirmed by `PublishDialog` needing exactly one line
  (`ShortcutPickerLauncher.BindKey(this)`) to gain working `?` support.

**Non-Goals:**
- No change to `ShortcutAggregator.Collect`'s upward-only walk semantics.
- No change to which shortcuts are visible where (`shortcut-picker` spec's exclusion of top-level
  shortcuts, alphabetical ordering, etc. are all unaffected).
- Not retrofitting `?` onto dialogs that don't implement `IShortcutSource` (directly or via a
  descendant) at all, e.g. `CreateStreamDialog` — there's still nothing for those to surface.
  `PublishDialog` doesn't fall in this bucket, though: its `HeaderEditorView` already implements
  `IShortcutSource`, so once `BindKey`'s auto-resolving default (see `ResolveStartView` below)
  made wiring it up a one-line, zero-hand-reasoning call, doing so stopped being a scope
  expansion and became the natural proof that the default generalizes past the original two call
  sites.

## Decisions

**Parameterize by `Func<View?> startView`, not a fixed policy.** Considered hardcoding "always
`MostFocused`" or "always `this`" into the helper and picking whichever fits more call sites
today (2-1 in `this`'s favor). Rejected: the two existing call sites need genuinely different
policies for correctness (see Context), and a helper that hardcodes one would either dead-end
`MainWindow` or silently under-collect for the next dialog with real descendant focus. A lazily-
evaluated `Func<View?>` (not a plain `View?` snapshot) is `MakeAction`, or plain `BindKey` at
subscription time — this matters because `MainWindow` builds its `ShortcutHint` once at startup
but the actual current focus target obviously changes call to call.

**`MakeAction`/`BindKey` take the owning `View`, not an `IApplication` (in any shape) —
discovered during implementation, went through two iterations before landing here.** The first
cut took a plain `IApplication app` parameter, matching the literal signature this design
originally sketched. That crashed (`NullReferenceException`) the moment `?` was pressed from
`MainWindow`: confirmed via tmux that `Runnable.App` is still null when `MainWindow`'s constructor
runs (it's only set once `App.Run<T>()` is called in `Program.cs`, after construction completes),
so `MakeAction(App!, ...)` captured that still-null reference at `topLevelShortcuts`-build time.
The original inline code never hit this because `App!.AddTimeout(...)` was itself inside the
lambda the `ShortcutHint`'s `Action` wrapped — `App!` was only read when the action actually ran,
by which point `MainWindow` was the running top-level. The second cut fixed this by making the
parameter a `Func<IApplication>` (`MainWindow` passing `() => App!`, `MessageDetailDialog` passing
`() => app` around its already-non-null DI-resolved reference) — restoring "read at invocation
time" laziness at the cost of every caller having to wrap its own `App`/`app` reference in a
closure just to get that laziness back, even `MessageDetailDialog`, which never had the hazard in
the first place.

The shipped form drops the `IApplication` parameter (in either shape) entirely: `MakeAction(View
owner, Func<View?> startView)` and `BindKey(View owner, ...)` take the `View` itself and read
`owner.App!` *inside* the deferred closure. `owner` is already a plain (non-lazy) reference every
caller has on hand regardless — `this` for `MessageDetailDialog`/`PublishDialog`, `this` for
`MainWindow` itself (not `App!`, which is what was actually null) — so deferring the *property
read* on it, rather than asking each caller to additionally construct a `Func<IApplication>`
around a value it already had, gets the identical laziness for one less parameter and no
caller-side wrapping.

**Two entry points, not one.** `MakeAction(app, startView) : Action` returns just the
deferred-collect-run-invoke closure, for callers that already have their own key-to-action
dispatch (`MainWindow`'s `topLevelShortcuts` loop, which also needs that same `Action` for the
status-bar `Shortcut` widget's click handler). `BindKey(owner, app, startView)` is the convenience
wrapper most future callers actually want: it subscribes `owner.KeyDown`, matches the `?` key,
sets `key.Handled = true`, and calls `MakeAction(...)`. `BindKey` is implemented in terms of
`MakeAction` so there's still exactly one copy of the core sequence.
- Alternative considered: only `BindKey`, and have `MainWindow` also bind fresh instead of folding
  `?` into `topLevelShortcuts`. Rejected — that would drop `?` from the status bar's mouse-clickable
  widget list and from the single-source-of-truth list the design.md of `add-shortcut-picker`
  established `topLevelShortcuts` to be; `MakeAction` alone preserves that.

**`BindKey`'s `startView` is optional, defaulting to a runtime-resolved `ResolveStartView`
heuristic — added once a third caller (`PublishDialog`) existed, superseding the "each call site
must always supply its own policy" framing above.** With only `MessageDetailDialog` needing `()
=> this` and `MainWindow` needing `MostFocused` (via `MakeAction`, not `BindKey` — see above),
2-1 in favor of always requiring an explicit argument looked justified, and the "Parameterize by
`Func<View?>`, not a fixed policy" decision above rejected hardcoding either one into the helper.
Wiring `?` onto `PublishDialog` (see the Non-Goals note above) needed `this.MostFocused` — the
opposite of `MessageDetailDialog` — which meant `BindKey` calls would keep splitting down the
middle forever as more `IShortcutSource`-implementing dialogs showed up, each needing its own
hand-reasoned comment block explaining which policy applies and why (exactly the copy-paste this
whole extraction exists to kill).

`ResolveStartView(View owner)` replaces that per-caller reasoning with one runtime check: walk
`owner.MostFocused` upward via `SuperView` looking for `owner` itself. If the walk reaches
`owner`, `owner.MostFocused` is a genuine focusable descendant on the path back up to `owner` —
use it (the `PublishDialog`/`MainWindow` case). If the walk dead-ends at `null` first (no
`SuperView` link all the way up — true for `MessageDetailDialog`, whose only focus candidates are
`CanFocus=false` or Terminal.Gui's internal adornment scaffolding, which isn't linked into the
ordinary parent-child chain), fall back to `owner` itself. This isn't the fixed policy the earlier
decision rejected — it's still computed per call, per current focus state, not hardcoded to one
answer — it just removes the need for a *caller* to reason about which answer applies, since the
one thing that actually determines it (does the walk from current focus reach back to `owner`?) is
exactly what the check tests. `BindKey`'s `startView` parameter stays optional-but-overridable, not
removed, for a hypothetical future caller whose correct policy isn't expressible as "does
`MostFocused` reach `owner`" (none exists today).

**A shared `Key` constant on the helper.** `new Key('?')` currently appears at both call sites (and
would appear again at every future one). Exposing it once (e.g. `ShortcutPickerLauncher.Key`) means
`BindKey` uses it internally and `MainWindow`'s `ShortcutHint` construction references the same
constant instead of its own literal — one definition of "what key this is" instead of N.

**Location: `Components/ShortcutPickerLauncher.cs`.** Alongside `ShortcutAggregator.cs`/
`IShortcutSource.cs`/`ShortcutPickerDialog.cs`, following the existing convention that
shortcut-picker plumbing lives together in `Components/`.

**Generic reasoning moves to the helper; call-site-specific reasoning stays at the call site.**
The "why `AddTimeout(Zero, ...)`", "why raw `KeyDown` and not `AddCommand`/`Command.Context`", and
"why `Collect` only walks upward" explanations are the same regardless of caller — they move to
`ShortcutPickerLauncher`'s own comments, referenced (not repeated) from call sites. With
`ResolveStartView` now covering both known policies automatically, `MessageDetailDialog` and
`PublishDialog` no longer need a per-caller comment justifying a manually-chosen `startView` — each
keeps a short local comment instead explaining *why the default is correct for it* (which of
`ResolveStartView`'s two branches it lands in, and why). `MainWindow` still supplies its own
explicit `startView` (it uses `MakeAction` directly, not `BindKey`, and its correct start point —
app-wide `MostFocused`, not scoped to itself as `owner` — isn't what `ResolveStartView` computes
anyway), so it keeps a comment explaining that explicit choice, same as before.

## Risks / Trade-offs

- **Multiple `KeyDown` subscribers on the same view, ordering.** `BindKey` adds one more `KeyDown`
  subscriber to whatever view it's called on. If a call site later adds another handler that
  should get first refusal at some other key, subscription order (not the helper) determines
  precedence. Mitigation: this is pre-existing behavior (all three call sites already rely on
  Terminal.Gui's depth-first, focused-child-first dispatch for correctness against text-field
  input), not something the extraction changes or worsens — just keep `BindKey` calls positioned
  the same way the current hand-rolled subscriptions are (after field-level bindings are set up).
- **A future dialog needs a `startView` policy `ResolveStartView` doesn't compute correctly and
  silently under-collects.** `ResolveStartView`'s "does `MostFocused` reach back to `owner`"
  check covers both policies known today, but the helper still doesn't validate what a caller's
  own explicit `startView` override returns — a caller supplying one could still pass e.g. `this`
  when it actually needed `this.MostFocused` (or vice versa), reproducing the empty-picker failure
  mode `MessageDetailDialog`'s own comment describes hitting during development, before
  `ResolveStartView` existed. Mitigation: prefer the default (omit `startView` entirely) unless a
  new call site's correct policy genuinely isn't "does `MostFocused` reach `owner`" — `MainWindow`
  is the only caller today with that property (it calls `MakeAction` directly, scoped to the whole
  app rather than to itself as `owner`) — and verify any new explicit override via tmux the same
  way `MessageDetailDialog`'s original choice was confirmed.

## Migration Plan

Three-call-site refactor (`MainWindow`, `MessageDetailDialog`, and — once `ResolveStartView` made
it a one-line, no-hand-reasoning addition — `PublishDialog`), no data/state migration. Land as one
change: add the helper, switch all three call sites to it, remove the now-dead duplicated code and
comments. Rollback is a plain revert if `?` regresses at any site.
