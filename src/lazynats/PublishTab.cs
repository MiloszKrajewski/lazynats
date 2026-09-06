using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.Core;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats;

internal sealed class PublishTab: View, IShortcutSource
{
    private static readonly Attribute InvalidSubject = new(ColorName16.Red, Theme.EditableBackground);

    private readonly NatsConnection _connection;
    private readonly ShortcutTracker _shortcutTracker;
    private readonly ObservableCollection<HeaderPair> _headers = [];
    private readonly TextField _subjectField;
    private readonly EditFrame _subjectFrame;
    // TextView is obsolete in favor of Terminal.Gui.Editor's Editor view (confirmed AOT-clean by
    // AotProbe.EditorProbe), but the payload box is just a quick-and-dirty text/JSON field - none
    // of Editor's multi-caret/folding/highlighting is needed here. Revisit if that changes.
#pragma warning disable CS0618
    private readonly TextView _payloadView;
#pragma warning restore CS0618
    private readonly Button _sendButton;

    // Payload is the only band that's a live, always-editable multiline buffer (Subject is a
    // single-line TextField, Headers only ever edits via a modal) - that's what traps Tab/arrow
    // keys inside it instead of letting them bubble to focus navigation. Gate it behind an
    // explicit Navigate/Edit toggle: Navigate is the default, arrows/Tab move focus like they do
    // for every other band, and only Ctrl+E/Enter (Edit) hand keys back to TextView unchanged.
    private bool _payloadEditing;

    public event Action<string>? StatusChanged;

    public PublishTab(NatsConnection connection, ShortcutTracker shortcutTracker)
    {
        CanFocus = true;
        _connection = connection;
        _shortcutTracker = shortcutTracker;

        var subjectBand = new View { X = 0, Y = 0, Width = Dim.Fill(), Height = 4, CanFocus = true };
        var subjectLabel = new Label { Text = "Subject", X = 0, Y = 0 };
        _subjectField = new TextField();
        _subjectField.ValueChanged += (_, _) => UpdateValidity();
        // TextField/TextView both paint their fill via VisualRole.Editable, not Normal/Focus (see
        // openspec/changes/add-edit-frame/design.md) - reading it here mirrors whatever the field
        // actually renders, rather than a color guessed independently of it.
        var subjectBackground = _subjectField.GetAttributeForRole(VisualRole.Editable).Background;
        _subjectFrame = new EditFrame(_subjectField) {
            X = 0, Y = 1, Width = Dim.Fill(), Height = 3,
            InnerBackgroundNormal = subjectBackground, InnerBackgroundFocused = subjectBackground,
        };
        subjectBand.Add(subjectLabel, _subjectFrame);

        var headersBand = new View { X = 0, Y = Pos.Bottom(subjectBand), Width = Dim.Fill(), Height = 5, CanFocus = true };
        var headersLabel = new Label { Text = "Headers", X = 0, Y = 0 };
        var headerEditor = new HeaderEditorView(_headers) { Background = subjectBackground };
        var headerFrame = new EditFrame(headerEditor) {
            X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(),
            InnerBackgroundNormal = subjectBackground, InnerBackgroundFocused = subjectBackground,
        };
        headersBand.Add(headersLabel, headerFrame);

        var payloadBand = new View { X = 0, Y = Pos.Bottom(headersBand), Width = Dim.Fill(), Height = Dim.Fill(1), CanFocus = true };
        var payloadLabel = new Label { Text = "Payload", X = 0, Y = 0 };
#pragma warning disable CS0618
        _payloadView = new TextView();
#pragma warning restore CS0618
        _payloadView.KeyDown += OnPayloadKeyDown;
        // Tab/Shift-Tab landing here (from Headers or Send) must always start in Navigate, not
        // carry over a stale Edit state left from a previous visit that didn't go through Esc.
        _payloadView.HasFocusChanged += (_, _) => {
            if (_payloadView.HasFocus && _payloadEditing) ExitPayloadEditMode();
        };
        var payloadBackground = _payloadView.GetAttributeForRole(VisualRole.Editable).Background;
        var payloadFrame = new EditFrame(_payloadView) {
            X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(),
            InnerBackgroundNormal = payloadBackground, InnerBackgroundFocused = payloadBackground,
        };
        payloadBand.Add(payloadLabel, payloadFrame);

        _sendButton = new Button { Text = "_Send", X = Pos.AnchorEnd(10), Y = Pos.AnchorEnd(1) };
        _sendButton.Accepting += (_, _) => Send();

        Add(subjectBand, headersBand, payloadBand, _sendButton);

        UpdateValidity();
    }

    // Navigate is the default and swallows everything except the keys below - "typing" must not
    // leak into Payload's text without the user deliberately entering Edit first. Up/Down and
    // Tab/Shift-Tab all mirror the exact same focus-advance Tab already performs everywhere else
    // in the tab; TextView's own handling of them (cursor movement, literal tab insertion) only
    // ever runs once Edit is entered, so Edit-mode behavior is unchanged from before this gate.
    private void OnPayloadKeyDown(object? sender, Key key)
    {
        if (_payloadEditing) {
            if (key != Key.Esc) return;
            ExitPayloadEditMode();
            key.Handled = true;
            return;
        }

        if (key == Key.CursorUp || key == Key.Tab.WithShift) {
            key.Handled = true;
            App!.Navigation!.AdvanceFocus(NavigationDirection.Backward, null);
        } else if (key == Key.CursorDown || key == Key.Tab) {
            key.Handled = true;
            App!.Navigation!.AdvanceFocus(NavigationDirection.Forward, null);
        } else if (key == Key.E.WithCtrl || key == Key.Enter) {
            EnterPayloadEditMode();
            key.Handled = true;
        } else {
            key.Handled = true;
        }
    }

    // Shared by the keyboard path (Ctrl+E/Enter/Esc above) and the StatusBar hint's own Action,
    // so clicking the advertised shortcut does exactly what pressing its key would.
    private void EnterPayloadEditMode()
    {
        _payloadEditing = true;
        _shortcutTracker.Refresh();
    }

    private void ExitPayloadEditMode()
    {
        _payloadEditing = false;
        _shortcutTracker.Refresh();
    }

    public IEnumerable<ShortcutHint> Shortcuts =>
        _payloadEditing
            ? [new ShortcutHint(Key.Esc, "Stop Editing", ExitPayloadEditMode)]
            : _payloadView.HasFocus
                ? [new ShortcutHint(Key.E.WithCtrl, "Edit Payload", EnterPayloadEditMode)]
                : [];

    private void UpdateValidity()
    {
        var valid = _subjectField.Text.Trim().Length > 0;
        _sendButton.Enabled = valid;
        // new Scheme(Attribute)'s single-value constructor derives Editable independently and
        // silently drops our background (defaults it to Black) - re-set Editable explicitly so
        // invalid state only changes the foreground, never the background (EditFrame's own
        // background is never touched here either, for the same reason).
        _subjectField.SetScheme(valid ? null : new Scheme(InvalidSubject) { Editable = InvalidSubject });
    }

    private void Send()
    {
        var subject = _subjectField.Text.Trim();
        if (subject.Length == 0) return;

        NatsHeaders? headers = null;
        if (_headers.Count > 0) {
            headers = new NatsHeaders();
            foreach (var pair in _headers) headers.Add(pair.Key, pair.Value);
        }

        _ = PublishAsync(subject, headers, _payloadView.Text);
    }

    private async Task PublishAsync(string subject, NatsHeaders? headers, string payload)
    {
        try {
            await _connection.PublishAsync(subject, payload, headers: headers);
            App?.Invoke(() => StatusChanged?.Invoke($"Published to {subject}"));
        } catch (Exception ex) {
            App?.Invoke(() => StatusChanged?.Invoke($"Publish failed: {ex.Message}"));
        }
    }
}
