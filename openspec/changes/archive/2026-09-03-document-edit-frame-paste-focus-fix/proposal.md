## Why

`EditFrame` already carries a fix (uncommitted in the working tree) for a Terminal.Gui bug where
the first real-terminal paste (bracketed paste, e.g. physical Ctrl+V) into an `EditFrame`-wrapped
field is silently swallowed right after its containing `Dialog` becomes modal — because
`Application.Navigation`'s tracked focused view lags one level behind the correctly-cascaded
`HasFocus` chain, and paste (unlike ordinary typing) reads that tracked value directly. The fix
itself is implemented and commented in code; this change captures it as a spec requirement and
records the workaround alongside the project's other documented Terminal.Gui workarounds, so it
isn't lost, silently regressed, or reintroduced from scratch when someone eventually deletes it
after upstream fixes the one-word root cause.

## What Changes

- No behavioral or code changes — the fix already exists in `EditFrame.cs`
  (`WireInitialPasteFocusFix`, wired via `Initialized`).
- Add a spec requirement to `edit-frame` documenting that `EditFrame` corrects
  `Application.Navigation`'s focused-view tracking after its containing dialog becomes modal, so
  bracketed paste reaches the wrapped child immediately rather than only after a subsequent
  Tab/Shift+Tab.
- Record the underlying Terminal.Gui bug (verified against v2.4.10 and v2.4.17 source, no upstream
  issue filed yet) and the removal condition, matching how `ViewPasteExtensions.FixPasteRedraw`'s
  workaround is written up in code, for consistency and discoverability.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `edit-frame`: add a requirement that `EditFrame` re-syncs `Application.Navigation`'s focused
  view to its wrapped child once the containing dialog becomes modal, so the child is
  paste-ready immediately rather than only after a subsequent focus change.

## Impact

- Documentation only: `openspec/specs/edit-frame/spec.md` gains a requirement; no source changes
  beyond what's already sitting in the working tree (`src/lazynats/Components/EditFrame.cs`).
