namespace lazynats.Subscriptions;

internal interface ISubscriptionInfo
{
    Guid Id { get; }
    string Pattern { get; }
}
