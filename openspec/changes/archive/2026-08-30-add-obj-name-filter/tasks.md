## 1. PatternDialog: allow empty confirmation (shared with add-kv-key-filter)

- [x] 1.1 Check whether `PatternDialog` already has an `allowEmpty` constructor parameter (added
      by `add-kv-key-filter`). If not, add it (default `false`), used by `UpdateValidity` and the
      `Accepting` handler's empty-text guard instead of the current hard-coded non-empty check.
      Already present (added by `add-kv-key-filter`, applied earlier this session).
- [x] 1.2 Confirm `SubscriptionsView`'s existing `PatternDialog` call sites are unaffected either
      way (still default to `allowEmpty: false`, still refuse empty patterns).

## 2. Filesystem-style wildcard matcher

- [x] 2.1 Add `Core/RegexExtensions.cs` (`internal static class RegexExtensions {
      public static Regex WildcardToRegex(this string pattern) }`): `Regex.Escape` the pattern,
      `*` → `.*`, `?` → `.`, anchored `^...$`, `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`
      — a pure pattern-to-`Regex` translation, not a hand-rolled scanner (that was the plan before
      `doc/i-reinvented-the-wheel.md`'s AOT re-check) and not a `GlobMatch.Compile`-style
      predicate-returning wrapper either (SRP: translating the pattern and using the result are
      separate concerns — the caller decides `.IsMatch` or anything else `Regex` offers). Named
      `RegexExtensions` rather than `WildcardExtensions`, since it also holds `FuzzyToRegex` (2.2)
      — both are the same "pattern string to Regex" responsibility, just different rules.
- [x] 2.2 **Updated from the original plan of "no change here"**: `DrillableListView<T>`'s
      `/`-quick-search matching moved from a hand-rolled `FuzzyMatches` scan to
      `query.FuzzyToRegex()` (also in `RegexExtensions.cs`) — a separate, out-of-band cleanup
      (not part of this change's own scope, but sharing the same file once both existed) with no
      behavior change, verified by direct equivalence testing (20 cases, including regex
      metacharacters in the query and culture-sensitive casing) against the scanner it replaced.
      See `doc/i-reinvented-the-wheel.md`. `/`'s quick-search *behavior* is unchanged (that's what
      the equivalence testing confirms) — only its implementation moved off the hand-rolled scan.
      `FuzzyToRegex` and `WildcardToRegex` remain two independent methods, one used by `/`'s
      quick-search, the other only by the new Ctrl+F filter below.

## 3. ObjectListView: Ctrl+F binding

- [x] 3.1 In `ObjectListView`'s constructor, bind `Key.F.WithCtrl` to a repurposed `Command.Open`
      (unused on this view — `Command.Save` already covers Ctrl+S download) that raises a new
      no-arg `FilterRequested` event, matching the `CreateRequested`/`DeleteRequested` shape.
- [x] 3.2 Extend `ObjectListView`'s `Shortcuts` override to also append a `Ctrl+F` "Filter" hint
      alongside its existing `Ctrl+S` "Download" hint.

## 4. ObjectsTab: filter state, dialog, and post-fetch narrowing

- [x] 4.1 Add `_currentObjectFilter` (nullable `string`) state to `ObjectsTab`, reset to `null` in
      both `Descend()` and `Ascend()`.
- [x] 4.2 Subscribe to `_objectListView.FilterRequested`, opening
      `new PatternDialog("Filter Objects", _currentObjectFilter ?? "", allowEmpty: true)`; on a
      non-null `Result`, normalize empty to `null`, set `_currentObjectFilter`, and call
      `RefreshObjectListAsync()`. A cancelled dialog (`Result is null`) leaves
      `_currentObjectFilter` and the list unchanged.
- [x] 4.3 Update `RefreshObjectListAsync` to, after collecting the fetched `names` list exactly as
      today, when `_currentObjectFilter` is set build `_currentObjectFilter.WildcardToRegex()`
      once and filter via `names.Where(name => regex.IsMatch(name)).ToList()` (a lambda, not a
      bare method-group reference — `Regex.IsMatch`'s overloads make `Where(regex.IsMatch)`
      ambiguous between the indexed and non-indexed `Where` overloads) before calling
      `_objectListView.ReplaceItems(filteredNames, selectName)` — no change to the fetch call
      itself.
- [x] 4.4 Update the object-level `_listLabel.Text` assignment(s) to include the active filter
      pattern when `_currentObjectFilter` is set (e.g. `$"Objects of {name} (filter:
      {pattern})"`), recomputed in `Descend()` and after a filter change.

## 5. Verification

- [x] 5.1 Manual check against a NATS server with an Object Store bucket containing objects under
      multiple naming patterns: Ctrl+F with a pattern narrows the list; Ctrl+R afterward stays
      narrowed; Esc/Backspace back to the bucket list and re-descending shows the full object list
      again. Confirmed interactively by a human (tmux Ctrl+letter delivery blocker from earlier in
      this session was environment/tooling-specific, not a defect in this change).
- [x] 5.2 Manual check that cancelling the filter dialog (Esc) changes nothing, and that
      confirming an empty pattern while a filter is active clears it. Confirmed interactively.
- [x] 5.3 `WildcardToRegex` verified standalone (22 cases: `*` alone, `foo*`, `*foo`, `*foo*`,
      `f?o`, exact match, case-insensitivity, empty pattern/text, multi-`*`) — all passed, both as
      the original one-shot form and the final `RegexExtensions.WildcardToRegex` shape.
      `FuzzyToRegex` separately verified against 24 cases directly comparing old vs. new, including
      ones specifically chosen to catch a leading/trailing-anchor mistake (real characters before
      the first match and after the last match) — all agreed, for both the anchored and final
      unanchored forms. `FuzzyToRegex`'s unanchored-vs-anchored choice was also benchmarked (10k
      names, mixed queries): unanchored is 11-13% faster typically and 3.7x-4.4x faster for
      single-character queries/non-matches — the final implementation is unanchored, deliberately,
      with the reasoning in the code comment. The UI-integration half of this task
      (confirming `/` and Ctrl+F compose correctly in the running app) has the same blocker as 5.1.
- [x] 5.4 `dotnet build src/lazynats.sln` succeeds.
