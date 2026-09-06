## Context

Edit-capable controls in `PublishView` (`TextField` for Subject, `TextView` for Payload,
`ListEditorView<T>`-based `HeaderEditorView` for headers) sit flush against their surrounding
`View` with no visual distinction from static content. `Terminal.Gui`'s own bordered containers
(`Window`/`FrameView`/`Dialog`) are too heavy for this — a full box with corners would compete
with the app's real bordered panels rather than reading as a soft affordance.

Investigation (decompiling `Terminal.Gui.dll` 2.4.10 with `ilspycmd`, since `TextField`/
`TextView` source isn't otherwise available) found that `VisualRole.Normal`/`VisualRole.Focus` —
the obvious first choice for "the two colors a focus-aware component would use" — do not reflect
what these controls actually paint:

- `TextField.OnGettingAttributeForRole` remaps `Normal → Focus`.
- `TextView.OnGettingAttributeForRole` remaps `Normal → Editable`.
- Both controls' `OnDrawingContent` paint their real content/fill using `VisualRole.Editable`
  (or `ReadOnly`/`Disabled`), and per `Scheme`'s documented derivation rules, `Editable` derives
  from `Normal`, not `Focus` — so, in the stock scheme, neither control's fill actually changes
  color when it gains or loses focus. Only a single cursor cell (`Active`) or, in `TextView`, the
  current line, changes.

This means asking the wrapped child "what's your color right now" generically (via
`GetAttributeForRole`) is fragile and control-type-coupled — different control types remap roles
differently, and the role that determines the *visible* fill isn't the one whose name suggests
"focused."

## Goals / Non-Goals

**Goals:**
- A reusable `EditFrame` component that draws a 1-row flat rule above and below exactly one
  wrapped child `View`, in colors that visually match the child's background.
- Focus passes through to the child untouched; the frame is invisible to keyboard/mouse focus
  navigation.
- The frame's colors are explicit, caller-supplied configuration — not derived by introspecting
  the child's scheme/`VisualRole` — so it works uniformly regardless of what control type it
  wraps or how that control happens to remap its own roles internally.
- Wire it into `PublishView`'s three edit surfaces (Subject, Payload, header editor) as the first
  real usage.
- Give `ListEditorView<T>` an explicit background so it can be wrapped consistently.

**Non-Goals:**
- Not attempting to make `EditFrame` derive colors automatically from an arbitrary child (that
  approach was evaluated and rejected — see Decisions).
- Not reconciling every edit control app-wide in this change — scope is `PublishView` only (per
  the proposal); other tabs (Streams/Consumers/KV/OBJ) are unbuilt and out of scope.
- Not redesigning `TextField`/`TextView`'s own focus-coloring behavior — any pre-existing
  inconsistency between them (see Open Questions) is either documented or worked around at the
  `EditFrame` configuration level, not patched inside `Terminal.Gui` itself.

## Decisions

**1. `EditFrame` is a pass-through container — `CanFocus = true`, corrected from an initial wrong
assumption.** It holds exactly one child `View` as its only SubView, laid out at `Y = 1` with the
frame reserving row 0 (top rule) and row `Height - 1` (bottom rule). Tab order and mouse click
focus resolve directly to the child via normal focus drill-down — the same pattern already used
by `PublishView`'s own `subjectBand`/`headersBand`/`payloadBand` wrapper views, each of which is a
plain `View { CanFocus = true }` around a single real control.
- **Corrected during implementation** (task 5.1 manual verification): the original decision here
  set `CanFocus = false`, on the assumption that not setting it was sufficient for pass-through.
  Empirically this **blocked all keyboard input from ever reaching the wrapped child** — nothing
  typed landed anywhere. Per `Terminal.Gui`'s own `View.CanFocus` docs (verified against decompiled
  2.4.10): *"SuperView must also have CanFocus set to true"* for a descendant to be focusable at
  all — `CanFocus` is not "can this exact view take focus," it gates the entire ancestor chain.
  `CanFocus = false` doesn't make a container transparent to focus; it makes every descendant
  permanently unfocusable while wrapped in it.
- *Alternative considered:* frame forwards focus events to child via custom key handling. Still
  rejected, now for the right reason — `CanFocus = true` with no focusable content of the frame's
  own already gives correct pass-through via `Terminal.Gui`'s normal drill-down, no forwarding
  needed.

**2. Focus-driven redraw via the child's own `HasFocusChanged` event.**
The frame subscribes directly to `child.HasFocusChanged` (confirmed against decompiled `View.cs`
to fire reliably on both gain and loss) and calls `SetNeedsDraw()` on change. No polling, no
reliance on the frame's own focus state (it has none).
- *Alternative considered:* override `OnFocusedChanged` on the frame and rely on the SuperView
  bubbling path (`RaiseFocusedChanged`). Rejected for this single-child case — that path exists
  for compound widgets with multiple focusable descendants tracking "which one currently has
  focus"; subscribing directly to the one child's own event is simpler and equally reliable here.

**3. Explicit background configuration instead of `VisualRole` introspection.**
`EditFrame` exposes four plain properties:
  - `OuterBackground` (`Color`) — the ambient background behind the frame; defaults to inheriting
    from the frame's own `SuperView` (consistent with `Terminal.Gui`'s normal scheme-inheritance
    behavior when a view has no explicit scheme of its own), overridable if a caller needs to pin
    it.
  - `InnerBackgroundNormal` / `InnerBackgroundFocused` (`Color`) — colors matching the wrapped
    child's own background in each state, set explicitly by whoever constructs the frame.
  - `InnerBackgroundOverride` (`Color?`, default `null`) — when non-null, wins outright over both
    `InnerBackgroundNormal` and `InnerBackgroundFocused` regardless of the child's current focus
    state; when null (the default), the frame behaves exactly as if this property didn't exist.
    This is a deliberately generic escape hatch, not an "invalid-state color" — `EditFrame` itself
    has no concept of validity, disabled-ness, or any other specific state. The caller (e.g.
    `PublishView`) is the one that knows Subject can be invalid, and sets/clears
    `InnerBackgroundOverride` itself alongside its own existing `SetScheme` call in
    `UpdateValidity()`; `EditFrame` just always prefers a non-null override when present.
- *Alternative considered:* have `EditFrame` call `child.GetAttributeForRole(...)` itself. Rejected
  per the Context section — the correct role to ask for is control-type-specific (`Editable` vs.
  `ReadOnly` vs. whatever a future non-text child might use), and the role names `Normal`/`Focus`
  don't mean what they sound like for the very controls this component targets. Explicit config
  is one extra thing for the caller to set correctly, but that's a bounded, visible cost versus a
  hidden, fragile one.
- *Alternative considered:* a dedicated named `InnerBackgroundInvalid`-style property. Rejected —
  it would bake a specific concept (validity) into a component that should stay agnostic to why a
  caller might need a third color; a nullable generic override covers this and any future
  one-off state a caller needs to force, without `EditFrame` needing to know what it means.

**4. Rendering uses half-block/quadrant glyphs with the color deliberately placed in Foreground,
not Background.**
Half-block glyphs (`▄`/`▀`/`▐`) and quadrant glyphs (`▗`/`▝`) paint their visible "ink" using the
attribute's foreground; the background only shows on the glyph's other, unfilled portion. So
`EditFrame`'s draw step constructs, per glyph:
`new Attribute(foreground: <ink color for this position>, background: OuterBackground)` — never
the reverse, and never the child's attribute copied verbatim. This fg/bg-swap is an internal
rendering detail; it must not leak into `EditFrame`'s public API, which is why the public
properties are named as backgrounds rather than as "Attribute" pairs (also avoids confusion with
`Terminal.Gui`'s own `VisualRole.Normal`/`.Focus`, which mean something different here per the
Context section).

**5. Full padded-frame layout: top/bottom rule, a two-column left margin, a one-column right
margin — extended from an initial top/bottom-only cut through live iteration with mockups (see
`doc/glyphs.md` for the complete glyph/position naming and color reference this was worked out
against).** `EditFrame` adds exactly 2 rows of height (1 top, 1 bottom) and 3 columns of width
(2 left, 1 right) beyond the wrapped child's own size. Position names (frame slots, independent of
which glyph occupies them) and their current glyph, per `doc/glyphs.md`:

| Position | Glyph | Foreground | Notes |
|---|---|---|---|
| TL (top-left corner) | QLR `▗` | EAC | quadrant glyph — the intersection of LM's "right half" and TOP's "bottom half" |
| BL (bottom-left corner) | QUR `▝` | EAC | mirrors TL for the bottom rule |
| TOP (top rule) | HBD `▄` | IBC | spans the full width including the top-right corner cell — no distinct top-right glyph |
| BOT (bottom rule) | HBU `▀` | IBC | mirrors TOP |
| LM (left margin, outer) | HBR `▐` | EAC | one column, every content row |
| LP (left padding, inner) | FUL `█` | IBC | one column, every content row, between LM and the child's content |
| RM (right margin) | FUL `█` | IBC | one column, every content row — no outer accent tick on the right, asymmetric by design |

All positions share `background: OuterBackground`. TOP/BOT/LP/RM ink with IBC (whichever of
`InnerBackgroundOverride ?? (child.HasFocus ? InnerBackgroundFocused : InnerBackgroundNormal)` is
currently active); TL/BL/LM ink with **EAC** (edge accent color) instead — a deliberate visual
accent distinguishing the frame's outermost left edge from the rest of the frame, independent of
IBC. EAC is currently hardcoded to white (`ColorName16.White`) in `EditFrame`; making it a
caller-configurable property (mirroring `OuterBackground`/`InnerBackground*`) is deferred — noted
in Open Questions below.
- *Alternative considered:* symmetric treatment (same margin width/glyphs on both sides). Rejected
  per explicit design direction — the left/right asymmetry (2 columns + accent tick vs. 1 plain
  solid column) and the corner quadrant glyphs (vs. a plain full-width/half-height glyph on the
  right, since RM's full-width-at-the-corner-row need is already satisfied by TOP/BOT's own fill)
  were deliberately iterated on and confirmed visually, not an oversight.

**6. `ListEditorView<T>` gets an explicit background property.**
Currently the list editor has no defined background concept — it relies entirely on inherited
scheme. Add a `Background` (or equivalent) property so `PublishView`'s header editor can supply
`InnerBackgroundNormal`/`Focused` to its `EditFrame` wrapper consistent with the Subject/Payload
fields, rather than being wrapped with an arbitrary/mismatched color.

## Risks / Trade-offs

- **[Risk]** Explicit `InnerBackgroundNormal`/`Focused` config can drift from the child's actual
  rendered color if the child's scheme changes later (e.g. a theme switch) and the caller forgets
  to update the frame. → **Mitigation**: keep the values colocated with each control's
  construction in `PublishView` (not scattered), so a future scheme change is a local, visible
  edit; revisit automatic derivation only if this proves to be a recurring maintenance cost.
- **[Risk]** The observed `TextField` vs. `TextView` focus-color inconsistency (see Open
  Questions) means `PublishView`'s Subject and Payload fields may need genuinely different
  `InnerBackgroundFocused` values, which could look inconsistent once both are framed identically.
  → **Mitigation**: verify live behavior before finalizing those two controls' configured colors;
  document whichever values are chosen and why.
- **[Trade-off]** The left/right margins are asymmetric (2 columns + accent tick on the left, 1
  plain column on the right) — a deliberate visual choice, not an oversight, but worth flagging as
  a departure from the otherwise-symmetric top/bottom treatment if it reads as inconsistent later.

## Migration Plan

Additive, presentation-only change with no data model, no persisted state, and no NATS-facing
behavior change:
1. Add `EditFrame` under `src/lazynats/Components/`.
2. Add a background property to `ListEditorView<T>`.
3. Wrap `PublishView`'s three fields in `EditFrame` instances, supplying colors per field.
4. No rollback complexity beyond reverting the `PublishView` wiring if the visual result is
   unsatisfactory — `EditFrame` itself has no external state to migrate.

## Open Questions (resolved)

- **TextField vs. TextView focus coloring — resolved, no discrepancy.** Verified live (tmux +
  `capture-pane -e` against the running app, reading actual truecolor escape codes): both the
  Subject `TextField` and Payload `TextView` render `fg (255,255,255) / bg (128,128,128)`
  identically, regardless of focus — confirmed by toggling focus between them and diffing the
  captured attributes each time. This matches the static analysis (`Editable`'s derivation from
  `Normal`, independent of focus), and means the earlier live impression of "TextView goes grey
  only when focused" was a **false read** — it was actually conflating Subject's *validity*
  styling with focus. Concretely: `PublishView.UpdateValidity()` calls
  `_subjectField.SetScheme(new Scheme(InvalidSubject))` whenever Subject is empty, which paints it
  `fg (255,0,0) / bg (0,0,0)` — a look easy to mistake for "unfocused" since a freshly-opened,
  not-yet-typed-into Subject field is both empty and (often) not the one currently focused. Once
  Subject holds any text, it reverts to the same grey as Payload, focused or not.
  - **Consequence for task 1.2**: `InnerBackgroundNormal == InnerBackgroundFocused` for *both*
    controls in their valid state — `(128,128,128)`. The two-color mechanism stays in `EditFrame`
    (still correct in general, and still the right hook per Decision 2/3), it just happens both
    values are equal for this first usage.
  - **New open item this surfaces — resolved.** Subject has a *third* visual state (`SetScheme`'s
    red/black invalid style) that the two-color (`Normal`/`Focused`) model doesn't account for.
    Resolved via `InnerBackgroundOverride` (see Decision 3): `PublishView.UpdateValidity()` sets
    `subjectFrame.InnerBackgroundOverride` to the invalid color alongside its existing
    `_subjectField.SetScheme(...)` call when Subject is empty, and clears it (`null`) when valid —
    so the frame's rule always matches whatever Subject is actually showing, without `EditFrame`
    needing any built-in notion of "invalid."
- **Should `ListEditorView<T>`'s new background respond to focus at all**, or is a single static
  background sufficient for its first cut, given it doesn't have per-character focus/cursor
  rendering like `TextField`/`TextView`? Confirmed: a single static background is sufficient —
  consistent with the finding above that none of these controls actually vary their fill by focus
  in the stock scheme.

## Open Questions (new, resolved)

- **EAC (edge accent color) configurability — resolved.** `EditFrame` now exposes `EdgeAccent`
  (`Color?`, default `null`), mirroring `OuterBackground`'s pattern: `null` falls back to the same
  white (`ColorName16.White`) default as before, so no existing caller's behavior changes.
  `PublishView` doesn't set it yet — deferred until a concrete need for a non-white accent shows
  up for a given field.
