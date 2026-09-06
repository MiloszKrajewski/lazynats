## Context

`MainWindow` (`src/lazynats/MainWindow.cs`) currently mixes two concerns: (1) resolving
dependencies from `Services.Root` and registering tab content with `ManagementTabs`/wiring the
status bar — the "registration" role it's meant to have — and (2) assembling the Subscribe tab's
actual layout (a `Label`, an `EditFrame` wrapping `SubscriptionsView`, and an outer titled/padded
band), which is presentation logic that belongs to the tab's own component, the way `PublishView`
already keeps its Subject/Headers/Payload band assembly to itself.

This split happened historically: `SubscriptionsView` (`add-subscriptions-feed`) predates
`EditFrame` and the label/frame presentation (`fix-subscriptions-edit-frame` bolted the framing
onto the outside, in `MainWindow`, rather than into the component) — while `PublishView` was
built after `EditFrame` existed and so absorbed that pattern internally from the start. Nobody
went back to reconcile the two.

## Goals / Non-Goals

**Goals:**
- Make Subscribe tab content symmetric with Publish tab content: one self-contained component
  per tab, constructed and added to `ManagementTabs` by `MainWindow` with no further assembly.
- Establish a naming/structure convention (`*Tab` suffix) so future tabs (Streams, Consumers, KV,
  OBJ) have an unambiguous pattern to follow instead of re-deciding this each time.
- Preserve current behavior and visuals exactly — this is a structural move, not a redesign.

**Non-Goals:**
- Wiring `ShortcutTracker`/`IShortcutSource` into `StatusBar` (the Clear and Publish-status
  shortcuts stay hand-wired in `MainWindow` as they are today) — `doc/ui-design.md` already
  tracks that as separate, not-yet-done work, and folding it in here would mix an unrelated
  behavioral change into a pure structural refactor.
- Changing the live feed pane (`LiveUpdatesView`/`feedFrame`) — it isn't a `ManagementTabs` tab,
  it's the persistent lower pane, so it's out of scope for the `*Tab` convention.
- Any change to `SubscriptionRegistry`, `ListEditorView<T>`, `EditFrame`, or NATS behavior.

## Decisions

**`SubscribeTab` owns its band, mirroring `PublishView`'s internal structure.** New
`src/lazynats/Subscriptions/SubscribeTab.cs`: a `View` whose constructor takes
`SubscriptionRegistry`, builds the `Label` + `EditFrame`-wrapped `SubscriptionsView` internally
(the exact code moved out of `MainWindow`'s constructor), and exposes no more surface area than
`PublishTab` does. `Title`/`Padding` are set by the caller (`MainWindow`), same as `PublishTab`,
since those are cosmetic properties any `View` already exposes — not worth a bespoke constructor
parameter.

**Rename `PublishView` → `PublishTab`.** Pure rename (file + class), no behavior change. Chosen
over introducing only `SubscribeTab` and leaving `PublishView` unrenamed, because leaving the
asymmetric naming in place defeats the point of establishing a convention — the next person
adding a Streams tab would have two conflicting precedents to choose from instead of one.

**`SubscriptionsView` (the bare `ListEditorView<SubscriptionInfo>`) keeps its name.** It's the
inner list-editor piece, not the tab's outer shell — same role as `HeaderEditorView` inside
`PublishTab`. Only the outermost, directly-registered-with-`ManagementTabs` component gets the
`*Tab` suffix.

**Convention captured in `doc/ui-design.md`, not enforced in code.** No base class or interface
is introduced for "`*Tab`-ness" — `ManagementTabs.Add` already accepts any `View`, and a marker
type would add indirection without catching anything a code-review glance at `MainWindow`
wouldn't already catch (`MainWindow` should visibly do nothing but construct-and-register).
Documented as a naming/structure convention in `doc/ui-design.md`'s Management Tabs section
instead.

## Risks / Trade-offs

- [Risk: renaming `PublishView` touches every reference to it (the class itself, `MainWindow`,
  any doc/comment mentioning it by name).] → Mitigation: it's a mechanical rename with no logic
  change; grep for `PublishView` after the rename to confirm no stray references remain.
- [Risk: moving the Subscribe band's construction code could silently change focus/layout
  ordering if `Add` order or `X`/`Y`/`Width`/`Height` bindings aren't preserved exactly.] →
  Mitigation: move the existing code verbatim into `SubscribeTab`'s constructor rather than
  rewriting it, and manually verify Subscribe tab focus/layout (Tab-navigation-and-shortcuts
  scenarios) still hold after the move.

## Open Questions

None — scope is a same-behavior structural refactor with no remaining ambiguity.
