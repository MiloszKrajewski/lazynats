# General UI design

UI consist of two main areas: managements part (top) and live feed part (bottom). I guess fair split
for now is 70/30, but I will need to see it live to decide.

# Management part

Management has tabs, each with a dedicated Alt+letter shortcut to switch to it directly from
anywhere in the app (only Subscribe and Publish exist today; the rest are reserved for when their
tabs are built, so future additions don't collide):

| Tab | Title | Shortcut |
|---|---|---|
| Subscribe (live NATS monitoring) | `Su[b]scribe` | Alt+B |
| Publish | `[P]ublish` | Alt+P |
| Streams (durable streams) | `[S]treams` | Alt+S |
| Consumers (durable consumers on streams) | `[C]onsumers` | Alt+C |
| KV stores (key/value stores) | `[K]V` | Alt+K |
| OBJ stores (object stores) | `[O]BJ` | Alt+O |

"Subscribe"/"Publish" are a matched verb pair (NATS's own `nats sub`/`nats pub` vocabulary).
Subscribe uses Alt+B rather than Alt+S so it doesn't collide with Streams.

Not decided: do consumers have their own tab or are part os streams tab

# Feed part

This is log of received events. Even consists of:
* timestamp
* subject
* headers (key/value pairs, both strings)
* payload (byte buffer, however is something is JSON it should be shown as json, it looks like a UTF8 string is should be shown as UTF8 string)

messages are shown line per message, to get deeper you need to select it and "go in" (presumably enter)

# Sending messages

I would like ability to send a message: subject, headers, payload. This lives in its own
"Publish" management tab (inserted right after Subscriptions), not a modal window: subject as a
single field, headers as an editable list of key/value pairs, payload as a multi-line text area.

Headers are edited keyboard-only: a key/value input row adds a pair on Enter, Delete removes the
selected pair from the list below it. No mouse-only affordances (no decorative or per-row
buttons) for this.

The Send button is disabled whenever the current entry is invalid (e.g. empty subject), with
invalid fields flagged visually (e.g. red text/icon) rather than via a popup. Sending gives
feedback in the status bar and keeps the form filled in, so the same message can be tweaked and
resent.

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