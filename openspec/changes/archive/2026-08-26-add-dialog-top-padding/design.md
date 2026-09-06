## Context

All five `Dialog<T>` subclasses set `Padding.Thickness = new Thickness(1, 0, 1, 0)` (left 1,
top 0, right 1, bottom 0) in their constructor, per the archived
`2026-08-26-dialog-content-spacing` change. Three of them (`CreateStreamDialog`,
`CreateConsumerDialog`, `CreateBucketDialog`) are multi-field forms with a Cancel/Create button
row added via `AddButton`. The blank row those three already show above their button row is not
`Padding.Bottom` (which the code above sets to `0`) and not a button/dialog shadow (`Button
.DefaultShadow` is `None` by default and none of these dialogs override it; `Dialog.DefaultShadow`
is a `Transparent` shadow drawn outside the dialog's own border, unrelated to internal spacing) —
it's baked into Terminal.Gui's own `Dialog<TResult>.AddButton()` (`Terminal.Gui.Views
.DialogTResult`, v2.4.10, confirmed by reading the source): each button is placed at `Y = 1`
inside an auto-sized `_buttonContainer`, and `Padding.Thickness.Bottom` is then stretched to that
container's full required height (the blank row plus the button row):

```csharp
dialogButton.Y = 1;
...
Padding.Thickness = Padding.Thickness with { Bottom = _buttonContainer!.GetHeightRequiredForSubViews() };
```

So the framework already gives these three dialogs one blank row before their button row; there's
no equivalent for the *top* edge, which is why they read as airy at the bottom and cramped at the
top (the first label sits immediately under the title border). This change's top-padding fix is a
deliberate mirror of that same "one blank row before content" pattern, applied manually since
Terminal.Gui doesn't provide it automatically above the first field. The other two dialogs
(`PatternDialog`, `HeaderDialog`) are single-field, no-button dialogs that commit on Enter — no
`AddButton` call means no `_buttonContainer`, so they have no existing bottom blank row to be
asymmetric against.

The user wants the top cramped-ness fixed for the three button-row dialogs specifically, and
wants this recorded as a standing rule rather than an ad hoc fix repeated per file — while
explicitly keeping the two compact single-field dialogs as they are for now.

## Goals / Non-Goals

**Goals:**
- `CreateStreamDialog`, `CreateConsumerDialog`, `CreateBucketDialog` each gain one blank row
  between their title/border and their first field, matching the blank row already above their
  button row.
- The rule is written down (CLAUDE.md UI conventions + `dialog-spacing` spec) so a future
  button-row dialog picks it up without needing this same fix repeated.

**Non-Goals:**
- No change to `PatternDialog`/`HeaderDialog` — they have no button row, so there's no existing
  bottom gap to balance, and the user wants them to stay horizontally compact. (Explicitly
  revisitable later per the user's own note.)
- No change to field/label `Y` coordinates, dialog widths, button behavior, validation, or
  commit/cancel logic.
- No change to `MessageBox` — out of scope; it's covered by the existing `dialog-spacing`
  horizontal-padding requirements, not this vertical one, and has no `Padding.Thickness` to lean
  on the way `Dialog<T>` does.

## Decisions

**Fix via `Padding.Thickness`'s existing top slot, not new `Y` offsets on every field.** Each
affected dialog already sets `Padding.Thickness = new Thickness(1, 0, 1, 0)`; changing the second
value from `0` to `1` (`new Thickness(1, 1, 1, 0)`) shifts the whole content area down by one row
without touching any label/frame `Y` value, since those coordinates are already relative to the
padded content area. This is the smallest possible diff and can't drift the fields out of their
existing vertical rhythm relative to each other.

**Scope the rule to "has a button row," not "every `Dialog<T>`."** The asymmetry the user is
reacting to only exists where a button row creates a bottom gap to be asymmetric against.
Stating the rule unconditionally ("every dialog gets top padding") would be simpler to write but
wrong today for `PatternDialog`/`HeaderDialog`, which would gain an unmatched top gap with
nothing at the bottom to mirror it. The conditional phrasing keeps the rule accurate to what it's
actually fixing.

**Record the rule in both CLAUDE.md and the `dialog-spacing` spec, not just one.** CLAUDE.md's
"UI conventions" section is the place a contributor reads before writing new UI code; the
`dialog-spacing` spec is the authoritative, scenario-level behavior contract already covering
this same class of dialog (title/message horizontal padding). Adding a `Requirement` there keeps
the vertical-spacing rule in the same place as its horizontal sibling rather than starting a
second, disconnected spec for what's really one "dialogs read as sparse" convention.

## Risks / Trade-offs

- **[Risk]** A future single-field dialog that later grows a button row would need someone to
  remember to add the top padding at that point, since the rule is conditional rather than
  blanket → **Mitigation**: the spec's "has a button row" scenario and CLAUDE.md wording both
  call out the condition explicitly, and the existing convention-by-review precedent
  (`Theme.EditableBackground`, the horizontal `dialog-spacing` rule) already relies on the same
  enforcement-by-review model.
- **[Risk]** None of the three affected dialogs' fixed field `Width = 43` / dialog auto-sizing is
  expected to be sensitive to a one-row height increase — each dialog's `Height` is itself
  `Dim.Auto()`-derived from its subviews plus `Padding`, per the same `DialogTResult.cs` source
  read for the Context section above, so a `Padding.Top` of `1` simply grows the dialog by one row
  rather than clipping anything → **Mitigation**: visual confirmation via tmux (per `CLAUDE.md`'s
  testing guidance) is still a task below, same as the archived horizontal-spacing change did, to
  catch anything the source reading missed.

## Migration Plan

No data migration. Purely additive UI/rendering; existing dialog behavior (fields, buttons,
commit/cancel, validation) is unaffected. No feature flag — ships as soon as merged.

## Open Questions

- Whether `PatternDialog`/`HeaderDialog` should eventually gain matching top spacing (making them
  taller/less compact) is left open per the user's own "for now" caveat — not decided here.
