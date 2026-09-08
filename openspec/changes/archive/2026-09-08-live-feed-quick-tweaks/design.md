## Context

Two isolated, low-risk tweaks to existing, already-shipped UI: `MainWindow`'s hardcoded
`topLevelShortcuts` list (Live Feed's bound key) and `MessageDetailDialog`'s per-section color
overrides (Headers). Neither touches shared state, data flow, or another capability's contract.

## Goals / Non-Goals

**Goals:**
- Rebind Live Feed's global shortcut to `Alt+0`.
- Color `MessageDetailDialog`'s Headers section the same green already used everywhere else a
  message's headers are shown.
- Retitle the Live Feed frame to match the `N:Title` pattern the numbered management tabs use.

**Non-Goals:**
- No change to the message-preview open key (Enter stays as-is; `v` alternate binding was
  considered and dropped).
- No change to follow/sticky behavior of the live feed (a focus-lost-resumes-follow idea was
  considered and dropped - see conversation history, not carried into this change).
- No new `Theme.cs` color - `HeaderColor` already exists and already means "message headers".

## Decisions

- **Alt+0 over any other free key**: `MainWindow.cs`'s `topLevelShortcuts` already binds
  `Alt+1`..`Alt+5` to the five management tabs in `ManagementTabs`. Live Feed sits visually below
  those tabs and isn't itself a `ManagementTabs` entry, so `Alt+0` reads as "slot zero" in the
  same sequence rather than an arbitrary letter (`M`) that doesn't relate to the other bindings.
  Plain rebind: swap `Key.M.WithAlt` for `Key.D0.WithAlt` on the existing `ShortcutHint` entry -
  no structural change to how `topLevelShortcuts` is built or dispatched.
- **Reuse `Theme.HeaderColor` rather than adding a new color**: grepping the codebase turned up
  `Theme.HeaderColor` (CSS LimeGreen) already in use for the exact same concept in two places -
  the live feed row's header segment (`FeedRowFormatter.cs`) and `ValueDetailDialog`'s metadata
  section, the latter with an explicit comment noting it's "the same color the live feed uses for
  a message's own headers". `MessageDetailDialog` not doing this already reads as an oversight
  rather than a deliberate omission. Apply it the same way Subject already applies
  `Theme.SubjectColor`: `headerView.SetScheme(new Scheme(new Attribute(Theme.HeaderColor,
  Theme.EditableBackground)))` on the view `EditFrame.CreateReadOnly` hands back for the headers
  section (mirrors `MessageDetailDialog.cs`'s existing `subjectView.SetScheme(...)` call, using
  the `_` discard for the headers frame's returned view today - swap that discard for a named
  variable).
- **`0:Live Feed` over `Live Feed`**: same reasoning as the `Alt+0` decision above - the
  management tabs already title themselves `" 1:Subscribe "`..`" 5:Templates "`, so prefixing
  Live Feed's own frame title with `0:` makes the visible title match the shortcut that focuses
  it, the same way each tab's title already matches its own `Alt+N`.

## Risks / Trade-offs

- [Alt+0 could collide with a terminal/OS-level binding on some platforms] -> Alt+digit is
  already used for `Alt+1`..`Alt+5` in this app with no reported conflicts; `Alt+0` sits in the
  same family and carries the same risk profile, not a new one.
- [Coloring Headers green in `MessageDetailDialog` could read as inconsistent if the live feed row
  ever changes what `HeaderColor` means] -> not a new risk introduced by this change; already true
  for `ValueDetailDialog`'s existing reuse of the same constant.
