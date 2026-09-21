## ADDED Requirements

### Requirement: Save Feed Message as Template
The system SHALL allow the user to open the Create Template dialog pre-populated from a Live Feed
message, per `live-feed`'s "Save as Template Shortcut" requirement. The opened dialog SHALL be the
same Create Template dialog "Create Template" specifies, with Name left empty (as in a blank
Create), Subject copied verbatim from the message's subject, Headers copied verbatim from the
message's headers, Payload Type defaulted from the message's payload content classification (per
`payload-content-probe`/`payload-presentation`; an empty payload SHALL default to `Text`, matching
a blank Create Template dialog's own default), and Payload seeded by rendering the message's raw
payload bytes for display, per the `payload-edit-section` capability's seeding-from-bytes rule -
the same rendering `Edit Template` already uses (pretty-printed for `Json`, wrapped/grouped for
`Hex`/`Base64`, decoded UTF-8 text for `Text`) - rather than the raw wire bytes shown unformatted.
From that point on the dialog behaves exactly as "Create Template" and "Create Template Field
Validation" specify: every field remains editable, Name/Subject/Payload validation applies
unchanged, and Create/Cancel/error-reopen behave unchanged. Neither opening the dialog nor
cancelling it SHALL create a template or otherwise modify the `lazynats-templates` bucket; only
confirming (Create) does.

#### Scenario: The dialog is pre-populated from the message
- **WHEN** the user opens the Create Template dialog via a Live Feed message (per `live-feed`'s
  `T` shortcut)
- **THEN** the dialog opens with Name empty, Subject and Headers matching that message's subject
  and headers, Payload Type defaulted from the message's payload classification, and Payload
  showing that message's payload rendered for display

#### Scenario: An empty payload defaults to Payload Type Text
- **WHEN** the message used to pre-populate the dialog has an empty payload
- **THEN** the dialog opens with Payload Type defaulted to `Text`, matching a blank Create Template
  dialog's own default

#### Scenario: A message with no headers still opens with an empty Headers field
- **WHEN** the message used to pre-populate the dialog has no headers
- **THEN** the dialog opens with an empty Headers field, the same as a blank Create Template
  dialog

#### Scenario: The pre-populated Name field is left for the user to fill in
- **WHEN** the dialog is opened via a Live Feed message
- **THEN** the Name field is empty and the Create action is unavailable until the user enters a
  Name (per "Create Template Field Validation")

#### Scenario: Confirming creates the template exactly as Create Template does
- **WHEN** the user fills in a Name, optionally edits the pre-populated Subject, Headers, Payload
  Type, or Payload, and confirms (Create)
- **THEN** the system creates the template per "Create Template", including ensuring the
  `lazynats-templates` bucket exists and refreshing the template list with the new template shown
  and highlighted

#### Scenario: Cancelling creates nothing and leaves the source message unaffected
- **WHEN** the user opens the dialog via a Live Feed message and cancels (Esc) instead of
  confirming
- **THEN** no template is created, the `lazynats-templates` bucket is unchanged, and the source
  message in the Live Feed is unaffected

#### Scenario: Pre-populating does not require the Templates tab to have been visited
- **WHEN** the user saves a Live Feed message as a template before ever having activated the
  Templates tab in the current session
- **THEN** the template is created successfully, the same as if the Templates tab had already been
  visited
