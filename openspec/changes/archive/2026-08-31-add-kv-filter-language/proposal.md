## Why

The KV key filter (Ctrl+F, `add-kv-key-filter`) currently passes the user's typed text straight
through as a raw NATS subject-wildcard filter, with no validation beyond non-empty — an invalid
pattern's failure mode is whatever the server or client library happens to surface. Native NATS
wildcards are also more restrictive than what a user reaching for "filter my keys" actually wants:
`*` only ever matches one whole dot-separated token, `>` only the remaining tokens, and neither can
express "one character" or "this literal fragment, then anything, possibly across dots." We want a
richer filter expression language — validated up front, and compiled into whatever combination of
a server-side native NATS filter and a client-side regex actually answers it exactly — without the
KV key list (`KeyListView`/`ValuesTab`) needing to know or care which phase did the work.

## What Changes

- Introduce a filter expression grammar, a superset of native NATS subject-wildcard syntax, built
  from three wildcards rather than four — `**` was considered and dropped as redundant (see
  Decision below):
  - `.` — token separator (same as native).
  - `*` — matches any run of characters *within one token* (never crosses a `.`); a bare `*`
    filling a whole segment keeps native's existing "match exactly one token" meaning as a special
    case of this.
  - `?` — matches exactly one character, excluding `.`.
  - `>` — native's own "one-or-more remaining whole tokens" meaning, but *only* when it is the
    entire final segment. Anywhere else — a non-terminal whole segment, or mixed inline with other
    characters, in any position — it instead matches any run of characters, *including* `.`,
    exactly at the position it appears. There is no ambiguity in that reading, so no reason to
    reject it as an error.
  - Literal characters — matched exactly, case-sensitively (matching NATS subject semantics).
- Compile an expression into two artifacts: a NATS-native filter subject (for scoping the
  server-side fetch) and, when the expression can't be represented exactly in native syntax alone,
  a client-side regex applied to whatever the native fetch returns. When the expression is already
  valid native syntax as-is (literals, whole-segment `*`, terminal `>`), the native filter alone is
  exact and the regex phase is skipped entirely — existing plain-native filters cost nothing extra.
- Replace `ValuesTab`'s current raw-passthrough key filter with this compiled two-phase filter:
  `GetKeysAsync` is scoped with the derived native filter, then results are further narrowed by the
  derived regex when one exists. `KeyListView`/`ValuesTab` interact with a single filtering
  operation and do not need to know which phase(s) actually ran.
- Bound every filtered key-list fetch at a fixed cap (10,000 matched keys) — an over-approximated
  native filter (e.g. one collapsing to `>`) could otherwise still stream an unbounded number of
  keys from a huge bucket before the client-side regex gets a chance to narrow it down; this is the
  actual reason the filter needs a fetch limit, not just narrower matching. A fetch that hits the
  cap is indicated in the key list's title alongside the existing filter indicator.
- Compose the fetch/filter/cap pipeline as an Rx (`IObservable<T>`) chain, matching the codebase's
  existing Rx usage (`Core/AsyncExtensions.cs`, `PollingDetailsView`) rather than adding a new
  dependency for `IAsyncEnumerable<T>` LINQ support.
- `PatternDialog`'s validity check (used for the key filter) now parses the typed text against this
  grammar and reflects a genuinely invalid expression (e.g. an empty segment from a leading,
  trailing, or doubled `.`) rather than only checking for non-empty text.

**Non-goals for this change:**
- Object Store's Ctrl+F filter (`ObjectsTab`/`ObjectListView`, filesystem-style `*`/`?` via
  `WildcardToRegex`) is explicitly untouched. Unifying it onto this grammar — including the
  semantic shift that would cause for object names containing literal dots — is deferred.
- The in-memory quick-search (`/`, `FuzzyToRegex`) on any list is untouched.

## Capabilities

### New Capabilities
- `kv-filter-expression`: the filter expression grammar and its compiler — parsing an expression
  string into a native NATS subject filter plus an optional client-side regex, including the
  native-fast-path and `>`'s dual meaning (native-terminal vs. arbitrary-span everywhere else).

### Modified Capabilities
- `nats-kv`: the "Server-Side Key Filter" requirement's pattern syntax changes from raw
  NATS-subject-wildcard passthrough to the new filter expression language (via `kv-filter-expression`),
  including real validation feedback in the filter dialog instead of a non-empty-only check.

## Impact

- `src/lazynats/Subscriptions/PatternDialog.cs`: validity check gains real syntax validation for
  the KV filter use (still just non-empty for `SubscriptionsView`'s own use, which stays plain NATS
  subject syntax, not this grammar).
- `src/lazynats/Values/ValuesTab.cs` (`RefreshKeyListAsync`, `OpenKeyFilterDialog`): fetch scoping
  changes from raw pattern passthrough to the compiled two-phase filter.
- `src/lazynats/Core/`: new compiler (grammar parser + native/regex emitter), alongside the existing
  `RegexExtensions.cs` (`WildcardToRegex`/`FuzzyToRegex`), which this deliberately does not reuse or
  merge into — different grammar, different guarantees (exact vs. best-effort).
- `src/lazynats/Core/AsyncExtensions.cs`: new `IAsyncEnumerable<T>.ToObservable()` bridge, alongside
  the existing `SelectAsync`/`ObserveOnApp` Rx helpers.
- `openspec/specs/nats-kv/spec.md`: "Server-Side Key Filter" requirement delta, including the new
  fetch cap and its title indicator.
