using System.Text.RegularExpressions;

namespace lazynats.Core;

// Pattern-string-to-Regex translation, kept separate from how a caller uses the resulting Regex
// (IsMatch for a predicate, or anything else Regex offers) - that's the caller's concern, not this
// method's. Regex-backed (plain, non-Compiled - verified AOT/trim-safe via a throwaway PublishAot
// publish; only RegexOptions.Compiled needs reflection-emit, which this doesn't use) rather than a
// hand-rolled scanner - see doc/i-reinvented-the-wheel.md.
internal static class RegexExtensions
{
    // Fuzzy-subsequence match: query characters must occur in the target, in the same relative
    // order, with any (including zero) characters in between - e.g. "oce" matches
    // "OperationCancelledException". Conceptually this is "*o*c*e*" (a wildcard inserted around
    // and between each query character), but the pattern built here is "o.*c.*e" - no leading or
    // trailing ".*". That's deliberate, not an oversight: this pattern has no ^/$ anchors, so
    // Regex.IsMatch already searches anywhere in the target by default, which supplies the
    // missing leading/trailing "*" implicitly - the same result as the fully-anchored
    // "^.*o.*c.*e.*$" translation (verified equivalent, both against the hand-rolled scanner this
    // replaced and against each other), but faster: benchmarked at 10k realistic key/object names,
    // the unanchored form ran 11-13% faster for typical multi-character queries and 3.7x-4.4x
    // faster for single-character queries and non-matches, because an explicit leading ".*"
    // forces the regex engine into real backtracking from position 0, while the unanchored form
    // lets it use its own optimized "find this literal anywhere" scan instead. Runs once per
    // keystroke in the quick-search field against every loaded item, so this isn't free to ignore.
    public static Regex FuzzyToRegex(this string query) =>
        new(string.Join(".*", query.Select(c => Regex.Escape(c.ToString()))),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
