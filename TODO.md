# TODO

- editing sdingle entry triggers full refresh of the list? (it does in templates)
- be better explaining why you cannot edit binary key (or allow editing)
- background is black-black (not transparent black)
- explicit `$` subscriptions
- NatsFilter: isExact == regex is null
- wrap "failed to connect" (no nats, bad auth, etc) into user facing message (no stack trace)
- `?` shows "big" shortcuts as well (with ---)
- key/stream/consumer updates through big pipe
- do NATS accept su*>bject?
- subscriptions: don't delete failed, just show as broken, try restart every now and then
- OPTIONAL: delete subscription does not ask for permission
