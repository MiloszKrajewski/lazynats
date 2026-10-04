using lazynats.Core.Subjects;

namespace lazynats.Core.Tests.Subjects;

// Covers nats-subscriptions' "Subject Exclusion Filter" requirement (default matching, empty/"^"
// extremes, apply-or-keep semantics, alternation/lookahead, case-sensitivity) and the
// Invalid/Pending/Applied classification behind "Editing and Applying the Exclusion Filter".
public class SubjectExclusionFilterTests
{
    [Theory]
    [InlineData("$SYS.ACCOUNT.PING", true)]
    [InlineData("$JS.API.INFO", true)]
    [InlineData("_INBOX.abc.1", true)]
    [InlineData("invoices.paid", false)]
    [InlineData("orders.$draft", false)] // '$' not at the start
    [InlineData("_INBOXES.x", false)] // "_INBOX" without the dot
    public void Default_ExcludesSystemAndInboxOnly(string subject, bool excluded)
    {
        var filter = new SubjectExclusionFilter();
        Assert.Equal(SubjectExclusionFilter.DefaultPattern, filter.Pattern);
        Assert.Equal(excluded, filter.IsExcluded(subject));
    }

    [Fact]
    public void Empty_ExcludesNothing()
    {
        var filter = new SubjectExclusionFilter(string.Empty);
        Assert.False(filter.IsExcluded("$SYS.ACCOUNT.PING"));
        Assert.False(filter.IsExcluded("_INBOX.abc"));
    }

    [Fact]
    public void BareCaret_ExcludesEverything()
    {
        var filter = new SubjectExclusionFilter("^");
        Assert.True(filter.IsExcluded("invoices.paid"));
    }

    [Fact]
    public void TryApply_Invalid_KeepsPreviousFilter()
    {
        var filter = new SubjectExclusionFilter();
        Assert.False(filter.TryApply(@"^(\$|_INBOX\."));
        Assert.Equal(SubjectExclusionFilter.DefaultPattern, filter.Pattern);
        Assert.True(filter.IsExcluded("_INBOX.abc"));
    }

    [Fact]
    public void TryApply_Valid_TakesEffect()
    {
        var filter = new SubjectExclusionFilter();
        Assert.True(filter.TryApply(@"^_INBOX\."));
        Assert.Equal(@"^_INBOX\.", filter.Pattern);
        Assert.False(filter.IsExcluded("$SYS.ACCOUNT.PING"));
        Assert.True(filter.IsExcluded("_INBOX.abc"));
    }

    [Theory]
    [InlineData("svc.a.heartbeat", true)]
    [InlineData("_INBOX.x", true)]
    [InlineData("svc.a.status", false)]
    public void Alternation_CombinesRules(string subject, bool excluded)
    {
        var filter = new SubjectExclusionFilter(@"^(\$|_INBOX\.)|\.heartbeat$");
        Assert.Equal(excluded, filter.IsExcluded(subject));
    }

    [Theory]
    [InlineData("$JS.API.INFO", true)]
    [InlineData("$SYS.ACCOUNT.PING", false)]
    [InlineData("_INBOX.x", true)]
    public void NegativeLookahead_CarvesOutException(string subject, bool excluded)
    {
        var filter = new SubjectExclusionFilter(@"^(\$(?!SYS\.)|_INBOX\.)");
        Assert.Equal(excluded, filter.IsExcluded(subject));
    }

    [Fact]
    public void Matching_IsCaseSensitive()
    {
        var filter = new SubjectExclusionFilter("heartbeat");
        Assert.True(filter.IsExcluded("svc.heartbeat"));
        Assert.False(filter.IsExcluded("svc.HEARTBEAT"));
    }

    [Fact]
    public void Constructor_InvalidPattern_Throws() =>
        Assert.Throws<ArgumentException>(() => new SubjectExclusionFilter("("));

    // One theory per state rather than an ExclusionEditState parameter - the enum is internal, and
    // xunit test methods must be public.
    [Fact]
    public void Classify_PatternInEffect_IsApplied() =>
        Assert.Equal(ExclusionEditState.Applied, new SubjectExclusionFilter().Classify(SubjectExclusionFilter.DefaultPattern));

    [Theory]
    [InlineData(@"^_INBOX\.")]
    [InlineData("")]
    [InlineData(@"^(\$|_INBOX\.) ")] // not trimmed
    public void Classify_OtherValidText_IsPending(string text) =>
        Assert.Equal(ExclusionEditState.Pending, new SubjectExclusionFilter().Classify(text));

    [Theory]
    [InlineData(@"^(\$|_INBOX\.")]
    [InlineData("(")]
    public void Classify_NonCompilingText_IsInvalid(string text) =>
        Assert.Equal(ExclusionEditState.Invalid, new SubjectExclusionFilter().Classify(text));

    [Fact]
    public void Classify_FollowsApply()
    {
        var filter = new SubjectExclusionFilter();
        Assert.Equal(ExclusionEditState.Pending, filter.Classify("x"));
        filter.TryApply("x");
        Assert.Equal(ExclusionEditState.Applied, filter.Classify("x"));
        Assert.Equal(ExclusionEditState.Pending, filter.Classify(SubjectExclusionFilter.DefaultPattern));
    }
}
