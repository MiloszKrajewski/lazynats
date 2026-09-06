## Context

The KV tab's bucket level (`KVStore/KvTab.cs`, `BucketListView.cs`) is read-only except for
creation, per `nats-kv`'s "Read-Only Tab" requirement — already narrowed once, by `add-kv-tab`'s
follow-on create-bucket work, to allow Create. `BucketListView` extends
`Components/DrillableListView<T>` (Ctrl+R only) and layers its own `Command.New` + Ctrl+N on top,
raising a `CreateRequested` event that `KvTab` handles. This mirrors the Streams tab's stream
level exactly, which already has delete (`add-stream-delete`): a `Command.DeleteAll` + Ctrl+D
binding on `StreamListView`, a `DeleteRequested` event, and `TryDeleteStreamAsync` in
`StreamsTab`. `add-consumer-delete` later repeated the identical shape one level down for
consumers. Bucket delete is the same shape at the KV tab's top (list) level.

`DeleteStoreAsync` removes the bucket **and every key/value/history it holds** on the server,
irreversibly — the same risk class as stream and consumer delete, so this reuses those changes'
confirmation approach (a single named `MessageBox.Query`, `DialogText.Pad`-wrapped per the
current dialog-spacing convention) rather than the codebase's other, no-confirm Ctrl+D precedent
(`Components/ListEditorView<T>.TryDeleteItem`, which only ever deletes cheap local rows).

## Goals / Non-Goals

**Goals:**
- Ctrl+D on the bucket-level list, with a bucket highlighted, asks for confirmation naming that
  bucket, then deletes it on confirm and refreshes the bucket list.
- Reuse the exact `TryDeleteStreamAsync`/`NeighborStreamName` shape already in `StreamsTab`
  (confirm → try API call → refresh-on-success / `MessageBox.ErrorQuery`-on-failure), applied to
  `KvTab` at the bucket level the same way `OpenCreateBucketDialog`/`TryCreateBucketAsync` already
  live there.

**Non-Goals:**
- No key-level delete — the key level's read-only guarantee (`nats-kv`'s "Read-Only Tab") is
  otherwise untouched; only the bucket level gains delete.
- No "type the name to confirm" friction pattern — same reasoning as `add-stream-delete` and
  `add-consumer-delete`: a single named Yes/No prompt is proportionate for a keyboard-driven TUI,
  and staying consistent across all three delete sites matters more than extra friction for the
  admittedly larger blast radius of a bucket (which can hold many keys) versus a single stream or
  consumer.
- No stream/consumer-level changes — those are already covered by prior changes.

## Decisions

**Confirm via `MessageBox.Query`, Cancel as the Enter-default.** Identical shape to
`TryDeleteStreamAsync`/`TryDeleteConsumerAsync`:

```csharp
var choice = MessageBox.Query(
    App!, DialogText.Pad("Delete Bucket"),
    DialogText.Pad($"Delete bucket '{name}'? This cannot be undone."),
    "_Delete", "_Cancel");
if (choice != 0) return; // Cancel (index 1) or Esc (null) both back out
```

Cancel stays last (the Enter-activated default), per `doc/terminal-gui-howto.md`'s button-order
gotcha and the same reasoning the two prior delete changes already used for their own destructive
prompts. `DialogText.Pad` wraps both title and message, matching every other `MessageBox.Query`/
`ErrorQuery` call site added since `dialog-content-spacing` landed.

**Wiring mirrors `StreamListView`'s delete exactly.** `BucketListView` gets:
```csharp
AddCommand(Command.DeleteAll, () => { DeleteRequested?.Invoke(); return true; });
KeyBindings.Add(Key.D.WithCtrl, Command.DeleteAll);
```
alongside its existing `Command.New`/Ctrl+N block, plus a `DeleteRequested` event next to
`CreateRequested`, and a `Shortcuts` entry appended the same way `StreamListView.Shortcuts` does.

`KvTab` wires `_listView.DeleteRequested += () => _ = TryDeleteBucketAsync()`:

```csharp
private async Task TryDeleteBucketAsync()
{
    if (_listView.SelectedBucket is not { } status) return;
    var name = BucketName.From(status);

    var choice = MessageBox.Query(
        App!, DialogText.Pad("Delete Bucket"),
        DialogText.Pad($"Delete bucket '{name}'? This cannot be undone."),
        "_Delete", "_Cancel");
    if (choice != 0) return;

    var neighborName = NeighborBucketName(name);

    try {
        await _kv.DeleteStoreAsync(name);
        _ = RefreshListAsync(neighborName);
    } catch (Exception ex) {
        App?.Invoke(() => MessageBox.ErrorQuery(App!, DialogText.Pad("Delete Bucket Failed"), DialogText.Pad(ex.Message), "_Ok"));
    }
}
```

No `_currentBucket` scoping is needed (unlike consumer delete's `_currentStream`) — the bucket
level is the top level of the KV tab, reachable whenever `_currentBucket is null`, and
`BucketListView`'s own Ctrl+D binding only fires while that list holds focus, i.e. only at that
level.

**Selection lands on a neighbor, not the first item.** `NeighborBucketName` mirrors
`NeighborStreamName`/`NeighborConsumerName` exactly, scanning `_items` (the
`ObservableCollection<NatsKVStatus>` already backing `BucketListView`) and comparing
`BucketName.From(item)` instead of `Config.Name`/`.Name`, passed as `RefreshListAsync`'s existing
`selectName` parameter (already used by `TryCreateBucketAsync`) — the shared
`DrillableListView<T>.ReplaceItems` fallback stays untouched for every other caller.

**No-op when nothing is highlighted.** `TryDeleteBucketAsync` reads `_listView.SelectedBucket`; if
it's `null` (empty list), the confirm prompt never opens — same guard shape
`TryDeleteStreamAsync`/`TryDeleteConsumerAsync` already use.

**Confirmation stays inline, not a shared component.** Same reasoning `add-consumer-delete`
already gave for not extracting a helper at two call sites: this would be the third
(`MessageBox.Query` delete-confirm at stream, consumer, and now bucket level), still not enough
churn to justify a `ConfirmDialog` abstraction on its own — worth revisiting only if a fourth
site (e.g. OBJ store delete) shows up, and not a blocker for this change either way.

## Risks / Trade-offs

- **[Risk]** A user fat-fingers Ctrl+D and then Enter out of habit → **Mitigation**: Enter
  activates Cancel (the last/default button), not Delete — same fix as the stream/consumer delete
  changes.
- **[Risk]** A bucket delete has a larger blast radius than a single stream or consumer delete
  (it removes every key, value, and history entry the bucket holds) → **Mitigation**: accepted,
  matching the Non-Goals decision above — the confirmation prompt names the bucket and states the
  action is irreversible, same as the other two delete sites; a heavier confirmation flow is not
  introduced for consistency's sake.
- **[Risk]** `DeleteStoreAsync` succeeding server-side but the subsequent `RefreshListAsync`
  failing (e.g. a transient network blip) would leave a deleted bucket still showing in the list
  → **Mitigation**: accepted — `RefreshListAsync`'s existing catch path already reports the error
  via `StatusChanged` and leaves the previous list in place rather than clearing it, same as every
  other refresh failure in this tab.

## Migration Plan

No data migration. Purely additive UI/behavior; existing buckets, the key level, and its
Read-Only guarantee are unaffected. No feature flag — ships as soon as merged.

## Open Questions

None.
