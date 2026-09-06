using lazynats.Components;

namespace lazynats.Subscriptions;

internal sealed class SubscriptionPatternPresenter: IValuePresenter<SubscriptionInfo>
{
    public string Format(SubscriptionInfo value) => value.Pattern;
}
