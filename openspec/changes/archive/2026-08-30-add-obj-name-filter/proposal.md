## Why

`add-kv-key-filter` (a companion change) adds a Ctrl+F server-side filter for KV buckets with too
many keys to browse comfortably. Object Store buckets have the same "too many entries to browse"
problem, but not the same fix: `INatsObjStore.ListAsync` (`NATS.Client.ObjectStore` 2.8.2) has no
filter parameter at all, and object names are base64url-encoded into a single opaque subject token
in the underlying stream (`$O.<bucket>.M.<encoded-name>`) specifically so arbitrary object names
can't be misread as subject hierarchy — so there is no NATS-native mechanism to filter by
human-readable name prefix server-side, unlike KV. The full object-name list still has to be
fetched either way. What a Ctrl+F filter *can* still do here — confirmed as the actual goal, not
a reduced-fetch-cost claim — is keep the number of items held in the list's own backing collection
and rendered in the list view small, by applying the filter to the fetched result before it
becomes the list's contents, rather than only live-narrowing an already-fully-populated list the
way the existing `/` quick-search does.

## What Changes

- The object-level list in the Objects tab (`ObjectListView`) gains a Ctrl+F binding — the same
  key and dialog shape `add-kv-key-filter` gives `KeyListView` — that opens a pattern dialog
  (reusing `PatternDialog` with its `allowEmpty` flag) seeded with the currently active filter, if
  any.
- Unlike KV, the fetch itself (`store.ListAsync(...)`) is unchanged and still retrieves every
  object name every time. Confirming the dialog filters that fetched result set down to the
  matching names *before* they're handed to `ObjectListView.ReplaceItems`, so the list's own
  backing collection and rendered rows stay small even though the wire fetch doesn't shrink.
  Matching uses case-insensitive filesystem-style wildcards (`*`/`?`), not NATS subject-wildcard
  notation (object names aren't subject-shaped — no token/dot-hierarchy contract to match against)
  and not the existing `/` quick-search's fuzzy-subsequence semantics either (too loose to
  reliably shrink a huge list down to a small, predictable window, which is this feature's actual
  purpose).
- The active filter persists across Ctrl+R and any create/delete-triggered refresh for the
  currently drilled-into bucket (so re-fetching doesn't undo the narrowing), and resets on
  ascend/descend, mirroring `add-kv-key-filter`'s KV behavior.
- The object list's title reflects an active filter, the same way KV's does.
- Explicitly not a fetch-size or network-cost reduction — every refresh still lists every object
  in the bucket from the server; only what's retained/rendered afterward shrinks.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `nats-obj`: adds a post-fetch, pre-population name-filter affordance (Ctrl+F) at the object
  level, and defines the filter's lifetime across refresh vs. ascend/descend, mirroring
  `nats-kv`'s server-side filter shape but implemented client-side over the already-fetched name
  list.

## Impact

- `src/lazynats/Objects/ObjectListView.cs`: bind Ctrl+F (via `Command.Open`, unused on this view —
  `Command.Save` is already taken by Ctrl+S download), raise a filter-requested event, add a
  "Filter" shortcut hint.
- `src/lazynats/Objects/ObjectsTab.cs`: track the active filter pattern per drilled-into bucket,
  open the pattern dialog on the new event, filter the fetched name list before calling
  `ReplaceItems`, update the list title to show the active filter.
- A new `Core/RegexExtensions.cs` (`string.WildcardToRegex()`, `*`/`?`, case-insensitive,
  full-name anchored) translating a wildcard pattern to a `Regex`, called once per refresh and
  reused across every name in that refresh rather than re-parsed per name — used only by this new
  pre-population filter. `RegexExtensions` also gained `FuzzyToRegex()`, the regex-based
  replacement for `/`'s pre-existing fuzzy-subsequence matcher (a separate, out-of-band cleanup —
  see `doc/i-reinvented-the-wheel.md` — with no behavior change, verified by direct equivalence
  testing against the hand-rolled scanner it replaced).
- `src/lazynats/Subscriptions/PatternDialog.cs`: reuses the `allowEmpty` flag added by
  `add-kv-key-filter` — no further change here if that change lands first; otherwise this change
  adds it (see design.md).
- No change to `DrillableListView<T>`'s public shape beyond the extracted matching helper — this
  stays `ObjectListView`-specific, not a new shared `Enable*` opt-in, for the same reason
  `add-kv-key-filter` kept its filter `KeyListView`-specific.
