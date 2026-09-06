## MODIFIED Requirements

### Requirement: Bucket List
The system SHALL provide a KV management tab listing the names of all Key/Value store buckets
currently present on the connected server. A stream counts as a KV bucket only when its name
matches the `KV_<name>` convention AND its subjects include one rooted at `$KV.<name>.` for that
same, stripped name — a stream whose name merely resembles the convention without the matching
subject binding is not treated as a bucket.

#### Scenario: Existing buckets are listed
- **WHEN** one or more KV buckets exist on the server
- **THEN** the Values tab's list shows each bucket's name

#### Scenario: No buckets exist
- **WHEN** no KV buckets exist on the server
- **THEN** the Values tab shows a non-interactive hint in place of the list, rather than a blank list

#### Scenario: A stream matching only the name convention is not listed as a bucket
- **WHEN** a JetStream stream's name starts with `KV_` but its subjects do not include one rooted
  at `$KV.<name>.` for the stripped name
- **THEN** that stream does not appear in the Values tab's bucket list
