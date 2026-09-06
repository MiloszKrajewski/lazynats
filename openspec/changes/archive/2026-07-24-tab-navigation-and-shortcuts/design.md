## Context

`MainWindow` hosts a `Terminal.Gui.Views.Tabs` with two tabs today (`SubscriptionsView`,
`PublishView`); `doc/UI.md` describes four more (Streams, Consumers, KV stores, OBJ stores) not
yet built. Two problems surfaced while planning that growth:

1. **Naming/shortcuts.** "Subscriptions" (noun) reads asymmetrically next to "Publish" (verb) and
   collides on its first letter with the planned "Streams" tab.
2. **Arrow-key leak.** Fetched from the installed Terminal.Gui source
   (`tui-cs/Terminal.Gui`, `Tabs.cs`): the constructor unconditionally does

   ```csharp
   KeyBindings.Add(Key.CursorUp, Command.Up);
   KeyBindings.Add(Key.CursorDown, Command.Down);
   KeyBindings.Add(Key.CursorLeft, Command.Left);
   KeyBindings.Add(Key.CursorRight, Command.Right);
   ```

   Terminal.Gui's key routing walks an unhandled key up the SuperView chain, so any arrow key a
   focused child doesn't fully consume falls through to `Tabs` and switches tabs. This reproduces
   today via `SubscriptionsView` → `ListEditorView` → `Terminal.Gui.Views.ListView`, which only
   consumes Up/Down while there's somewhere to move (never Left/Right at all), but it is a
   property of `Tabs` itself, not of the list — every future list-bearing tab inherits it unless
   fixed at the `Tabs` level.

   Reading further, `Tabs` already implements a two-level focus model for exactly this situation
   (`NavCommandHandler`, for the default `TabSide.Top`):

   ```csharp
   Side.Top or Side.Bottom when ctx.Command == Command.Right => SelectNextTab(),
   Side.Top or Side.Bottom when ctx.Command == Command.Left  => SelectPreviousTab(),
   Side.Top    when ctx.Command == Command.Down => FocusContent(),
   Side.Top    when ctx.Command == Command.Up   => SelectPreviousTab(),
   ```

   `FocusContent()` self-guards (`if (!(Value?.Border.View?.HasFocus ?? false)) return false;`),
   so Down only acts when a header already has focus — it's already safe as shipped. But
   `SelectPreviousTab()`/`SelectNextTab()` (used for Up and for Left/Right) don't check which
   layer triggered them: Up always jumps to the *previous* tab's header (wrapping around) rather
   than the current tab's own header, and Left/Right switch tabs whether they came from a
   genuinely-focused header or bubbled up unhandled from content — the same leak as Up, just not
   yet noticed. `BorderView` and `TitleView` (`Terminal.Gui.ViewBase.BorderView`/`TitleView`,
   confirmed `public` via reflection against the installed 2.4.10 assembly) are what a header
   actually is — `BorderView.TitleView` is a focusable `View`, and `SelectNextTab`/
   `SelectPreviousTab` work by calling `.SetFocus()` on it.

## Goals / Non-Goals

**Goals:**
- Tab titles read as a matched verb pair and each management tab gets a unique, collision-free
  Alt+letter shortcut.
- Tab switching happens directly via the Alt+letter shortcuts from anywhere, plus indirectly
  through a layered arrow-key climb: content → text input → the *current* tab's own header; once a
  header holds focus, Left/Right cycle between headers (switching tabs) and Down returns focus to
  that tab's content.
- Arrow keys never switch tabs as a side effect of merely being left unhandled by content — tab
  switching via arrows only happens once a header is genuinely focused.
- `ListEditorView`'s list makes productive use of the "Up at the top row" case by moving focus to
  its text input, which composes with the `Tabs` change above: Up left unhandled there continues
  climbing to the tab header.

**Non-Goals:**
- No C# identifier/type/file renames (`SubscriptionsView`, `SubscriptionRegistry`, etc. keep their
  names) — this change is UI-label and key-binding only, per the proposal's scope.
- No implementation of the Streams/Consumers/KV/OBJ tabs themselves; this change only reserves
  their Alt+letter shortcuts and establishes the convention they'll follow.
- No change to `ListEditorView`'s Down-at-bottom, Ctrl+N/E/D, or validation behavior beyond the
  new Up-at-top case.

## Decisions

- **Tab titles**: change `SubscriptionsView`'s `Title` from `"Subscriptions"` to `"Subscribe"` in
  `MainWindow.cs`. `PublishView`'s `Title` stays `"Publish"`.

- **Alt+letter map** (reserved now, only first two wired to a tab today):

  | Tab | Title | Shortcut |
  |---|---|---|
  | Subscribe | `Su[b]scribe` | Alt+B |
  | Publish | `[P]ublish` | Alt+P |
  | Streams *(future)* | `[S]treams` | Alt+S |
  | Consumers *(future)* | `[C]onsumers` | Alt+C |
  | KV *(future)* | `[K]V` | Alt+K |
  | OBJ *(future)* | `[O]BJ` | Alt+O |

  Recorded in `doc/UI.md` so the convention survives until those tabs exist.

- **Wiring the shortcuts**: reuse the existing pattern already proven for Quit in `MainWindow`
  (`Shortcut` with `BindKeyToApplication = true`, added to the `StatusBar`) rather than inventing a
  second mechanism. Alt+B sets `tabs.Value = subscriptionsView`, Alt+P sets
  `tabs.Value = publishView`. Both entries are visible in the status bar (like `Quit`), not hidden
  like the contextual `clearShortcut` — they're primary, always-available navigation rather than a
  situational action, so they earn permanent status bar space.

- **Layer-aware arrow navigation via a `Tabs` subclass**: introduce a small subclass
  (`ManagementTabs : Tabs`) so its constructor can call the protected `AddCommand` to *replace* the
  base class's handlers for `Command.Up`, `Command.Down`, `Command.Left`, and `Command.Right`. A
  subclass is required here, not a `KeyBindings` edit on a plain `Tabs` instance from `MainWindow`,
  because `AddCommand` is `protected` (per `doc/terminal-gui-howto.md` gotcha #4) and because the
  fix is about *what a command does*, not which physical key triggers it.

  - `Command.Up`: if `Value`'s content currently has focus (its header does not), focus that same
    tab's own header — `(Value.Border.View as BorderView)?.TitleView?.SetFocus()` — instead of the
    base class's `SelectPreviousTab()`. If a header already has focus, there's nothing further up
    to climb to.
  - `Command.Down`: if a header currently has focus, return focus to that tab's content
    (`Value?.Border.View.HasFocus = false; Value?.SetFocus();` — see implementation note below on
    why this differs slightly from the base class). If content already has focus, there's nothing
    to do.
  - `Command.Left` / `Command.Right`: only cycle tabs when a header currently has focus
    (`Value?.Border.View?.HasFocus`); otherwise there's nothing to do.

  **Implementation note (mechanism)**: the base class's own `SelectPreviousTab()`/
  `SelectNextTab()`/`GetTabs()` are `private`, not accessible from a subclass — discovered via
  reflection while implementing. `Command.Left`/`Right` instead reimplement the cycle directly
  against the public `TabCollection`/`Value` API: find `Value`'s index in `TabCollection.ToList()`,
  wrap to the neighboring index, `Value = tabs[nextIndex]`, then re-focus that tab's header (the
  `Value` setter's own auto-focus would otherwise push focus into the new tab's *content*).
  `Command.Down` similarly can't call the base class's `RestoreFocus()` (also not accessible — see
  Risks) and uses the public `SetFocus()` instead, at the cost of always landing on the tab's
  default focus target rather than exactly where focus was before the header was entered. Same
  observable behavior as originally planned in each case, different (accessible) mechanism.

  **Implementation note (all four commands unconditionally report handled)**: see Risks — this
  was not the original plan (`Command.Down` was meant to stay unoverridden, and the others were
  meant to report unhandled when they had nothing to do) but manual testing showed that was unsafe.

  `MainWindow` constructs `ManagementTabs` instead of `Tabs`; nothing else about its usage changes
  (`Add`, `.Value`, `Title`-per-SubView all work the same).

- **`ListEditorView` Up-at-top-of-list**: add a `Command.Up` binding on `ListEditorView` itself
  (its `KeyBindings.Add(Key.CursorUp, Command.Up)` / `AddCommand(Command.Up, …)`, same pattern
  already used there for `Command.New/Edit/DeleteAll`). Terminal.Gui only offers this binding a
  chance once the focused `_listView` has itself left the key unhandled — i.e. exactly when the
  list is at (or has nothing above) the top row, since Terminal.Gui.Views.ListView already
  consumes Up whenever there's somewhere to move. The handler moves focus to `_inputField` and
  reports handled only when `_listView.HasFocus`; if `_inputField` already has focus (it doesn't
  consume Up itself), the same binding sees the bubbled key but declines it — which is exactly
  what lets it keep bubbling up to `ManagementTabs`' own `Command.Up` handling above, completing
  the list → input → header climb.

## Risks / Trade-offs

- [**Found and fixed during manual verification.** Reporting an overridden command as *unhandled*
  when it had nothing to do is not a safe no-op] → the original plan had `Command.Up`/`Left`/
  `Right` return unhandled (`false`) in their "nothing to do" branches (header already focused for
  Up; header not focused for Left/Right), and left `Command.Down` unoverridden entirely on the
  assumption that the base class's own `FocusContent()` guard was sufficient. Driving the running
  app under tmux showed that an unhandled arrow key doesn't stop at `Tabs` — it keeps walking up
  Terminal.Gui's key-routing chain to some further ancestor's own generic arrow-key
  focus-navigation, which can land on a *different* tab's content and silently retrigger `Value`
  via `Tabs.OnFocusedChanged`. Concretely: pressing Up while a header already had focus switched
  tabs instead of staying put, and pressing Down at the bottom of a single-item list (where the top
  row is also the bottom row) did the same. This is the exact arrow-key tab-switch leak the whole
  change exists to close, reopened via the decline path. Fixed by making all four overrides on
  `ManagementTabs` unconditionally report the key as handled once they reach it, whether or not
  they act, and by overriding `Command.Down` too instead of leaving it as the library default.
  Re-verified clean after the fix, including repeated/edge-case presses.
- [`RestoreFocus()` — used by the base class's `FocusContent()` to return focus to wherever it was
  before a header was entered — is `internal`, not `public`, so `ManagementTabs`' own `Command.Down`
  override can't call it either (discovered via reflection during implementation)] → substituted
  the public `SetFocus()`, which is what the base `Value` setter itself already uses. Behavioral
  difference: Down from a focused header always lands on the tab's default focus target (e.g. its
  text input) rather than exactly wherever focus was before climbing to the header. Acceptable —
  matches what switching tabs via Alt+letter or `Value =` already does, and wasn't specifically
  requested to behave otherwise.
- [Overriding `Command.Up` to land on the *current* tab's own header intentionally diverges from
  `Tabs`' stock behavior (which jumps to the *previous* tab's header, wrapping around)] →
  deliberate choice per this app's UX design, not an oversight; worth a one-line code comment on
  `ManagementTabs` so a future maintainer doesn't "fix" it back to the library default.
- [`BorderView`/`TitleView` are public but read as internal plumbing (undocumented in the main
  `Tabs` API surface) rather than a stable, primary-supported extension point] → confirmed
  `public` by reflecting on the installed 2.4.10 assembly directly (not just source inspection),
  but their shape could still change across Terminal.Gui releases with less API-stability
  guarantee than e.g. `Tabs.Value`; re-check this cast on any future Terminal.Gui version bump.
- [Only 2 of the 6 planned Alt+letter shortcuts are wired today] → document the full reservation in
  `doc/UI.md` now so Streams/Consumers/KV/OBJ implementers don't pick colliding letters later.
- [Tab header rendering may not visually underline the reserved mnemonic letter the way
  `Shortcut`/menu hotkeys do] → `View` exposes `HotKey`/`HotKeySpecifier`/`AssignHotKeys`, and a
  tab header's `TitleView` is itself a `View`, so the mechanism likely exists — but whether `Tabs`
  wires a tab's `Title` string through to its header's hotkey automatically is unverified; confirm
  during implementation and fall back to plain-text titles (shortcuts still work via
  `KeyBindings` regardless) if not supported.

## Migration Plan

Pure code change, no data/state migration. Implement and manually verify in the running app: Alt+B
and Alt+P switch tabs from anywhere (including from inside the subscriptions list/input); Up
climbs list → input → the *current* tab's own header (not a different tab) and stops there; Down
from a focused header returns focus to that tab's content; Left/Right switch tabs only once a
header is focused, and have no effect on tab selection while list/input content holds focus; Down
at the bottom row of the list and Left/Right within the list/input remain unaffected no-ops.

## Open Questions

None outstanding — the StatusBar-visibility question was resolved during implementation (see
Decisions).
