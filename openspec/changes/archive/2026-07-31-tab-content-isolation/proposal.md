## Why

`MainWindow` is supposed to be pure registration — wire up dependencies, build each tab's
content component, register it with `ManagementTabs` — but the Subscribe tab never got brought
in line with that. `PublishView` is self-contained: it builds its own labeled/framed sub-bands
internally, and `MainWindow` just constructs it and sets `Title`/`Padding` before adding it to
the tabs. The Subscribe tab's equivalent band (label + `EditFrame` + bordered/titled wrapper) is
instead hand-assembled inline in `MainWindow` around the bare `SubscriptionsView` list, so
`MainWindow` ends up doing tab-content layout work for one tab but not the other. This asymmetry
is a maintenance trap as more tabs (Streams, Consumers, KV, OBJ) get added — each would otherwise
need to decide ad hoc whether to be self-contained or leaned on `MainWindow` for assembly.

## What Changes

- Introduce a `SubscribeTab` component (`lazynats.Subscriptions`) that owns the label,
  `EditFrame`, and titled/padded outer band currently assembled inline in `MainWindow` around
  `SubscriptionsView` — mirroring how `PublishView` owns its own internal bands today.
- Rename `PublishView` → `PublishTab` for naming symmetry with `SubscribeTab`: both are
  self-contained `ManagementTabs` pane components, as distinct from inner pieces like
  `HeaderEditorView` or `SubscriptionsView` (the bare list) that stay `*View`.
- Establish "`*Tab` = self-contained `ManagementTabs` pane, constructed and added to the tabs by
  `MainWindow` with no further assembly" as the naming/structure convention for future tabs
  (Streams, Consumers, KV, OBJ), documented in `doc/ui-design.md`.
- Trim `MainWindow` down to: resolving dependencies from `Services.Root`, constructing each tab
  component, registering them with `ManagementTabs`, wiring the (non-tab) live feed pane, and the
  app-wide status bar/shortcuts. No layout/band assembly for tab content remains in `MainWindow`.
- No observable behavior changes: same visual layout, titles, padding, colors, and keyboard
  shortcuts as today — this is a structural refactor only.

## Capabilities

### New Capabilities

- `tab-content-structure`: the structural contract between `MainWindow` and each management
  tab's content — every tab's content is a single, self-contained component that owns its
  complete internal layout, and `MainWindow` only constructs and registers those components,
  performing no tab-specific band/frame/label assembly itself.

### Modified Capabilities

(none — `nats-subscriptions` and `nats-publish`'s existing requirements, including their
"Framed List/Field Presentation" requirements, are satisfied identically after this change; only
where that presentation is assembled moves, not what the user sees)

## Impact

- `src/lazynats/MainWindow.cs` — remove inline Subscribe band assembly; construct `SubscribeTab`
  and `PublishTab` directly.
- `src/lazynats/PublishView.cs` → renamed `src/lazynats/PublishTab.cs` (class renamed
  `PublishView` → `PublishTab`).
- `src/lazynats/Subscriptions/SubscribeTab.cs` — new file/component.
- `doc/ui-design.md` — document the `*Tab` naming/structure convention.
- No changes to `SubscriptionRegistry`, `NatsConnection` usage, `ListEditorView<T>`, or any spec
  behavior.
