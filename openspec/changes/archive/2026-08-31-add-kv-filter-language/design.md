## Context

`ValuesTab`'s KV key filter (Ctrl+F, from `add-kv-key-filter`) passes the user's typed text
straight through to `store.GetKeysAsync([pattern])` as a raw NATS subject-wildcard filter, with
`PatternDialog` only checking for non-empty text — an invalid pattern's failure mode is whatever
the server/client library surfaces. Object Store's own Ctrl+F (`add-obj-name-filter`) instead uses
a filesystem-style `*`/`?` grammar via `WildcardToRegex`, applied client-side post-fetch, because
`INatsObjStore.ListAsync` has no server-side filter to scope. Both changes explicitly accepted this
as intentional asymmetry at the time — this change doesn't reopen that; it only replaces KV's raw
passthrough with a richer, validated grammar. Object Store's filter is untouched.

The desired grammar is a strict superset of native NATS subject-wildcard syntax (`.` token
separator, `*` = one whole token, `>` = one-or-more remaining whole tokens, terminal-only), adding
`?` (exactly one non-`.` character) and giving `>` a second meaning everywhere it *isn't* a bare
final segment: an arbitrary run of characters, including `.`, at the position it appears. An
earlier draft of this design used a fourth symbol, `**`, for that arbitrary-span behavior; it was
dropped once we confirmed `>` already produces the identical compiled result in every position
`**` could occupy (see Decision 2) — a stray `>` outside its native terminal spot is never
ambiguous, so reusing the symbol rather than adding a new one keeps the grammar to three wildcards
(`*`, `?`, `>`) instead of four. Compiling an expression produces a native NATS filter (to scope
the server-side fetch) and, only when the expression can't be represented exactly in native syntax,
a client-side regex applied to the native fetch's results. `KeyListView` and `ValuesTab`'s
consumers of "the filtered key set" have no need to know which phase(s) ran.

## Goals / Non-Goals

**Goals:**
- Define the filter expression grammar and its compile algorithm precisely enough to implement
  and test directly from this document: segment classification, the native-syntax fast path (skip
  the regex phase entirely when the expression is already valid native syntax), and the rule that
  `>` keeps its native meaning only as the entire final segment and otherwise matches an arbitrary
  run of characters, including `.`, at the position it appears.
- Give the KV filter dialog real validation (reject genuinely malformed expressions before they're
  ever sent anywhere), replacing the current non-empty-only check for this use.
- Keep `KeyListView`/`ValuesTab`'s consumption of "the current filtered key set" a single, phase-
  agnostic operation — same shape the user asked to verify: "the consumer... should not know where
  it happens."
- Bound every filtered key-list fetch at a fixed cap (10,000 matched keys) — this is the actual
  point of having a filter at all, not just a UX narrowing convenience: an expression whose native
  filter degrades to something broad (any segment needing the arbitrary-span `>` reading collapses
  everything after it into a bare `>`) would otherwise still let a huge bucket stream every
  server-side match before the client regex ever gets to narrow it down. The user's own framing:
  "otherwise we would read more keys than needed... this limit is the reason we have a filter."

**Non-Goals:**
- Object Store's Ctrl+F filter is untouched — no shared grammar, no shared compiler call site.
  Unifying it later is possible but would change what `*` means for object names containing
  literal dots (filesystem-style `*` today spans dots; this grammar's `*` never does) — a real
  behavior change, deliberately deferred rather than folded in here.
- No multi-expression (OR'd) filter UI — still a single expression via the existing single-field
  `PatternDialog`, same as today.
- No attempt to derive the *tightest possible* native filter beyond the segment-classification
  algorithm below (e.g. no cleverness to recover a literal suffix after a `**` collapse into the
  native filter) — "correct via the regex phase, reasonably narrowed via the native phase" is the
  bar, not "optimal."
- No change to `/`'s in-memory quick-search (`FuzzyToRegex`) or to `SubscriptionsView`'s own use of
  `PatternDialog` (still plain NATS subject syntax, no `?`/arbitrary-span `>`).

## Decisions

**1. A new `Core/` compiler, not an extension to `RegexExtensions`.** `RegexExtensions` is a
`string -> Regex` translation with no other output; this grammar's compile result is a pair (a
native filter subject, and an optional `Regex`), and the two phases have different jobs (scope a
fetch vs. exact-match a result) rather than being alternate ways to produce the same kind of
answer. New file, `Core/KeyFilterExpression.cs`, with a `TryCompile(string expression) ->
CompiledKeyFilter?` shape — `null` on invalid input (an empty token). This departs from the
codebase's usual `Try*`-with-`out error` convention (`ValueText.TryDecode`,
`INatsKVStore.TryGetEntryAsync`): neither call site needs a reason string — `PatternDialog`'s
validator delegate only needs pass/fail to color the field, and `RefreshKeyListAsync` only ever
calls `TryCompile` on text a validator has already accepted — so a bare nullable return is
sufficient and there's no error message to plumb through. `CompiledKeyFilter` carries
`NativeFilter` (`string`, always present) and `PostFilter` (`Regex?`, null when the native filter
alone is exact).

**2. Compile algorithm.** Split the expression on `.` into segments (safe to do textually — every
wildcard is expressed with literal characters in the *pattern string*; a wildcard's ability to
*match* a `.` at matching time is separate from how the pattern string itself is split). `>` has
two distinct meanings depending on where it sits: as the entire final segment, it keeps native's
"one-or-more remaining whole tokens" meaning; everywhere else — a non-terminal whole segment, or
mixed inline with other characters, in either position within a segment — it instead matches an
arbitrary run of characters, including `.`, right there. (An earlier draft gave that second meaning
its own symbol, `**`; dropped once we confirmed it produced byte-for-byte identical compile output
to a same-position `>` in every case, including the one edge case worth checking — `>` as a whole
*terminal* segment written with two stars instead, e.g. `orders.**` vs. `orders.>` — which turned
out to differ only in whether the (harmless, always-satisfied) regex phase runs at all, never in
which keys match. See the worked examples below.)

Per-segment classification (left to right), building the native filter's tokens:
- **Native fast path, checked first, over the whole expression**: if every segment is *exactly*
  one of a plain literal run, a bare `*`, or (only as the final segment) a bare `>`, the expression
  is already valid native syntax. `NativeFilter` = the expression verbatim, `PostFilter` = `null` —
  no regex phase at all. This keeps every filter written in plain NATS syntax today (the common
  case) exactly as cheap and exact as it is now.
- Otherwise, walk segments and classify each:
  - Literal-only → native emits that literal token; regex emits the escaped literal.
  - Bare `*` → native emits `*`; regex emits `[^.]+`.
  - Bare `>` as the final segment → native emits `>`; regex emits `.*`, same as any other `>` (see
    the regex-atoms bullet below) rather than a token-structured `[^.]+(?:\.[^.]+)*`. This is still
    exact: this branch is only reached when *some other* segment already forced the regex phase —
    a bare terminal `>` alone is the native fast path above — so whenever this branch's `.*` is
    evaluated, the native filter (built alongside it) already ends in a native `>` at this same
    position, which the server has already used to admit only candidates with ≥1 remaining token
    there. The looser client-side `.*` can't admit anything the native fetch didn't already
    constrain to be non-empty.
  - Mixed literal/`?`/inline-`*` (no arbitrary-span `>` present) → guaranteed to still occupy
    exactly one token, but its exact shape can't be expressed natively → native emits `*` (over-
    approximation); regex emits the literal/`[^.]`/`[^.]*` translation of the segment's contents.
  - Any segment containing a `>` that isn't a bare final segment (a non-terminal whole segment, or
    `>` mixed inline with other characters anywhere, including as part of the final segment) →
    native emits `>` and **stops** — every subsequent segment is absorbed into that same `>`, since
    native syntax has no way to promise a fixed token count once an unbounded span is possible;
    regex continues translating every remaining segment/atom exactly, with this `>` itself
    translating to `.*` at its exact position (a literal suffix after it is still enforced by the
    regex even though the native filter can no longer scope for it).
- Regex atoms, applied uniformly regardless of position or bare/terminal status: `.` → `\.`; `*` →
  `[^.]+` (whole segment) or `[^.]*` (inline); `?` → `[^.]`; `>` → `.*` (see the bare-terminal-`>`
  bullet above for why this is exact even in that position); literal → `Regex.Escape`. Full pattern
  anchored `^...$`, case-sensitive (matches NATS subject semantics — deliberately *not* the
  case-insensitivity `WildcardToRegex`/`FuzzyToRegex` use, since those serve UX search, not
  protocol-shaped matching).
- Validation: the only rejected input is an empty token — a leading, trailing, or doubled `.`
  (`.foo`, `foo.`, `foo..bar`). Every other combination of literals and `*`/`?`/`>` compiles to
  *something*, per the user's own framing: "if it's not valid native but technically we could
  handle it — we should," rather than rejecting anything the grammar has a coherent reading for.

Worked examples (verified by hand against this algorithm, kept here for the implementer to check
a first draft against before writing tests). Wildcard *position* matters — an arbitrary-span `>`
substitutes for a would-be `**` only at the exact spot it occupied, not anywhere in the expression;
`>errors` (arbitrary-then-literal, "ends with errors") and `errors>` (literal-then-arbitrary,
"starts with errors") are different patterns, not interchangeable:

| Expression | Native filter | Regex (or "skipped") |
|---|---|---|
| `orders.eu.completed` | `orders.eu.completed` | skipped (native fast path) |
| `orders.*.completed` | `orders.*.completed` | skipped (native fast path) |
| `x.*.z.>` | `x.*.z.>` | skipped (native fast path) |
| `ord?rs.completed` | `*.completed` | `^ord[^.]rs\.completed$` |
| `ord*.completed` | `*.completed` | `^ord[^.]*\.completed$` |
| `x.y>` | `x.>` | `^x\.y.*$` |
| `x.>.z` | `x.>` | `^x\..*\.z$` |
| `x.>.y` | `x.>` | `^x\..*\.y$` |
| `>errors` | `>` | `^.*errors$` |
| `errors>` | `>` | `^errors.*$` |
| `a.b?.>` | `a.*.>` | `^a\.b[^.]\..*$` |
| `orders.>` (bare, terminal) | `orders.>` | skipped (native fast path — requires ≥1 trailing token) |

**3. A hand-rolled `IAsyncEnumerable<T>` → `IObservable<T>` bridge lives in `Core/AsyncExtensions.cs`,
not a new dependency.** `INatsKVStore.GetKeysAsync` returns `IAsyncEnumerable<string>`; expressing
the fetch-then-filter-then-cap pipeline as `source.Where(...).Take(...).ToList()` reads best as an
`IObservable<T>` chain, matching this codebase's existing Rx idiom (`Core/AsyncExtensions.cs`
already hosts `SelectAsync`/`ObserveOnApp`, and `PollingDetailsView` is built on Rx throughout).
`System.Reactive` (6.1.0, already referenced by `lazynats.csproj`) has no built-in bridge for this
— verified directly: `someIAsyncEnumerable.ToObservable()` fails to compile against 6.1.0, since
the package's only `ToObservable` overload targets `IEnumerable<T>`. `System.Linq.Async` (Ix.NET)
would add LINQ operators directly on `IAsyncEnumerable<T>` without needing this bridge at all, but
was rejected per explicit preference: no new dependency when Rx is already in use here and
elsewhere in the app. So `AsyncExtensions` gains:
```csharp
public static IObservable<T> ToObservable<T>(this IAsyncEnumerable<T> source) =>
    Observable.Create<T>(async (observer, cancellationToken) => {
        await foreach (var item in source.WithCancellation(cancellationToken))
            observer.OnNext(item);
        observer.OnCompleted();
    });
```
Verified (throwaway probe, not checked in) that this correctly propagates an exception thrown
mid-enumeration through to `OnError` and back out through `await`, so `RefreshKeyListAsync`'s
existing `catch` block needs no changes to keep reporting fetch failures via `StatusChanged`.

**4. `ValuesTab.RefreshKeyListAsync` applies both phases plus the fetch cap; `KeyListView` is
untouched.** `RefreshKeyListAsync` compiles `_currentKeyFilter` (unchanged as tab-level state —
same lifetime, reset-on-ascend/descend rules as today) once per refresh, then builds:
```csharp
var observable = store.GetKeysAsync([compiled.NativeFilter]).ToObservable();
if (compiled.PostFilter is { } regex) observable = observable.Where(regex.IsMatch);
var fetched = await observable.Take(KeyFilterCap + 1).ToList();
var truncated = fetched.Count > KeyFilterCap;
var keys = truncated ? fetched.Take(KeyFilterCap).ToList() : fetched;
```
`KeyFilterCap` is a fixed constant, `10_000`. The `Take(cap + 1)` "peek one extra" is what makes
`truncated` derivable without a second round-trip: fetching one more than the cap and checking
whether that many actually came back is the only way to distinguish "exactly `cap` matches exist"
from "there are more we didn't fetch." The cap applies only while a filter is active — an
unfiltered fetch (`_currentKeyFilter is null`) is untouched, still `store.GetKeysAsync()` with no
`Take` at all, per this change's existing non-goal of not touching the general "very large bucket,
no filter" problem. When no filter is active, behavior is identical to today. `KeyListView` needs
no changes at all — it already only ever sees "the current key list," never how it was derived or
whether it was truncated (that surfaces through the list title, same mechanism as the existing
filter indicator — see the `nats-kv` spec delta).

**5. `PatternDialog` gains an injectable validator instead of becoming grammar-aware itself.**
`PatternDialog`'s `UpdateValidity` currently hardcodes "non-empty or `allowEmpty`." This becomes
"non-empty (or `allowEmpty`) **and**, if a validator delegate was supplied, the validator accepts
the trimmed text" — default `null` validator preserves today's behavior for every other
`PatternDialog` call site (`SubscriptionsView`'s pattern create/edit, which must stay plain NATS
subject syntax — `?` and arbitrary-span `>` are not valid subscribe patterns and must not be
silently accepted there). `ValuesTab.OpenKeyFilterDialog` passes a validator that calls
`KeyFilterExpression.TryCompile` and discards the compiled result, using only success/failure.
Considered making `PatternDialog` itself understand this grammar; rejected — it would need a way
to *not* apply it for `SubscriptionsView`'s use anyway, which is exactly what an injectable
delegate already gives for free, without the dialog needing to know grammars exist at all.

## Risks / Trade-offs

- [Over-approximated native filters (`*` for a `?`-shaped or inline-`*`-shaped segment, `>` for any
  arbitrary-span-containing tail) can still fetch far more than the expression ultimately matches —
  e.g. `x.>.z` fetches everything under `x.>` before the regex narrows it] → Mitigated, not fully
  solved, by Decision 4's fetch cap: the fetch is now bounded at `KeyFilterCap` (10,000) matched
  keys regardless of how broad the native filter degrades to, with truncation surfaced via the list
  title rather than silently loading an unbounded result set. It doesn't make the native filter any
  tighter — NATS syntax still has no way to encode "ends with this token" without fixing the token
  count in between — it just stops "broad native filter + huge bucket" from being unbounded.
- [Case-sensitive matching diverges from `/`'s and Object Store's case-insensitive filters on the
  same screen] → Accepted, deliberately: this grammar's literals are NATS subject tokens, which are
  case-sensitive on the server; matching case-insensitively here would make the native and regex
  phases *disagree* about matches in a case-varying bucket, which is worse than the inconsistency
  with unrelated features elsewhere in the app.
- [The grammar accepts inputs a user likely mistyped — e.g. a stray `>` they meant as a literal —
  and silently interprets it as an arbitrary-span wildcard rather than flagging it] → Accepted per
  the user's explicit direction; the only rejected shape is a structurally empty token, everything
  else gets a coherent (if occasionally surprising) reading rather than an error.

## Open Questions

None blocking. Worth revisiting only if real usage shows `>`'s dual meaning (native-terminal vs.
arbitrary-span everywhere else) produces confusing results in practice — no evidence of that yet,
since this hasn't shipped.
