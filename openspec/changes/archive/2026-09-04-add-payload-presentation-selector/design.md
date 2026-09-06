## Context

`MessageDetailDialog` (`src/lazynats/LiveFeed/MessageDetailDialog.cs`) currently formats a
payload exactly once, in its constructor, by switching on `PayloadContentProbe.Classify(data)`
(`PayloadContentKind.Json | Utf8Text | Binary`) and calling one of three private methods
(`FormatJson`, plain `Encoding.UTF8.GetString`, `FormatHex`). There is no UI to pick a different
presentation than the probe's guess.

Elsewhere in the app (`PublishDialog`, `TemplateDialog`), a payload-type field is a
`DropDownList<PayloadType>` — Terminal.Gui's generic, enum-bound dropdown. Per its XML docs
(`Terminal.Gui.Views.DropDownList\`1`, `terminal.gui` 2.4.17): "The dropdown source is
automatically populated from `TEnum` values in the constructor" — there is no supported way to
give it a subset. That generic type is a thin, type-safe wrapper: `Value`/`ValueChanged` are typed
as `TEnum?`, but the item list itself is always `Enum.GetValues<TEnum>()`, fixed at construction.

The user-facing ask (raised while reviewing the Message Detail dialog) was: can the same dropdown
control be driven by a dynamically computed subset of values instead of the full enum, since
"you cannot show raw binary as json if it isn't json" — i.e. some presentations are only valid for
some payloads. This design confirms that's possible, but not via `DropDownList<TEnum>`: it
requires the framework's non-generic `DropDownList`, which exposes a plain `Source` property
(any `IListDataSource`, typically a `ListWrapper<T>` over an arbitrary `T` collection) instead of
deriving its item list from a type parameter. That's the base class `DropDownList<TEnum>` itself
wraps — trading away the typed `Value`/`ValueChanged` convenience for full control over what's
listed.

## Goals / Non-Goals

**Goals:**
- Let the user switch a message's displayed payload presentation among `Json`, `Text`, `Hex`, and
  `Base64`, restricted at any moment to the subset valid for that payload's actual bytes.
- Keep the probe-driven default (today's behavior) unchanged when the user never touches the
  selector.
- Extract the byte-to-text rendering rules (currently inline in `MessageDetailDialog`) into a
  reusable `payload-presentation` component, so the dialog only wires UI to it.

**Non-Goals:**
- No change to `PayloadValidation`/`PayloadEncoding` (the compose/send-side logic that consumes
  `PayloadType`) — this feature reuses the `PayloadType` *enum* for its display-side labels but
  adds none of its own validate/encode behavior; rendering already-received bytes is a distinct
  operation from validating/encoding user-typed text, even when both are keyed by the same enum.
- No change to `PublishDialog`'s `DropDownList<PayloadType>` — its four options are always valid
  (any text is valid `Text`, and the others are pre-validated on submit), so it has no gating need
  and stays on the simpler generic control.
- No persistence of the user's presentation choice across dialog opens or app runs; reopening
  Message Detail for any message (even the same one) resets to the probe's default.

## Decisions

### Decision 1: Reuse the existing `PayloadType` enum; no new enum
The four presentation values needed (`Json`, `Text`, `Hex`, `Base64`) are exactly `PayloadType`'s
four values, already defined by `payload-types`. An earlier draft of this design introduced a
second, `PayloadPresentationKind` enum to keep "what I'm about to send" and "how I'm displaying
what I received" conceptually separate — but unlike `PayloadContentKind` (`Json | Utf8Text |
Binary`, a genuinely different value set answering "what are these bytes"), there's no second
value set here to justify a second type: it would have been the same four names, same order,
purely duplicated. `PayloadType` stays what it's always been — a label for "how to
interpret/produce this payload's bytes" — and `payload-presentation` (this capability) is simply
the second, inverse operation defined over it (render bytes under a `PayloadType`, vs.
`payload-types`' validate/encode text under one), plus the availability/default lookup keyed by
`PayloadContentKind`. No `PayloadValidation`/`PayloadEncoding` logic is reused or implied by this
reuse — only the enum.

### Decision 2: `PayloadType` availability is a pure function of `PayloadContentKind`
```
Json     -> [Json, Text, Hex, Base64]  // valid JSON is also valid text
Utf8Text -> [Text, Hex, Base64]        // renderable as text, but doesn't parse as JSON
Binary   -> [Hex, Base64]              // not renderable as text at all
```
`Hex`/`Base64` are always available (any byte sequence encodes as either); `Text` requires the
bytes to be the probe's well-formed-UTF-8-with-only-tab/LF/CR-controls text (i.e. `Json` or
`Utf8Text`, matching `payload-content-probe`'s own text/binary line — the probe already refuses
to call control-character-laden UTF-8 "text" for exactly this reason, per
`payload-content-probe`'s spec); `Json` requires the bytes to actually parse as JSON. This mirrors
the proposal's constraint verbatim and needs no new classification work — it's a lookup keyed on
the existing `PayloadContentKind`, returning a subset of the existing `PayloadType` values.

Default selection: `Json -> Json`, `Utf8Text -> Text`, `Binary -> Hex` — i.e. today's fixed
behavior becomes the initial selection rather than the only one.

### Decision 3: Non-generic `DropDownList` with an explicit `Source`, not `DropDownList<PayloadType>`
Construct `new DropDownList { Source = new ListWrapper<PayloadType>(new(allowedTypes)), ReadOnly = true }`
rather than reusing `DropDownList<PayloadType>` (the generic control `PublishDialog` already uses
for its own, unrelated Payload Type field). This is the direct answer to "can it be driven by
dynamic selection of values": the generic form's constructor always calls the equivalent of
`Enum.GetValues<TEnum>()` and offers no override, so — even though the *enum* is shared with
`PublishDialog`'s field (Decision 1) — the *control* can't be, since this dialog needs a
runtime-computed subset (`allowedTypes`, from Decision 2), not all four values every time. The
non-generic base class is what `DropDownList<TEnum>` delegates to internally, and its `Source`
property accepts any `ListWrapper<T>`-wrapped collection — exactly the "dynamic selection of
values" the proposal asks for. The trade-off is losing the generic form's typed
`Value`/`ValueChanged`: selection is read back via the list's selected index into the same
`allowedTypes` array the dialog constructed the source from, and re-render is wired off
`DropDownList`'s untyped `ValueChanged`/selection-changed event instead of a typed one.

`ReadOnly = true` (dropdown-only, no free-text typing) matches this dialog's read-only nature —
same reasoning as `PublishDialog`'s `_payloadTypeDropDown`, which also never accepts arbitrary
typed text for a closed option set.

When the allowed-types list changes size (never happens today — one dialog instance is built for
one fixed message, so the allowed set is computed once in the constructor and never recomputed
after), a fresh `ListWrapper` would need to be assigned to `Source`; not needed for this feature,
noted only so a future per-tab-reusable version of this dialog doesn't have to rediscover it.

### Decision 4: Re-render in place, reuse the existing scroll/frame wiring
Selecting a different `PayloadType` re-runs the same `FormatPayload`-equivalent call with the new
type, replacing the payload `Label`'s `Text`, recomputing `CountLines`, and re-applying
`WireScrolling` if the new text overflows `MaxPayloadVisibleLines` (or removing scroll bindings if
it no longer does). The frame itself is not rebuilt — only the inner `Label`'s content and the
dialog's scroll-command bindings are refreshed — since `EditFrame`/`WrapText` are generic
containers already indifferent to what text they hold.

Note: the payload frame's height is currently fixed at construction time from the *default*
type's line count (`payloadFrameHeight`, based on `payloadLineCount`). Switching type can change
line count (e.g. `Hex` of a short JSON payload may need more or fewer lines than the formatted
JSON) — this design keeps the frame height fixed at its default-type size for layout stability
(avoids the dialog visibly resizing as the user cycles the selector) and lets the existing scroll
wiring handle any type whose rendering overflows that fixed height, rather than resizing the
dialog per-selection.

### Decision 5: The selector starts unfocused; `V` summons it, discoverable via `?`
Initial review of the first implementation found the dropdown grabbing focus the instant the
dialog opened - it's `CanFocus`, every other view in the dialog is a plain non-focusable `Label`
(see `WrapText`), so Terminal.Gui's own "nothing focused yet, fall back to the only focusable
descendant" behavior landed on it unconditionally. That makes a minor display toggle the dialog's
de-facto primary control on open, when this is meant to stay a read-only viewer - Esc-to-close and
scrolling the payload should be the default interaction, not picking a presentation.

The dropdown is now constructed with `CanFocus = false`; nothing in the dialog is a focus
candidate by default, so the Dialog itself holds focus on open (verified: TabStop = NoStop alone
is insufficient here - it only opts a view out of Tab-key cycling, not out of that
activation-time fallback, so a first attempt using it alone still left the dropdown auto-focused,
reproduced via tmux by pressing bare `Down` immediately after opening the dialog, before ever
pressing `V`). `V` is bound on the Dialog (`Command.Expand`, chosen for its "expand a list"
semantics) to `OpenPresentationSelector`, which flips `CanFocus` back to `true`, calls
`SetFocus()`, then `InvokeCommand(Command.Toggle)` to open the popover in one step -
`ToggleDropDown()`/`OpenDropDown()` are public only in a newer Terminal.Gui than the 2.4.10 this
project targets (confirmed via reflection against the installed package), so `Command.Toggle` -
the same command Space/F4/Alt+Down already invoke per `DropDownList`'s own default key bindings -
is the version-safe equivalent.

For this to be discoverable rather than a hidden key, `MessageDetailDialog` also implements
`IShortcutSource` (`Shortcuts` yields the `V`/"Presentation" hint whenever a selector exists,
empty otherwise) and binds its own `?` picker, mirroring `MainWindow`'s. This turned out to need
its own two fixes, both confirmed via tmux against a running instance:
- A modal `Dialog` is its own top-level with no `SuperView` link back to `MainWindow`, so
  `MainWindow`'s own `?` `KeyDown` handler never sees a keypress made while this dialog is open -
  `?` was a no-op here before `MessageDetailDialog` bound its own copy.
- That copy binds `?` via a raw `KeyDown` subscription, not `AddCommand`/`KeyBindings` on
  `Command.Context` - the latter never reached a custom handler in testing (Terminal.Gui's
  built-in "open context/popover menu" semantics for that command appear to take over first),
  matching why `MainWindow`'s own `?` binding already avoids `AddCommand`/`KeyBindings` entirely.
- The picker's shortcut list is collected via `ShortcutAggregator.Collect(this)` - not
  `App.TopRunnableView?.MostFocused` (`MainWindow`'s own form, which needs to discover whatever's
  focused app-wide) or `this.MostFocused` (which, in this dialog's default no-explicit-focus
  state, resolves to an internal adornment scaffolding `View` that isn't a descendant reachable
  back to `this` via `SuperView` - both left the picker reporting "No shortcuts for this view").
  Since this dialog's own `IShortcutSource` is the only one anywhere in its subtree, starting the
  walk at `this` finds it unconditionally, in every focus state.

Selecting a value is a one-shot action, not the start of an editing session: `OnPresentationChanged`
mirrors `OpenPresentationSelector` in reverse - `SetFocus()` on the Dialog first, then
`CanFocus = false` on the dropdown (focus must move away before `CanFocus` is revoked, not after,
since the dropdown still holds focus at that point - `DropDownList` returns focus to itself once
its popover closes, per its own docs). Without this, the dropdown would stay in the "de-facto
primary control" focus state this whole decision exists to avoid, just one keypress later than
before; verified via tmux that a bare `Down` after a selection no longer re-cycles the value, and
that `Esc` still closes the dialog immediately afterward.

The dropdown's `X` is `Pos.AnchorEnd()` (not `Pos.Right(payloadLabel) + 2`) - the width-aware form
that tracks the dropdown's own `Width` and flushes its right edge against the content area's right
edge (inside `Padding`, the same edge `Dim.Fill()` already targets on `subjectFrame`/
`headerFrame`/`payloadFrame`), rather than sitting immediately after the "Payload" label.

## Risks / Trade-offs

- **[Losing typed Value/ValueChanged]** → Selection is read via index into the constructor's
  `allowedTypes` array rather than a typed enum property; acceptable since the array is
  dialog-local and never mutated after construction.
- **[Payload frame height fixed to the default type's size]** → A type whose rendering is much
  taller than the default (e.g. `Hex` of a large JSON payload) is fully reachable via the existing
  scrollbar/PageUp/PageDown wiring (Decision 4), just not fully visible without scrolling;
  consistent with today's behavior for any single rendering exceeding `MaxPayloadVisibleLines`.
- **[Reusing `PayloadType` for a second, unrelated purpose]** → A reader could assume
  `PublishDialog`'s `PayloadType` validation/encoding rules (`PayloadValidation`/
  `PayloadEncoding`) apply here; they don't — `payload-presentation` only reuses the enum's four
  names as display labels and defines its own render function. Mitigated with a doc comment on
  the new render function pointing out it's the display-side counterpart, not a consumer of
  `PayloadValidation`/`PayloadEncoding`.

## Migration Plan

Additive, single-dialog change: no data model, storage, or wire-format impact. Ships as one
implementation pass to `MessageDetailDialog.cs` plus a new `Payloads/` component; no flag or
rollback beyond a normal revert, and no new enum for anything downstream to depend on.

## Open Questions

None — scope is confined to the read-only Message Detail dialog and does not touch the outbound
`PayloadType` compose/validate/encode path.
