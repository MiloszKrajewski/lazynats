using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Subscriptions;

// Single-field modal used by both SubscriptionsView.TryCreate and TryEdit - Result carries the
// committed pattern text, null if the dialog was cancelled (Esc), per Dialog<TResult>'s own
// cancellation convention. No buttons: Enter (on the field) commits while the pattern is valid,
// Esc cancels via Dialog<TResult>'s inherited behavior. Handled by Accepting (not Accepted) and
// always marked e.Handled - otherwise, with no button to consume it, Enter bubbles up to
// Dialog<TResult>'s own default accept handling and closes the dialog regardless of validity.
internal sealed class PatternDialog: Dialog<string>
{
    private static readonly Attribute InvalidPatternAttribute = new(ColorName16.Red, ColorName16.DarkGray);

    private readonly TextField _patternField;

    public PatternDialog(string title, string initialPattern)
    {
        Title = title;
        Padding.Thickness = new Thickness(1, 0, 1, 0);

        var patternLabel = new Label { Text = "Pattern", X = 0, Y = 0 };
        _patternField = new TextField { X = 0, Y = 1, Width = 40, Text = initialPattern };
        _patternField.ValueChanged += (_, _) => UpdateValidity();
        _patternField.Accepting += (_, e) => {
            e.Handled = true;
            if (_patternField.Text.Trim().Length == 0) return;
            Result = _patternField.Text.Trim();
            RequestStop();
        };
        Add(patternLabel, _patternField);

        UpdateValidity();
    }

    private void UpdateValidity()
    {
        var valid = _patternField.Text.Trim().Length > 0;
        _patternField.SetScheme(valid ? null : new Scheme(InvalidPatternAttribute));
    }
}
