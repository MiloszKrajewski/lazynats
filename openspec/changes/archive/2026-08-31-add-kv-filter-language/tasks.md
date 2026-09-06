## 1. Compiler

- [x] 1.1 Add `Core/KeyFilterExpression.cs` with `CompiledKeyFilter` (`string NativeFilter`,
      `Regex? PostFilter`) and `TryCompile(string expression, out CompiledKeyFilter? result, out
      string? error)`.
- [x] 1.2 Implement expression validation (reject a leading, trailing, or doubled `.`; accept
      everything else, including a non-terminal `>`).
- [x] 1.3 Implement the native fast path: detect an expression that is already valid native syntax
      (literal segments, a bare `*`, and/or a bare `>` only as the final segment) and short-circuit
      to `NativeFilter = expression`, `PostFilter = null`.
- [x] 1.4 Implement per-segment classification for the non-fast-path case (literal, bare `*`,
      terminal `>`, mixed single-token, non-bare-terminal `>`) and native filter construction,
      including the non-bare-terminal-`>`-collapses-the-remainder-to-`>` rule.
- [x] 1.5 Implement the parallel regex-atom translation (`.` → `\.`, `*` → `[^.]+`/`[^.]*`, `?` →
      `[^.]`, non-bare-terminal `>` → `.*`, bare terminal `>` → `[^.]+(?:\.[^.]+)*`, literal →
      `Regex.Escape`), anchored `^...$`, case-sensitive (no `RegexOptions.IgnoreCase`). Take care
      that a non-bare-terminal `>` translates at its own position (`>errors` and `errors>` must
      produce different regexes).
- [x] 1.6 Verify against every worked example in design.md's table (native filter and regex output
      match exactly), plus the validation and case-sensitivity scenarios from
      `specs/kv-filter-expression/spec.md`.

## 2. Dialog validation

- [x] 2.1 Add an optional validator delegate parameter to `PatternDialog` (default `null`,
      preserving today's non-empty-only behavior for `SubscriptionsView`'s existing calls).
- [x] 2.2 `UpdateValidity` calls the validator (when supplied) in addition to the existing
      empty/`allowEmpty` check.
- [x] 2.3 `ValuesTab.OpenKeyFilterDialog` passes a validator backed by
      `KeyFilterExpression.TryCompile`.

## 3. Rx bridge

- [x] 3.1 Add `ToObservable<T>(this IAsyncEnumerable<T> source)` to `Core/AsyncExtensions.cs`, via
      `Observable.Create<T>(async (observer, cancellationToken) => ...)` reading the source with
      `await foreach` and calling `observer.OnNext`/`OnCompleted`.
- [x] 3.2 Confirm (throwaway probe, not checked in) that an exception thrown mid-enumeration
      propagates through `OnError` and surfaces via `await` on the resulting observable chain.

## 4. Wiring into the KV fetch path

- [x] 4.1 `ValuesTab.RefreshKeyListAsync` compiles `_currentKeyFilter` once per call (when set) via
      `KeyFilterExpression.TryCompile`, and builds `store.GetKeysAsync([compiled.NativeFilter])
      .ToObservable()` instead of passing the raw pattern through.
- [x] 4.2 When `compiled.PostFilter` is non-null, apply `.Where(regex.IsMatch)` to the observable
      before it reaches the cap/collect step.
- [x] 4.3 Add a `KeyFilterCap` constant (`10_000`). When a filter is active, apply
      `.Take(KeyFilterCap + 1)` before `.ToList()`; if the resulting count exceeds `KeyFilterCap`,
      trim to `KeyFilterCap` and track that this fetch was truncated. When no filter is active,
      fetch exactly as today (no `Take`, no truncation tracking).
- [x] 4.4 Confirm `KeyListView` needs no code changes — it only ever consumes the already-filtered,
      already-capped key list.
- [x] 4.5 Update `UpdateKeyListTitle` to show a distinct indicator when the last fetch was
      truncated, alongside (not replacing) the existing filter-applied indicator.

## 5. Verification

- [x] 5.1 Manually verify via `tmux` (per CLAUDE.md's UI-testing guidance) against a real bucket:
      a plain native expression (e.g. `orders.*`), a `?`-bearing expression, a non-terminal-`>`
      (arbitrary-span) expression, and an invalid (empty-token) expression that the dialog refuses
      to confirm. **Caveat**: `tmux send-keys C-f` could not actually open the filter dialog in
      this environment - reproduced identically on unmodified HEAD with Ctrl+D/Ctrl+E at the key
      level (works at the bucket level, fails at the key level, both before and after this
      change), so it's a pre-existing, unrelated focus/key-routing issue, not something this
      change introduces or something in scope to fix here. Verified instead with a throwaway probe
      running the exact production pipeline (`KeyFilterExpression.TryCompile` ->
      `store.GetKeysAsync([...]).ToObservable()` -> `Where`/`Take`/`ToList`, copied verbatim from
      `RefreshKeyListAsync`) against a live NATS server and a purpose-built bucket
      (`kv-filter-test`, 19 keys covering every grammar case) - all 9 checks (native fast path,
      `?`, inline `*`, non-terminal `>`, `>`-position asymmetry, case-sensitivity, bare terminal
      `>`, unfiltered) passed against real fetched data.
- [x] 5.2 Manually verify the cap against a bucket (or filter) with more than 10,000 matching keys:
      the fetch stops at 10,000 and the title shows the truncation indicator; verify a filter
      matching fewer keys than the cap shows the plain filter indicator instead. Verified via the
      same live-server probe (see 5.1's caveat) against a dedicated `kv-filter-cap-test` bucket
      (10,050 single-token keys): filter `*` fetched exactly 10,000 with `truncated=true`; a
      narrower filter matching only 10 keys in the same bucket came back `truncated=false`. The
      title-indicator rendering itself (`UpdateKeyListTitle`) is a direct, small code path reviewed
      by hand rather than re-derived from screen output, for the same tmux-blocker reason as 5.1.
- [x] 5.3 Confirm the existing `add-kv-key-filter` scenarios not touched by this change (reset on
      ascend/descend, title suffix, persists across refresh, cancel leaves filter unchanged) still
      hold. Verified by code review (Descend/Ascend still unconditionally reset
      `_currentKeyFilter`/`_keyListTruncated`; `OpenKeyFilterDialog` still returns before mutating
      state when `dialog.Result` is null on cancel; `RefreshKeyListAsync` still reads
      `_currentKeyFilter` directly so Ctrl+R/create/edit refreshes stay scoped) - not re-driven
      interactively, same tmux blocker as 5.1.
