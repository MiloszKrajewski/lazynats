## Context

`LiveUpdatesView` is always constructed by `MainWindow` and added as the sole child of a
`FrameView` titled `" Live Feed "` (`MainWindow.cs`). The view currently draws its own `Line`
divider and `Label` heading ("Live Updates") at `Y = 0` / `Y = 1`, ahead of the `ListView` at
`Y = 2`. This predates the `FrameView` wrapper and was never removed once the frame took over
providing a title. This is a small, single-file, purely presentational fix — no new dependencies,
no data-model or architectural change, no migration concerns.

## Goals / Non-Goals

**Goals:**
- Remove the duplicated header so the frame's own title is the only title shown.
- Reclaim the two rows for the message list.

**Non-Goals:**
- No change to `MainWindow.cs`, `FrameView` layout/title, or any other view.
- No change to feed data handling, dedup, or batching (`live-feed`'s existing pipeline
  requirements are untouched).

## Decisions

- Delete the `divider`/`heading` fields and their `Add(...)` call in `LiveUpdatesView`'s
  constructor; move `_listView`'s `Y` from `2` to `0`. No alternative considered — the frame
  already supplies the title, so there is nothing else for the in-view header to do.
- Keep the requirement in the `live-feed` capability (rather than, say, `MainWindow`/no capability
  at all) since `live-feed` is the existing spec home for `LiveUpdatesView`'s behavior.

## Risks / Trade-offs

- [Risk] A future reuse of `LiveUpdatesView` outside a titled frame would show no heading at all.
  → Mitigation: none needed today — the view has exactly one call site (`MainWindow`); if a
  second, unframed call site appears later, that's the point to revisit.
