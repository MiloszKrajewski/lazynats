using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Components;

// Segment-walking render loop shared by any IListDataSource drawing ColoredRows into a ListView
// (LiveLogDataSource, ColoredRowListDataSource) - extracted verbatim from LiveLogDataSource.Render
// (no logic change) since it has zero FeedEnvelope-specific logic: cursor tracking, viewport
// intersection, capture-and-restore color, pad-to-width all operate purely on ColoredRow/RowSegment.
internal static class ColoredRowRenderer
{
    public static void Render(ListView listView, ColoredRow coloredRow, int col, int row, int width, int viewportX)
    {
        var segments = coloredRow.Segments;
        var viewportEnd = viewportX + width;

        listView.Move(col, row);

        // Walk segments in order, tracking a running cursor (position in the row's full,
        // unclipped text) - each segment picks up exactly where the previous left off, so
        // [cursor, cursor + segment.Text.Length) is its text-space range. Intersect that with
        // the visible window and slice/draw only the intersected portion, under the segment's
        // own color (capture-and-restore, doc/multi-color-rendering.md idiom 2) when set, or the
        // attribute ListView already put in place for this row (normal or selected) when null -
        // see live-feed's "Row Subject/Header Text Is Colored" and "Row Payload Type Prefix Is
        // Colored" requirements.
        var cursor = 0;
        var written = 0;
        foreach (var segment in segments)
        {
            var segmentStart = cursor;
            var segmentEnd = cursor + segment.Text.Length;
            cursor = segmentEnd;

            var visibleStart = Math.Max(segmentStart, viewportX);
            var visibleEnd = Math.Min(segmentEnd, viewportEnd);
            if (visibleStart >= visibleEnd) continue;

            var slice = segment.Text.Substring(visibleStart - segmentStart, visibleEnd - visibleStart);

            if (segment.Color is { } color)
            {
                var prior = listView.GetCurrentAttribute();
                listView.SetAttribute(new Attribute(color, prior.Background));
                listView.AddStr(slice);
                listView.SetAttribute(prior);
            }
            else
            {
                listView.AddStr(slice);
            }

            written += slice.Length;
        }

        if (written < width) listView.AddStr(new string(' ', width - written));
    }
}
