# CLAUDE.md

## What this is

`lazynats` is a terminal UI for NATS, in the spirit of `lazygit`/`lazydocker`: a fast,
keyboard-driven client for watching and managing a NATS server without leaving the terminal.
See [`doc/UI.md`](./doc/UI.md) for the intended UI shape (management tabs for subscriptions,
streams, consumers, KV/OBJ stores, plus a live feed and message sending) and how much of it
exists today.

## Stack

- .NET 10, `PublishAot` enabled — avoid patterns that don't trim/AOT well (heavy reflection,
  dynamic codegen).
- [Terminal.Gui v2](https://github.com/tui-cs/Terminal.Gui) for the UI. **This is a from-scratch
  v2 rewrite, not the v1 API most training data and web examples show.** Before writing or
  editing any UI code, read [`doc/terminal-gui-howto.md`](./doc/terminal-gui-howto.md) — it has
  the v1→v2 corrections table, canonical patterns, layout (`Pos`/`Dim`), and known gotchas.
- `NATS.Client.Core` / `.JetStream` / `.KeyValueStore` / `.ObjectStore` for the NATS side.
- `Microsoft.Extensions.DependencyInjection` via the static `Services` provider in
  `Services.cs` (`Services.Root.GetRequiredService<T>()`), not constructor-injected app-wide.
- For API surface not already covered by `doc/terminal-gui-howto.md` (exact method signatures,
  event args, optional params), check the context7 MCP server first —
  [`nats-io/nats.net`](https://context7.com/nats-io/nats.net) and
  [`websites/gui-cs_github_io_terminal_gui`](https://context7.com/websites/gui-cs_github_io_terminal_gui)
  — before falling back to grepping the installed package's shipped XML docs
  (`~/.nuget/packages/<package>/<version>/lib/*/*.xml`).

## UI conventions

- Keyboard-first: the app is designed to be used entirely without a mouse. Mouse input may work
  incidentally (Terminal.Gui provides it for free in places) but is never the primary way to
  reach a feature — every interaction needs a keyboard path, and that path is what to design and
  test first.
- Bordered containers (`Window`, `FrameView`, `Dialog`) get horizontal breathing room: set
  `Padding.Thickness` rather than letting content butt against the border (e.g.
  `SubscribeTab`/`PublishTab`, set where they're constructed in `MainWindow.cs`, and
  `PatternDialog`).
- Titles on bordered containers get a leading and trailing space (e.g. `" Live Feed "` in
  `MainWindow.cs`) so the border corners don't crowd the text.

## Architecture

- `Program.cs` wires everything up: opens the `NatsConnection`, creates an unbounded
  `Channel<FeedEnvelope>`, constructs `SubscriptionRegistry`, registers singletons, then runs
  `MainWindow`.
- `SubscriptionRegistry` owns one background task per active subscription pattern
  (`_connection.SubscribeAsync`); each task writes received messages into the shared channel as
  `FeedEnvelope`s. `Add`/`Remove` are UI-thread-only by design (see comments in the file) — don't
  add locking without revisiting that assumption.
- The live feed side (`FeedReaderLoop` → `MessageDeduplicator` → `LiveLogDataSource` →
  `LiveUpdatesView`) batches channel reads, drops near-duplicate messages within a short window,
  and renders rows via `FeedRowFormatter`. `LiveLogDataSource` intentionally reports
  `MaxItemLength = 0` to avoid an O(n²) rescan on append — see the comment in that file before
  "fixing" it.
- `MainWindow` hosts `ManagementTabs` (`SubscribeTab`, `PublishTab`, `StreamsTab`, `KvTab`,
  `ObjTab` — the full list in `doc/UI.md`) over the live feed, plus a `StatusBar`. Each tab's
  content is a self-contained component (one per top-level folder: `Subscriptions/`, `Publish/`,
  `Streams/`, `KVStore/`, `ObjStore/`) that owns its own internal layout (labels, `EditFrame`
  wrapping, sub-bands); `MainWindow` only resolves dependencies, constructs the tab, and
  registers it with `ManagementTabs` — see `openspec/specs/tab-content-structure/spec.md`.
- `StreamsTab`/`KvTab`/`ObjTab` share the same LHS-list/RHS-details, drill-down shape (e.g.
  stream → its consumers, KV bucket → its keys), built on two more `Components/` base classes:
  `DrillableListView<T>` (list wiring, empty-hint, identity-preserving `ReplaceItems`, Ctrl+R
  refresh — level-specific navigation like Enter-to-descend/Esc-to-ascend is left to each
  subclass) and `PollingDetailsView<TTarget, TInfo>` (the RHS pane's active-gated poll-on-timer +
  debounced fetch-on-target-change pipeline, built on Rx). A tab keeps both levels' views alive
  simultaneously and toggles them via `Visible` rather than tearing down/rebuilding on
  descend/ascend. See `openspec/specs/drillable-list/spec.md` and
  `openspec/specs/polling-details/spec.md`; `nats-streams`/`nats-kv`/`nats-obj` specs cover each
  tab's own behavior. Production KV buckets can be large, so detail-panel polling is scoped to
  the single selected item, not the whole bucket — keep any new poll path O(1) in bucket size and
  gate genuinely O(n) operations (e.g. listing all keys) behind an explicit user action instead.
- Two more reusable pieces in `Components/` back the flatter, non-drilling per-tab UI:
  `EditFrame` (a thin padded frame around a single edit-capable child — see `doc/glyphs.md` for
  its border glyph/position naming) and `ListEditorView<T>` (a presenter-formatted list with
  New/Edit/Delete, generalizing the shape shared by `SubscriptionsView` and `PublishTab`'s header
  editor; row creation/edit is delegated to abstract callbacks, row formatting to an injected
  `IValuePresenter<T>`).
- Status-bar shortcuts are discovered, not hardcoded: a view opts in via `IShortcutSource`,
  `ShortcutAggregator` walks the focused-view ancestor chain collecting hints, and
  `ShortcutTracker` (`ShortcutAggregator.cs`) recomputes them on every focus change (or on
  demand via `Refresh()`) and raises `ShortcutsChanged`, which `MainWindow` uses to resync the
  `StatusBar`'s dynamic tail. See `openspec/specs/keyboard-shortcut-discovery/spec.md`.
- `Theme.cs` centralizes tunable theme colors (currently just `EditableBackground`); dependents
  (`Program.cs`'s Base/Dialog scheme overrides, invalid-input highlights in
  `PublishTab`/`HeaderDialog`/`PatternDialog`) read from there instead of repeating literals —
  see `openspec/specs/color-theme/spec.md`.
- `src/lazynats.AotProbe` is a standalone console app (not in `lazynats.sln`) used to validate
  that a package/pattern (NATS, DI, Rx, ...) actually trims/AOT-publishes cleanly before it's
  relied on in the main app. `test-aot.ps1` in that folder builds it, publishes it
  `PublishAot`, and runs it against a throwaway dockerized `nats-server` on port 14442.

## Build / run

```bash
dotnet run --project src/lazynats     # launch the app (needs a NATS server at nats://localhost:4222)
dotnet build src/lazynats.sln         # compile only
```

There is no automated test project yet. `.nuke/build/Program.cs` defines the CI/release
pipeline (`Clean`, `Restore`, `Build`, `Test`, `Release`, `ReleaseDocker`, ...), invoked via
`./build.ps1 <target>`; day-to-day development uses the plain `dotnet` commands above instead.

For verifying keyboard-driven UI changes without asking for manual testing, `tmux` can drive the
app non-interactively: launch it in a detached session (`tmux new-session -d -s <name>
'dotnet run --project src/lazynats'`), drive it with `tmux send-keys -t <name> <key>` (e.g. `M-1`
for Alt+1), and read the rendered screen with `tmux capture-pane -t <name> -p` (text only — colors
and attributes don't come through, so use status-bar text or dialog appearance as a proxy for
focus state). Needs a real NATS server reachable at `nats://localhost:4222`.

For inspecting server-side state directly (streams, consumers, KV/OBJ stores, ...) rather than
through the app, check `.bin/` for a `nats` CLI executable (`nats.exe`/`nats.sh`/`nats`) before
falling back to raw `$JS.API...` subjects or the monitoring HTTP endpoint — it may already be
sitting there for exactly this.

## Change workflow

Non-trivial features go through OpenSpec (`openspec/`) before implementation: a proposal +
design + spec-delta under `openspec/changes/`, later archived into `openspec/specs/` once
shipped. Check `openspec/specs/` for the current, authoritative behavior spec of a feature
before assuming the code is the only source of truth. Use the `opsx:*` / `openspec-*` skills to
drive this workflow rather than hand-writing the files.
