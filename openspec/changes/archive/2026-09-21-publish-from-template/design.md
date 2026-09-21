## Context

`PublishDialog` (`src/lazynats/Publish/PublishDialog.cs`) is currently only ever constructed one
way - `new PublishDialog(connection)` from MainWindow's global Alt+P binding - and always starts
every field empty (`nats-publish`'s "Each open starts empty" scenario). `TemplateDialog`
(`src/lazynats/Templates/TemplateDialog.cs`) already solves an equivalent problem for Edit
Template: it seeds a `PayloadEditSection` from raw bytes via `SeedFromBytes`, rendering `Json`/
`Hex`/`Base64` the same way the read-only Message Detail view does, so the field starts formatted
for readability rather than as whatever was last stored.

The dependency direction today is Templates -> Publish: `TemplateDialog` already reuses
`HeaderPair`/`HeaderEditorView` from `lazynats.Publish`. This change keeps that direction - Publish
must not reference `Templates.Template`.

## Goals / Non-Goals

**Goals:**
- Let `PublishDialog` be constructed pre-populated (Subject, Headers, Payload Type, Payload)
  without changing its always-empty behavior when constructed the existing way.
- Reuse the same bytes-seeding rendering `TemplateDialog`'s Edit path already uses, so a
  Json-typed template opens pretty-printed, Hex grouped, Base64 wrapped - consistent with how the
  user already sees that same template's payload when editing it.
- Add the `P` shortcut to the Templates list following the exact wiring Export (`X`)/Import (`O`)
  already establish in `TemplatesTab`.

**Non-Goals:**
- No change to Alt+P's own behavior, to Send/Cancel/close-on-success, or to any other Publish
  dialog requirement.
- No "send and keep dialog open for another template" batch flow - this opens one pre-populated
  dialog per `P` press, same single-shot shape Alt+P already has.

## Decisions

- **Seed shape**: add a small `internal readonly record struct PublishSeed(string Subject,
  IReadOnlyDictionary<string, string> Headers, PayloadType PayloadType, byte[] PayloadBytes)` to
  `lazynats.Publish`, and an optional `PublishSeed? seed = null` parameter on `PublishDialog`'s
  constructor. Considered passing `Templates.Template` directly - rejected, since it would make
  `Publish` depend on `Templates` and invert the existing direction for no benefit (Publish only
  ever needs these four values, not the whole `Template`).
- **Payload seeding**: when `seed` is given, call `Layout()` then `_payloadSection.SeedFromBytes(seed.Value.PayloadBytes, seed.Value.PayloadType)` after the dialog's controls (including
  `_sendButton`, which `UpdateValidity` reads) are constructed - the exact sequencing
  `TemplateDialog`'s own `seedBytes` branch already uses, for the same reason (`SeedFromBytes`
  synchronously raises `Changed`, which drives `UpdateValidity`).
- **Header seeding**: when `seed` is given, populate `_headers` (the dialog's
  `ObservableCollection<HeaderPair>`) from `seed.Value.Headers` at construction, the same
  `Select(pair => new HeaderPair(pair.Key, pair.Value))` shape `TemplateDialog` already uses for
  its own `_headers` seed.
- **Subject seeding**: `_subjectField.Text = seed?.Subject ?? string.Empty` in place of the current
  always-empty initializer; `UpdateValidity()` at the end of the constructor already re-evaluates
  Send's enabled state, so no separate validity-seeding path is needed.
- **`TemplatesTab` -> `PublishDialog` wiring**: `TemplatesTab` gains a `NatsConnection connection`
  constructor parameter (MainWindow already holds `connection` at the point it constructs
  `templatesTab`, so this is a one-line call-site change) and a new `_extraOperations` entry
  `new ShortcutHint(Key.P, "Publish", OpenPublishDialog, Group: ExtraOperationsGroup)`, appended
  alongside Export/Import. `OpenPublishDialog()` returns early (no-op) when
  `_listView.SelectedTemplate is null`, mirroring `OpenEditDialog()`'s identical guard for `E` on
  an empty list. When a template is selected, it builds the seed the same way `OpenEditDialog()`
  already builds `seedBytes` for that template
  (`PayloadEncoding.ToBytes(template.PayloadType, template.Payload)`) and calls
  `App!.Run(new PublishDialog(connection, seed))` directly - no `AddTimeout` re-entrancy dodge
  needed here, since (unlike Alt+P) this isn't dispatched from inside a still-unwinding global key
  handler; it runs from `TemplatesTab.OnKeyDownNotHandled`, the same call stack Export/Import's own
  `App!.Run(picker)` already runs from without that dodge.
- **Key choice**: bare `P`, not Alt+P - list-focus-scoped like every other Templates operation
  (`N`/`D`/`E`/`F`/`X`/`O`), distinct from the existing global Alt+P. No collision: `P` is unused
  in `TemplateListView`'s `TabOperations` or `TemplatesTab`'s existing `_extraOperations`.

## Risks / Trade-offs

- [`PublishSeed` duplicates four fields already present on `Template`] -> Acceptable: it's a
  four-field read-only struct at a module boundary that otherwise has zero coupling to
  `Templates`; any second caller wanting to pre-populate Publish (there is none today) would reuse
  the same struct.
- [`SeedFromBytes`'s `Layout()`-before-seed call could behave differently for `PublishDialog`'s
  larger field set than it does for `TemplateDialog`'s] -> Mitigated by copying the exact sequencing
  (`Layout()` immediately before `SeedFromBytes`, after all buttons exist) `TemplateDialog` already
  proves works for a structurally identical dialog.

## Open Questions

None.
