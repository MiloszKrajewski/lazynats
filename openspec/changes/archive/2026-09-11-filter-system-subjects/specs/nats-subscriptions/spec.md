## ADDED Requirements

### Requirement: Implicit System and Inbox Subject Filtering
Each active subscription SHALL implicitly exclude, per-subscription, any received message whose
subject starts with `$` unless that subscription's own pattern also starts with `$`, and any
received message whose subject starts with `_INBOX.` unless that subscription's own pattern also
starts with `_INBOX.`. This check SHALL be evaluated against the subscription's own raw pattern
text, independent of whether the pattern compiled to a native-exact filter or requires additional
client-side regex matching. An excluded message SHALL NOT be wrapped into a `FeedEnvelope` or
otherwise contributed to the shared feed pipeline.

#### Scenario: A wildcard subscription does not surface system traffic
- **WHEN** the user adds subject pattern `>` (or another pattern not starting with `$`), and a
  message is published on a subject starting with `$` (e.g. `$SYS.ACCOUNT.PING`)
- **THEN** that message is received from NATS but excluded before becoming a `FeedEnvelope`, and
  does not appear in the feed

#### Scenario: A wildcard subscription does not surface inbox traffic
- **WHEN** the user adds subject pattern `>` (or another pattern not starting with `_INBOX.`), and
  a message is published on a subject starting with `_INBOX.` (e.g. `_INBOX.abc123.1`)
- **THEN** that message is received from NATS but excluded before becoming a `FeedEnvelope`, and
  does not appear in the feed

#### Scenario: A pattern that explicitly opts into system subjects receives them
- **WHEN** the user adds subject pattern `$SYS.>` (a pattern starting with `$`), and a message is
  published on a subject starting with `$` and matching that pattern (e.g. `$SYS.ACCOUNT.PING`)
- **THEN** that message is not excluded by the implicit system-subject filter and appears in the
  feed as normal

#### Scenario: A pattern that explicitly opts into inbox subjects receives them
- **WHEN** the user adds subject pattern `_INBOX.>` (a pattern starting with `_INBOX.`), and a
  message is published on a subject starting with `_INBOX.` and matching that pattern
- **THEN** that message is not excluded by the implicit inbox-subject filter and appears in the
  feed as normal

#### Scenario: Ordinary subjects are unaffected
- **WHEN** a message is published on a subject that starts with neither `$` nor `_INBOX.`
- **THEN** the implicit filter does not exclude it, and existing pattern-matching behavior
  (native scoping plus any client-side filter) is unchanged
