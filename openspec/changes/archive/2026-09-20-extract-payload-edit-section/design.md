## Context

`PublishDialog`, `TemplateDialog`, and `CreateKeyDialog` each independently compose the same
Payload Type/Payload(Value) editing shape: a `Label` + `DropDownList<PayloadType>` in an
`EditFrame` (fixed 3-row frame + 1-row label), a `Label` + `TextView` in an `EditFrame` (the rest
of the dialog's height), `WordWrap` toggled off only for `Json`, invalid-scheme highlighting
driven by `PayloadValidation.IsValid`, and commit-time `PayloadEncoding.ToBytes`. The read-only
counterpart of this shape — byte payload, selectable `PayloadType`, adaptive-width render — was
already extracted once as `PayloadDetailSection` (`Components/`), shared by `MessageDetailDialog`
and the KV Value Detail dialog. This change does the same extraction for the editable side.

The one place the three dialogs genuinely differ today is how the field is seeded from an existing
payload when opening for Edit: `TemplateDialog.SeedPayloadText` preserves a template's stored
`Json`/`Text` verbatim, re-rendering only `Hex`/`Base64`; `ValuesTab.SeedValueText` always renders
from raw bytes per `PayloadPresentation`, except `Text`, which it deliberately decodes raw to
avoid a `TextView`-`WordWrap`/fixed-width-chop double-wrap bug that `TemplateDialog` never hit
because it never renders `Text` through `PayloadPresentation` at all. Per the proposal decision,
this change unifies both onto the `ValuesTab` rule (render `Json`/`Hex`/`Base64`, raw-decode
`Text`), fixing the drift by construction rather than by remembering to port the fix a second time.

A second, separate seeding path exists in both `TemplateDialog` and `CreateKeyDialog` and is
**not** part of this unification: reopening a fresh dialog instance pre-filled with whatever the
user already typed, after a failed Create/Edit save (`ValuesTab.TryEditKeyAsync`'s catch branch,
and the equivalent Create retry). That path never touches bytes — it's already-valid typed text
being redisplayed — and must keep working exactly as today.

## Goals / Non-Goals

**Goals:**
- One `Components/` view, `PayloadEditSection`, owning the dropdown/`TextView`/validity/`WordWrap`
  wiring, composed identically by all three dialogs.
- The section adapts to whatever `Width`/`Height` (`Dim`) it's given; it has no internal
  minimum/maximum — callers keep computing and owning any clamping themselves (e.g.
  `CreateKeyDialog`'s screen-size-based cap), same as today.
- One seeding-from-bytes rule (Json/Hex/Base64 rendered via `PayloadPresentation`, `Text` raw-
  decoded), applied identically regardless of which dialog is seeding.
- Preserve the "reopen with exactly what was typed" retry path in both `TemplateDialog` and
  `CreateKeyDialog`, unchanged.

**Non-Goals:**
- No changes to `lazynats.Core/Payloads/*` — `PayloadType`/`PayloadValidation`/`PayloadEncoding`/
  `PayloadPresentation`/`PayloadContentProbe` are reused as-is.
- No change to `nats-kv`'s Edit Key behavior — it already matches the unified rule.
- No change to per-dialog layout that isn't the type/payload band itself: `Name` locking, the
  `Headers` band, dialog width, and button rows stay exactly as each dialog already lays them out.
- No change to `PublishDialog`'s behavior — it never seeds from existing bytes (always opens
  blank), so the unification has no observable effect there.

## Decisions

### One component, two rows, `Dim`-sized

`PayloadEditSection : View` lays out internally as:
- Row 0: type label (1 row)
- Rows 1-3: `EditFrame(DropDownList<PayloadType>)` (fixed height 3)
- Row 4: payload/value label (1 row)
- Rows 5..: `EditFrame(TextView)`, `Height = Dim.Fill()` relative to the section

The section's own `Width`/`Height` are `Dim` properties set by the caller exactly like any other
view (`PublishDialog`/`TemplateDialog` pass a literal `76`×`N`; `CreateKeyDialog` passes
`Dim.Fill()`×a screen-computed value) — the section has no opinion on limits, per the "caller
problem" decision. The dropdown is a full, unrestricted `DropDownList<PayloadType>` (all four
values, generic form) — unlike `PayloadDetailSection`'s read-only dropdown, which restricts to the
allowed set for a classified payload, editing always lets the user deliberately pick any type
regardless of the payload's current shape, matching all three dialogs' existing behavior.

**Alternative considered**: a container that also owns the label text above it (mirroring
`PayloadDetailSection`'s single external label). Rejected — `PayloadDetailSection` has one label
for one frame; this section has two label+frame pairs, and giving it a single fixed external
label wouldn't fit either literal ("Payload" vs "Value") that callers already use.

### Two seeding entry points, not one

- `SeedFromBytes(byte[] data, PayloadType type)` — used when opening Edit from a freshly-fetched
  server value/entry. Applies the unified rule: `Json`/`Hex`/`Base64` via
  `PayloadPresentation.Render(data, type, width)` (using the section's own resolved inner width,
  computed after layout — see below), `Text` via a raw UTF-8 decode.
- A plain initial `(PayloadType, string)` constructor pair — used for a blank Create, and for
  reopening pre-filled with exactly what the user last typed after a failed save. No rendering
  happens for this path; the text is set into the `TextView` verbatim, exactly as
  `TemplateDialog`/`CreateKeyDialog` do today for their retry-reopen case.

These are genuinely different operations (bytes needing a rendering decision, vs. already-decided
display text that must round-trip untouched) — collapsing them into one method would force one of
the two call sites to fake the other's shape (e.g. wrapping already-typed text as if it were bytes
just to share a signature).

### Width for `SeedFromBytes` is resolved by the section itself, after layout

Mirrors `PayloadDetailSection`'s existing two-phase pattern (constructor placeholder, then a
`MeasureAndRender()`-style call once the owner's initial layout has run) rather than requiring the
caller to precompute a width before construction. This removes `CreateKeyDialog.SeedValueWidth`
(today a public static helper `ValuesTab` calls before constructing the dialog) — the section
computes its own inner width from its own resolved frame, the same way `PayloadDetailSection`
already does.

### Validity/WordWrap/highlighting stay internal; dialogs read `IsValid`/`Bytes`

The section exposes `IsValid` (from `PayloadValidation.IsValid` against its current type/text) and
`Bytes` (from `PayloadEncoding.ToBytes`, only meaningful when `IsValid`), plus a changed
notification the owning dialog's own `UpdateValidity`/commit-button-enable logic subscribes to.
`WordWrap` toggling (off for `Json`) and invalid-scheme highlighting move entirely into the
section — no dialog re-implements either.

### Label text and starting type stay per-dialog constructor parameters

`"Payload"` (Publish/Template) vs `"Value"` (Values), and each dialog's own default `PayloadType`
for a blank Create (`Text` in all three today), remain arguments the owning dialog passes in —
these are call-site identity, not shared behavior.

## Risks / Trade-offs

- **Two-phase construction (placeholder, then post-layout render)** → adds one required call
  (`MeasureAndRender()` or equivalent) each owning dialog's constructor must remember to make
  after `Add(...)`, same obligation `PayloadDetailSection`'s callers already have today. Mitigation:
  match `PayloadDetailSection`'s exact method name/shape so it reads as the same, already-familiar
  step.
- **Templates' Json edit-seed behavior visibly changes** (pretty-printed instead of verbatim) →
  a template imported or saved with minified JSON will now reopen reformatted. Mitigation: this
  is the explicit, deliberate scope of the change (see proposal's Modified Capabilities), and
  matches both `nats-kv`'s existing behavior and the read-only Message Detail default, so the app
  becomes more consistent, not less predictable.
- **Removing `CreateKeyDialog.SeedValueWidth`/`ValuesTab.SeedValueText`** → any future code
  depending on those (none currently, both are internal/private to their files) would need to
  move to the section's own `SeedFromBytes`. Low risk — both are `internal`/`private` with a
  single caller each, already identified in the proposal's Impact section.

## Migration Plan

1. Add `PayloadEditSection` alongside `PayloadDetailSection` in `Components/`.
2. Rewire `PublishDialog` first (no seeding involved — the simplest of the three, blank-open only).
3. Rewire `CreateKeyDialog`, moving `ValuesTab.SeedValueText`'s rule into the section's
   `SeedFromBytes` and deleting both `SeedValueText` and `SeedValueWidth`.
4. Rewire `TemplateDialog`, deleting `SeedPayloadText`, and update
   `openspec/specs/nats-templates/spec.md`'s Edit Template scenarios to match the unified rule.
5. Manual verification per dialog: Create (blank), Edit (seeded from server bytes), and the
   Values/Template retry-reopen-after-failure path, across all four `PayloadType` values.

No server-side or data-model changes; nothing to roll back beyond reverting the commit(s).

## Open Questions

None blocking — method/property naming on `PayloadEditSection` (`SeedFromBytes` vs. an alternative
name) is a naming detail to settle during implementation, not a design fork.
