# General UI design

UI consist of two main areas: managements part (top) and live feed part (bottom). I guess fair split
for now is 70/30, but I will need to see it live to decide.

# Management part

Management has tabs, each titled with its 1-based position (`N:Title`) and switchable directly
from anywhere in the app via Alt+N, where N is that position:

| Tab | Title | Shortcut |
|---|---|---|
| Subscribe (live NATS monitoring) | `1:Subscribe` | Alt+1 |
| Streams (durable streams, drill down into consumers) | `2:Streams` | Alt+2 |
| KV stores (key/value stores) | `3:KV` | Alt+3 |
| OBJ stores (object stores) | `4:OBJ` | Alt+4 |

Tab shortcuts use Alt+digit rather than Alt+letter so they never collide with a tab's own
mnemonic buttons, which Terminal.Gui also binds via Alt+letter. Publish isn't a tab: composing
and sending a message is a one-off action, so it lives behind Alt+P as a modal dialog instead
(see "Sending messages" below) - freeing the tab strip for the drill-down management views.

Consumers don't get their own tab. JetStream itself never addresses a consumer without its
parent stream (`consumer info`/`consumer ls` both require a stream), so the UI mirrors that:
there's no flat, cross-stream consumer list to show.

## Streams tab

Layout is the familiar LHS list / RHS info split. LHS starts as a list of streams; RHS shows
info for the highlighted stream. Pressing Enter on a stream drills LHS down into that stream's
consumers, and RHS switches to tracking the highlighted consumer instead. Esc/Backspace goes
back up a level to the stream list. The tab title or a breadcrumb should make the current level
obvious (e.g. which stream's consumers you're looking at), since the LHS list looks the same
shape at both levels.

RHS info (whether stream- or consumer-level) refreshes periodically while displayed, not just
on selection change, so it reflects live state (message counts, pending/ack counts, etc.)
without the user having to re-select. Not aggressively, though — this is a poll, not a
subscription, so pick an interval that stays cheap against the server (a few seconds, exact
value TBD) rather than refreshing on every tick.

Baseline operations needed on both levels: info (the RHS panel itself) and delete. Anything
beyond that (create/edit, purge, seal, etc.) is open — `nats stream --help` / `nats consumer
--help` lists the fuller vocabulary if we want to grow into it later.

# Feed part

This is log of received events. Even consists of:
* timestamp
* subject
* headers (key/value pairs, both strings)
* payload (byte buffer, however is something is JSON it should be shown as json, it looks like a UTF8 string is should be shown as UTF8 string)

messages are shown line per message, to get deeper you need to select it and "go in" (presumably enter)

# Sending messages

I would like ability to send a message: subject, headers, payload. This lives in a modal Publish
dialog, opened from anywhere via Alt+P: subject as a single field, headers as an editable list of
key/value pairs, payload as a multi-line text area, with Cancel and Send buttons. Composing and
sending a message is a one-off action, so a dialog fits better than a permanent tab; each Alt+P
press opens a fresh, empty dialog rather than resuming whatever was last typed.

Headers are edited keyboard-only: a key/value input row adds a pair on Enter, Delete removes the
selected pair from the list below it. No mouse-only affordances (no decorative or per-row
buttons) for this.

The Send button is disabled whenever the current entry is invalid (e.g. empty subject), with
invalid fields flagged visually (e.g. red text/icon) rather than via a popup. A successful send
closes the dialog. A failed send reports the error inline and keeps the form filled in without
closing the dialog, so the message can be fixed and resent. Cancel (or Esc) closes the dialog
without sending.

Initially payload as text only (enables JSON and UTF8), but loading a binary file might be
considered later.

Editing binary payloads may be disabled depending on availability/amount of work needed for hex
editor.

# Message templates

This streach goal. We need ability to keep message templates so we can send some standard messages easily. Templates can be loaded from json file. Templates are grouped by arbitrary name (let's say we can have "invoicing" or "bookings" template groups)

```json
{
    "invoicing": {
        "get-invoice-by-id": {
            "subject": "invoices.get",
            "headers": {
                "tenant": "name"
            },
            "type": "json|text|hex|base64",
            "payload": {
                "id": -1
            }
        }
    }
}
```

Note, `type` doescribes intepreatation of payload. 
* `json`: `payload` is a json, it should be read as JsonNode as send as UTF8 rendering of this node
* `text`: `payload` needs to be a string and will be send as UTF8 encoded string
* `base64`: `payload` is a string, but will be interpreted as base64 byte array
* `hex`: `payload` is a string, but will be interpreted as hex encoded byte array