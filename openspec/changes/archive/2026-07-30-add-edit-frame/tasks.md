## 1. Verify current focus-coloring behavior

- [x] 1.1 Run the app against a live NATS server and inspect the Publish tab's Subject
      (`TextField`) and Payload (`TextView`) fields focused/unfocused, confirming (or correcting)
      the observed grey-on-focus-for-TextView-only behavior noted in design.md's Open Questions.
      **Result**: no focus-dependent color difference exists for either control — see
      design.md's Open Questions for the corrected finding and its knock-on effect on 1.2.
- [x] 1.2 Based on 1.1, decide the concrete `InnerBackgroundNormal`/`InnerBackgroundFocused`
      values for the Subject field and Payload field (they may differ from each other).
      **Result**: both fields use `(128,128,128)` grey for both Normal and Focused (identical, per
      1.1's finding). Subject's invalid (empty) red/near-black state is handled separately via
      `InnerBackgroundOverride`, not via `InnerBackgroundNormal`/`Focused` — see task 4.1.

## 2. Implement `EditFrame`

- [x] 2.1 Create `src/lazynats/Components/EditFrame.cs`: a `View` subclass with `CanFocus = true`
      (corrected from an initial `false` during task 5.1 verification — see design.md Decision 1),
      constructed with a single child `View`.
- [x] 2.2 Lay out the child at `Y = 1`, `Height = Dim.Fill(1)` (reserving row 0 for the top rule
      and the last row for the bottom rule), full width, 0 extra columns.
- [x] 2.3 Add `OuterBackground`, `InnerBackgroundNormal`, `InnerBackgroundFocused` (`Color`) and
      `InnerBackgroundOverride` (`Color?`, default `null`) properties; `OuterBackground` defaults
      to the color inherited from `SuperView` unless explicitly set. Setting any of these SHALL
      trigger `SetNeedsDraw()`.
- [x] 2.4 Subscribe to the child's `HasFocusChanged` event in the constructor; on change, call
      `SetNeedsDraw()`.
- [x] 2.5 Override the drawing method to paint row 0 with lower-half-block glyphs (`▄`) and the
      last row with upper-half-block glyphs (`▀`), using
      `new Attribute(foreground: InnerBackgroundOverride ?? (child.HasFocus ?
      InnerBackgroundFocused : InnerBackgroundNormal), background: OuterBackground)` — verify
      against `doc/terminal-gui-howto.md` for the correct v2 draw/attribute APIs
      (`OnDrawingContent`/`SetAttribute`/`AddRune`).
      **Note**: implemented via `FillRect(Rectangle, Rune)` for the two full-width rule rows
      (simpler than a per-column loop), plus a single-cell corner tick (`╷`/`╵`) at column 0 of
      each row via `Move`+`AddRune`, matching the original sketch.
- [x] 2.6 Confirm no left/right border glyphs are drawn, matching the layout contract in
      `specs/edit-frame/spec.md`. Confirmed by inspection of the implementation — only rows 0 and
      `Viewport.Height - 1` are painted; no side columns touched.

## 3. Add background support to `ListEditorView<T>`

- [x] 3.1 Add a background color property to `src/lazynats/Components/ListEditorView.cs`,
      applied to `_listView`/`_emptyHintLabel` when set (see `specs/list-editor/spec.md`).
      **Note**: applied to `_listView` only (via `SetScheme`, preserving its resolved foreground
      and swapping only the background); `_emptyHintLabel` keeps its existing dim/disabled
      overlay style, which is orthogonal to background matching.
- [x] 3.2 Confirm behavior is unchanged when the property is left unset (falls back to today's
      inherited-scheme rendering). Confirmed: `Background` defaults to `null`, and the setter
      calls `_listView.SetScheme(null)` in that case, reverting to inherited-scheme behavior.

## 4. Wire `EditFrame` into `PublishView`

- [x] 4.1 Wrap `_subjectField` in an `EditFrame` using the colors decided in task 1.2. In
      `UpdateValidity()`, alongside the existing `_subjectField.SetScheme(...)` call, set the
      Subject frame's `InnerBackgroundOverride` to the invalid color (`(255,0,0)`-derived, matching
      `InvalidSubject`) when empty, and clear it (`null`) when valid, so the frame's rule always
      matches the field's actual current background.
      **Note**: colors read via `field.GetAttributeForRole(VisualRole.Editable).Background` at
      construction rather than hardcoded literals — matches whatever the field actually renders
      (see design.md), and `InnerBackgroundOverride` reuses `InvalidSubject.Background` directly
      rather than a second hardcoded constant.
- [x] 4.2 Wrap `_payloadView` in an `EditFrame` using the colors decided in task 1.2.
- [x] 4.3 Set `HeaderEditorView`'s new background (task 3.1) and wrap it in an `EditFrame`.
- [x] 4.4 Adjust `subjectBand`/`headersBand`/`payloadBand` layout heights/positions in
      `PublishView.cs` to account for each frame's extra 2 rows.
      **Note**: subjectBand 2→4 (mandatory — a single-line field needs the 2 extra rule rows to
      show any content at all). headersBand was tried at 8→10 (to preserve its prior 7 visible
      list rows) but this **starved `payloadBand` of all its space** — see 5.1 — and was corrected
      to 8→5 instead: headersBand's fixed rows are the cheapest thing to give up (a header list
      rarely needs many visible rows) versus Payload, the primary arbitrary-length editing
      surface, which needs real room far more. `payloadBand` itself keeps `Dim.Fill(1)` unchanged
      and absorbs its own frame's 2-row overhead from within that flexible share.

## 5. Manual verification

- [x] 5.1 Run the app (`dotnet run --project src/lazynats`) against a live NATS server; visually
      confirm all three Publish-tab fields show the padded frame, recolor correctly on
      focus/unfocus, and that existing Subject-validation, header New/Edit/Delete, and Send
      behavior are all unchanged.
      **Findings** (tmux + `capture-pane -e`, real NATS server via Docker on :4222):
      - **Bug found and fixed**: `EditFrame`'s original `CanFocus = false` blocked all keyboard
        input from reaching the wrapped child entirely (see design.md Decision 1). Fixed to
        `CanFocus = true`; retested — typing now reaches Subject/Payload/Headers correctly.
      - **Layout bug found and fixed**: growing `headersBand` to preserve its old row count left
        `payloadBand` with 0 visible content rows at the app's actual ~30-row terminal height (the
        real constraint, not a test artifact — confirmed the host pane caps nested sessions at
        120x30 regardless of requested size). Fixed per 4.4's note.
      - End-to-end verified working: Subject typing clears the red invalid override back to grey;
        Headers Ctrl+N → dialog → Enter commits a header pair, visible inside its frame; Payload
        typing works, grey background matching Subject/Headers; Send (via its `Alt+S` hotkey)
        published successfully (`Published to orders.created` in the status bar). Tab-out-of-
        Payload not reaching Send is pre-existing `TextView` behavior (Tab is a text-editing key
        inside multi-line text views), unrelated to this change.
- [x] 5.2 Confirm `dotnet build src/lazynats.sln` succeeds with no new warnings introduced by
      `EditFrame`. Confirmed: 0 warnings, 0 errors on the final build.

## 6. Extend to full padded frame (left/right margins, corners, edge accent)

Follow-up to task group 5's shipped top/bottom-only version, worked out live via ASCII mockups
(saved to `doc/glyphs.md`) before implementation — see design.md Decision 5 for the full
position/glyph table.

- [x] 6.1 Add `doc/glyphs.md`: glyph names (`HBD`/`HBU`/`HBR`/`HBL`/`FUL`/`QLR`/`QUR`/`QLL`/`QUL`)
      separate from position names (`TL`/`BL`/`TOP`/`BOT`/`LM`/`LP`/`RM`), the current
      position→glyph mapping, and the `OBC`/`IBC` color reference.
- [x] 6.2 Extend `EditFrame`'s child layout: `X = 2` (past `LM`+`LP`), `Width = Dim.Fill(1)`
      (leaving `RM`), on top of the existing `Y = 1`/`Height = Dim.Fill(1)`.
- [x] 6.3 Draw `TL`/`BL` as quadrant glyphs (`▗`/`▝`) rather than the original plain tick
      (`╷`/`╵`) — the intersection of `LM`'s "right half" ink and `TOP`/`BOT`'s "bottom/top half"
      ink, so the corner blends with both meeting edges instead of being a distinct shape.
- [x] 6.4 Draw `LM` (`▐`, outer, one column) and `LP` (`█`, inner, one column) down every content
      row on the left; draw `RM` (`█`, one column, no accent) down every content row on the right —
      asymmetric by confirmed design choice, not a mistake.
- [x] 6.5 Add the edge accent color (**EAC**, hardcoded `ColorName16.White` for now — see design.md
      Open Questions for making it configurable) and ink `TL`/`BL`/`LM` with it instead of the
      currently-active inner background color, while `TOP`/`BOT`/`LP`/`RM` keep inking with IBC as
      before.
- [x] 6.6 Rebuild and visually verify (tmux + `capture-pane -e`) against the running app: correct
      glyph placement and raw color codes confirmed for `TL`/`LM`/`BL` (white/EAC) vs.
      `TOP`/`LP`/`RM`/`BOT` (IBC) on all three Publish-tab fields.
- [x] 6.7 Make EAC a caller-configurable property on `EditFrame` (mirroring `OuterBackground`),
      instead of the current hardcoded white — deferred until a concrete need for a non-white
      accent shows up (see design.md Open Questions).
      **Note**: added `EditFrame.EdgeAccent` (`Color?`, default `null`), mirroring
      `OuterBackground`'s pattern exactly — `null` falls back to the same white
      (`ColorName16.White`) default as before. Verified live: no callers set it yet, so default
      behavior is unchanged (confirmed identical raw color codes, `255,255,255`, on TL/LM/BL).
