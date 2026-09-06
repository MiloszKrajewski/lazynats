## Context

The Streams tab's consumer level (`Streams/StreamsTab.cs`, `ConsumerListView.cs`) is read-only
except for creation, per `nats-streams`' "Read-Only List" requirement — already narrowed once, by
`add-consumer-create`, to allow Create. `ConsumerListView` extends
`Components/DrillableListView<T>` (Ctrl+R only) and layers its own `Command.New` + Ctrl+N on top,
raising a `CreateRequested` event that `StreamsTab` handles. This mirrors the stream level exactly,
which already has delete (`add-stream-delete`): a `Command.DeleteAll` + Ctrl+D binding on
`StreamListView`, a `DeleteRequested` event, and `TryDeleteStreamAsync` in `StreamsTab`. Consumer
delete is the identical shape one level down, scoped to `_currentStream`.

`DeleteConsumerAsync` removes the consumer **and its delivery/ack state** on the server,
irreversibly — the same risk class as stream delete, so this reuses that change's confirmation
approach rather than the codebase's other, no-confirm Ctrl+D precedent
(`Components/ListEditorView<T>.TryDeleteItem`, which only ever deletes cheap local rows).

## Goals / Non-Goals

**Goals:**
- Ctrl+D on the consumer-level list, with a highlighted consumer, asks for confirmation naming
  that consumer, then deletes it on confirm and refreshes the consumer list.
- Reuse the exact `TryDeleteStreamAsync`/`NeighborStreamName` shape already in `StreamsTab`
  (confirm → try API call → refresh-on-success / `MessageBox.ErrorQuery`-on-failure), scoped to
  `_currentStream` the same way `OpenCreateConsumerDialog`/`TryCreateConsumerAsync` already are.

**Non-Goals:**
- No consumer edit — only delete is added; `nats-streams`' consumer-level read-only guarantee is
  otherwise untouched.
- No stream-level changes — `add-stream-delete` already covers that level.
- No "type the name to confirm" friction pattern — same reasoning as `add-stream-delete`: a single
  named Yes/No prompt is proportionate for a keyboard-driven TUI.

## Decisions

**Confirm via `MessageBox.Query`, Cancel as the Enter-default.** Identical shape to
`TryDeleteStreamAsync`:

```csharp
var choice = MessageBox.Query(
    App!, "Delete Consumer", $"Delete consumer '{name}'? This cannot be undone.",
    "_Delete", "_Cancel");
if (choice != 0) return; // Cancel (index 1) or Esc (null) both back out
```

Cancel stays last (the Enter-activated default), per `doc/terminal-gui-howto.md`'s button-order
gotcha and the same reasoning `add-stream-delete` already used for its own destructive prompt.

**Wiring mirrors `StreamListView`'s delete exactly.** `ConsumerListView` gets:
```csharp
AddCommand(Command.DeleteAll, () => { DeleteRequested?.Invoke(); return true; });
KeyBindings.Add(Key.D.WithCtrl, Command.DeleteAll);
```
alongside its existing `Command.New`/Ctrl+N block, plus a `DeleteRequested` event next to
`CreateRequested`. `StreamsTab` wires `_consumerListView.DeleteRequested += () => _ =
TryDeleteConsumerAsync()`:

```csharp
private async Task TryDeleteConsumerAsync()
{
    if (_currentStream is not { } stream) return;
    if (_consumerListView.SelectedConsumer?.Name is not { } name) return;

    var choice = MessageBox.Query(
        App!, "Delete Consumer", $"Delete consumer '{name}'? This cannot be undone.",
        "_Delete", "_Cancel");
    if (choice != 0) return;

    var neighborName = NeighborConsumerName(name);

    try {
        await _jetStream.DeleteConsumerAsync(stream, name);
        _ = RefreshConsumerListAsync(neighborName);
    } catch (Exception ex) {
        App?.Invoke(() => MessageBox.ErrorQuery(App!, "Delete Consumer Failed", ex.Message, "_Ok"));
    }
}
```

Scoped to `_currentStream` at call time, same as `OpenCreateConsumerDialog`/
`TryCreateConsumerAsync` — no extra state needed, since the consumer level is only reachable once
`Descend()` has set it.

**Selection lands on a neighbor, not the first item.** `NeighborConsumerName` mirrors
`NeighborStreamName` exactly, scanning `_consumerItems` instead of `_items` and comparing
`ConsumerInfo.Name` instead of `StreamInfo.Config.Name`, passed as `RefreshConsumerListAsync`'s
existing `selectName` parameter — the shared `DrillableListView<T>.ReplaceItems` fallback stays
untouched for every other caller.

**No-op when nothing is highlighted.** `TryDeleteConsumerAsync` reads
`_consumerListView.SelectedConsumer`; if it's `null` (empty list), the confirm prompt never opens
— same guard shape `TryDeleteStreamAsync` already uses.

**Confirmation stays inline, not a shared component.** Same reasoning as `add-stream-delete`: two
one-line `MessageBox.Query` call sites (stream delete, consumer delete) don't yet justify
extracting a `ConfirmDialog` helper. A third destructive-confirm site (e.g. KV/OBJ delete) would
be the point to revisit — not this change, and not a blocker for it either way, since
`dialog-content-spacing`'s title/message padding work applies equally regardless of how many call
sites end up sharing it.

## Risks / Trade-offs

- **[Risk]** A user fat-fingers Ctrl+D and then Enter out of habit → **Mitigation**: Enter
  activates Cancel (the last/default button), not Delete — same fix as `add-stream-delete`.
- **[Risk]** `DeleteConsumerAsync` succeeding server-side but the subsequent
  `RefreshConsumerListAsync` failing (e.g. a transient network blip, or the user having ascended
  back to the stream level mid-flight) would leave a deleted consumer still showing in the list →
  **Mitigation**: accepted — `RefreshConsumerListAsync`'s existing catch path already reports the
  error via `StatusChanged`, and its existing `_currentStream == stream` guard already discards a
  stale in-flight result if the user ascended away; no new failure mode is introduced.

## Migration Plan

No data migration. Purely additive UI/behavior; existing consumers, the stream level, and the
Read-Only List guarantee's edit-affordance (still absent) are unaffected. No feature flag — ships
as soon as merged.

## Open Questions

None.
