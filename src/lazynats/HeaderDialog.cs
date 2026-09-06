using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats;

// Single-field modal used by both HeaderEditorView.TryCreate and TryEdit, mirroring
// Subscriptions/PatternDialog.cs. The field holds raw "key: value" text; splitting it into a
// HeaderPair is the caller's job (HeaderEditorView), not the dialog's - this dialog only knows
// about text, not headers.
internal sealed class HeaderDialog: Dialog<string>
{
    private static readonly Attribute InvalidHeaderAttribute = new(ColorName16.Red, ColorName16.DarkGray);

    private readonly TextField _headerField;

    public HeaderDialog(string title, string initialText)
    {
        Title = title;
        Padding.Thickness = new Thickness(1, 0, 1, 0);

        var headerLabel = new Label { Text = "Header (key: value)", X = 0, Y = 0 };
        _headerField = new TextField { X = 0, Y = 1, Width = 40, Text = initialText };
        _headerField.ValueChanged += (_, _) => UpdateValidity();
        _headerField.Accepting += (_, e) => {
            e.Handled = true;
            if (_headerField.Text.Trim().Length == 0) return;
            Result = _headerField.Text.Trim();
            RequestStop();
        };
        Add(headerLabel, _headerField);

        UpdateValidity();
    }

    private void UpdateValidity()
    {
        var valid = _headerField.Text.Trim().Length > 0;
        _headerField.SetScheme(valid ? null : new Scheme(InvalidHeaderAttribute));
    }
}
