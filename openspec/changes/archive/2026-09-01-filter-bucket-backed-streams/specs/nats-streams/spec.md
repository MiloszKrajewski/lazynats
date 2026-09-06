## MODIFIED Requirements

### Requirement: Stream List
The system SHALL provide a Streams management tab listing the names of all JetStream streams
currently present on the connected server, excluding any stream that backs a Key/Value or Object
Store bucket (see the `nats-kv` and `nats-obj` capabilities' "Bucket List" requirements for the
bucket definition) — those are shown in their own dedicated tabs instead.

#### Scenario: Existing streams are listed
- **WHEN** one or more JetStream streams exist on the server
- **THEN** the Streams tab's list shows each stream's name

#### Scenario: No streams exist
- **WHEN** no JetStream streams exist on the server (or JetStream is not enabled)
- **THEN** the Streams tab shows a non-interactive hint in place of the list, rather than a blank
  list

#### Scenario: KV bucket-backing streams are excluded
- **WHEN** a KV bucket exists on the server
- **THEN** the stream backing that bucket does not appear in the Streams tab's list

#### Scenario: Object Store bucket-backing streams are excluded
- **WHEN** an Object Store bucket exists on the server
- **THEN** the stream backing that bucket does not appear in the Streams tab's list

#### Scenario: A plain stream that merely resembles a bucket name is still listed
- **WHEN** a JetStream stream's name starts with `KV_` or `OBJ_` but its subjects do not include
  one rooted at the corresponding bucket's reserved subject (`$KV.<name>.` or `$O.<name>.`, using
  the name with the prefix stripped)
- **THEN** that stream still appears in the Streams tab's list, since it is not actually backing a
  bucket

#### Scenario: Only bucket-backing streams exist
- **WHEN** every JetStream stream on the server backs a KV or Object Store bucket
- **THEN** the Streams tab shows the same non-interactive hint as when no streams exist at all
