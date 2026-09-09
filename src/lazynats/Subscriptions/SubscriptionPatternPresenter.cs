using lazynats.Components;

namespace lazynats.Subscriptions;

internal sealed class SubscriptionPatternPresenter: IValuePresenter<ISubscriptionInfo>
{
    public string Format(ISubscriptionInfo value) => value.Pattern;
}
