# Demo scripts

PowerShell scripts that stand up a throwaway "Acme order platform" scenario on a dedicated
demo `nats-server`, for recording a lazynats screen-cast. They don't touch whatever `nats-server`
you already run for day-to-day dev work (a separate port keeps them apart).

## Usage

```powershell
./demo/start-nats.ps1       # docker: JetStream-enabled nats-server on nats://localhost:4223
./demo/seed-data.ps1        # provisions the streams/consumers/KV buckets/object stores below

# in the terminal you'll record, point lazynats at the demo server:
dotnet run --project src/lazynats -- -s nats://localhost:4223

# in a second terminal, while recording:
./demo/generate-events.ps1  # publishes fake events forever, Ctrl+C to stop

# once you're done recording:
./demo/stop-nats.ps1
```

`seed-data.ps1` is only safe to run once per `start-nats.ps1` (streams/buckets error if they
already exist) - re-run `start-nats.ps1` first for a clean slate.

## What gets seeded

| Kind | Name | Notes |
|---|---|---|
| Stream | `ORDERS` | `orders.>`, unlimited limits, 2 consumers (`fulfillment`, `notifications`) |
| Stream | `SYSTEM_LOGS` | `logs.>`, bounded (1h / 5MB), no consumers - drill-down empty state |
| KV bucket | `inventory` | 6 SKUs seeded, no TTL, history 5 - generator decrements quantities live |
| KV bucket | `sessions` | starts empty, 2-minute TTL - generator adds keys that expire live |
| Object store | `product-images` | 4 placeholder files |
| Object store | `reports` | 2 placeholder files |

`generate-events.ps1` also publishes to subjects with no backing stream (`metrics.cpu`/`mem`,
`chat.general`, `alerts.disk`/`latency`, `sensor.raw`), to show the Subscribe tab's Live Feed
works on arbitrary core-NATS subjects. Payload types are mixed on purpose - JSON (`orders.*`,
`alerts.*`), plain text (`logs.*`, `chat.general`), and binary (`sensor.raw`) - to show the
feed's per-type rendering and the Message Detail dialog's hex view.

## Talking points while recording

- **Subscribe (Live Feed)**: subscribe to `orders.>`, then `>` to show everything at once -
  headers and payload-type-aware rendering are visible per row; Enter on a row opens the full
  Message Detail dialog.
- **Streams**: `ORDERS` vs. `SYSTEM_LOGS` contrasts unlimited vs. bounded limits; drill into
  `ORDERS` to show its 2 consumers with live delivered/pending counts, then `SYSTEM_LOGS` for
  the empty-consumers state.
- **Values**: `inventory`'s revision count climbs as the generator updates SKUs; `sessions`
  shows entries appearing and then expiring as their 2-minute TTL lapses.
- **Objects**: `product-images` vs. `reports` shows two different bucket "purposes"; drill in
  to see per-object metadata (size, chunks, digest, modified).
