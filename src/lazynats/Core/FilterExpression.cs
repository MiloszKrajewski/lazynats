using System.Text;
using System.Text.RegularExpressions;

namespace lazynats.Core;

// Regex is always the exact match for the expression, regardless of whether NativeFilter is also
// exact - every consumer without a server-side scoped fetch to scope (every list but KV keys)
// applies Regex alone and never looks at NativeFilter/NativeFilterIsExact at all. NativeFilter is
// always a valid native NATS subject filter (over-approximating where the expression needs a regex
// phase) for the one consumer that does have a fetch to scope; NativeFilterIsExact is true exactly
// when that native filter alone already resolves the same matches Regex would, letting that one
// consumer skip re-applying Regex afterward as a perf optimization. See
// openspec/changes/unify-list-filtering/design.md Decision 1.
internal sealed record CompiledFilter(string NativeFilter, Regex Regex, bool NativeFilterIsExact);

// Compiles the shared list-filter grammar (openspec/specs/kv-filter-expression/spec.md) used by
// every Ctrl+F pattern filter in the app. Deliberately separate from RegexExtensions
// (WildcardToRegex/FuzzyToRegex) - those produce only a Regex for best-effort UX search; this
// produces a (native filter, exact regex) pair for protocol-shaped exact matching, and is
// case-sensitive (no RegexOptions.IgnoreCase) to match NATS subject semantics.
internal static class FilterExpression
{
    public static CompiledFilter? TryCompile(string expression)
    {
        var segments = expression.Split('.');
        if (segments.Any(s => s.Length == 0))
            return null;

        // Regex is built from every segment unconditionally - including when the expression is
        // already valid native NATS subject-wildcard syntax (the "fast path" below) - since a
        // consumer with no native-scoped fetch at all still needs an exact in-memory match to
        // apply, and this same per-segment translation already produces one correctly for that
        // case too (a bare `*` -> a single token, a bare terminal `>` -> one or more).
        var pattern = "^" + string.Join(@"\.", segments.Select(SegmentToRegexFragment)) + "$";
        var regex = new Regex(pattern);

        if (IsNativeFastPath(segments))
            return new CompiledFilter(expression, regex, NativeFilterIsExact: true);

        // Native filter construction stops (and collapses the remainder to '>') the moment a
        // segment's '>' isn't a bare final segment - native syntax has no way to promise a fixed
        // token count once an unbounded span is possible. Regex above is unaffected by that stop -
        // it keeps translating every segment, since the pattern string's '.' characters are always
        // literal separators regardless of what a wildcard can match.
        var nativeTokens = new List<string>();
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var isFinal = i == segments.Length - 1;

            if (segment == "*" || (segment == ">" && isFinal))
            {
                nativeTokens.Add(segment);
                continue;
            }

            if (segment.Contains('>'))
            {
                nativeTokens.Add(">");
                break;
            }

            if (segment.ContainsAny("*?".AsSpan()))
            {
                nativeTokens.Add("*");
                continue;
            }

            nativeTokens.Add(segment);
        }

        return new CompiledFilter(string.Join('.', nativeTokens), regex, NativeFilterIsExact: false);
    }

    // Every segment is exactly one of: a plain literal run, a bare `*`, or (only as the final
    // segment) a bare `>` - i.e. the expression is already valid native NATS subject-wildcard
    // syntax, so a native-scoped fetch using it verbatim already resolves the exact same matches
    // Regex would.
    private static bool IsNativeFastPath(string[] segments)
    {
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment == "*") continue;
            if (segment == ">" && i == segments.Length - 1) continue;

            if (segment.Any(c => c is '*' or '?' or '>')) return false;
        }

        return true;
    }

    // A bare whole-segment `*` matches a whole token ([^.]+); everywhere else `*` is inline within
    // a mixed segment and never matches an empty run there either side of other chars, but can
    // itself be empty ([^.]*). `>` always maps to `.*` here regardless of bare/terminal status -
    // verified against every worked example in design.md: once a `>` forces the regex phase at
    // all (a bare terminal `>` alone never does - that's the fast path above), the native filter
    // has already constrained the candidate's token structure, so `.*` and the "one-or-more whole
    // tokens" regex it could otherwise use match identical candidates in practice.
    private static string SegmentToRegexFragment(string segment)
    {
        if (segment == "*") return "[^.]+";

        var builder = new StringBuilder();
        foreach (var c in segment)
        {
            var expression = c switch {
                '*' => "[^.]*",
                '?' => "[^.]",
                '>' => ".*",
                _ => Regex.Escape(c.ToString())
            };
            builder.Append(expression);
        }

        return builder.ToString();
    }
}
