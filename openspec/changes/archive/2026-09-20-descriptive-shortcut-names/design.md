## Context

`DrillableListView<T>` and `ListEditorView<T>` (see `openspec/specs/drillable-list/spec.md` and
`openspec/specs/list-editor/spec.md`) are the two shared bases behind every management tab's list.
The shared Create/Delete/Edit/Filter wiring's advertised `ShortcutHint.Text` hardcoded generic
single-word labels ("New", "Edit", "Delete", "Filter") in the base classes, fine while the label
only had to fit the status bar. Now that `?` opens `ShortcutPickerDialog` with room for a full
line, the missing item-type context is what stops the user from knowing what N actually creates —
worse the moment a drillable list is reused at more than one level with different item types (e.g.
`ConsumerListView` nested under `StreamsTab`).

A first pass at fixing this gave each list an overridable single-word noun (`ItemNoun`,
mirroring the existing `EmptyHintText`/`FilterDialogTitle` per-subclass-override idiom) and had
the shared base compose it into a label: `$"Add new {ItemNoun}"`, `$"Edit {ItemNoun}"`, `$"Delete
{ItemNoun}"`. That shipped, built, and was verified via tmux across every tab/level — then was
rejected on review. The problem isn't cosmetic: interpolating one noun into a fixed sentence shape
means the *base class* is silently asserting an English grammar rule — regular word order, regular
`+s` pluralization — that doesn't hold universally even within English ("Matrix"/"Matrices"), let
alone across languages with different word order, case declension, or gendered articles. A shared
component has no business owning that assertion. This revision removes the composition step: each
subclass supplies the operation's complete, already-correct label text at the point it activates
that operation.

## Goals / Non-Goals

**Goals:**
- Let each list's Create/Delete/Edit/Filter labels read as an item-type-specific phrase, without
  requiring the shared base to know or assume anything about how to build that phrase from a word.
- Make every operation's label a required input at its activation site, so omitting one is a
  compile error, not a silent fallback to a generic default.
- Keep `DrillableListView<T>`'s genuinely-opt-in shapes (confirmed: `ObjectListView` doesn't call
  `EnableEdit`, `StreamListView`/`BucketListView`s don't call `EnableAscend`) expressed as method
  calls, since which shapes are active varies per subclass.

**Non-Goals:**
- Renaming Refresh, Search, or Back — not part of this change (see the companion investigation
  into Refresh/Clear/the live feed's missing Enter hint, tracked separately).
- Renaming the handful of tab-specific, non-shared hints (`KeyListView`'s "View",
  `ObjectListView`'s "Download", `PayloadDetailSection`'s "Presentation").
- Any actual localization of the app — this change only stops the *shared base classes* from
  assuming English grammar; it doesn't add a translation mechanism.

## Decisions

**Each `Enable*` method on `DrillableListView<T>` takes a required `string label`, not a
noun the base composes.** `EnableCreate(label)`/`EnableDelete(label)`/`EnableEdit(label)` each
store their label and use it verbatim in `TabOperations`. The subclass author writes the complete
phrase ("Add new Stream", "Delete Matrices", whatever is grammatically correct for that noun/
language), so the base class never needs to know about pluralization, word order, or
capitalization rules. No default is offered — every call site is already explicit today (every
concrete list calls these unconditionally with no arguments), so requiring an argument costs
nothing at existing call sites and makes forgetting one a compile error instead of a silent
fallback to a generic noun.

**`EnableFilter(label)`'s label is reused for both the advertised hint and the filter dialog's
title; the separate `FilterDialogTitle` property is removed.** The original design kept these
two deliberately separate (Filter never derived from `ItemNoun`, specifically so the hint and the
dialog could never disagree) — but that separation was solving a problem only `ItemNoun`
composition created. With one label authored once at the `EnableFilter` call site and used
directly in both places, there is exactly one source and the two literally cannot diverge; keeping
`FilterDialogTitle` as a second property alongside it would just be two ways to set the same
fact.

**`ListEditorView<T>`'s Create/Edit/Delete labels become required constructor parameters, not
`Enable*`-method parameters.** Unlike `DrillableListView<T>`, no current `ListEditorView<T>`
subclass selectively opts out of create/edit/delete — `SubscriptionsView` and `HeaderEditorView`
both always have all three, wired unconditionally in the base constructor. There is nothing to
gate a method call on, so the natural, equally-explicit place for a required label is the
constructor itself: `ListEditorView(items, presenter, createLabel, editLabel, deleteLabel,
bindSharedKeys, textColor)`. `EnableFilter(label)` stays a method (mirroring
`DrillableListView<T>`'s shape) since it genuinely is opt-in — no current subclass calls it, but
the mechanism (and `HeaderEditorView`'s existing comment explaining why it doesn't use it) stays
intact.

**`ItemNoun` is removed entirely, not deprecated or left as a fallback path.** Keeping it around
as an alternate way to supply a label would reintroduce the exact base-composes-grammar problem
this revision removes, just as an unused escape hatch nobody would remember not to reach for.

## Risks / Trade-offs

- [More boilerplate per subclass: four full label strings instead of one noun] → Accepted
  deliberately — the whole point is that composition was actively wrong for cases a single noun
  can't express (irregular plurals, non-English grammar), so the extra typing buys correctness a
  shared property can't. The strings live together in each subclass's own constructor, so a
  reviewer sees all of one list's labels at a glance and can catch an inconsistency (e.g. "Add new
  Stream" next to "Delete Streams") directly, rather than trusting a base-class formula.
- [`ListEditorView<T>`'s constructor gains three more required parameters] → All current call
  sites are already fully explicit (two subclasses, `SubscriptionsView` and `HeaderEditorView`),
  so this is a one-time, compiler-enforced update, not an ongoing burden; no subclass can compile
  without supplying its own labels.

## Migration Plan

No runtime data or persisted state involved — this only changes `ShortcutHint.Text` strings
computed at advertise-time. This revision replaces the already-implemented `ItemNoun`-composition
version described by the previous iteration of this change's tasks; rollout is a single code
change with no flag or staged rollout needed.
