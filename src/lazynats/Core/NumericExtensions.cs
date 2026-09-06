namespace lazynats.Core;

// Comparer<T>.Default-based clamp helpers, generic rather than int-only since neither call site
// (Hex bytes/row, Base64 line width - see openspec/changes/adaptive-payload-width/design.md
// Decision 5) needs anything narrower, and a generic pair is no more expensive at those two int
// call sites.
internal static class NumericExtensions
{
    public static T NotLessThan<T>(this T value, T min) =>
        Comparer<T>.Default.Compare(value, min) < 0 ? min : value;

    public static T NotMoreThan<T>(this T value, T max) =>
        Comparer<T>.Default.Compare(value, max) > 0 ? max : value;
}
