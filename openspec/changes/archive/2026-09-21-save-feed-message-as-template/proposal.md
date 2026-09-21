## Why

Templates already save typing for messages the user sends often, but the only way to create one is
to retype its Subject/Headers/Payload from scratch in the New Template dialog - even when a message
carrying exactly that content just scrolled through the Live Feed. Letting the user save a feed
message directly as a template closes that loop, mirroring the existing "Publish From Template"
shortcut in the opposite direction (feed message -> template, instead of template -> published
message).

## What Changes

- Add a `T` ("Save as Template") shortcut to the Live Feed, scoped to the currently highlighted
  message (same highlighted-row scoping `Space`/`Enter`/`C` already use), following the same bare
  letter shortcut convention as every other list-scoped action in the app.
- Pressing it opens the New Template dialog pre-populated from that message: Subject copied
  verbatim, Headers copied verbatim, Payload Type defaulted from the message's payload
  classification (`payload-content-probe`/`payload-presentation`, the same classification already
  computed for that row), and Payload rendered from the message's raw bytes for readability (same
  seeding-from-bytes rendering `Edit Template` already uses) - Name is left blank for the user to
  fill in. The dialog behaves exactly like a normal Create Template dialog from that point: Name/
  Subject/Payload validation, Create/Cancel, and error-reopen-on-failure are unchanged.
- No template is created, and the source message is unaffected, unless the user confirms Create.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `live-feed`: adds the `T` "Save as Template" shortcut, scoped to the highlighted message row,
  advertised via the same `IShortcutSource` contract `Clear` already uses.
- `nats-templates`: adds a new entry point into "Create Template" - opening the same dialog
  pre-populated from a Live Feed message's Subject/Headers/Payload Type/Payload instead of blank.

## Impact

- `src/lazynats/LiveFeed/LiveUpdatesView.cs`: new `T` key binding/command and shortcut hint,
  scoped to the highlighted row (same guard `Enter`'s existing accept handler uses); raises a new
  event carrying the highlighted message up to its host.
- `src/lazynats/MainWindow.cs`: wires that new event to the existing `TemplatesTab` instance
  (already constructed there) instead of constructing a second, independent write path.
- `src/lazynats/Templates/TemplatesTab.cs`: exposes a way to open the Create Template dialog
  pre-populated from a message's Subject/Headers/payload bytes, reusing its existing
  `EnsureBucketExistsAsync`/`WriteAsync` write path unchanged.
- No changes to `TemplateDocument`, KV storage shape, or the Publish dialog.
