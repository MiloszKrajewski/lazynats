## Why

Templates exist so the user doesn't have to retype common messages, but today the only way to
actually send one is to read its Subject/Headers/Payload Type/Payload off the detail panel and
retype them into the Publish dialog (Alt+P) by hand. That's exactly the busywork Templates was
meant to eliminate. Letting the user press P on a highlighted template to open the Publish dialog
already filled in closes that gap.

## What Changes

- The Templates list gains a new bare `P` shortcut (list-focus-scoped, distinct from the existing
  global Alt+P): with a template highlighted, it opens the Publish dialog pre-populated with that
  template's Subject, Headers, Payload Type, and Payload, rendered for display the same way "Edit
  Template" seeds its Payload field (pretty-printed Json, grouped Hex, wrapped Base64).
- With no template highlighted (empty list), `P` does nothing - same convention as `E`/`D` on an
  empty Templates list.
- The pre-populated dialog is otherwise the same Publish dialog: every field stays editable before
  Send, and Send/Cancel/close-on-success behavior is unchanged.
- Alt+P's own behavior is unchanged - it still always opens the Publish dialog empty.

## Capabilities

### Modified Capabilities
- `nats-publish`: the Publish Dialog requirement gains an alternate open path (from a template)
  that seeds its fields instead of opening empty; a new requirement documents how that seeding
  renders each Payload Type.
- `nats-templates`: the Templates list gains a new `P` shortcut that opens a pre-populated Publish
  dialog for the highlighted template.

## Impact

- `src/lazynats/Publish/PublishDialog.cs`: gains an optional seed input (Subject, Headers, Payload
  Type, Payload bytes) so it can be constructed pre-populated instead of always empty.
- `src/lazynats/Templates/TemplatesTab.cs` / `TemplateListView.cs`: gains a new `P` shortcut hint
  dispatched the same way Export (`X`)/Import (`O`) already are, guarded on a template being
  highlighted.
- `src/lazynats/MainWindow.cs`: `TemplatesTab` needs the `NatsConnection` already available there
  (currently only passed to the global Alt+P binding) so it can construct a seeded
  `PublishDialog`.
