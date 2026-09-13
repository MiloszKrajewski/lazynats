## 1. Implicit Namespace Filtering

- [x] 1.1 In `SubscriptionRegistry.RunAsync`, compute `excludeSystem`/`excludeInbox` booleans once
      per subscription from `pattern` (`!pattern.StartsWith('$')` /
      `!pattern.StartsWith("_INBOX.")`), before the receive loop starts.
- [x] 1.2 In the receive loop, exclude a message when `excludeSystem && message.Subject.StartsWith('$')`
      or `excludeInbox && message.Subject.StartsWith("_INBOX.")`, short-circuiting before the
      existing `clientFilter` check and before constructing a `FeedEnvelope`.

## 2. Verification

- [x] 2.1 Manually verify via `tmux`: subscribe to `>`, confirm `$SYS.>`/`_INBOX.>` traffic does
      not appear in the live feed while ordinary application traffic does.
- [x] 2.2 Manually verify: subscribe to `$SYS.>` explicitly, confirm matching system traffic does
      appear in the live feed.
- [x] 2.3 Manually verify: subscribe to `_INBOX.>` explicitly, confirm matching inbox traffic does
      appear in the live feed.
