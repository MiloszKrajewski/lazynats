using System.Collections.ObjectModel;
using lazynats.Components;
using lazynats.Core;
using Terminal.Gui.Views;

namespace lazynats.Subscriptions;

internal sealed class SubscriptionsView: ListEditorView<ISubscriptionInfo>
{
    private static readonly SubscriptionPatternPresenter Presenter = new();

    private readonly SubscriptionRegistry _registry;
    private readonly ObservableCollection<ISubscriptionInfo> _items;

    public SubscriptionsView(SubscriptionRegistry registry):
        this(registry, new ObservableCollection<ISubscriptionInfo>(registry.Active)) { }

    private SubscriptionsView(
        SubscriptionRegistry registry,
        ObservableCollection<ISubscriptionInfo> items):
        base(items, Presenter, bindSharedKeys: false, textColor: Theme.SubjectColor)
    {
        _registry = registry;
        _items = items;
        _registry.Changed += RefreshFromRegistry;
        _registry.Failed += OnRegistryFailed;
    }

    protected override string EmptyHint => "No subscriptions — N to add one";

    protected override bool TryCreate(out ISubscriptionInfo result) =>
        TryEditPattern("New Subscription", string.Empty, out result);

    protected override bool TryEdit(ISubscriptionInfo original, out ISubscriptionInfo result) =>
        TryEditPattern("Edit Subscription", original.Pattern, out result);

    private bool TryEditPattern(string title, string initialPattern, out ISubscriptionInfo result)
    {
        var dialog = new PatternDialog(title, initialPattern, validator: p => FilterExpression.TryCompile(p) is not null);
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
    protected override void Add(ISubscriptionInfo value) => _registry.Add(value.Pattern);

    protected override void Replace(int index, ISubscriptionInfo value)
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

    // Remove goes through the registry, same as a user-initiated delete, so _items resyncs via
    // the existing Changed -> RefreshFromRegistry path rather than a one-off splice here.
    private void OnRegistryFailed(ISubscriptionInfo subscription, Exception ex) =>
        App!.Invoke(() => {
            _registry.Remove(subscription.Id);
            MessageBox.ErrorQuery(App!, " Subscription Failed ", ex.Message.Pad(), "_Ok");
        });

    protected override void Dispose(bool disposing)
    {
        if (disposing) {
            _registry.Changed -= RefreshFromRegistry;
            _registry.Failed -= OnRegistryFailed;
        }
        base.Dispose(disposing);
    }
}
