## Context

Streams, Consumers, KV buckets, and OBJ buckets each already have: a `DrillableListView<T>`
subclass (list + Ctrl+R/N/D wiring), a `Create*Dialog: Dialog<NewXOptions>` (multi-field modal,
`initial` seed param, per-field validation, a single `_createButton`), a `NewXOptions` record
whose `ToXConfig()` builds a **from-scratch** wire config — filling every field the dialog doesn't
expose with a Create-safe default (e.g. `NewStreamOptions.ToStreamConfig()` hardcodes
`MaxMsgs = -1, MaxBytes = -1, MaxConsumers = -1, NumReplicas = 1, ...`), and a `Try*Async` method
on the owning tab that does `try { await CreateXAsync(...) } catch { ErrorQuery(ex.Message); reopen
pre-filled; }`.

Live testing against the dev server (`nats-server 2.12.8`, via `.bin/nats.exe`, throwaway
resources cleaned up afterward) established exactly which fields each entity's dialog exposes are
actually mutable post-creation:

- **Stream**: Subjects and MaxAge update cleanly. Storage and Retention are hard-rejected —
  `err_code 10052`, `"stream configuration update can not change storage type"` /
  `"...can not change retention policy to/from workqueue"`.
- **Consumer**: FilterSubjects updates cleanly (`nats consumer edit --filter=...`). AckPolicy and
  DeliverPolicy are hard-rejected — `err_code 10012`, `"ack policy can not be updated"` /
  `"deliver policy can not be updated"`. (Sanity check: `max_ack_pending`, a field not in this
  dialog at all, updated fine — confirms the two rejections are real policy, not a malformed
  request.)
- **KV bucket**: History (`max_msgs_per_subject`), MaxAge, and Limit Marker TTL
  (`subject_delete_marker_ttl` + `allow_msg_ttl`) all update cleanly — including *enabling* the
  marker TTL on a bucket created without one. Storage and Retention are hard-rejected, same
  `err_code 10052` as Stream (KV buckets are backed by a stream).
- **OBJ bucket**: only MaxAge is in the dialog at all; by the same Stream-backing mechanics, it's
  expected to update the same way MaxAge does for Stream/KV (not separately re-verified, since
  it's the identical underlying mechanism already confirmed twice).

`INatsObjContext` (`NATS.Client.ObjectStore` 2.8.2) has no bucket-level update method — only
`CreateObjectStoreAsync`/`GetObjectStoreAsync`/`DeleteObjectStore` and object-level
`UpdateMetaAsync`. `ObjTab` already works around the parallel gap in *listing* (no
bucket-enumeration call either) by calling `INatsJSContext.ListStreamsAsync()` directly and
filtering to the `OBJ_` prefix — it already holds an `INatsJSContext` alongside `INatsObjContext`
for exactly this reason.

## Goals / Non-Goals

**Goals:**
- Ctrl+E on the Stream, Consumer, KV bucket, and OBJ bucket lists opens the matching Create dialog
  in edit mode, seeded from and constrained to what the server will actually accept.
- Edit never silently clobbers server-side config the dialog doesn't expose (replica count,
  discard policy, byte/message limits, description, metadata, ...).
- Edit failure behaves exactly like Create failure: `ex.Message` in a modal, dialog reopens
  pre-filled, Esc to back out.

**Non-Goals:**
- KV key edit, OBJ object edit/upload/content access — both stay fully read-only, untouched by
  this change.
- Exposing fields beyond what each Create dialog already collects (e.g. not adding Consumer's
  AckWait/MaxDeliver/MaxAckPending, even though those are genuinely mutable) — this change reuses
  existing dialogs as-is, it doesn't grow them.
- Storage-backend conversion, retention-policy conversion, Consumer AckPolicy/DeliverPolicy
  editing — all confirmed server-rejected; these fields stay disabled, not routed to the server.
- Adding a bucket-level update method to `INatsObjContext` upstream — the raw-stream workaround is
  scoped narrowly, not a general replacement for the missing API.

## Decisions

**1. `isEdit: bool` constructor flag on each existing dialog, not a new class or enum.**
Confirmed with the user. Each `Create*Dialog` grows `isEdit = false` as a constructor parameter.
`isEdit == true` flips: `Title` to `"Edit X"`, the confirm button's text/mnemonic from `"_Create"`
to `"_Save"`, and — per entity, from the table below — the corresponding fields' `Enabled = false`
(shown with their current value, not hidden, so the user can see what's locked and why validation
still passes). `UpdateValidity()` is unaffected: a disabled field's existing value is already
valid by construction (it came from the server), so validation logic doesn't need an edit-mode
branch.

Locked-field set per entity (fields not listed are already outside that dialog):

| Entity     | Name   | Subjects/Filter | Retention/Storage | MaxAge   | Ack/Deliver |
|------------|--------|------------------|--------------------|----------|-------------|
| Stream     | locked | editable         | locked             | editable | n/a         |
| Consumer   | locked | editable         | n/a                | n/a      | locked      |
| KV bucket  | locked | n/a              | locked             | editable | n/a         |
| OBJ bucket | locked | n/a              | n/a                | editable | n/a         |

Name is locked on every entity for the same reason: it's the update target's identity, not a
value being changed.

**2. Edit always merges onto the live-fetched config; it never calls `NewXOptions.ToXConfig()`.**
This is the load-bearing decision. `ToXConfig()` is a *Create*-time constructor — every field the
dialog doesn't expose gets a hardcoded Create-safe default. If `TryEditStreamAsync` called
`options.ToStreamConfig()` the way `TryCreateStreamAsync` calls it, every edit would silently
reset `NumReplicas` to 1, `MaxBytes`/`MaxMsgs`/`MaxConsumers`/`MaxMsgSize` to unlimited, discard
policy, description, metadata, etc. back to Create defaults — on *every* edit, even one that only
touches MaxAge.

Instead: the edit dialog is opened with the entity's **currently-fetched full wire config**
(`StreamConfig` for Stream, `ConsumerConfig` for Consumer, `NatsKVConfig` for KV, `StreamConfig`
again for OBJ) captured alongside the `NewXOptions` seed built from it. On commit, the owning
tab applies only the unlocked field(s) onto a `with`-copy of that original config —
`original with { Subjects = edited.Subjects, MaxAge = edited.MaxAge ?? TimeSpan.Zero }` for
Stream, not `edited.ToStreamConfig()` — and sends *that* to `UpdateStreamAsync`/
`UpdateConsumerAsync`/`UpdateStoreAsync`. Every field the dialog doesn't touch round-trips
untouched because it was never reconstructed.

**Consumer's FilterSubject/FilterSubjects duality.** `NewConsumerOptions.ToConsumerConfig()`'s own
comment already establishes the app's convention: always write the plural `FilterSubjects`, never
the singular `FilterSubject`. A consumer created outside lazynats (e.g. `nats consumer add
--filter=X`, as used in this change's own testing) can have the singular field set instead. Edit's
merge must explicitly null out `FilterSubject` whenever it sets `FilterSubjects`, regardless of
which one the original had — otherwise a consumer with singular-only history keeps both fields
populated inconsistently after its first lazynats-driven edit.

**3. OBJ bucket edit bypasses `INatsObjContext` and calls `INatsJSContext.UpdateStreamAsync`
directly against `OBJ_<bucket>`**, following the existing precedent `ObjTab.RefreshListAsync`
already set for listing. `_jetStream` is already a constructor dependency of `ObjTab` — no new
wiring needed to reach it. Per Decision 2, this fetches the bucket's current `StreamInfo.Config`
(already available — `ObjTab`'s bucket list is literally a `List<StreamInfo>`, so the selected
item already carries it) and applies `with { MaxAge = edited.MaxAge ?? TimeSpan.Zero }` onto it,
touching nothing else.

**4. `DrillableListView<T>.EnableEdit()`** mirrors `EnableCreate`/`EnableDelete` exactly: an
`EditRequested` event, Ctrl+E via `AddCommand(Command.Edit, ...)`, and an "Edit" entry in
`Shortcuts`, activatable independently of the other opt-in shapes (per `drillable-list`'s existing
"Subclass-Defined Navigation Commands" requirement). Matches `ListEditorView<T>`'s existing Ctrl+E
convention elsewhere in the app (Subscriptions, Publish headers) — no new keybinding convention.

**5. Error handling matches Create exactly.** `Try*Async` catches, shows
`MessageBox.ErrorQuery(App!, DialogText.Pad("Edit X Failed"), DialogText.Pad(ex.Message), "_Ok")`,
then reopens the edit dialog seeded with the values the user had entered (not re-fetched from the
server) so a rejected attempt can be adjusted without retyping everything. The user can Esc out if
the failure means a field they thought was safe actually wasn't.

## Risks / Trade-offs

- **[Risk]** OBJ's raw-stream-update path is exactly the pattern `nats stream edit` itself refuses
  to do non-interactively for a KV-backing stream without an extra confirmation, warning:
  *"Operating on the underlying stream of a[n object store] bucket is dangerous... can lead [to]
  unexpected outcomes, data loss and, technically, will mean your bucket is no longer a [valid]
  bucket."* → **Mitigation**: scope this path to exactly one field (MaxAge), always built from a
  `with`-copy of the just-fetched `StreamInfo.Config` (never a reconstructed config), so every
  OBJ-internal invariant (subjects shape, discard policy, rollup/delete-deny flags, ...) is
  byte-for-byte preserved. If `INatsObjContext` ever grows a real bucket-update method, this path
  is a drop-in replacement target, not a permanent architecture.
- **[Risk]** KV bucket edit *does* have a proper API (`INatsKVContext.UpdateStoreAsync`) — unlike
  OBJ, there is no excuse to drop to raw stream update there, and doing so would trip the same CLI
  warning for no reason. → **Mitigation**: KV edit always goes through `UpdateStoreAsync`; the raw
  stream update path is OBJ-only, and this asymmetry should be called out in code comments so a
  future change doesn't "fix" KV into matching OBJ's workaround.
- **[Risk]** A field disabled in the dialog is a UI-layer guard, not a source of truth — the
  server's own rejection (Decision 5's error path) is what actually prevents a bad write if the
  disabled-field set is ever wrong for a given server version. → **Mitigation**: acceptable as-is;
  this is the same trust boundary Create's own validation already operates under.
- **[Trade-off]** Locked fields are shown-but-disabled rather than hidden, so the dialog's layout
  is identical between New and Edit modes (no field reflow) at the cost of some visual clutter
  (a Retention dropdown the user can see but never touch, mid-edit). Accepted: matches how the
  user described the feature ("disabled", not "removed").

## Migration Plan

No data migration. Pure UI/client addition — no schema, storage, or NuGet dependency changes.
Manual verification: `tmux` + `dotnet run` per `CLAUDE.md`, exercising Ctrl+E on all four lists
against a real `nats-server`, including at least one forced server-side rejection (e.g. attempt to
observe the Retention-locked field truly can't reach the server, not just that the UI disables
it) to confirm the error-dialog/reopen path.

## Open Questions

- OBJ's MaxAge-update mechanics were inferred from Stream/KV's identical underlying mechanism, not
  independently re-verified live — worth a quick empirical check during implementation (same
  throwaway-bucket-and-cleanup approach used for Stream/Consumer/KV in this design) before
  treating it as settled.
