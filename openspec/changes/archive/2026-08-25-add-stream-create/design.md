## Context

The Streams tab (`Streams/StreamsTab.cs`, `StreamListView.cs`, `StreamDetails.cs`) is currently
read-only, per `nats-streams`' "Read-Only List" requirement. `StreamListView` extends
`Components/DrillableListView<T>`, which only wires Ctrl+R (Refresh) — unlike
`Components/ListEditorView<T>` (used by Subscriptions/Publish-headers), which bakes in Ctrl+N/E/D
but assumes the item collection itself is the source of truth and New/Edit run a single-field
modal. Neither base class fits as-is: this feature needs Create only (no Edit/Delete in this
slice — `nats-streams`' scenario "No mutation affordance is present at the consumer level" and
the delete/edit gap stay as-is), and the modal needs multiple fields of different kinds, which
`PatternDialog`/`HeaderDialog` (both single-`TextField` `Dialog<string>`) don't cover.

`NATS.Client.JetStream.Models.StreamConfig` (already used read-only via `StreamDetails.cs`) has
34 settable properties. Per the proposal, this slice exposes only Name, Subjects, Retention, and
Max Age; everything else is left for a future "Advanced" section. `StreamConfig` itself uses
type-default sentinels for "unset" (`0`/`-1` depending on the field, per-field and inconsistent —
see the `MaxMsgs`/`MaxBytes` landmine below), which is a poor shape for a dialog's own result
type: the dialog should be able to say "the user didn't set Max Age" as `null`, not as a
particular magic `TimeSpan`/`long` value it has to remember matches "unlimited" in this one NATS
API. This design keeps the dialog's result type and `StreamConfig` separate for that reason (see
Decisions).

## Goals / Non-Goals

**Goals:**
- Ctrl+N on the stream-level list opens a modal that creates a stream with the four essential
  fields, and the new stream appears highlighted in the list afterward.
- Establish the multi-field-dialog pattern (labeled fields, per-field validation, a single
  `Create` button, Esc-cancels) that a later Advanced-fields change and other future create/edit
  dialogs can reuse.
- Field parsing (Subjects splitting, Max Age duration) lives in dedicated, individually testable
  methods rather than inline in the dialog, so richer formats (NATS-CLI-style `7d`/`1h30m`
  duration shorthand, byte-size suffixes for a later Max Bytes field) can replace them later
  without touching dialog wiring.

**Non-Goals:**
- No Advanced fields (Storage, Max Bytes/Msgs/MsgSize, Replicas, Discard, Compression,
  mirror/source topology, ...) in this slice — deferred, per the proposal, to a later change.
- No stream Edit or Delete — `nats-streams`' consumer-level read-only guarantee and the
  stream-level absence of edit/delete both stay true; only Create is added.
- No NATS-CLI-style shorthand duration/size parsing (`7d`, `10MB`) — `TimeSpan.Parse`/
  `long.Parse` only, per explicit scope decision.

## Decisions

**`NewStreamOptions`: a dialog-owned result type, separate from `StreamConfig`.** The dialog
returns `Streams/NewStreamOptions.cs`, a plain record shaped around what the *user* expresses,
not around `StreamConfig`'s wire representation:

```csharp
internal sealed record NewStreamOptions(
    string Name,
    IReadOnlyList<string> Subjects,
    StreamConfigRetention Retention,
    TimeSpan? MaxAge);
```

`MaxAge` is `TimeSpan?` — `null` means "the user left it unset / unlimited", not a magic
`TimeSpan.Zero` the reader has to know is special. This is the seam the future Advanced page
grows from: each additional field (`MaxBytes`, `MaxMsgs`, ...) joins this record as its own
nullable CLR-natural type (`long?`, not `-1`-means-unlimited), so "not set" is always expressible
the same way regardless of what the underlying NATS field's own sentinel convention happens to be.

A separate `ToStreamConfig()` conversion (co-located in the same file, either an instance method
or an extension method on `NewStreamOptions`) is the single place that translates "the user's
intent" into `StreamConfig`'s actual wire shape, including the sentinel substitution described
next. `StreamsTab` calls `dialogResult.ToStreamConfig()` right before
`_jetStream.CreateStreamAsync(...)` — the dialog itself never constructs or even references
`StreamConfig`.

**Field → widget → parse mapping (dialog side, in terms of `NewStreamOptions`):**

| Field | Widget | Parse | Validity |
|---|---|---|---|
| Name | `TextField` | — | non-empty after trim |
| Subjects | `TextField` | split on space/comma/semicolon (`ParseSubjects(string) -> IReadOnlyList<string>`, dedicated method per the "extensible parsing" ask) | ≥1 non-empty subject after split |
| Retention | `DropDownList<StreamConfigRetention>` | — | always valid (has a default selection, `Limits`) |
| Max Age | `TextField` | `TryParseMaxAge(string, out TimeSpan? result)` — empty input succeeds with `result = null`; non-empty input succeeds with the parsed `TimeSpan` or fails (returns `false`) if `TimeSpan.Parse` throws | field is valid when `TryParseMaxAge` returns `true` (covers both "empty/unset" and "a real value") |

`DropDownList<TEnum>` (confirmed present in the installed Terminal.Gui 2.4.10 package,
`Terminal.Gui.Views.DropDownList<T>`, implements `IValue<TEnum?>` the same as other typed views)
is used over `OptionSelector<TEnum>` specifically for its compact single-line footprint — this
dialog already has 4 fields plus buttons, and leaving room for Advanced later favors the smaller
widget.

**Explicit safe defaults for `StreamConfig` fields `NewStreamOptions` doesn't carry yet.** A bare
`new StreamConfig(name, subjects)` leaves `MaxMsgs`/`MaxBytes`/`MaxConsumers`/`MaxMsgSize`/
`MaxMsgsPerSubject`/`NumReplicas` at the CLR default of `0`. `MaxMsgs`/`MaxBytes` are
`[JsonIgnore(Condition = Never)]` (always serialized) and their own doc comments say "`-1` for
unlimited" — `0` is a distinct, meaningful value (admit zero messages), not "unset". Sending an
unmodified default `StreamConfig` would therefore ask the server to create a stream that rejects
every message on arrival. To avoid that landmine, `ToStreamConfig()` explicitly sets
`MaxMsgs = -1`, `MaxBytes = -1`, `MaxConsumers = -1`, `MaxMsgSize = -1`,
`MaxMsgsPerSubject = -1` (all their own "unlimited" values) and `NumReplicas = 1`. Because these
live in the translation method rather than scattered at a dialog call site, the day `NewStreamOptions`
grows a real `MaxMsgs`/`NumReplicas` field, `ToStreamConfig()` is the one place that stops
hardcoding them — the dialog and its validation are untouched.

**Subjects field carries forward as free text for now.** A nested list-editor (New/Edit/Delete
rows for individual subjects) was considered and explicitly deferred — noted here because it's a
likely shape for the eventual Advanced page, not because it's needed now: `NewStreamOptions.Subjects`
(and `StreamConfig.Subjects` after translation) stays a flat list either way, so today's
delimiter-split `TextField` doesn't paint the later richer editor into a corner.

**Dialog shape: `Dialog<NewStreamOptions>` with a single `Create` button; Esc cancels.** Unlike
`PatternDialog`/`HeaderDialog` (Enter-on-the-single-field commits), Enter can't mean "submit" on
a specific field here — Tab must be free to move between 4 fields, so Enter pressed while a field
has focus is a no-op (the dialog overrides `OnAccepting` to swallow that bubbled, unhandled Accept
rather than let `Dialog<T>`'s own default handling silently close the dialog with `Result` unset,
discarding whatever was typed). `Create` is only enabled once Name/Subjects/Max Age are all valid;
invalid fields get the same red-text treatment `PatternDialog` already uses for its one field.
There is no separate `Cancel` button — Esc already cancels via `Dialog<T>`'s own built-in
behavior, the same as the buttonless `PatternDialog`/`HeaderDialog`, and a second button sharing
Create's mnemonic letter ("_Cancel" vs "_Create") would only collide.

**Ctrl+N lives on `StreamListView`, not the shared base.** `DrillableListView<T>` stays
Refresh-only (it's also the base for the consumer list, which must remain create-less per
`nats-streams`). `StreamListView` adds its own `Command.New` + `Key.N.WithCtrl` binding, the same
way `ConsumerListView` already layers Esc/Backspace on top of the shared base, and raises a
`CreateRequested` event that `StreamsTab` handles: run the dialog, translate the result via
`ToStreamConfig()`, call `CreateStreamAsync`, and on success call `_listView.ReplaceItems(...)`
with the refreshed list — `ReplaceItems`'s existing identity-preserving logic
(`DrillableListView<T>`) naturally highlights the new stream since it falls back to selecting by
identity/first-item after a replace, so no separate "select the new one" logic is needed as long
as the post-create refresh includes it.

**Errors surface via a modal `MessageBox.ErrorQuery`, not the `StatusChanged` status-bar path.**
`StatusChanged` is used elsewhere for background poll/refresh failures — transient, non-blocking,
fine to leave as a passive status-bar line. A failed `CreateStreamAsync` is the direct result of
an explicit user action and deserves a blocking acknowledgment instead of a line the user might
not even be looking at. `StreamsTab` catches the exception and calls
`MessageBox.ErrorQuery(App!, "Create Stream Failed", ex.Message, "_Ok")` — the body is exactly
`ex.Message`, nothing prepended/appended, matching the existing `MessageBox.Query(...)` precedent
in `MainWindow.cs` (feed-item detail). After the user dismisses it, `StreamsTab` reopens
`CreateStreamDialog` seeded with the just-entered `NewStreamOptions` (a new constructor overload,
`CreateStreamDialog(NewStreamOptions? initial = null)`) so nothing typed is lost and the user can
fix-and-retry without retyping. This resolves the "exact re-open-with-prior-values mechanics"
open question from the first pass of this design: reopen, pre-seeded, every time.

## Risks / Trade-offs

- **[Risk]** `TimeSpan.Parse`'s `[d.]hh:mm:ss[.fffffff]` format is unfriendly for a field whose
  whole purpose is "how many days should I keep this" → **Mitigation**: explicitly scoped-down by
  the user for this slice; the dedicated `TryParseMaxAge` method is the seam a friendlier parser
  drops into later without touching the dialog.
- **[Risk]** Explicitly setting five never-shown-in-UI fields (`MaxMsgs`, `MaxBytes`,
  `MaxConsumers`, `MaxMsgSize`, `MaxMsgsPerSubject` → `-1`, `NumReplicas` → `1`) inside
  `ToStreamConfig()` is invisible behavior a future reader could mistake for dead code →
  **Mitigation**: comment at the point of assignment explaining the CLR-default-vs-"unlimited"
  mismatch (this file is the durable record of the "why").
- **[Risk]** A second type parallel to `StreamConfig` is one more thing to keep in sync as
  Advanced fields get added later → **Mitigation**: accepted trade-off — `NewStreamOptions` only
  ever needs to grow in lockstep with what the dialog actually exposes, and `ToStreamConfig()` is
  the single, obvious place that breaks if the two drift, rather than sentinel values scattered
  across dialog validation code.
- **[Risk]** No server-side stream-name validation happens client-side beyond non-empty, so
  invalid names (NATS restricts stream names to a limited character set) only fail after the
  round-trip → **Mitigation**: acceptable for this slice since the failure still surfaces clearly
  via the `MessageBox.ErrorQuery` and the dialog reopens pre-seeded for a quick fix; client-side
  name-charset validation is a cheap follow-up, not a blocker.

## Migration Plan

No data migration. Purely additive UI/behavior; existing streams and the consumer-level
read-only behavior are unaffected. No feature flag — ships as soon as merged.

## Open Questions

- Whether Name gets client-side charset validation now or in a follow-up (see Risks) — leaning
  follow-up, confirm during implementation if it turns out cheap.
