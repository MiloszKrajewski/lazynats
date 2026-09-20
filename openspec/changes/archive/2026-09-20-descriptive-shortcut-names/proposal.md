## Why

`?` used to be the discovery mechanism it isn't: shortcut labels ("New", "Edit", "Delete",
"Filter") were originally sized for the status bar's limited width, so they stayed
single-word and generic. Now they're read in the shortcut picker dialog (`ShortcutPickerDialog`,
opened via `?`), which has room, but the labels didn't change with it. A user drilled into a
stream's consumers sees the same "New" the streams list itself would show — the label no longer
tells them what pressing N actually creates.

An earlier iteration of this change fixed that by giving each list an overridable single-word
noun (`ItemNoun`) and having the shared base classes compose it into a label
(`$"Add new {ItemNoun}"`, etc.). That was rejected on review: composing a label by interpolating
one noun into a fixed English sentence shape bakes an English-specific grammar rule (word order,
regular `+s` pluralization) into a shared base class that has no business knowing grammar —
irregular plurals ("Matrix"/"Matrices"), different capitalization depending on sentence position,
or any language whose word order or inflection differs from English all fall outside what a
single interpolated noun can express. This revision replaces noun-composition with each
subclass supplying the operation's full label text verbatim.

## What Changes

- `DrillableListView<T>`'s opt-in `EnableCreate()`/`EnableDelete()`/`EnableEdit()`/`EnableFilter()`
  methods each take a required `string label` parameter — the exact text to advertise for that
  operation — instead of taking no parameter and having the base compose one. There is no default;
  every call site must supply its own label.
- `EnableFilter(label)`'s label is used both as the advertised shortcut's text and as the filter
  dialog's title (the same string, one place it's authored) — replacing the separate
  `FilterDialogTitle` overridable property.
- `ListEditorView<T>`'s create/edit/delete operations are unconditionally available on every
  current subclass (never selectively opted out of), so — unlike `DrillableListView<T>`'s
  genuinely-opt-in shapes — their labels become required constructor parameters
  (`createLabel`/`editLabel`/`deleteLabel`) rather than method parameters. `EnableFilter(label)`
  mirrors `DrillableListView<T>`'s shape (still opt-in — no current subclass calls it, but the
  mechanism stays available).
- Every concrete list (`StreamListView`, `ConsumerListView`, both `BucketListView`s,
  `KeyListView`, `ObjectListView`, `TemplateListView`, `SubscriptionsView`, `HeaderEditorView`)
  supplies its own fully-written labels at the point it activates each operation, instead of a
  single noun the base composes.
- The shared Ascend ("Back") wiring is unchanged — it never took a label and still doesn't.
- Out of scope for this change (unchanged from the prior revision): Refresh/Search labels, and
  the handful of one-off tab-specific hints (`KeyListView`'s "View", `ObjectListView`'s
  "Download", `PayloadDetailSection`'s "Presentation").

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `drillable-list`: the Shared Create/Delete/Edit/Filter Wiring requirements change from
  base-composed, noun-derived labels to labels supplied verbatim by the activating subclass at
  the point it calls `EnableCreate`/`EnableDelete`/`EnableEdit`/`EnableFilter`. The Shared Ascend
  Wiring requirement (Back) is unchanged.
- `list-editor`: the create/edit/delete operations' labels become required constructor
  parameters (since these operations are always active, not opt-in), and the Filter operation's
  label follows the same verbatim, activation-site-supplied shape as `drillable-list`.

## Impact

- Code: `DrillableListView<T>` (`Enable*` method signatures, removal of `FilterDialogTitle`),
  `ListEditorView<T>` (constructor signature, `EnableFilter` signature, removal of
  `FilterDialogTitle`), plus every one of the 9 concrete list subclasses updated to pass its
  labels at the activation call site instead of overriding a noun property.
- No change to key bindings, dispatch, or the shortcut picker's rendering — only how each
  advertised `ShortcutHint.Text` value is supplied.
