using System.Collections.ObjectModel;
using NATS.Client.Core;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats;

internal sealed class PublishView: View
{
    private static readonly Attribute InvalidSubject = new(ColorName16.Red, ColorName16.DarkGray);

    private readonly NatsConnection _connection;
    private readonly ObservableCollection<HeaderPair> _headers = [];
    private readonly HeaderListDataSource _headerSource;
    private readonly TextField _subjectField;
    private readonly TextField _headerKeyField;
    private readonly TextField _headerValueField;
    private readonly ListView _headerListView;
    private readonly TextView _payloadView;
    private readonly Button _sendButton;
    private int? _editingIndex;

    public event Action<string>? StatusChanged;

    public PublishView(NatsConnection connection)
    {
        CanFocus = true;
        _connection = connection;

        var subjectBand = new View { X = 0, Y = 0, Width = Dim.Fill(), Height = 2, CanFocus = true };
        subjectBand.SetScheme(new Scheme(new Attribute(ColorName16.White, ColorName16.DarkGray)));
        var subjectLabel = new Label { Text = "Subject", X = 0, Y = 0 };
        _subjectField = new TextField { X = 0, Y = 1, Width = Dim.Fill() };
        _subjectField.ValueChanged += (_, _) => UpdateValidity();
        subjectBand.Add(subjectLabel, _subjectField);

        var headersBand = new View { X = 0, Y = Pos.Bottom(subjectBand), Width = Dim.Fill(), Height = 8, CanFocus = true };
        headersBand.SetScheme(new Scheme(new Attribute(ColorName16.White, ColorName16.Gray)));
        var headersLabel = new Label { Text = "Headers", X = 0, Y = 0 };
        _headerKeyField = new TextField { X = 0, Y = 1, Width = Dim.Percent(30) };
        _headerValueField = new TextField { X = Pos.Right(_headerKeyField) + 1, Y = 1, Width = Dim.Fill() };
        _headerValueField.Accepted += (_, _) => CommitHeaderInput();

        _headerSource = new HeaderListDataSource(_headers);
        _headerListView = new ListView { X = 0, Y = 2, Width = Dim.Fill(), Height = Dim.Fill() };
        _headerListView.KeystrokeNavigator = null;
        _headerListView.Source = _headerSource;
        headersBand.Add(headersLabel, _headerKeyField, _headerValueField, _headerListView);

        var payloadBand = new View { X = 0, Y = Pos.Bottom(headersBand), Width = Dim.Fill(), Height = Dim.Fill(1), CanFocus = true };
        payloadBand.SetScheme(new Scheme(new Attribute(ColorName16.White, ColorName16.DarkGray)));
        var payloadLabel = new Label { Text = "Payload", X = 0, Y = 0 };
        _payloadView = new TextView { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill() };
        payloadBand.Add(payloadLabel, _payloadView);

        _sendButton = new Button { Text = "_Send", X = Pos.AnchorEnd(10), Y = Pos.AnchorEnd(1) };
        _sendButton.Accepting += (_, _) => Send();

        Add(subjectBand, headersBand, payloadBand, _sendButton);

        // Bound here (on the whole tab), not on the individual header fields/list, so
        // Ctrl+N/E/D work no matter which of those three currently has focus - mirrors
        // how SubscriptionsView binds Key.Delete on itself rather than on its ListView.
        AddCommand(Command.New, () => { ClearHeaderInput(); return true; });
        AddCommand(Command.Edit, () => { LoadSelectedForEditing(); return true; });
        AddCommand(Command.DeleteAll, () => { RemoveSelectedHeader(); return true; });
        KeyBindings.Add(Key.N.WithCtrl, Command.New);
        KeyBindings.Add(Key.E.WithCtrl, Command.Edit);
        KeyBindings.Add(Key.D.WithCtrl, Command.DeleteAll);

        UpdateValidity();
    }

    private void ClearHeaderInput()
    {
        _headerKeyField.Text = string.Empty;
        _headerValueField.Text = string.Empty;
        _editingIndex = null;
    }

    private void LoadSelectedForEditing()
    {
        if (_headerListView.SelectedItem is not { } index || index < 0 || index >= _headers.Count) return;
        var pair = _headers[index];
        _headerKeyField.Text = pair.Key;
        _headerValueField.Text = pair.Value;
        _editingIndex = index;
        _headerKeyField.SetFocus();
    }

    private void CommitHeaderInput()
    {
        var key = _headerKeyField.Text.Trim();
        var value = _headerValueField.Text.Trim();
        if (key.Length == 0) return;

        var pair = new HeaderPair(key, value);
        if (_editingIndex is { } index && index < _headers.Count) _headers[index] = pair;
        else _headers.Add(pair);

        ClearHeaderInput();
    }

    private void RemoveSelectedHeader()
    {
        if (_headerListView.SelectedItem is not { } index || index < 0 || index >= _headers.Count) return;
        _headers.RemoveAt(index);
        if (_editingIndex == index) ClearHeaderInput();
    }

    private void UpdateValidity()
    {
        var valid = _subjectField.Text.Trim().Length > 0;
        _sendButton.Enabled = valid;
        _subjectField.SetScheme(valid ? null : new Scheme(InvalidSubject));
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

    protected override void Dispose(bool disposing)
    {
        if (disposing) _headerSource.Dispose();
        base.Dispose(disposing);
    }
}
