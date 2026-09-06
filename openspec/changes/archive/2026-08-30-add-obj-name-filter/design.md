## Context

`ObjectsTab.RefreshObjectListAsync` (`src/lazynats/Objects/ObjectsTab.cs:298`) is the single fetch
path for the object-level list — called on descend, Ctrl+R, and after upload/delete — and always
does `await foreach (var metadata in store.ListAsync(new NatsObjListOpts())) names.Add(...)`,
listing every object in the bucket. `ObjectListView` already has the in-memory fuzzy quick-search
(`/`, via `DrillableListView<T>.AttachFilterBox`, same as `KeyListView`) from
`add-drillable-list-search`. Unlike `INatsKVStore.GetKeysAsync`, `INatsObjStore.ListAsync` takes no
filter — confirmed via `NATS.Client.ObjectStore` 2.8.2's shipped XML docs, `NatsObjListOpts` has
only an `OnNoData` callback — and object metadata subjects
(`$O.<bucket>.M.<base64url-encoded-name>`) encode the whole name into one opaque token, so even a
raw subject-wildcard subscription against the underlying stream can't filter by human-readable name
prefix. There is no way to shrink the wire fetch itself for Object Store the way
`add-kv-key-filter` does for KV.

The user-confirmed goal for this change is narrower than "reduce fetch cost": with a bucket holding
many thousands of objects, the concern is the number of rows the list ends up holding and
rendering, not the network round-trip (already accepted as unavoidable here). A persisted,
pre-population filter — applied to the fetched name list before it becomes `ObjectListView`'s
backing collection — addresses that even though the fetch itself stays full-size, in contrast to
the existing `/` quick-search, which narrows only what's *displayed* from an already-fully-loaded
master set (see `add-drillable-list-search`'s design.md Decision 2 — `_items` stays the full set
regardless of `/`'s filter text).

## Goals / Non-Goals

**Goals:**
- Let the user apply a persisted filter (Ctrl+F) to the object-level list, keeping the resulting
  in-app list (backing collection and rendered rows) small even for a bucket with a very large
  object count, by filtering the fetched result before `ReplaceItems` rather than after.
- Keep the active filter applied across whatever already triggers a re-fetch today (Ctrl+R,
  post-upload, post-delete), matching `add-kv-key-filter`'s KV behavior, so the narrowing isn't
  undone by the next refresh.
- Give the user precise, predictable narrowing — filesystem-style wildcards (`*`/`?`), not a loose
  fuzzy match — since the point of this filter is to reliably shrink a large list down to a small
  window, not to aid exploratory search (that's what `/` is already for).

**Non-Goals:**
- Not a fetch-size or network-cost reduction — `store.ListAsync(...)` still lists every object in
  the bucket on every refresh; this change only shrinks what's kept afterward. Explicitly called
  out (per the user's own framing) so this isn't mistaken for parity with KV's server-side
  reduction.
- Not NATS subject-wildcard syntax (`*`/`>`, per-token/dot-hierarchy matching) — object names carry
  no such structure, so `add-kv-key-filter`'s KV pattern syntax doesn't transfer meaningfully here
  (see Decision 3).
- Not the existing `/` quick-search's fuzzy-subsequence matching either, despite already being
  available and tested — reused code isn't the goal, fit-for-purpose matching is; fuzzy-subsequence
  is too permissive to reliably narrow a huge list down to a small window (a short query can still
  match a large fraction of names). The result is three distinct matching syntaxes across the app
  (KV's Ctrl+F: NATS subject wildcard; Objects' Ctrl+F: filesystem wildcard; `/` on both: fuzzy
  subsequence) — an accepted, intentional asymmetry, each chosen for what its mechanism/goal
  actually needs (see Decisions, Risks).
- No change to `DrillableListView<T>`'s public shape beyond extracting the matching helper — this
  stays `ObjectListView`-specific, same reasoning `add-kv-key-filter`'s design.md gives for why its
  filter isn't a new shared `Enable*` opt-in.
- No change to the existing `/` quick-search's own behavior (still live, in-memory, resets on
  refresh) — the two filters remain independent and composable, same as KV.

## Decisions

**1. `ObjectListView` binds Ctrl+F directly, mirroring `add-kv-key-filter`'s `KeyListView`
approach.** Binds `Key.F.WithCtrl` to a repurposed `Command.Open` (unused on this view —
`Command.Save` already covers Ctrl+S download), raises a no-arg `FilterRequested` event, and
appends a `Ctrl+F` "Filter" hint via `Shortcuts`. Same shape, same rationale as
`add-kv-key-filter`'s Decision 1 — not a new base-class opt-in, since no other current subclass
needs a persisted pre-population filter either.

**2. The filter is applied to the fetched result, before `ReplaceItems`, not as a second live
projection alongside `/`.** `RefreshObjectListAsync` fetches every name via `store.ListAsync(...)`
exactly as today, then — if `_currentObjectFilter` is set — filters the resulting `names` list
down to matches before calling `_objectListView.ReplaceItems(...)`. This is the entire mechanism
that keeps the backing collection small: `DrillableListView<T>._items` (the master set `/`
projects from) only ever contains what `ReplaceItems` was handed, so pre-filtering the input
achieves the goal without touching `DrillableListView<T>` itself. Considered adding this as a
second `IFilterable`-style live projection inside the base class (mirroring `/`); rejected —
that would keep the full name list in memory regardless (defeating the "limit what's held" goal)
and would require reconciling two independently-driven filtered projections over one list.

**3. Matching is filesystem-style wildcard (`*`/`?`), via a `string.WildcardToRegex()` extension
method, independent of `/`'s fuzzy matcher.** `Core/RegexExtensions.cs`
(`internal static class RegexExtensions { public static Regex WildcardToRegex(this string
pattern) }`) escapes the pattern (`Regex.Escape`), reintroduces `*` → `.*` and `?` → `.`, anchors
it (`^...$`), and returns a case-insensitive `Regex` — a pure pattern-to-`Regex` translation, with
no opinion on how the caller uses the result. `ObjectsTab` calls it once per refresh
(`pattern.WildcardToRegex()`) and reuses the resulting `Regex` across every name in that refresh's
`Where`, rather than re-parsing the pattern per name. `*` matches any run of characters (including
none), `?` matches exactly one character, matching is case-insensitive and anchored to the full
name (so `foo*` means "starts with foo", `*foo*` means "contains foo", `*.json` means "ends with
.json" — the same convention as shell/DOS wildcards).

Earlier in this change's implementation this lived as `GlobMatch.Compile(pattern):
Func<string, bool>`, a static class wrapping the same `Regex` construction behind a
"compile-a-predicate" verb. Replaced with the extension method on reflection that translating a
wildcard pattern into a `Regex` is the actual, reusable single responsibility here — what a caller
does with that `Regex` (`.IsMatch` for a predicate, as `ObjectsTab` does, or anything else `Regex`
offers) isn't this method's concern, and a bare `string -> Regex` extension needs no wrapper class
or "Compile" verb to say that.

`WildcardToRegex` now lives in `Core/RegexExtensions.cs` alongside `FuzzyToRegex` (the
regex-based replacement for `DrillableListView<T>`'s pre-existing hand-rolled `/` quick-search
matcher, from the already-shipped `add-drillable-list-search` change) rather than in its own
single-method file — both are the identical "translate a UI search pattern into a `Regex`"
responsibility, just different translation rules, so splitting them into two near-empty classes
was worse cohesion, not better SRP. `FuzzyToRegex` itself went through two more rounds after that.
`oce`'s fuzzy-subsequence semantics are conceptually `*o*c*e*` (a wildcard around and between each
query character); the first cut built that as an unanchored `o.*c.*e`, relying on
`Regex.IsMatch`'s own default anywhere-in-string search to implicitly supply the missing
leading/trailing wildcard — correct, but required knowing that implicit default to trust by
inspection. Changed to the literal, anchored translation instead (`^.*o.*c.*e.*$`, matching
`WildcardToRegex`'s style), removing the implicit reliance — then reverted back to the unanchored
form once benchmarked: against 10k realistic key/object names, the anchored form ran 11-13% slower
for typical multi-character queries and 3.7x-4.4x slower for single-character queries and
non-matches, since an explicit leading `.*` forces real backtracking from position 0 instead of
letting the regex engine use its own optimized "find this literal anywhere" scan. This runs once
per keystroke in the quick-search field against every loaded item, so the difference isn't
negligible. Final form is unanchored, with the trade-off (and the numbers behind it) spelled out
in the code comment rather than left implicit — see `doc/i-reinvented-the-wheel.md`.

First cut of this decision hand-rolled a two-pointer/backtrack wildcard scanner instead, on the
stated (but unverified) grounds that it needed to "stay unambiguously PublishAot-friendly without
relying on Regex's AOT/trimming behavior" — copying `DrillableListView<T>.FuzzyMatches`'s own
hand-rolled precedent without checking whether the same constraint actually applied here. It
didn't: `RegexOptions.Compiled` (or `Regex.CompileToAssembly`) needs reflection-emit and is
AOT-unsafe; plain, non-`Compiled` `Regex` (what's used here) doesn't and is fine. Verified directly
— a throwaway console project, `PublishAot=true`, `dotnet publish -r win-x64 -c Release`, ran this
exact escape-and-replace pattern: zero AOT/trim warnings, correct runtime output. See
`doc/i-reinvented-the-wheel.md` for the full account. ~30 lines of hand-rolled backtracking logic
replaced by one `Regex` construction, verified against the same 22 test cases with identical
results either way.

Considered reusing `/`'s fuzzy-subsequence matcher (as originally proposed, before this
reconsideration); rejected — fuzzy-subsequence is deliberately loose (good for live exploratory
search over a small-ish set) and works against this feature's actual goal of reliably shrinking a
huge list to a small window, where a user wants "exactly the names matching this pattern," not
"anything whose characters appear in this order." Considered NATS-style subject-wildcard syntax for
surface consistency with KV's Ctrl+F; rejected since object names have no token/dot-hierarchy
structure for `*` (single-token) / `>` (remaining-tokens) to mean anything meaningful against — see
Non-Goals.

**4. Filter state and lifetime mirror `add-kv-key-filter`'s KV design exactly.**
`ObjectsTab` gets `_currentObjectFilter` (nullable `string`), reset to `null` in `Descend()` and
`Ascend()`, read directly by `RefreshObjectListAsync` (no new parameter), set from
`PatternDialog("Filter Objects", _currentObjectFilter ?? "", allowEmpty: true)`'s result (empty
normalized to `null`). The object list's title gains the same `(filter: {pattern})` suffix
`add-kv-key-filter` adds to the key list's title.

**5. `PatternDialog.allowEmpty` is a shared, additive dependency on `add-kv-key-filter`, not
re-added here.** Both changes need the exact same one-line capability (allow confirming with an
empty pattern to mean "clear the filter"). If `add-kv-key-filter` is applied first, this change's
task list simply confirms the flag exists; if this change is applied first instead, its tasks add
it (identical change either way — see tasks.md). No duplicated or divergent implementation either
way, since the flag's behavior is independent of which tab drives it.

## Risks / Trade-offs

- [The Ctrl+F dialog looks identical between the Values (KV) and Objects tabs but interprets its
  text differently — NATS subject wildcard there, filesystem wildcard here, and neither matches
  `/`'s fuzzy-subsequence semantics on either tab] → Accepted per Decision 3; the dialog's field is
  unlabeled beyond "Pattern" in both cases (matching `PatternDialog`'s existing subscribe-pattern
  use), and each syntax is chosen for what its own mechanism/goal actually needs — forcing one
  syntax across all three would be surface consistency without matching substance. Filesystem
  wildcards are common enough (shell globbing, Windows `dir`/`Get-ChildItem`) that this shouldn't
  need much explanation in practice.
- [A newly uploaded object that doesn't match the active filter won't appear in the post-upload
  refresh, reading as if the upload silently failed] → Same accepted trade-off
  `add-kv-key-filter`'s design.md calls out for KV creates/edits; the list title makes the active
  filter visible, and clearing it (Ctrl+F, confirm empty) immediately shows it.
- [This doesn't solve the actual "10,000+ objects" fetch-time/network problem — a bucket that's
  slow or expensive to list in full still is, filter or not] → Explicitly accepted; the user
  confirmed the primary concern is what's held/rendered afterward, not the fetch itself. A genuine
  fetch-size reduction for Object Store isn't achievable without a different mechanism (e.g.
  reading the underlying stream directly and reconstructing names by decoding subjects, bypassing
  `INatsObjStore` entirely) — out of scope here, and not attempted.

## Open Questions

- If a future NATS.Client.ObjectStore release adds server-side list filtering, should this be
  revisited to match KV's shape (scoped fetch) instead of the post-fetch approach here? Deferred —
  nothing in the current SDK (2.8.2) supports it, and speculative future-proofing against an SDK
  capability that doesn't exist yet isn't worth designing around now.
