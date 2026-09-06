using System.Text;
using System.Text.RegularExpressions;

namespace lazynats.Core;

// NativeFilter is always present - even when PostFilter is non-null, NativeFilter still scopes
// the server-side fetch to a (possibly over-approximated) superset. PostFilter is null when
// NativeFilter alone is exact - see KeyFilterExpression.TryCompile's native-fast-path check.
internal sealed record CompiledKeyFilter(string NativeFilter, Regex? PostFilter);

// Compiles the KV key filter grammar (openspec/changes/add-kv-filter-language/design.md) into a
// native NATS subject filter plus an optional client-side regex. Deliberately separate from
// RegexExtensions (WildcardToRegex/FuzzyToRegex) - those produce only a Regex for best-effort UX
// search; this produces a (native filter, regex?) pair for protocol-shaped exact matching, and is
// case-sensitive (no RegexOptions.IgnoreCase) to match NATS subject semantics.
internal static class KeyFilterExpression
{
    public static CompiledKeyFilter? TryCompile(string expression)
    {
        var segments = expression.Split('.');
        if (segments.Any(s => s.Length == 0))
            return null;

        if (IsNativeFastPath(segments))
            return new CompiledKeyFilter(expression, null);

        // Native filter construction stops (and collapses the remainder to '>') the moment a
        // segment's '>' isn't a bare final segment - native syntax has no way to promise a fixed
        // token count once an unbounded span is possible. The regex phase below is unaffected by
        // that stop - it keeps translating every segment, since the pattern string's '.'
        // characters are always literal separators regardless of what a wildcard can match.
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

        var pattern = "^" + string.Join(@"\.", segments.Select(SegmentToRegexFragment)) + "$";
        return new CompiledKeyFilter(string.Join('.', nativeTokens), new Regex(pattern));
    }

    // Every segment is exactly one of: a plain literal run, a bare `*`, or (only as the final
    // segment) a bare `>` - i.e. the expression is already valid native NATS subject-wildcard
    // syntax, so it can be used verbatim with no regex phase at all.
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
