## Why

The management area is growing from two tabs (Subscriptions, Publish) into the six described in
`doc/UI.md` (adding Streams, Consumers, KV stores, OBJ stores). Naming and navigation decisions
made now need to hold up across all six: "Subscriptions" (noun) collides alphabetically with the
planned "Streams" tab for an Alt+S shortcut, and reads asymmetrically next to "Publish" (verb).
Separately, `Tabs` binds all four arrow keys to itself for tab switching, so any arrow key a
focused child doesn't fully consume (e.g. Up at the top row of a list) bubbles up and silently
switches tabs — surprising today, and a liability for every future list-bearing tab.

## What Changes

- Rename the "Subscriptions" tab to "Subscribe", pairing it with "Publish" as a verb pair matching
  NATS's own pub/sub vocabulary (`nats sub`/`nats pub`).
- Add Alt+letter shortcuts to switch tabs directly: Alt+B (Subscribe), Alt+P (Publish), reserving
  Alt+S / Alt+C / Alt+K / Alt+O for the not-yet-built Streams / Consumers / KV / OBJ tabs.
- Remove `Tabs`' own built-in arrow-key tab-switching bindings so tab switching is Alt-shortcut-only;
  arrow keys never leave the focused tab's content.
- `ListEditorView`: pressing Up while the first item is selected moves focus to the text input
  above the list, instead of being a dead end (previously this boundary silently switched tabs via
  the bubbling behavior being removed above).

## Capabilities

### New Capabilities
- `tab-navigation`: management tab titles and their Alt+letter keyboard shortcuts; arrow keys are
  confined to in-tab content and never switch tabs.

### Modified Capabilities
- `list-editor`: add a requirement that Up at the top row of the list moves focus to the text
  input above it.

## Impact

- `src/lazynats/MainWindow.cs`: tab titles, `Tabs` arrow-key bindings, new Alt+letter key bindings.
- `src/lazynats/Components/ListEditorView.cs`: top-of-list Up-key handling.
- `doc/UI.md`: tab naming reference.
- Future Streams/Consumers/KV/OBJ tabs inherit the Alt+letter convention and the arrow-key
  isolation established here.
