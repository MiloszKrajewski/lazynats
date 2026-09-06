## Context

`CreateConsumerDialog` and `CreateStreamDialog` are the only call sites that construct
`DropDownList<T>` today (Ack Policy / Deliver Policy / Retention). Every other editable field in
the app is a `TextField`/`TextView` wrapped in `EditFrame`, colored via `WrapField`:

```csharp
private static EditFrame WrapField(View field, int y)
{
    var background = field.GetAttributeForRole(VisualRole.Editable).Background;
    return new EditFrame(field) {
        X = 0, Y = y, Width = 43, Height = 3,
        InnerBackgroundNormal = background, InnerBackgroundFocused = background,
    };
}
```

`WrapField` asks the wrapped control for its own `VisualRole.Editable` background and paints the
frame with that color, so a `TextField` (whose `Editable` role is the app's
`Theme.EditableBackground` gray per the `color-theme` spec) makes its `EditFrame` gray too.
`DropDownList<T>` is passed through the same `WrapField`, but the closed control still renders on
black and the expanded popup renders on black — this change investigates why and fixes it.

Decompiled `Terminal.Gui.Views.DropDownList` (2.4.10, via `ilspycmd`) shows the mechanism:

```csharp
protected override bool OnGettingAttributeForRole(in VisualRole role, ref Attribute currentAttribute)
{
    // ... when role == VisualRole.Editable and base.ReadOnly (the default: DropDownList's
    // ctor sets base.ReadOnly = true unconditionally) ...
    currentAttribute = GetAttributeForRole(base.HasFocus ? VisualRole.Focus : VisualRole.Normal);
    return true;
    // ... same redirect for VisualRole.Active when ReadOnly, and for VisualRole.ReadOnly when ReadOnly ...
}
```

So `WrapField`'s `field.GetAttributeForRole(VisualRole.Editable)` call on a `DropDownList<T>` is
silently redirected to that control's own `Normal`/`Focus` attribute — which, absent any
per-instance `Scheme` override, resolves through the ambient app-wide `Normal` scheme (black), not
`Theme.EditableBackground`. This is intentional upstream behavior (the doc comment says it exists
so a read-only dropdown "uses the Normal or Focus attributes" rather than looking like a text
field a user could type into) — it's not a bug, just a default that conflicts with this app's
choice to make dropdowns look like every other editable field.

The expanded popup is a separate `Popover<ListView, string?>` built in the same constructor:

```csharp
ListView contentView = new ListView {
    Width = Dim.Auto(DimAutoStyle.Content),   // sized to the widest row, not the anchor
    Height = Dim.Auto(DimAutoStyle.Content, 1, Dim.Func(...)),
    ViewportSettings = ViewportSettingsFlags.HasVerticalScrollBar
};
_listPopover = new Popover<ListView, string>(contentView) { Anchor = GetAnchor };
_listPopover.GettingAttributeForRole += (sender, args) => {
    if (sender is View && args.Role == VisualRole.Normal) {
        args.Result = App?.TopRunnableView?.MostFocused?.GetAttributeForRole(VisualRole.Normal);
        args.Handled = true;
    }
};
```

This confirms both remaining problems structurally:
- The popup's `ListView.Width` is explicitly `Dim.Auto(DimAutoStyle.Content)` — sized to its
  longest item, with no reference to the anchor control's own width anywhere in this code path.
- The popup's `Normal` role is deliberately *not* a fixed color — it's redirected, at draw time, to
  whatever `Application.TopRunnableView.MostFocused` currently resolves to. What exactly that
  view is while the popup is open (the `DropDownList` itself, the popup's own `ListView`, or
  something else depending on Terminal.Gui's focus-transfer timing during `MakeVisible()`) is not
  resolvable from static decompilation — it depends on runtime focus state. **This needs to be
  confirmed against the running app** (see Open Questions / the investigation task in `tasks.md`).

Two public, non-reflective extension points exist and were confirmed present on the installed
package (no private-field access needed, so this stays AOT/trim-friendly per `CLAUDE.md`):
- `Popover<TView, TResult>.ContentView` is a public, settable property.
- `Application.Popovers` (`ApplicationPopover`) exposes a public `PopoverRegistered` event
  (`EventHandler<EventArgs<IPopoverView>>`) fired every time a popover — including a
  `DropDownList`'s internal one — is registered (which happens on every focus/click that can open
  it), plus a public `Anchor` (`Func<Rectangle?>`) property on the popover itself that `DropDownList`
  already populates with its own screen rectangle via `GetAnchor()`.

## Goals / Non-Goals

**Goals:**
- The closed `DropDownList<T>` control renders with the same background as a `TextField` wrapped
  in `EditFrame` (`Theme.EditableBackground`), in both focused and unfocused states.
- The expanded popup renders with that same background.
- The expanded popup's width matches the anchor `DropDownList<T>`'s own width, not its longest
  item's text width.
- The fix lives in one central, reusable place (mirroring how `Theme.cs` is already the single
  declared home for tunable colors) rather than being repeated per dialog, since more dropdowns
  are expected as the app grows (see `doc/UI.md`).

**Non-Goals:**
- Changing `DropDownList<T>`'s editable/combo-box mode, key bindings, or selection behavior.
- Reflection-based access to `DropDownList`'s private `_listPopover` field — a fix must go through
  public API surface only, to stay AOT/trim-friendly.
- Redesigning `EditFrame` itself; its `edit-frame` spec (explicit-colors-only, no introspection of
  the wrapped child) already fits this use case as-is.
- Committing to line-level implementation for the popup's background/width before the interactive
  investigation task confirms the exact runtime behavior (see Open Questions).

## Decisions

### Decision: Give each `DropDownList<T>` its own `Scheme` instead of changing `WrapField`
Rather than special-casing `DropDownList` inside `WrapField` (e.g. reading `Normal`/`Focus` instead
of `Editable` when the field is a dropdown), set an explicit `Scheme` directly on each
`DropDownList<T>` instance (`view.SetScheme(...)`, confirmed pattern in
`doc/terminal-gui-howto.md`) whose `Normal`/`Focus` backgrounds are `Theme.EditableBackground`.

This makes `WrapField`'s existing `GetAttributeForRole(VisualRole.Editable)` call keep working
unmodified — the redirect inside `DropDownList.OnGettingAttributeForRole` still fires, but now
lands on a `Normal`/`Focus` attribute that already carries the right background, so both the frame
color and the control's own self-painted background agree by construction, without `WrapField`
needing to know `DropDownList` is special.

**Alternative considered**: teach `WrapField` to special-case `DropDownList` and read `Normal`
instead of `Editable`. Rejected — `WrapField` would then depend on the same
implementation-specific redirect behavior this design just spent time decompiling to understand,
which is exactly the kind of fragile-to-upstream-changes coupling worth avoiding when the
`Scheme`-based fix achieves the same visual result without it.

Where exactly to construct/apply this `Scheme` (inline at each of the two current call sites vs. a
small shared helper, e.g. `Theme.ApplyEditableScheme(View)` or a tiny `Components` wrapper) is left
to `tasks.md`; given there are only two call sites today, a shared helper in `Theme.cs` (consistent
with its existing role as "single declared place for tunable theme colors") is the likely shape,
but this should be confirmed once the popup fix (which likely needs its own central wiring point
in `Program.cs`) is nailed down, so both fixes aren't split across two different "central places"
for no reason.

### Decision: Fix the popup's width/background centrally via `Application.Popovers.PopoverRegistered`, not per-dialog
Rather than reaching into `DropDownList`'s private popover (which would require reflection), hook
`App.Popovers.PopoverRegistered` once — likely in `Program.cs`, alongside the other one-time app
wiring — and when the registered popover is a `Popover<ListView, string?>` with a non-null
`Anchor`:
- set `ContentView.Width` from `Anchor()?.Width`, so the list matches the dropdown's own width
  instead of its content's width.
- set (or confirm — pending the investigation task) `ContentView`'s own background so it's
  `Theme.EditableBackground` regardless of what `MostFocused` resolves to at draw time.

This is deliberately app-wide rather than per-dialog: `PopoverRegistered` fires for *any*
`DropDownList` popover anywhere in the app (present or future), so new dropdowns elsewhere get the
same fix automatically, matching this app's existing pattern of centralizing cross-cutting visual
concerns (`Theme.cs`, the `color-theme` spec's dialog-scheme override in `Program.cs`) rather than
repeating them per call site.

**Alternative considered**: subclass `DropDownList<T>` and use reflection to reach `_listPopover`.
Rejected outright — `CLAUDE.md` explicitly calls out avoiding reflection-heavy patterns for
AOT/trim reasons, and a public, event-based path exists that achieves the same result.

**Alternative considered**: fix width/background only where `DropDownList<T>` is currently used
(the two dialogs), by grabbing the popover via the public `Source`-adjacent surface. Rejected —
there is no public accessor to the popover from outside `DropDownList` at all (the `_listPopover`
field is private with no exposing property), so a per-dialog fix isn't actually reachable without
reflection; the `PopoverRegistered` hook is the only public path found, and it's inherently
app-wide by nature (the event lives on `Application.Popovers`, not on `DropDownList`).

### Decision: Treat the popup's exact fix as an investigation spike, not a locked design
Both remaining sub-problems — (a) what `MostFocused` actually resolves to while the popup is open,
which determines whether the `Scheme` decision above is even necessary for the popup or whether it
already inherits correctly, and (b) whether setting `ContentView.Width` in a `PopoverRegistered`
handler fires early enough (before the first `MakeVisible()`) and stays correct if the dropdown is
resized or re-opened — depend on Terminal.Gui runtime behavior that static decompilation can't
settle. `tasks.md` includes an explicit spike task, driven interactively via `tmux` per
`CLAUDE.md`'s testing guidance, to pin these down before writing the final fix; if the
`PopoverRegistered`-timing approach doesn't pan out, the fallback is to still avoid reflection by
instead setting the same `Scheme`/width directly inside `OpenDropDown`... but `OpenDropDown` is
also private, so absent a public per-open hook, the `PopoverRegistered` event (fired on every
registration, which happens on every focus/click that can lead to an open) is the best-known
entry point; the spike should also confirm there isn't a better one (e.g. `ContentView`'s own
`VisibleChanged`, reachable once `ContentView` is grabbed off the registered popover).

## Risks / Trade-offs

- **[Risk]** Terminal.Gui behavior in 2.4.17 (a newer version already present in the local NuGet
  cache, though the project pins 2.4.10) may differ from what was decompiled here → Mitigation:
  the spike re-verifies against the actual running app (behavior, not just decompiled source), and
  `tasks.md` should note the pinned version this was verified against so a future upgrade knows to
  recheck.
- **[Risk]** `PopoverRegistered` firing on every focus/click (not just the first) means the width
  fix runs redundantly often → Mitigation: this is idempotent (setting `Width` to the same value
  repeatedly is harmless) and cheap; not worth guarding against.
- **[Risk]** Constraining popup width to the anchor's width means long enum member names could get
  truncated/clipped in the list → Mitigation: acceptable per the request ("I would like to change
  it as well if possible") and consistent with how the closed control itself already truncates;
  flagged for the spike to visually confirm truncation (not wrapping/overflow) is what actually
  happens once `Width` is constrained.
- **[Risk]** If `MostFocused` during the open popup turns out to already resolve to the
  `DropDownList`'s own (now-gray) `Normal` attribute, the `PopoverRegistered`-based background fix
  would be redundant with the `Scheme` decision above → Mitigation: the spike checks this first: if
  the `Scheme` fix alone already makes the popup gray, the design should drop the redundant
  popover-background half of the `PopoverRegistered` hook and keep only its width-fixing half.

## Migration Plan

Purely additive/visual; no data migration. Roll out as a single change; rollback is reverting the
commit (no persisted state depends on the new coloring/sizing).

## Open Questions

- ~~While the popup is open, does `Application.TopRunnableView.MostFocused` resolve to the
  `DropDownList` itself, its popup's `ListView`, or something else?~~ **Resolved by the `tmux`
  spike** (built a temporary `PopoverRegistered`/`VisibleChanged` diagnostic in `Program.cs`,
  logging to a file rather than stderr to avoid corrupting the TUI, then drove `CreateStreamDialog`
  interactively): `MostFocused` while the popup is open **is the `DropDownList` itself**
  (`ReferenceEquals(mostFocused, popover.Target)` was `true` in every capture). Once the closed-
  control `Scheme` fix (task 2) is applied, `MostFocused.GetAttributeForRole(VisualRole.Normal)
  .Background` and the popup's own `GetAttributeForRole(VisualRole.Normal).Background` both logged
  as `#202020` (`Theme.EditableBackground`) with no further code — confirming the "redundant"
  branch of the Risks section: **the popup's background fix is not needed**; the `Scheme` fix
  alone makes both the closed control and the open popup the correct gray.
- ~~Does `PopoverRegistered` fire early enough, and on every open (not just the first), for the
  width fix to reliably apply before the popup becomes visible?~~ **Resolved by the same spike**:
  `PopoverRegistered` fires synchronously inside `ApplicationPopover.Register`, which
  `DropDownList.OnHasFocusChanging`/`OnMouseEvent` call before `OpenDropDown()`'s
  `_listPopover.MakeVisible()` — so it always lands before the first `Layout()`/draw. It also
  reliably re-fires on every subsequent open: `DropDownList.OnHasFocusChanging` calls
  `Popovers.DeRegister(_listPopover)` on focus-loss and `Popovers.Register(_listPopover)` on
  focus-gain, and `ApplicationPopover.Register` only raises `PopoverRegistered` when the popover
  isn't already registered — so the DeRegister/Register cycle on every focus round-trip re-fires
  the event each time. Confirmed empirically: tabbing away from and back to the same `DropDownList`
  instance within one open dialog produced a second `PopoverRegistered` log line before the second
  `F4`-triggered open. `ContentView.Frame.Width` after the fix matched `Anchor()?.Width` (39, the
  `DropDownList`'s own viewport width) exactly, both on first open and after a re-open.

Incidental finding (out of scope for this change, not fixed here): tabbing between fields in
`CreateStreamDialog`/`CreateConsumerDialog` under the `tmux`-driven test setup causes empty
`TextField`s (and, transiently, the `DropDownList` row) to stop rendering their `EditFrame`
padding text in `tmux capture-pane` output, collapsing wide rows to a couple of glyphs. Reproduced
identically on an unmodified checkout (no relation to this change's `Scheme`/`PopoverRegistered`
code), so it's either a pre-existing Terminal.Gui redraw quirk or an artifact specific to
driving/capturing this app under `tmux`/PTY. Layout state itself is unaffected (confirmed via the
`ContentView.Frame.Width` diagnostic above), only the `tmux` text capture. Worth its own
investigation separately if it turns out to affect real terminal usage, not just `tmux capture`.
- ~~Should the `Scheme` applied to `DropDownList<T>` also set a foreground, or only override
  `Background`?~~ **Resolved by reading `Program.cs`**: `ApplyColorTheme()` already declares the
  exact `Editable` attribute as `new Attribute(ColorName16.White, Theme.EditableBackground)`, used
  for both the `"Base"` and `"Dialog"` schemes. The `Scheme` applied to each `DropDownList<T>`
  should set both `Normal` and `Focus` to that same `White`/`Theme.EditableBackground` pair (per
  the `color-theme` spec's "Editor background does not change with focus" scenario, which this
  change extends to cover dropdowns too) — not just override `Background`. This also sharpens the
  problem `design.md` set out to fix: `CreateConsumerDialog`/`CreateStreamDialog` use the
  `"Dialog"` scheme, whose `Focus` is `Black`-on-`White` (a stock focus highlight, unrelated to
  `Editable`) — so today, a *focused* dropdown redirects to `Black`-on-`White`, not just the wrong
  shade of gray but an entirely different, jarring color pair from every other focused field in
  the dialog.
