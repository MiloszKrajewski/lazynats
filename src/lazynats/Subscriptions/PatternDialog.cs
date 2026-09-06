using lazynats.Components;
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
    private static readonly Attribute InvalidPatternAttribute = new(ColorName16.Red, Theme.EditableBackground);

    private readonly TextField _patternField;
    private readonly EditFrame _patternFrame;
    private readonly bool _allowEmpty;
    private readonly Func<string, bool>? _validator;

    // `validator`, when supplied, is only consulted for non-empty text - an empty field's
    // validity is still governed solely by `allowEmpty`, so a grammar-aware validator (e.g.
    // KeyFilterExpression.TryCompile, which rejects an empty expression as an empty segment)
    // never has to special-case "field is empty" itself. Default null preserves today's
    // non-empty-only behavior for SubscriptionsView's plain-NATS-subject use.
    public PatternDialog(string title, string initialPattern, bool allowEmpty = false, Func<string, bool>? validator = null)
    {
        Title = DialogText.Pad(title);
        Padding.Thickness = new Thickness(1, 0, 1, 0);
        _allowEmpty = allowEmpty;
        _validator = validator;

        var patternLabel = new Label { Text = "Pattern", X = 0, Y = 0 };
        _patternField = new TextField { Text = initialPattern };
        _patternField.ValueChanged += (_, _) => UpdateValidity();
        _patternField.Accepting += (_, e) => {
            e.Handled = true;
            if (!IsValid()) return;
            Result = _patternField.Text.Trim();
            RequestStop();
        };
        // A bare TextField only paints under its own text - width beyond that (notably the whole
        // field when empty/invalid) would otherwise show through to the dialog's own background
        // instead of reading as an editable region. EditFrame's own fill covers that regardless of
        // content (see openspec/changes/add-dark-theme).
        var patternBackground = _patternField.GetAttributeForRole(VisualRole.Editable).Background;
        _patternFrame = new EditFrame(_patternField) {
            X = 0, Y = 1, Width = 43, Height = 3,
            InnerBackgroundNormal = patternBackground, InnerBackgroundFocused = patternBackground,
        };
        Add(patternLabel, _patternFrame);

        UpdateValidity();
    }

    private void UpdateValidity()
    {
        var valid = IsValid();
        // new Scheme(Attribute)'s single-value constructor derives Editable independently and
        // silently drops our background (defaults it to Black) - re-set Editable explicitly so
        // invalid state only changes the foreground, never the background (EditFrame's own
        // background is never touched here either, for the same reason).
        _patternField.SetScheme(valid ? null : new Scheme(InvalidPatternAttribute) { Editable = InvalidPatternAttribute });
    }

    private bool IsValid()
    {
        var trimmed = _patternField.Text.Trim();
        if (trimmed.Length == 0) return _allowEmpty;
        return _validator?.Invoke(trimmed) ?? true;
    }
}
