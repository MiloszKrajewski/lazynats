## Context

CLAUDE.md already documents a title-spacing convention for bordered containers (`Window`/
`FrameView`): a leading and trailing space so the border corners don't crowd the text (e.g.
`" Live Feed "` in `MainWindow.cs`, and its tab titles `" 1:Subscribe "` etc.). Separately, every
`Dialog<T>` subclass (`CreateStreamDialog`, `CreateConsumerDialog`, `PatternDialog`,
`HeaderDialog`) sets `Padding.Thickness = new Thickness(1, 0, 1, 0)` so field/label content gets a
1-column inset from the left/right border.

Neither convention currently reaches two places: (1) those same four `Dialog<T>` subclasses'
own `Title` strings (`"New Stream"`, `"New Consumer"`, and the caller-supplied
`"New Header"`/`"Edit Header"`/`"New Subscription"`/`"Edit Subscription"`), all set with no
leading/trailing space; (2) every `MessageBox.Query`/`ErrorQuery` call site (`StreamsTab.cs`
create/delete confirm and failure prompts, `MainWindow.cs`'s feed-item "Selected" prompt), whose
title and message strings also carry no padding. `MessageBox` has no `Padding.Thickness` of its
own to lean on the way `Dialog<T>` does — per its XML docs it auto-sizes tightly around whichever
of title/message/buttons is widest — so an unpadded message can render with a character touching
the border on both left and right.

## Goals / Non-Goals

**Goals:**
- Every `Dialog<T>` subclass's `Title` and every `MessageBox.Query`/`ErrorQuery` call's title and
  message get the same leading/trailing single-space treatment already established for
  `Window`/`FrameView` titles.
- Because `MessageBox` auto-sizes around its content, padding its title/message text is
  sufficient by itself to grow the box and create real horizontal room — no separate sizing logic
  needed there.
- One shared place defines the convention, so every call site applies it identically instead of
  each hand-typing `" ... "` and risking a missed trailing space.

**Non-Goals:**
- No change to `Dialog<T>`'s existing `Padding.Thickness = new Thickness(1, 0, 1, 0)` — see
  Decisions below for why that's already sufficient for field/label body content and doesn't need
  widening.
- No word-wrap or multi-line formatting changes to exception messages (`ex.Message`) — padding is
  applied to the string as a whole, which is correct for the single-line messages every current
  call site actually passes; a hypothetical future multi-line message is out of scope.
- No visual/behavioral change to buttons, field validation, or dialog commit/cancel logic.

## Decisions

**A single `DialogText.Pad` helper, not per-call-site literals.** `MainWindow.cs`'s
`" Live Feed "`-style titles are one-off literals because each is written once, in one place. This
convention has roughly ten call sites across three files (four `Dialog<T>` titles, six-plus
`MessageBox` title/message pairs, growing to eight once `add-consumer-delete` adds its own
confirm/failure prompts) — enough that a shared helper earns its keep, the same way `Theme.cs`
centralizes color constants "so dependents read from there instead of repeating the literal."
Lives in `Components/DialogText.cs` as a single static method:

```csharp
internal static class DialogText
{
    public static string Pad(string text) => $" {text} ";
}
```

**Padding is applied inside each `Dialog<T>` subclass, not by its callers.** `PatternDialog`/
`HeaderDialog` receive `title` as a constructor parameter from `SubscriptionsView`/
`HeaderEditorView`; padding is applied once, at `Title = DialogText.Pad(title);` inside the
dialog's own constructor, so the four caller call sites (`"New Header"`, `"Edit Header"`,
`"New Subscription"`, `"Edit Subscription"`) stay as plain, readable strings and can't drift out
of sync with the convention. `CreateStreamDialog`/`CreateConsumerDialog` apply it the same way to
their own literal titles (`"New Stream"`, `"New Consumer"`).

**`MessageBox` call sites pad both arguments inline: `DialogText.Pad("Delete Stream")`,
`DialogText.Pad($"Delete stream '{name}'? This cannot be undone.")`.** No wrapper around
`MessageBox.Query`/`ErrorQuery` itself — the static method calls stay exactly as they are today,
just with `DialogText.Pad(...)` around each of the two text arguments, keeping the diff obvious
at each call site and avoiding a shim around an API this codebase otherwise calls directly
everywhere else (per `doc/terminal-gui-howto.md`'s own recipes).

**`Dialog<T>`'s existing `Padding.Thickness = new Thickness(1, 0, 1, 0)` is left unchanged.** All
four custom dialogs give their inner fields a fixed `Width = 43`; a 2-character growth in `Title`
length from padding is negligible against that, and none of the four titles are long enough to
threaten the dialog's content-driven width even after padding. The body-content "room" the
proposal raised turns out to be a title/`MessageBox` gap specifically, not a field-padding gap —
recorded here as a closed question rather than left open.

## Risks / Trade-offs

- **[Risk]** `MessageBox`'s auto-sizing could, in principle, still clip a very long padded title
  against a very short message (or vice versa) if Terminal.Gui sizes off the wrong dimension →
  **Mitigation**: every current call site's title and message are both short, single-line strings
  well within typical terminal widths; verified visually via tmux per the tasks below rather than
  reasoned about in the abstract.
- **[Risk]** Forgetting to route a future new dialog/MessageBox call through `DialogText.Pad`
  reintroduces the cramped look one call site at a time → **Mitigation**: accepted — same
  class of risk as any convention enforced by review rather than the compiler (e.g. the existing
  `Theme.EditableBackground` convention), not something this change can fully close.

## Migration Plan

No data migration. Purely additive UI/rendering; existing dialog behavior (fields, buttons,
commit/cancel, validation) is unaffected. No feature flag — ships as soon as merged.

## Open Questions

None.
