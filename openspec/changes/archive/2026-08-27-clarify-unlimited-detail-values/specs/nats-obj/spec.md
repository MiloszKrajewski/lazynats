## MODIFIED Requirements

### Requirement: Bucket Detail Panel
The system SHALL show, alongside the bucket list, a detail panel for the currently highlighted
bucket, presenting at least its compression setting, object/message count, byte size, replica
count, and max age. Limit fields that carry a server "no limit" sentinel value SHALL render as
`(unlimited)` rather than their raw sentinel.

#### Scenario: Highlighting a bucket shows its details
- **WHEN** the user moves the highlight to a bucket in the list
- **THEN** the detail panel shows that bucket's stats

#### Scenario: No bucket highlighted
- **WHEN** the bucket list is empty and no bucket is highlighted
- **THEN** the detail panel shows no bucket's details

#### Scenario: Unlimited Max Age renders as unlimited
- **WHEN** the highlighted bucket's `MaxAge` is zero (the server's "no limit" sentinel)
- **THEN** the Max Age row shows `(unlimited)` rather than `00:00:00`

#### Scenario: A configured limit still renders as its value
- **WHEN** the highlighted bucket's Max Age is set to an actual positive limit rather than the
  unlimited sentinel
- **THEN** that row shows the configured value, unchanged from today's rendering
