## 1. PollingDetailsView pipeline

- [x] 1.1 Replace `_target`/`_hasTarget` fields with a
      `BehaviorSubject<(bool HasTarget, TTarget Target)>` seeded `(false, default!)`; update
      `SetPollTarget`/`ClearPollTarget` to `OnNext` into it instead of setting fields.
- [x] 1.2 Add the `private static readonly TimeSpan SwitchDebounce = TimeSpan.FromMilliseconds(100);`
      constant.
- [x] 1.3 Rewrite `StartPolling` (the pipeline built inside it) per design.md Decision 4: separate
      `toTarget` (throttled), `toClear` (untouched), and `toPoll` (reading `_targetChanges.Value`,
      gated by `_active`) branches, merged, then `Select(...).Switch()` — `Observable.Empty<TInfo?>()`
      for the clear case, `Observable.FromAsync(() => FetchInternalAsync(t.Target))` otherwise.
- [x] 1.4 Delete `RefreshNow()` and `FetchAndShowAsync()`.
- [x] 1.5 Dispose `_targetChanges` alongside `_subscription` in `Dispose(bool disposing)`.

## 2. Caller updates

- [x] 2.1 Remove the explicit `_keyDetails.RefreshNow()` call in `KvTab.OnKeyHighlightChanged`
      (the base class now fetches on target change automatically); keep the existing
      `_keyDetails.Show(null)` clear-before-set-target call, since clearing still doesn't
      auto-display anything.
- [x] 2.2 Re-read `StreamsTab`/`StreamDetails`/`ConsumerDetails` highlight-change call sites to
      confirm no code change is needed there (per proposal.md's Impact section) — only behavior
      changes. Confirmed: only `SetTarget`/`Show` are called, never `RefreshNow`.

## 3. Verification

- [x] 3.1 Build (`dotnet build src/lazynats.sln`) and confirm no leftover references to
      `RefreshNow`/`FetchAndShowAsync`. Build succeeds; only remaining mention is an explanatory
      comment (no code symbol).
- [x] 3.2 Manually verify via the tmux-driven flow (CLAUDE.md's testing approach) across Streams,
      Consumers, and KV tabs: highlight-change shows fresh data promptly without waiting ~3s, and
      holding an arrow key through a longer list doesn't visibly stutter. Verified against a live
      NATS server: single-step and rapid triple-step (3x Down, single capture) both landed on the
      correct final item with fresh data (KV key body, stream stats) in both the KV and Streams
      tabs.
- [x] 3.3 Manually verify clearing a highlight (e.g. an emptied list, or ascending out of a KV
      bucket) doesn't flash a stale value before settling to empty. Verified: ascending out of the
      LAZYNATS_DEMO bucket re-showed the cached bucket-level details cleanly, no stale key data.

## 4. Spec sync

- [x] 4.1 After implementation is verified, sync this change's `polling-details` delta spec into
      `openspec/specs/polling-details/spec.md` (via `openspec-sync-specs`/archive) so the main spec
      reflects the unconditional, debounced "Immediate Fetch On Demand" behavior. Also fixed a
      requirement-name mismatch found while syncing ("Debounced Fetch On Target Change" →
      "Immediate Fetch On Demand") in both the delta and main spec.
