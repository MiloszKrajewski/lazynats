using Terminal.Gui.Drawing;

namespace lazynats.Components;

// Generic (color, text) piece of a rendered row - named generically, not FeedRowSegment, because
// nothing about the shape is feed-specific; only FeedRowFormatter (assembly) and
// LiveLogDataSource (the FeedEnvelope binding) are. `Color: null` means "leave whatever attribute
// the row already has (normal or selected) alone" - see live-feed's "Row Subject/Header Text Is
// Colored" requirements and doc/multi-color-rendering.md idiom 2.
internal readonly record struct RowSegment(Color? Color, string Text);

// An ordered, contiguous sequence of RowSegments making up one row's full (unclipped) text -
// replaces a single Text string plus offset pairs so a renderer can walk it generically
// regardless of how many colored regions a row has.
internal readonly record struct ColoredRow(IReadOnlyList<RowSegment> Segments)
{
    public int Length => Segments.Sum(s => s.Text.Length);
}
