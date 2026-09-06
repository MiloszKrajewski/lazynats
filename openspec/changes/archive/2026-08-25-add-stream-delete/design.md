## Context

The Streams tab (`Streams/StreamsTab.cs`, `StreamListView.cs`) is read-only at the stream level
except for creation, per `nats-streams`' "Read-Only List" requirement (already narrowed once, by
`add-stream-create`, to allow Create). `StreamListView` extends `Components/DrillableListView<T>`
(Ctrl+R only) and layers its own `Command.New` + Ctrl+N on top, raising a `CreateRequested` event
that `StreamsTab` handles. Delete follows the identical shape: a `Command.DeleteAll` + Ctrl+D
binding on `StreamListView`, a `DeleteRequested` event, and a handler in `StreamsTab`.

The codebase already has a Ctrl+D precedent — `Components/ListEditorView<T>.TryDeleteItem`
(`ListEditorView.cs:190`) — but it deletes immediately with no confirmation. That fits its actual
targets (in-memory subscription patterns, publish headers: local, free to undo by re-adding).
Stream delete is a different risk class: `DeleteStreamAsync` removes the stream **and all of its
messages** on the server, irreversibly. This design deliberately does not reuse that no-confirm
behavior — see Decisions.

## Goals / Non-Goals

**Goals:**
- Ctrl+D on the stream-level list, with a highlighted stream, asks for confirmation naming that
  stream, then deletes it on confirm and refreshes the list.
- Reuse the exact `OpenCreateStreamDialog`/`TryCreateStreamAsync` shape already in `StreamsTab`
  (dialog-or-confirm → try API call → refresh-on-success / `MessageBox.ErrorQuery`-on-failure) so
  the two mutating operations read the same way in the file.

**Non-Goals:**
- No consumer-level delete — `nats-streams`' consumer-level read-only guarantee is untouched (see
  proposal; tracked as a possible future slice, not part of this change).
- No purge/seal or other stream operations — delete only.
- No "type the stream name to confirm" friction pattern (as some tools use for destructive
  actions) — a single named Yes/No prompt is enough for a single-item, keyboard-driven TUI; typed
  confirmation is more at home in a web console where Ctrl+D can't be an accidental keystroke in
  quite the same way, but is still just as reachable here as any other shortcut, so a clear prompt
  is proportionate.

## Decisions

**Confirm via `MessageBox.Query`, Cancel as the Enter-default.** Terminal.Gui's `MessageBox.Query`
signature is `int? Query(IApplication app, string title, string message, params string[] buttons)`
— returns the clicked button's index, or `null` on Esc; per `doc/terminal-gui-howto.md`'s
gotcha #1, **the last button is always the default (Enter-activated)**. Because this action is
destructive, the safe outcome (Cancel) is deliberately last:

```csharp
var choice = MessageBox.Query(
    App!, "Delete Stream", $"Delete stream '{name}'? This cannot be undone.",
    "_Delete", "_Cancel");
if (choice != 0) return; // Cancel (index 1) or Esc (null) both back out
```

This is the opposite button order from a typical `Cancel … OK` layout (also called out in the
same howto gotcha) — here the "OK" action is the dangerous one, so it does *not* get the default
slot. Esc and explicit Cancel both map to "do nothing," so they're handled identically (`choice !=
0`), matching how `CreateStreamDialog`'s own Esc-cancels behavior needs no special-casing either.

**Wiring mirrors Create exactly, including error handling.** `StreamListView` gets:
```csharp
AddCommand(Command.DeleteAll, () => { DeleteRequested?.Invoke(); return true; });
KeyBindings.Add(Key.D.WithCtrl, Command.DeleteAll);
```
right alongside the existing `Command.New`/Ctrl+N block, and a `DeleteRequested` event next to
`CreateRequested`. `StreamsTab` wires `_listView.DeleteRequested += () => _ =
TryDeleteStreamAsync()`, following `TryCreateStreamAsync`'s exact pattern: try the call, on
success `_ = RefreshListAsync(neighborName)`, on failure `App?.Invoke(() =>
MessageBox.ErrorQuery(App!, "Delete Stream Failed", ex.Message, "_Ok"))`. Unlike create's failure
path, there's nothing to reopen/reseed on failure — delete has no dialog state to preserve, so the
handler just reports the error and stops.

**Selection lands on a neighbor, not the first item.** `DrillableListView<T>.ReplaceItems`'s
identity-preserving fallback (falling back to the first item when the previously-highlighted
item's identity is gone — see `openspec/specs/drillable-list/spec.md`'s "Identity-Preserving
Replace") is shared by every list built on that base and stays untouched; changing it would move
every other refresh's fallback too, including plain Ctrl+R after someone else deletes the
highlighted item. Instead, `TryDeleteStreamAsync` computes `neighborName` from `_items` *before*
deleting — the name of the stream immediately below the one being deleted, or immediately above it
if it was last, or `null` if it was the only item — and passes that as `RefreshListAsync`'s
`selectName`, the same parameter `TryCreateStreamAsync` already uses to highlight a specific
stream post-refresh. This keeps the shared base's spec'd fallback intact while giving delete a
better-than-default outcome, exactly the way `selectName` already exists to do.

**No-op when nothing is highlighted.** `TryDeleteStreamAsync` reads `_listView.SelectedStream`;
if it's `null` (empty list), the confirm prompt never opens — same guard shape `Descend()` already
uses for `_listView.SelectedStream?.Config.Name is not { } name`.

**Confirmation dialog stays inline in `StreamsTab`, not a separate reusable component.** A single
`MessageBox.Query` call is a one-line, single-use construct — extracting a
`ConfirmDialog`/`ConfirmationHelper` for one call site would be premature; if a second destructive
confirm shows up later (e.g. KV bucket delete, OBJ store delete), that's the point to look for a
shared helper, not before.

## Risks / Trade-offs

- **[Risk]** A user fat-fingers Ctrl+D and then Enter out of habit → **Mitigation**: Enter now
  activates Cancel (the last/default button), not Delete — the exact fix for this scenario, per
  the button-order decision above.
- **[Risk]** `DeleteStreamAsync` succeeding server-side but the subsequent `RefreshListAsync()`
  failing (e.g. transient network blip right after) would leave a deleted stream still showing in
  the list → **Mitigation**: accepted — `RefreshListAsync`'s existing catch path already reports
  the error via `StatusChanged` and leaves the stale list as-is; the user's next Ctrl+R clears it.
  No new failure mode is introduced here beyond what `RefreshListAsync` already tolerates.

## Migration Plan

No data migration. Purely additive UI/behavior; existing streams and the consumer-level read-only
behavior are unaffected. No feature flag — ships as soon as merged.

## Open Questions

None.
