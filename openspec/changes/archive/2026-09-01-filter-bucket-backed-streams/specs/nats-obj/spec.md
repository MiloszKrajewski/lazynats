## MODIFIED Requirements

### Requirement: Bucket List
The system SHALL provide an OBJ management tab listing the names of all Object Store buckets
currently present on the connected server. A stream counts as an Object Store bucket only when its
name matches the `OBJ_<name>` convention AND its subjects include one rooted at `$O.<name>.` for
that same, stripped name — a stream whose name merely resembles the convention without the
matching subject binding is not treated as a bucket.

#### Scenario: Existing buckets are listed
- **WHEN** one or more OBJ buckets exist on the server
- **THEN** the Objects tab's list shows each bucket's name

#### Scenario: No buckets exist
- **WHEN** no OBJ buckets exist on the server
- **THEN** the Objects tab shows a non-interactive hint in place of the list, rather than a blank list

#### Scenario: A stream matching only the name convention is not listed as a bucket
- **WHEN** a JetStream stream's name starts with `OBJ_` but its subjects do not include one rooted
  at `$O.<name>.` for the stripped name
- **THEN** that stream does not appear in the Objects tab's bucket list
