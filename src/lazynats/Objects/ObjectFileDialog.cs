using lazynats.Components;
using lazynats.Core;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Objects;

// Single dialog type driving both Upload and Download (design.md Decision 3), analogous to
// CreateKeyDialog's isEdit toggle: in upload mode Key is an editable TextField seeded empty and
// Path must point to an existing, readable local file; in download mode Key is disabled/locked to
// the highlighted object's name (`initial.Key`) and Path only needs to be non-empty (the download
// creates the file). Both modes share a "Browse" button in the footer row (plus an F2 shortcut for
// keyboard users already in the Path field) that opens Terminal.Gui's built-in OpenDialog/
// SaveDialog (design.md Decision 3's second alternative considered and rejected free-text-only
// input) - reusing Command.Find the same way ObjectListView reuses Command.Save for its own
// app-specific Download binding. Fields are wider than the 43 CreateKeyDialog/CreateBucketDialog
// use - local file paths run noticeably longer than a bucket or key name - but capped so the
// dialog's own outer border, not just the fields, stays within DialogWidth: fits an 80-column
// terminal with a few columns to spare, rather than exactly filling it edge-to-edge the way
// PublishDialog's FieldWidth=76 fields do (measured at 80 columns of outer border - Padding.Thickness
// plus Dialog's own border adds 4 columns beyond the field width, so a 76-wide field yields an
// 80-wide dialog with zero margin). Browse lives in the footer rather than squeezed inline next to
// Path so every field stays the same full FieldWidth.
internal sealed class ObjectFileDialog: Dialog<ObjectFileTransfer>
{
    private const int DialogWidth = 76;
    // Padding.Thickness(1,1,1,0) plus Dialog's own 1-column border on each side = 4 columns of
    // chrome outside the field content area.
    private const int FieldWidth = DialogWidth - 4;

    private static readonly Attribute InvalidAttribute = new(ColorName16.Red, Theme.EditableBackground);

    private readonly bool _isUpload;
    private readonly TextField _keyField;
    private readonly TextField _pathField;
    private readonly Button _commitButton;

    public ObjectFileDialog(bool isUpload, ObjectFileTransfer? initial = null)
    {
        _isUpload = isUpload;
        Title = isUpload ? " Upload Object " : " Download Object ";
        Padding.Thickness = new Thickness(1, 1, 1, 0);

        var keyLabel = new Label { Text = "Key", X = 0, Y = 0 };
        _keyField = new TextField {
            Text = initial?.Key ?? string.Empty, Enabled = isUpload, CanFocus = isUpload,
            TabStop = isUpload ? TabBehavior.TabStop : TabBehavior.NoStop,
        };
        _keyField.ValueChanged += (_, _) => UpdateValidity();
        _keyField.FixPasteRedraw();
        var keyFrame = WrapField(_keyField, 1);

        var pathLabel = new Label { Text = "Path", X = 0, Y = 4 };
        _pathField = new TextField { Text = initial?.Path ?? string.Empty };
        _pathField.ValueChanged += (_, _) => UpdateValidity();
        _pathField.FixPasteRedraw();
        var pathFrame = WrapField(_pathField, 5);

        Add(keyLabel, keyFrame, pathLabel, pathFrame);

        // Bound on the dialog itself, not the Path TextField instance - AddCommand is protected
        // on View, so only a subclass (this dialog) can call it on itself; a plain TextField
        // instance is out of reach. Firing regardless of which field currently has focus is fine
        // here since Browse always targets the Path field specifically. Kept alongside the
        // Browse footer button below as a faster path for keyboard users already in the field.
        AddCommand(Command.Find, () => { Browse(); return true; });
        KeyBindings.Add(Key.F2, Command.Find);

        // Added first so it reads leftmost in the footer, ahead of Cancel/commit. Doesn't call
        // RequestStop - Browse only fills in the Path field, it never closes this dialog. Marked
        // Handled for the same reason Cancel/commit are below: left unhandled, the click bubbles
        // up as an unhandled Accept, which OnAccepting swallows harmlessly, but there's no reason
        // to rely on that fallback when this handler already knows what to do.
        var browseButton = new Button { Text = "_Browse" };
        browseButton.Accepting += (_, e) => { e.Handled = true; Browse(); };
        AddButton(browseButton);

        // Result is left unset (null), matching Esc's own cancellation convention. Added before
        // the commit button so it - not Cancel - stays the last-added, Enter-activated default.
        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (_, e) => { e.Handled = true; RequestStop(); };
        AddButton(cancelButton);

        // Marks the event Handled - same reason CreateKeyDialog's Create button does: left
        // unhandled, Dialog<T>'s own default "unhandled Accept -> RequestStop" behavior fires
        // regardless of what Commit() decided, closing the dialog even while a field is invalid.
        _commitButton = new Button { Text = isUpload ? "_Upload" : "_Download" };
        _commitButton.Accepting += (_, e) => { e.Handled = true; Commit(); };
        AddButton(_commitButton);

        UpdateValidity();

        // See CreateKeyDialog's identical block for why: in download mode Key has CanFocus=false,
        // and nothing would otherwise be focused until the user's first Tab.
        if (!isUpload) _pathField.SetFocus();
    }

    // Enter pressed on a field isn't consumed by it, so it bubbles up as an unhandled Accept
    // instead of routing through the commit button's own Accepting event above - see
    // CreateKeyDialog's identical override for the full rationale.
    protected override bool OnAccepting(CommandEventArgs args) => true;

    private static EditFrame WrapField(View field, int y)
    {
        var background = field.GetAttributeForRole(VisualRole.Editable).Background;
        return new EditFrame(field) {
            X = 0, Y = y, Width = FieldWidth, Height = 3,
            InnerBackgroundNormal = background, InnerBackgroundFocused = background,
            // See CreateKeyDialog's identical WrapField for why: EditFrame's own CanFocus is
            // hardcoded true unconditionally, so without this a locked field's frame becomes a
            // "phantom" tab stop.
            CanFocus = field.CanFocus, TabStop = field.TabStop,
        };
    }

    // Opens Terminal.Gui's OpenDialog (upload - MustExist restricts to an existing file) or
    // SaveDialog (download - no such restriction, since downloading creates the file) modally; on
    // a non-cancelled pick, copies the chosen path into the Path field and re-validates
    // (design.md Decision 3 / "Browse For A Local File"). Cancelling leaves the field unchanged.
    private void Browse()
    {
        FileDialog picker = _isUpload
            ? new OpenDialog { Title = " Select File ", OpenMode = OpenMode.File, MustExist = true }
            : new SaveDialog { Title = " Select File " };

        if (_pathField.Text.Length > 0) picker.Path = _pathField.Text;

        App!.Run(picker);
        if (picker.Canceled) return;

        _pathField.Text = picker.Path;
        UpdateValidity();
    }

    private void Commit()
    {
        if (!_commitButton.Enabled) return;

        Result = new ObjectFileTransfer(_keyField.Text.Trim(), _pathField.Text.Trim());
        RequestStop();
    }

    private void UpdateValidity()
    {
        var keyValid = !_isUpload || _keyField.Text.Trim().Length > 0;
        var pathValid = _isUpload ? IsExistingReadableFile(_pathField.Text.Trim()) : _pathField.Text.Trim().Length > 0;

        SetFieldValidity(_keyField, keyValid);
        SetFieldValidity(_pathField, pathValid);
        _commitButton.Enabled = keyValid && pathValid;
    }

    private static bool IsExistingReadableFile(string path)
    {
        if (path.Length == 0) return false;

        try {
            if (!File.Exists(path)) return false;
            using var _ = File.OpenRead(path);
            return true;
        } catch {
            return false;
        }
    }

    private static void SetFieldValidity(TextField field, bool valid) =>
        // new Scheme(Attribute)'s single-value constructor derives Editable independently and
        // silently drops our background (defaults it to Black) - re-set Editable explicitly so
        // invalid state only changes the foreground, never the background (same as
        // CreateKeyDialog/CreateBucketDialog).
        field.SetScheme(valid ? null : new Scheme(InvalidAttribute) { Editable = InvalidAttribute });
}
