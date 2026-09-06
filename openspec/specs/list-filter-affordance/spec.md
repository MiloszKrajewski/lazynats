# list-filter-affordance Specification

## Purpose
TBD - created by syncing change unify-list-filtering. Update Purpose after archive.

## Requirements

### Requirement: One Filter-Expression Grammar Governs Every Pattern Filter
Every Ctrl+F pattern filter in the application, regardless of which list it's attached to, SHALL
compile the user's pattern using the same `* ? >` filter-expression grammar (see the
`kv-filter-expression` capability), matched case-sensitively. No list SHALL apply a different
wildcard dialect (e.g. a case-insensitive filesystem glob) for its own pattern filter.

#### Scenario: The same pattern behaves identically across two different lists
- **WHEN** the same filter expression (e.g. `foo.*.bar`) is confirmed as the active Ctrl+F filter
  on two different lists (e.g. the Streams list and the Objects list)
- **THEN** both lists resolve which of their currently-held items match using the same compiled
  grammar, differing only in what item text is being matched against

#### Scenario: An invalid expression is rejected the same way everywhere
- **WHEN** a filter expression containing an empty token (a leading, trailing, or doubled `.`) is
  entered into any list's Ctrl+F dialog
- **THEN** that list's dialog does not confirm on Enter, the same as every other list's dialog
  would refuse it

### Requirement: A Pattern Filter Never Requires Server-Side Scoping To Function
A Ctrl+F pattern filter SHALL be fully resolvable as an exact, in-memory match against whatever
items a list already holds, independent of whether that list's backing store offers any form of
server-side scoped or wildcarded fetch. A list MAY activate pattern filtering with no server-side
involvement at all.

#### Scenario: A list with no scoped-fetch capability still filters correctly
- **WHEN** a list whose backing store has no server-side name/subject-wildcard fetch API (e.g.
  Streams, Consumers, Buckets, Objects, or the Publish header list) has an active Ctrl+F filter
- **THEN** the list shows exactly the currently-loaded items matching the compiled expression, with
  no fetch narrower or wider than an unfiltered fetch ever attempted

### Requirement: Server-Side Fetch Scoping Is an Additive Optimization, Not a Prerequisite
Where a list's backing store does support a server-side subject-wildcard-scoped fetch (currently:
KV keys), the owning component MAY additionally use the compiled expression's native filter to
narrow what is requested from the server. This SHALL never change the resulting set of items shown
relative to an equivalent in-memory-only application of the same expression — it only changes how
much data crosses the network to arrive at that same result.

#### Scenario: Native scoping and in-memory matching agree
- **WHEN** the same filter expression is applied once via a server-side-scoped fetch plus in-memory
  matching (KV keys) and once via in-memory matching alone (any other list, hypothetically fetching
  the same item names)
- **THEN** both approaches select the identical set of matching item names

### Requirement: Quick-Search and Pattern Filter Are Offered as a Pair
Any list-bearing view this capability applies to SHALL offer both the fuzzy quick-search (`/`) and
the `* ? >` pattern filter (Ctrl+F) together, not one without the other, so narrowing a list works
the same two-tool way regardless of which tab or list the user is in.

#### Scenario: A list activating one activates the other
- **WHEN** a list gains quick-search or pattern-filter support as part of this change
- **THEN** it gains both, not only one of the two
