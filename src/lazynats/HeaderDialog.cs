using lazynats.Components;
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
    private static readonly Attribute InvalidHeaderAttribute = new(ColorName16.Red, Theme.EditableBackground);

    private readonly TextField _headerField;
    private readonly EditFrame _headerFrame;

    public HeaderDialog(string title, string initialText)
    {
        Title = title;
        Padding.Thickness = new Thickness(1, 0, 1, 0);

        var headerLabel = new Label { Text = "Header (key: value)", X = 0, Y = 0 };
        _headerField = new TextField { Text = initialText };
        _headerField.ValueChanged += (_, _) => UpdateValidity();
        _headerField.Accepting += (_, e) => {
            e.Handled = true;
            if (_headerField.Text.Trim().Length == 0) return;
            Result = _headerField.Text.Trim();
            RequestStop();
        };
        // A bare TextField only paints under its own text - width beyond that (notably the whole
        // field when empty/invalid) would otherwise show through to the dialog's own background
        // instead of reading as an editable region. EditFrame's own fill covers that regardless of
        // content (see openspec/changes/add-dark-theme).
        var headerBackground = _headerField.GetAttributeForRole(VisualRole.Editable).Background;
        _headerFrame = new EditFrame(_headerField) {
            X = 0, Y = 1, Width = 43, Height = 3,
            InnerBackgroundNormal = headerBackground, InnerBackgroundFocused = headerBackground,
        };
        Add(headerLabel, _headerFrame);

        UpdateValidity();
    }

    private void UpdateValidity()
    {
        var valid = _headerField.Text.Trim().Length > 0;
        // new Scheme(Attribute)'s single-value constructor derives Editable independently and
        // silently drops our background (defaults it to Black) - re-set Editable explicitly so
        // invalid state only changes the foreground, never the background (EditFrame's own
        // background is never touched here either, for the same reason).
        _headerField.SetScheme(valid ? null : new Scheme(InvalidHeaderAttribute) { Editable = InvalidHeaderAttribute });
    }
}
