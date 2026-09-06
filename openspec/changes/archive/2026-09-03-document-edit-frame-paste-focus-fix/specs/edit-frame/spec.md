## ADDED Requirements

### Requirement: Paste-Ready Focus After Modal Activation
When `EditFrame`'s wrapped child holds focus at the moment its containing dialog becomes modal,
`EditFrame` SHALL ensure the wrapped child, not `EditFrame` itself, is the view a bracketed paste
(e.g. a physical Ctrl+V in a real terminal) is delivered to — without requiring an intervening
Tab/Shift+Tab or other focus change first. This corrects a Terminal.Gui bug where becoming modal
re-points the application's tracked focused view one level short of the true focused leaf,
causing the wrapped child's first paste to be silently dropped.

#### Scenario: Paste into a focused wrapped child immediately after its dialog opens
- **WHEN** a dialog containing an `EditFrame`-wrapped field opens with that field already focused,
  and the user immediately performs a real-terminal (bracketed) paste with no prior Tab/Shift+Tab
- **THEN** the pasted content is delivered to the wrapped child, not silently dropped

#### Scenario: EditFrame whose child never gains focus is unaffected
- **WHEN** a dialog contains multiple `EditFrame`-wrapped fields and only one of them ends up
  focused when the dialog becomes modal
- **THEN** only the focused one's wrapped child is affected by this correction; the others take no
  action
