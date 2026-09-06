using System.Text;
using System.Text.RegularExpressions;

namespace lazynats.Core;

// Client is the exact matcher for the expression. Native is always a valid NATS subject filter
// used to scope server-side fetches; it may over-approximate and require client regex filtering.
// NativeFilterIsExact is true only when Native alone matches exactly, so regex can be skipped.
// See openspec/changes/unify-list-filtering/design.md Decision 1.
internal sealed record NatsFilter(string Native, Regex Client, bool NativeFilterIsExact);

// Compiles the shared list-filter grammar (openspec/specs/kv-filter-expression/spec.md) used by
// every Ctrl+F pattern filter in the app. Deliberately separate from RegexExtensions
// (WildcardToRegex/FuzzyToRegex) - those produce only a Regex for best-effort UX search; this
// produces a (native filter, exact regex) pair for protocol-shaped exact matching, and is
// case-sensitive (no RegexOptions.IgnoreCase) to match NATS subject semantics.
internal static class FilterExpression
{
    public static NatsFilter? TryCompile(string expression)
    {
        var segments = expression.Split('.');
        if (segments.Any(s => s.Length == 0))
            return null;

        // Regex is built from every segment unconditionally
        var pattern = "^" + string.Join(@"\.", segments.Select(SegmentToRegex)) + "$";
        var regex = new Regex(pattern);

        if (IsNativeFastPath(segments))
            return new NatsFilter(expression, regex, NativeFilterIsExact: true);

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

        return new NatsFilter(string.Join('.', nativeTokens), regex, NativeFilterIsExact: false);
    }

    // allowed: '*' as whole segment or '>' as final segment, or no wildcards at all
    private static bool IsNativeFastPath(string[] segments)
    {
        for (var i = 0; i < segments.Length; i++)
        {
            var last = i == segments.Length - 1;
            var segment = segments[i];
            var valid = segment == "*" || (segment == ">" && last) || !segment.ContainsAny("*?>".AsSpan());
            if (!valid) return false;
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
    private static string SegmentToRegex(string segment)
    {
        if (segment == "*") return "[^.]+";
        if (segment == ">") return ".*";
        
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
