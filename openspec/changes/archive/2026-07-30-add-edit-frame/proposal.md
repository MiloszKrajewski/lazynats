## Why

Edit-capable controls (`TextField`, `TextView`, `ListEditorView<T>`) currently sit flush against
their surrounding layout with no breathing room, and read visually the same as any other plain
`View` — nothing distinguishes "this is an input" from "this is inert content." A full bordered
box (`FrameView`) would fix the breathing-room problem but would visually compete with the app's
real bordered containers (`Window`/`FrameView`/`Dialog`), adding chrome where a hint of padding is
enough.

## What Changes

- Add `EditFrame`, a pass-through container that wraps exactly one edit-capable child `View` and
  draws a thin padded frame around it — half-block/quadrant glyphs (see `doc/glyphs.md`) for a
  top/bottom rule, a two-column left margin with an accent tick, and a one-column right margin —
  using colors that match the child's own background, without the visual weight of a full bordered
  box (no hard corners or border-drawing characters).
- `EditFrame` has no interactive content of its own (`CanFocus = true`, but nothing within it can
  take focus except the wrapped child); Tab/click focus goes straight through to the wrapped
  child via normal focus drill-down, and the frame repaints itself automatically when the child's
  focus state changes (via the child's `HasFocusChanged` event).
- Wire `EditFrame` into `PublishView`'s three edit surfaces: the Subject `TextField`, the Payload
  `TextView`, and the header `HeaderEditorView` (built on `Components/ListEditorView<T>`).
- Give `ListEditorView<T>` an explicit background concept (it currently has none) so it can be
  wrapped consistently alongside the text-edit fields instead of looking mismatched.
- Reconcile (or explicitly document as a known pre-existing quirk) the observed inconsistency
  where the multi-line Payload `TextView` shows a grey background on focus while the single-line
  Subject `TextField` does not — this needs live verification against the running app before
  `EditFrame`'s color inputs can be finalized for these two controls.

## Capabilities

### New Capabilities
- `edit-frame`: the `EditFrame` pass-through container itself — layout contract (1 row added
  above/below, 0 columns), focus-forwarding behavior, and its `OuterBackground` /
  `InnerBackgroundNormal` / `InnerBackgroundFocused` color model.

### Modified Capabilities
- `list-editor`: `ListEditorView<T>` gains a defined background so it can be wrapped by
  `EditFrame` consistently with `TextField`/`TextView`.
- `nats-publish`: Publish tab's Subject, Payload, and header editor are now presented inside
  `EditFrame`.

## Impact

- New component under `src/lazynats/Components/` (e.g. `EditFrame.cs`), following the same
  location as `Components/ListEditorView<T>`.
- `src/lazynats/PublishView.cs`: Subject, Payload, and header editor construction wrapped in
  `EditFrame` instances; `UpdateValidity`'s `_subjectField.SetScheme(...)` invalid-style logic
  needs to keep working with the field now nested inside a frame.
- `src/lazynats/Components/ListEditorView.cs` (or wherever it lives): add a background property/
  concept currently absent.
- No changes to `SubscriptionRegistry`, the live feed pipeline, or NATS connection handling —
  this is presentation-only.
