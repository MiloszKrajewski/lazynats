using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Subscriptions;

// Single-field modal used by both SubscriptionsView.TryCreate and TryEdit - Result carries the
// committed pattern text, null if the dialog was cancelled (Esc or Cancel), per Dialog<TResult>'s
// own cancellation convention.
internal sealed class PatternDialog: Dialog<string>
{
    private static readonly Attribute InvalidPatternAttribute = new(ColorName16.Red, ColorName16.DarkGray);

    private readonly TextField _patternField;
    private readonly Button _okButton;

    public PatternDialog(string title, string initialPattern)
    {
        Title = title;

        // _okButton is constructed before _patternField's ValueChanged is wired so that if setting
        // Text below happens to raise the event (e.g. changing from the TextField's own default),
        // UpdateValidity has a non-null button to update rather than crashing mid-construction.
        _okButton = new Button { Text = "_Ok" };

        var patternLabel = new Label { Text = "Pattern", X = 0, Y = 0 };
        _patternField = new TextField { X = 0, Y = 1, Width = 40, Text = initialPattern };
        _patternField.ValueChanged += (_, _) => UpdateValidity();
        Add(patternLabel, _patternField);

        var cancelButton = new Button { Text = "_Cancel" };
        cancelButton.Accepting += (_, _) => RequestStop();
        AddButton(cancelButton);

        _okButton.Accepting += (_, _) => {
            Result = _patternField.Text.Trim();
            RequestStop();
        };
        AddButton(_okButton);

        UpdateValidity();
    }

    private void UpdateValidity()
    {
        var valid = _patternField.Text.Trim().Length > 0;
        _okButton.Enabled = valid;
        _patternField.SetScheme(valid ? null : new Scheme(InvalidPatternAttribute));
    }
}
