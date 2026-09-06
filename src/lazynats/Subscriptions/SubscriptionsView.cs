using System.Collections.ObjectModel;
using lazynats.Components;

namespace lazynats.Subscriptions;

internal sealed class SubscriptionsView: ListEditorView<SubscriptionInfo>
{
    private static readonly SubscriptionPatternPresenter Presenter = new();

    private readonly SubscriptionRegistry _registry;
    private readonly ObservableCollection<SubscriptionInfo> _items;

    public SubscriptionsView(SubscriptionRegistry registry): 
        this(registry, new ObservableCollection<SubscriptionInfo>(registry.Active)) { }

    private SubscriptionsView(
        SubscriptionRegistry registry, 
        ObservableCollection<SubscriptionInfo> items): 
        base(items, Presenter)
    {
        _registry = registry;
        _items = items;
        _registry.Changed += RefreshFromRegistry;
    }

    protected override void Append(string raw)
    {
        if (!Presenter.TryParse(raw, out var value, out var error)) {
            OnParseError(raw, error);
            return;
        }

        if (EditingIndex is { } index && index < _items.Count) _registry.Remove(_items[index].Id);
        _registry.Add(value.Pattern);
        ClearInput();
    }

    protected override void Delete(int index)
    {
        if (index >= _items.Count) return;
        _registry.Remove(_items[index].Id);
        if (index == EditingIndex) ClearInput();
    }

    private void RefreshFromRegistry()
    {
        _items.Clear();
        foreach (var subscription in _registry.Active) _items.Add(subscription);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _registry.Changed -= RefreshFromRegistry;
        base.Dispose(disposing);
    }
}
