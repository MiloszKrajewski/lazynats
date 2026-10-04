using System.Text.RegularExpressions;

namespace lazynats.Core.Subjects;

// Where the exclusion input's current text stands relative to the expression in effect - see
// nats-subscriptions' "Editing and Applying the Exclusion Filter". Invalid takes precedence over
// Pending: text that doesn't compile is Invalid whether or not it differs from Pattern.
internal enum ExclusionEditState
{
    Invalid,
    Pending,
    Applied,
}

// The single, global subject exclusion regex consulted by every subscription's reader loop (see
// openspec/specs/nats-subscriptions/spec.md). Written only from the UI thread (TryApply),
// read concurrently from each subscription's background task (IsExcluded) - the active Regex is a
// single volatile reference swapped whole, so readers take one consistent snapshot per message
// without locking. Pattern is only read on the UI thread.
internal sealed class SubjectExclusionFilter
{
    // Hides system ($...) and request-reply inbox (_INBOX....) traffic - what the implicit
    // per-subscription rule this replaced hid for any ordinary pattern.
    public const string DefaultPattern = @"^(\$|_INBOX\.)";

    // Guards subscription reader loops against a pathological user expression; see IsExcluded.
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);

    // null = empty expression = exclude nothing.
    private volatile Regex? _regex;

    public SubjectExclusionFilter(string pattern = DefaultPattern)
    {
        if (!TryApply(pattern))
            throw new ArgumentException($"Pattern '{pattern}' does not compile.", nameof(pattern));
    }

    // Text of the expression currently in effect.
    public string Pattern { get; private set; } = string.Empty;

    public static bool IsValid(string pattern) => TryCompile(pattern, out _);

    public bool TryApply(string pattern)
    {
        if (!TryCompile(pattern, out var regex)) return false;

        _regex = regex;
        Pattern = pattern;
        return true;
    }

    // Fails open on a match timeout: showing a message is safer than silently hiding it, and a
    // timeout must never fault the subscription's reader loop.
    public bool IsExcluded(string subject)
    {
        var regex = _regex;
        if (regex is null) return false;

        try
        {
            return regex.IsMatch(subject);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public ExclusionEditState Classify(string text) =>
        !IsValid(text) ? ExclusionEditState.Invalid
        : string.Equals(text, Pattern, StringComparison.Ordinal) ? ExclusionEditState.Applied
        : ExclusionEditState.Pending;

    // Case-sensitive (NATS subjects are), plain rather than RegexOptions.Compiled (the AOT-safe
    // choice, see RegexExtensions) and not NonBacktracking (which would reject lookarounds such as
    // ^\$(?!SYS\.) - a natural way to carve an exception out of an exclusion).
    private static bool TryCompile(string pattern, out Regex? regex)
    {
        regex = null;
        if (pattern.Length == 0) return true;

        try
        {
            regex = new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
