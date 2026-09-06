using lazynats.Components;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.Subscriptions;

internal sealed class SubscribeTab: View
{
    public SubscribeTab(SubscriptionRegistry registry)
    {
        CanFocus = true;

        var subscriptionsView = new SubscriptionsView(registry) { Background = Theme.EditableBackground };
        var subscriptionsLabel = new Label { Text = "Subscriptions", X = 0, Y = 0 };
        var subscriptionsFrame = new EditFrame(subscriptionsView) {
            X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(),
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };

        Add(subscriptionsLabel, subscriptionsFrame);
    }
}
