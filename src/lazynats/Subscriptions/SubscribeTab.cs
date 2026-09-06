using lazynats.Components;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.Subscriptions;

internal sealed class SubscribeTab: View, IShortcutSource
{
    private readonly SubscriptionsView _subscriptionsView;

    public SubscribeTab(SubscriptionRegistry registry)
    {
        CanFocus = true;

        _subscriptionsView = new SubscriptionsView(registry) { Background = Theme.EditableBackground };
        var subscriptionsLabel = new Label { Text = "Subscriptions", X = 0, Y = 0 };
        var subscriptionsFrame = new EditFrame(_subscriptionsView) {
            X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(),
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };

        Add(subscriptionsLabel, subscriptionsFrame);
    }

    // No KeyBindings/AddCommand for specific keys here - that would hardcode which keys this tab
    // forwards. Instead this fires once Terminal.Gui has already tried the focused view (and its own
    // ancestors) and found no handler, at which point it's this tab's turn; whatever key
    // _subscriptionsView's own TabOperations happens to expose (New/Edit/Delete today) is what gets
    // dispatched, so the list is free to add or drop an operation without this tab needing to know
    // about it in advance. Single list, no branching - unlike the drill-down tabs' _shortcutSource,
    // there's no second level to point this at, so _subscriptionsView is read directly.
    protected override bool OnKeyDownNotHandled(Key key)
    {
        if (_subscriptionsView.TabOperations.FirstOrDefault(h => h.Key == key) is { Action: { } action }) {
            action();
            return true;
        }

        return base.OnKeyDownNotHandled(key);
    }

    public IEnumerable<ShortcutHint> Shortcuts => _subscriptionsView.TabOperations;
}
