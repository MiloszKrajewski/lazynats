using System.Collections.ObjectModel;
using NATS.Client.Core;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats;

internal sealed class PublishView: View
{
    private static readonly Attribute InvalidSubject = new(ColorName16.Red, ColorName16.DarkGray);

    private readonly NatsConnection _connection;
    private readonly ObservableCollection<HeaderPair> _headers = [];
    private readonly TextField _subjectField;
    // TextView is obsolete in favor of Terminal.Gui.Editor's Editor view (confirmed AOT-clean by
    // AotProbe.EditorProbe), but the payload box is just a quick-and-dirty text/JSON field - none
    // of Editor's multi-caret/folding/highlighting is needed here. Revisit if that changes.
#pragma warning disable CS0618
    private readonly TextView _payloadView;
#pragma warning restore CS0618
    private readonly Button _sendButton;

    public event Action<string>? StatusChanged;

    public PublishView(NatsConnection connection)
    {
        CanFocus = true;
        _connection = connection;

        var subjectBand = new View { X = 0, Y = 0, Width = Dim.Fill(), Height = 2, CanFocus = true };
        var subjectLabel = new Label { Text = "Subject", X = 0, Y = 0 };
        _subjectField = new TextField { X = 0, Y = 1, Width = Dim.Fill() };
        _subjectField.ValueChanged += (_, _) => UpdateValidity();
        subjectBand.Add(subjectLabel, _subjectField);

        var headersBand = new View { X = 0, Y = Pos.Bottom(subjectBand), Width = Dim.Fill(), Height = 8, CanFocus = true };
        var headersLabel = new Label { Text = "Headers", X = 0, Y = 0 };
        var headerEditor = new HeaderEditorView(_headers) { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill() };
        headersBand.Add(headersLabel, headerEditor);

        var payloadBand = new View { X = 0, Y = Pos.Bottom(headersBand), Width = Dim.Fill(), Height = Dim.Fill(1), CanFocus = true };
        var payloadLabel = new Label { Text = "Payload", X = 0, Y = 0 };
#pragma warning disable CS0618
        _payloadView = new TextView { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill() };
#pragma warning restore CS0618
        payloadBand.Add(payloadLabel, _payloadView);

        _sendButton = new Button { Text = "_Send", X = Pos.AnchorEnd(10), Y = Pos.AnchorEnd(1) };
        _sendButton.Accepting += (_, _) => Send();

        Add(subjectBand, headersBand, payloadBand, _sendButton);

        UpdateValidity();
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
}
