# Multi-color text rendering in Terminal.Gui v2

How to color part of a line differently from the rest of it — e.g. a live feed row's subject in
cyan, the rest in the row's normal color.

## There is no markup syntax

**Checked, not assumed:** loaded `Terminal.Gui.dll` (2.4.10) via reflection and enumerated every
type. There is no `Markup`, `RichText`, or `ColoredString` type anywhere in the assembly — nothing
like `[blue]text[/blue]` or `<color=blue>text</color>` tag parsing exists for app-facing text. The
only `Ansi*` types (`AnsiInput`, `AnsiOutput`, `AnsiEscapeSequenceRequest`, ...) are low-level
driver internals for parsing ANSI sequences *coming from* the terminal (mouse/keyboard input) and
serializing output to it — not something application code writes markup into.

The only mechanism is per-cell `Attribute` (fg/bg/style), set explicitly before each `AddStr`/
`AddRune` call. Coloring part of a line means manually splitting the draw into multiple
`SetAttribute` + `AddStr` calls — there's no shortcut around that.

## The two idioms used in this codebase

### 1. You own the View: sequential `SetAttribute` + `AddStr`

When the multi-color text is drawn inside your own `OnDrawingContent` override, just set a new
attribute before each segment — no need to save/restore anything, since you're free to leave the
attribute however you like when the method returns.

```csharp
// PollingDetailsView.OnDrawingContent — label/value split
var labelAttribute = GetAttributeForRole(VisualRole.Normal);
var valueAttribute = new Attribute(ValueColor, labelAttribute.Background);

Move(0, row);
SetAttribute(labelAttribute);
AddStr($"{label.PadLeft(labelWidth)}: ");
SetAttribute(valueAttribute);
AddStr(value);
```

Use this when you're a `View` subclass drawing its own content.

### 2. You're an external renderer handed someone else's View: capture-and-restore

`IListDataSource.Render(ListView listView, ...)` isn't a `View` subclass — it's handed a
`ListView` instance to draw into, and that `ListView` already committed to *some* attribute for
the row (normal, selected, focused, ...) before calling `Render`. You don't know which, and you
don't want to guess (hardcoding `VisualRole.Normal`'s background would look wrong on a selected
row). Confirmed via reflection that `View.SetAttribute`, `GetAttributeForRole`,
`SetAttributeForRole`, and `GetCurrentAttribute` are all `public` (not `protected`), so an external
class holding a `View` reference can call them directly — same primitive as idiom 1, just usable
from outside the View.

```csharp
// LiveLogDataSource.Render — color only the subject span, preserving row selection state
var prior = listView.GetCurrentAttribute();               // peek, don't disturb
listView.SetAttribute(new Attribute(Theme.SubjectColor, prior.Background));
listView.AddStr(subjectText);
listView.SetAttribute(prior);                              // restore for what follows
```

`GetCurrentAttribute()` peeks the attribute currently active without changing it — prefer it over
`SetAttribute(...)`'s return value (which also reports the previous attribute, but only as a
side effect of a redundant set) when you just want to look.

Use this whenever you're coloring inside someone else's already-attributed draw call and want your
change to compose with, not override, whatever they already decided (selection highlight, focus,
etc).

## Locating the span to color

The renderer needs to know *where* the colored substring sits in the full line, without
re-parsing the finished string. Compute and carry that position alongside the text, from wherever
the text itself is assembled — don't derive it downstream by scanning for a substring (fragile if
the value being searched for could itself appear elsewhere in the line, e.g. a subject that
happens to also appear inside the payload).

```csharp
// FeedRowFormatter.Format
var subjectStart = timestampText.Length + 2;   // computed next to the text it describes
var text = $"{timestampText}  {message.Subject}  {headerText}  {payloadText}";
return new FeedRow(text, subjectStart, message.Subject.Length);
```

A small `readonly record struct` (`Text`, plus one `Start`/`Length` pair per colored span) is
enough for one highlighted field. Don't build a generic list-of-spans shape until there's an
actual second span to justify it (YAGNI) — `FeedRow` above only carries one.

## Composing with horizontal scroll / width clipping

If the row can already be sliced for horizontal scroll (`viewportX`) and truncated to `width`,
the colored span's range has to be intersected with that visible window, not applied to the
original absolute offsets. This is a standard closed-interval intersection — clamp both ends:

```csharp
var visible = viewportX < text.Length ? text[viewportX..] : string.Empty;
if (visible.Length > width) visible = visible[..width];

// span range, in the same coordinate space as `visible` (0 = first on-screen column)
var spanVisibleStart = Math.Max(spanStart, viewportX) - viewportX;
var spanVisibleEnd = Math.Min(spanStart + spanLength, viewportX + visible.Length) - viewportX;

if (spanVisibleStart < spanVisibleEnd) {
    // non-empty intersection: draw visible[..spanVisibleStart], then the colored
    // visible[spanVisibleStart..spanVisibleEnd], then visible[spanVisibleEnd..] padded to width
} else {
    // span is scrolled entirely out of view: draw `visible` padded to width, no color change
}
```

`spanVisibleStart < spanVisibleEnd` being false covers both "span is left of the window" and
"span is right of the window" in one check — no separate case needed.

## Verifying colors from the terminal

`tmux capture-pane -p` strips all color/attribute information — text-only, exactly as documented
in `CLAUDE.md`'s tmux guidance. To actually confirm a color rendered, use `-e` to include ANSI
escape codes, then split the captured line on `\x1b` and inspect each run's SGR parameters (e.g.
`38;2;R;G;B` for truecolor foreground):

```bash
tmux capture-pane -t <session> -e -p | sed -n '<row>p' > /tmp/row.txt
awk 'BEGIN{RS="\x1b"} { match($0, /^\[[0-9;]*m/); esc=substr($0,RSTART,RLENGTH);
  text=substr($0,RLENGTH+1); printf "esc=%s len=%d text=[%s]\n", esc, length(text), text }' /tmp/row.txt
```

**Gotcha:** a run boundary can land a few space characters into the "wrong" color when two
adjacent segments share an otherwise-identical attribute (same fg/bg/style except for the
segment you actually colored) — this is a capture/terminal-serialization artifact around
invisible space glyphs, not a real defect, since a space has no visible foreground regardless of
its color. Confirm real correctness against a case where the surrounding attributes are genuinely
distinguishable (e.g. a selected row, where focus/selection is usually bold or a different
background) — there, boundaries should line up exactly with the intended span, character for
character.
