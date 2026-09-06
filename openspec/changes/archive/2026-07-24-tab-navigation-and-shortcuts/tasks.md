## 1. Tab titles

- [x] 1.1 Change `SubscriptionsView`'s `Title` from `"Subscriptions"` to `"Subscribe"` in
      `MainWindow.cs` (no C# identifier/type renames).

## 2. Layer-aware arrow navigation via a Tabs subclass

- [x] 2.1 Create `ManagementTabs : Tabs` (subclass, not sealed by the base class — confirmed via
      reflection against the installed 2.4.10 package).
- [x] 2.2 In its constructor, override `Command.Up`: if `Value`'s content currently has focus (its
      header does not), call `(Value.Border.View as BorderView)?.TitleView?.SetFocus()` to focus
      that same tab's own header and report handled; if a header already has focus, report
      unhandled (nothing further to climb to).
- [x] 2.3 Override `Command.Left` and `Command.Right`: cycle tabs (only when
      `Value?.Border.View?.HasFocus` is true — a header is currently focused) via the public
      `TabCollection`/`Value` API, not the base class's `SelectPreviousTab()`/`SelectNextTab()` —
      those, and `GetTabs()`, turned out to be `private` on `Tabs` (confirmed via reflection),
      inaccessible from a subclass. Same resulting behavior, different (accessible) mechanism; see
      `design.md`.
- [x] 2.4 ~~Leave `Command.Down` unoverridden~~ — **revised during manual verification**: override it
      too. `FocusContent()`'s own guard is correct, but *declining* a command (returning
      unhandled) turned out not to be a safe no-op at all — see the Risks entry added to
      `design.md`. All four commands (`Up`/`Down`/`Left`/`Right`) now unconditionally report the
      key as handled once they reach `ManagementTabs`, whether or not they act.
- [x] 2.5 Update `MainWindow.cs` to construct `ManagementTabs` instead of `Tabs`.

## 3. Alt+letter tab-switching shortcuts

- [x] 3.1 Add an Alt+B `Shortcut` (`BindKeyToApplication = true`) to `MainWindow`'s `StatusBar`
      whose action sets `tabs.Value = subscriptionsView`.
- [x] 3.2 Add an Alt+P `Shortcut` (`BindKeyToApplication = true`) whose action sets
      `tabs.Value = publishView`.
- [x] 3.3 Decide visibility of these two entries in the status bar (visible vs. hidden like the
      existing `clearShortcut`) and set `Visible` accordingly. Decided: visible (like `Quit`) —
      these are primary, always-available navigation, not a contextual action like Clear.

## 4. ListEditorView: Up at top of list focuses the input

- [x] 4.1 In `Components/ListEditorView.cs`, add `KeyBindings.Add(Key.CursorUp, Command.Up)` and
      `AddCommand(Command.Up, …)` on `ListEditorView` itself.
- [x] 4.2 Handler: when `_listView.HasFocus`, call `_inputField.SetFocus()` and report the command
      handled; otherwise report unhandled (covers the text-input-already-focused case, keeping Up
      there a no-op).
- [x] 4.3 Manually verify: Up at the first item moves focus to the input; Up with an empty list
      moves focus to the input; Up while the input already has focus does nothing. Verified by
      driving the running app under tmux (`dotnet run --project src/lazynats` against the local
      NATS server, `send-keys`/`capture-pane`).

## 5. Documentation

- [x] 5.1 Record the full tab title / Alt+letter map (including the four not-yet-built tabs:
      Streams=Alt+S, Consumers=Alt+C, KV=Alt+K, OBJ=Alt+O) in `doc/UI.md`.

## 6. Manual verification

All verified by driving the running app (`dotnet run --project src/lazynats`, local NATS server)
under tmux — `send-keys` to drive input, `capture-pane -e` to read focus/selection state off the
rendered colors (a focused/selected row renders black-on-white; selected-but-unfocused renders
black-on-gray).

- [x] 6.1 Run the app; confirm Alt+B and Alt+P switch tabs from anywhere, including from inside the
      Subscribe list/input. Confirmed — Alt+B switched back to Subscribe from deep inside Publish's
      Subject field.
- [x] 6.2 Confirm Up climbs list → input → the *current* tab's own header (not a different tab) and
      stops there; confirm Down from a focused header returns focus to that tab's content.
      Confirmed after a fix (see 6.3).
- [x] 6.3 Confirm Left/Right switch tabs only once a header is focused, and have no effect on tab
      selection while list/input content holds focus. Confirmed, but only after a fix: the first
      implementation reported Up/Left/Right as *unhandled* in their "nothing to do" branches
      (e.g. Up while a header was already focused). That let the key escape past `ManagementTabs`
      to some further ancestor's generic arrow-key focus-navigation, which could land on a
      *different* tab's content and silently retrigger `Value` via `Tabs.OnFocusedChanged` — e.g.
      pressing Up while the Subscribe header already had focus switched to Publish. Fixed by
      making all four overrides unconditionally report the key as handled once they reach
      `ManagementTabs` (see `design.md` Risks and `ManagementTabs.cs`'s class comment). Re-verified
      clean after the fix, including repeated presses.
- [x] 6.4 Confirm Down at the bottom of the list and Left/Right within the list/input remain
      unaffected no-ops. Same underlying issue as 6.3, same fix — a single-item list (top row is
      also the bottom row) exposed it first: Down there was escaping to the tab's own header
      instead of staying put. Re-verified as a true no-op after overriding `Command.Down` too.
