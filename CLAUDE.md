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
- `MainWindow` currently hosts a fixed two-pane layout (`SubscriptionsView` over the live feed).
  Per `doc/UI.md`, this is expected to grow into a tabbed management area.

## Build / run

```bash
dotnet run --project src/lazynats     # launch the app (needs a NATS server at nats://localhost:4222)
dotnet build src/lazynats.sln         # compile only
```
