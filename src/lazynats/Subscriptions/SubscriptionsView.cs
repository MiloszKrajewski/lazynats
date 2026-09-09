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
        base(items, Presenter, bindSharedKeys: false, textColor: Theme.SubjectColor)
    {
        _registry = registry;
        _items = items;
        _registry.Changed += RefreshFromRegistry;
    }

    protected override string EmptyHint => "No subscriptions — N to add one";

    protected override bool TryCreate(out SubscriptionInfo result) =>
        TryEditPattern("New Subscription", string.Empty, out result);

    protected override bool TryEdit(SubscriptionInfo original, out SubscriptionInfo result) =>
        TryEditPattern("Edit Subscription", original.Pattern, out result);

    private bool TryEditPattern(string title, string initialPattern, out SubscriptionInfo result)
    {
        var dialog = new PatternDialog(title, initialPattern);
        App!.Run(dialog);

        if (dialog.Result is { } pattern) {
            result = new SubscriptionInfo(Guid.Empty, pattern);
            return true;
        }

        result = default!;
        return false;
    }

    // A NATS subscription can't be altered in place, so Add/Replace both go through the registry
    // (add-new / remove-old-then-add-new) rather than touching `_items` directly; `_items` is kept
    // in sync by RefreshFromRegistry reacting to the registry's own Changed event.
    protected override void Add(SubscriptionInfo value) => _registry.Add(value.Pattern);

    protected override void Replace(int index, SubscriptionInfo value)
    {
        _registry.Remove(_items[index].Id);
        _registry.Add(value.Pattern);
    }

    protected override void Delete(int index) => _registry.Remove(_items[index].Id);

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
