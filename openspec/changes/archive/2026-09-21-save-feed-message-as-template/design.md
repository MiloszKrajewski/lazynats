## Context

`TemplatesTab` already owns two flows that build a `Template` seed value and hand it to
`TemplateDialog`: a blank `N` (Create) and a fully-seeded `P` (pre-populate `PublishDialog` from a
template - the reverse direction of this change). It also owns the only write path
(`EnsureBucketExistsAsync` + `WriteAsync`, including the error-reopen-with-values-intact retry
loop). `LiveUpdatesView` already raises one message-scoped event, `ItemSelected`, consumed by
`MainWindow` to open `MessageDetailDialog(envelope)`. This change adds a second message-scoped
action to the Live Feed and threads it into `TemplatesTab`'s existing dialog/write machinery
without duplicating either.

## Goals / Non-Goals

**Goals:**
- One new bare-key shortcut (`T`) on the Live Feed, scoped to the highlighted row, opening the
  Create Template dialog pre-populated from that message.
- Reuse `TemplatesTab`'s existing `EnsureBucketExistsAsync`/`WriteAsync` write path unchanged -
  this is still "Create Template", just seeded differently.
- Reuse the same bytes-seeding rendering `Edit Template` already uses (pretty-printed Json,
  grouped Hex, wrapped Base64, decoded Text) so the pre-populated Payload field is immediately
  readable, not a raw dump of whatever the wire happened to carry.

**Non-Goals:**
- No new entry point on `MessageDetailDialog` - see Decision 2.
- No change to `TemplateDocument`'s storage shape, to `PublishDialog`, or to how templates are
  listed/edited/deleted/exported/imported.
- No batch/multi-select "save several messages as templates" - one highlighted message at a time,
  same scoping every other feed-row action already uses.

## Decisions

### Decision 1: Entry point is a bare `T` on the Live Feed list, not the Message Detail dialog

The Live Feed's `LiveUpdatesView` already exposes row-scoped bare-key actions (`C` Clear, `Space`
Follow/Pause) via `IShortcutSource`, plus `Enter` to open `MessageDetailDialog`. `T` ("Save as
Template") joins that set, scoped to whatever `_listView.SelectedItem` currently points at - the
same lookup `OnAccepted` already performs for `Enter`, so it works identically whether the feed is
following (selection tracks the newest message) or sticky (selection is pinned).

Alternative considered: add the affordance inside `MessageDetailDialog` instead (once a user has
already opened a message to inspect it, offer to save it from there too). Rejected: the
`message-detail-dialog` spec's "Dialog Is Read-Only" requirement is explicit that the dialog offers
"no send/edit affordance" and exists "solely to display the message's content" - adding any
mutating action there, even one that doesn't touch the message itself, cuts against that
requirement's intent and would need to relitigate it. Keeping the action on the list row instead
needs no change to that spec at all.

### Decision 2: `T` letter choice

`C` and `Space` are already bound on `LiveUpdatesView`; `Enter` opens the detail dialog. `T` is
unused there, isn't shadowed by `ListView`'s own default bindings (`KeystrokeNavigator` is already
nulled out on this list, so plain letter keys never trigger quick-jump navigation), and matches
this codebase's convention of a single mnemonic letter per list-scoped action (`N`/`E`/`D`/`R`/`X`/
`O`/`P` on Templates, `C`/`Space` here) - "**T**emplate" reads naturally next to those.

### Decision 3: Boundary shape between `LiveFeed` and `Templates`

`TemplatesTab` gains one new public method, e.g. `OpenCreateDialogFromMessage(string subject,
NatsHeaders? headers, byte[] payloadBytes)`, taking the same primitive NATS Core types
`MainWindow` already reaches into `FeedEnvelope.Message` for today (see its existing
`liveUpdates.ItemSelected += envelope => App!.Run(new MessageDetailDialog(envelope))` wiring) -
not a `FeedEnvelope` or `NatsMsg<byte[]>` parameter, and not a new cross-namespace seed type.
`LiveUpdatesView` raises a new event (e.g. `SaveAsTemplateRequested`) carrying the selected
`FeedEnvelope`, exactly like `ItemSelected` does today; `MainWindow` unpacks it and calls the new
`TemplatesTab` method, the same shape of glue it already writes for `ItemSelected`.

This mirrors the existing `PublishSeed` precedent in the opposite direction: that boundary value
exists specifically because `Publish` must not depend on `Templates`. Here, `Templates` gains a
new public entry point rather than depending on `LiveFeed`'s `FeedEnvelope` type, and `LiveFeed`
gains no dependency on `Templates` at all - `MainWindow`, which already depends on both, is the
only place that touches both sides, same as its existing `ItemSelected` wiring.

Inside `OpenCreateDialogFromMessage`, `TemplatesTab` builds its own `Template` seed value (Name
empty, Subject as given, Headers converted from `NatsHeaders` the same key/value way
`MessageDetailDialog.FormatHeaders` already does, Payload Type defaulted via
`PayloadContentProbe.Classify(payloadBytes)` + `PayloadPresentation.DefaultType(...)` - empty
payload bytes default to `PayloadType.Text`, matching Create Template's own blank-dialog default -
and a placeholder Payload string, immediately overwritten by seeding from bytes) and opens
`TemplateDialog(seed, isEdit: false, seedBytes: payloadBytes)` - the same `seedBytes` rendering
path `OpenEditDialog` already uses, just with `isEdit: false` so Name stays blank and editable. On
`dialog.Result`, it calls the existing private `WriteAsync(template, isEdit: false)` unchanged.

No fresh classification caching is threaded through from `FeedEnvelope.CachedContentKind`: that
cache exists to keep per-row *rendering* cost off the hot redraw path (`live-feed`'s "Payload
Classification and Rendering Are Cached Per Envelope" requirement), not to speed up an explicit,
one-off user action - classifying once here is negligible.

### Decision 4: No special-casing of `TemplatesTab`'s lazy first-load

`TemplatesTab` only fetches its list on first becoming visible (`OnVisibleChanged`'s `_loaded`
guard) or on manual `R`efresh. `WriteAsync`'s own `RefreshListAsync(template.Name)` call already
refreshes and highlights the new template regardless of whether the tab has ever been visited, so
Save-as-Template needs no extra wiring for that. If the user later switches to the Templates tab
for the first time in the session, its own one-time `_loaded` fetch still fires - a harmless,
already-existing redundant fetch, not worth special-casing away.

## Risks / Trade-offs

- [Highlighting the wrong message] The Live Feed's selection semantics (following vs. sticky) are
  already well-established for `Enter`/detail-viewing; `T` reuses the identical lookup, so there's
  no new risk surface beyond what `Enter` already carries.
- [Payload Type mismatch on save] Auto-classification is a default, not a lock - like every other
  Create Template dialog, Payload Type remains a selectable field before confirming, so a
  misclassified payload (e.g. JSON-shaped plain text) is trivially correctable before Create.
- [Feature discoverability] `T` is advertised through the same `IShortcutSource`/shortcut-picker
  (`?`) mechanism every other list shortcut already uses, so no separate documentation surface is
  needed.
