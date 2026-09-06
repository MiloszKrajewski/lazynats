## Why

Composing a Publish message from scratch every time is repetitive for messages a user sends
often (health checks, standard test payloads, ...). `doc/UI.md`'s "Message templates" section
already sketches this as a stretch goal; this change delivers a first, storage-and-CRUD-only slice
of it, scoped down from that sketch (flat list, no template groups, no `hex` payload type - see
Design Decisions) so it can land as `5:Templates`, the app's fifth management tab.

## What Changes

- Add a `5:Templates` management tab (Alt+5): a flat list (no drill-down) with a details pane,
  the same LHS-list/RHS-details split Streams/Values/Objects already use (`DrillableListView<T>` +
  `PollingDetailsView<TTarget, TInfo>`), just without a second, drilled-into level - following the
  same tab-owned Ctrl+N/Ctrl+D/Ctrl+E/Ctrl+R/Ctrl+F shortcut dispatch as those tabs
  (`tab-scoped-list-shortcuts`).
- Templates are stored as JSON documents in a dedicated NATS KV bucket, `lazynats-templates`, one
  entry per template keyed by the template's name.
- The bucket is **not** created when the tab loads or lists templates - a missing bucket simply
  reads as "no templates yet" (empty-hint list). It is created lazily, on first successful
  create/edit write, the same "create on demand" shape `nats-kv`'s bucket-creation-on-write
  pattern already uses elsewhere in this codebase.
- A template consists of: Name (a KV key, so it follows the same NATS key-naming constraints as
  any KV key), Subject, Headers (string/string pairs), Payload Type (`Json` | `Text` | `Base64`),
  and Payload (a string, interpreted per Payload Type).
- Create/Edit reuses the Publish dialog's field shapes (Subject as a single-line field, Headers as
  a keyboard-only New/Edit/Delete list, Payload as a multi-line text area), adding a Name field
  (locked once created, like `CreateKeyDialog`'s Name-on-edit) and a Payload Type selector.
  Selecting `Json` enforces that Payload parses as valid JSON before Save is enabled; selecting
  `Base64` enforces that Payload parses as valid base64. `Text` accepts any string unvalidated.
- Delete removes the template's KV entry, with the same confirm-before-delete prompt
  Streams/Values/Objects already use for their own deletes.
- Out of scope for this change: any "use this template to send a message" action. That is a
  planned follow-up, expected to be substantially more than "open Publish pre-filled" (candidate
  future actions: Publish, Query, Import, Export) and deserves its own proposal.
- `lazynats-templates` is **not** hidden from the existing Values tab's KV bucket list - unlike
  the `KV_`-prefix convention `nats-kv`/`nats-obj` already use to hide JetStream's own KV/OBJ
  backing streams from the Streams tab, this bucket is an ordinary, user-manageable KV bucket that
  happens to be populated through a dedicated UI; a power user is free to inspect or delete it via
  Values like any other bucket.

## Capabilities

### New Capabilities
- `nats-templates`: the Templates management tab - template storage shape (KV bucket, JSON
  document per entry), list/create/edit/delete/refresh/filter operations, and payload-type-aware
  validation.

### Modified Capabilities
- `drillable-list`: fixes a pre-existing defect in "Shared Descend Wiring" - Enter on a "leaf"
  list (one with nothing to descend into, so no owning tab subscribes to the descend-requested
  event) used to fall through and move keyboard focus to the list's attached search field instead
  of doing nothing. Noticed while building the flat Templates list (this change's first
  standalone, non-drill-down consumer of `DrillableListView<T>` with an attached search field);
  also affects the pre-existing Values/Objects key/file lists, fixed the same way. `polling-details`
  and `list-editor` (for the Create/Edit dialog's Headers field), plus `tab-scoped-list-shortcuts`,
  `keyboard-shortcut-discovery`, and `edit-frame`, are otherwise reused as they already exist, with
  no requirement changes needed in any of them.

## Impact

- New `src/lazynats/Templates/` folder (tab, list view, details pane, template dialog, presenter,
  model types), mirroring the one-folder-per-tab structure `tab-content-structure` establishes.
- `MainWindow.cs`: construct and register the new tab (`INatsKVContext` is already resolved there
  for `ValuesTab`; the new tab reuses the same dependency), add its Alt+5 top-level shortcut and
  status-bar wiring.
- No changes to NATS server-side behavior beyond normal KV reads/writes/bucket-creation against a
  new, app-specific bucket name.
