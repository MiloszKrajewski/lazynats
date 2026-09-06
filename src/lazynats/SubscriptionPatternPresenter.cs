namespace lazynats;

internal sealed class SubscriptionPatternPresenter: IValuePresenter<SubscriptionInfo>
{
    public string Format(SubscriptionInfo value) => value.Pattern;

    public bool TryParse(string raw, out SubscriptionInfo value, out string? error)
    {
        var pattern = raw.Trim();
        if (pattern.Length == 0) {
            value = default!;
            error = "pattern must not be empty";
            return false;
        }

        value = new SubscriptionInfo(Guid.Empty, pattern);
        error = null;
        return true;
    }
}
