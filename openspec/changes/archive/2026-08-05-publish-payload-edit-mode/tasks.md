## 1. Make `IApplication`/`ShortcutTracker` available via DI

- [x] 1.1 In `Program.cs`, move `var app = Application.Create();` (and `ApplyColorTheme()`, which
      already depends on it) before `Services.Configure(services)`.
- [x] 1.2 Register `app` as an `IApplication` singleton and register `ShortcutTracker` (or a
      factory constructing it from the registered `IApplication`) in the `ServiceCollection`
      before `Services.Configure(services)` is called.
- [x] 1.3 Confirm `dotnet build src/lazynats.sln` still succeeds and the app still starts after
      the reordering (no other startup step implicitly depended on the old order).

## 2. Add refresh-without-focus-change support to `ShortcutTracker`

- [x] 2.1 Add a public `Refresh()` method to `ShortcutTracker` (`ShortcutAggregator.cs`) that
      re-runs the same recompute-and-raise logic as `OnFocusedChanged`, callable on demand.
- [x] 2.2 Verify `ShortcutsChanged` fires with the up-to-date aggregated list when `Refresh()` is
      called without any focus change having occurred.

## 3. Wire `ShortcutTracker` into `MainWindow`'s `StatusBar`

- [x] 3.1 In `MainWindow`, resolve `ShortcutTracker` via
      `Services.Root.GetRequiredService<ShortcutTracker>()`.
- [x] 3.2 Convert each `ShortcutHint` from `ShortcutTracker.ShortcutsChanged` into a `Shortcut`
      (`Text`/`Key`/`Action`, mirroring how `quitShortcut`/`subscribeTabShortcut`/etc. are already
      built) and keep the `StatusBar`'s displayed set in sync as the event fires.
- [x] 3.3 Leave the existing fixed shortcuts (`quitShortcut`, `subscribeTabShortcut`,
      `publishTabShortcut`, `clearShortcut`, `publishStatusShortcut`) and their existing
      show/hide wiring untouched — the dynamic set is additive, not a replacement.
- [x] 3.4 Dispose `ShortcutTracker`'s subscription appropriately (it implements `IDisposable`) so
      `MainWindow` doesn't leak the `FocusedChanged` handler.

## 4. Add the Payload Navigate/Edit gate to `PublishTab`

- [x] 4.1 Add a private editing-state field to `PublishTab` (default: Navigate) tracking whether
      Payload is currently in Edit mode.
- [x] 4.2 Hook `_payloadView.KeyDown` (confirming during implementation that `TextView` raises a
      cancelable `KeyDown` before its own internal key handling — flagged as an open question in
      design.md) to intercept keys based on the current mode:
      - Navigate + Up/Down/Tab/Shift-Tab → move keyboard focus to Headers/Subject/Send as
        appropriate (mirroring the tab's existing focus order) and mark the event handled.
      - Navigate + Ctrl+E or Enter → switch to Edit mode, mark the event handled.
      - Navigate + any other key → mark the event handled and do nothing (swallow it; Payload's
        text must not change while in Navigate).
      - Edit + Esc → switch back to Navigate mode (focus stays on `_payloadView`), mark the event
        handled.
      - Edit + any other key → leave unhandled so `TextView`'s existing behavior runs unchanged.
- [x] 4.3 Ensure focus landing on Payload via Tab/Shift-Tab always starts in Navigate mode (reset
      the field, don't carry over a stale Edit state from a previous focus visit).
- [x] 4.4 Call the injected `ShortcutTracker.Refresh()` (see Task 2.1) every time the mode toggles,
      since focus doesn't move and `FocusedChanged` won't fire on its own.

## 5. Advertise the gate's shortcuts

- [x] 5.1 Make `PublishTab` implement `IShortcutSource`, exposing `Ctrl+E`/`Enter`: "Edit" while
      Payload is in Navigate mode, and `Esc`: "Stop Editing" while in Edit mode (empty/omitted
      when Payload isn't the focused field, matching `ListEditorView<T>`'s existing pattern).

## 6. Verify

- [x] 6.1 Manually run the app (`dotnet run --project src/lazynats`) and confirm: Tab reaches
      Payload in Navigate mode; Up/Down move focus to Headers/Send; Ctrl+E and Enter both enter
      Edit; typing only changes text in Edit mode; Esc returns to Navigate without losing focus;
      the `StatusBar` shows the Ctrl+E/Esc hint and updates immediately on toggle.
- [x] 6.2 Confirm Headers' Ctrl+N/E/D hints now also appear in the `StatusBar` when Headers is
      focused, with no behavior change to Headers itself.
- [x] 6.3 Confirm Subject's and Headers' own Tab/arrow navigation is unchanged from before this
      change. (Found a Tab-key-dropped quirk while verifying; confirmed via `git stash` bisection
      it reproduces identically without this change's code, so it's pre-existing, not a
      regression. Tracked in `TODO.md`.)
