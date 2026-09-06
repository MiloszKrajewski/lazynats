using System.Collections.ObjectModel;
using System.Text.Json;
using lazynats.Components;
using lazynats.Core;
using lazynats.Payloads;
using NATS.Client.KeyValueStore;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.Templates;

// One level, list+details side by side - same shape as StreamsTab/ValuesTab/ObjectsTab's own
// stream/bucket-level screen (EditFrame-wrapped list on the left, a read-only details pane on the
// right), just without a second, drilled-into level: Templates has nowhere to descend to. Owns the
// actual lazynats-templates KV reads/writes/deletes itself (TemplateListView only owns list/
// search/selection mechanics, dispatching Create/Edit/Delete/Refresh requests up here) - mirrors
// how StreamsTab/ValuesTab own their own JetStream/KV calls rather than pushing them into the list
// view. See design.md's "List-editor hosting" revision.
internal sealed class TemplatesTab: View, IShortcutSource
{
    private const string BucketName = "lazynats-templates";
    private static readonly NatsKVConfig BucketConfig = new(BucketName) { Storage = NatsKVStorageType.File };

    private readonly INatsKVContext _kv;

    private readonly ObservableCollection<Template> _items = [];
    private readonly TemplateListView _listView;
    private readonly FilterBox _filterBox;
    private readonly EditFrame _listFrame;
    private readonly TemplateDetails _details;

    // Import/Export act on the whole bucket, not the highlighted template, so - unlike Create/
    // Edit/Delete/Refresh (dispatched through _listView.TabOperations) - they're a small fixed
    // table owned directly by this tab, appended to _listView.TabOperations rather than replacing
    // it. See design.md's "Keyboard: Ctrl+O / Ctrl+X" decision (bare O/X since
    // openspec/changes/single-key-shortcuts). Import uses O ("Open"), not I as originally
    // proposed for the Ctrl-based scheme - Ctrl+I is the ASCII Tab character (Ctrl+<letter> is
    // that letter's code with the top 3 bits masked off; 'I' masks to 0x09, the same byte Tab
    // itself sends), so no terminal could ever have delivered it as a distinct keystroke;
    // discovered and revised during that earlier implementation.
    private readonly ShortcutHint[] _extraOperations;

    // Guards the one-time initial fetch - the list is otherwise load-once + Ctrl+R only, per
    // nats-templates' "Manual Template List Refresh" requirement.
    private bool _loaded;

    public event Action<string>? StatusChanged;

    public TemplatesTab(INatsKVContext kv)
    {
        CanFocus = true;
        _kv = kv;

        var listLabel = new Label { Text = "Templates", X = 0, Y = 0 };
        _filterBox = new FilterBox { X = 0, Y = 1, Width = Dim.Percent(40) };
        _listView = new TemplateListView(_items) { Background = Theme.EditableBackground };
        _listView.AttachFilterBox(_filterBox);
        _listFrame = new EditFrame(_listView) {
            X = 0, Y = Pos.Bottom(_filterBox), Width = Dim.Percent(40), Height = Dim.Fill(),
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        _listView.RefreshRequested += () => _ = RefreshListAsync();
        _listView.HighlightChanged += OnHighlightChanged;
        _listView.CreateRequested += () => OpenCreateDialog(null);
        _listView.DeleteRequested += () => _ = TryDeleteAsync();
        _listView.EditRequested += OpenEditDialog;

        var detailsLabel = new Label { Text = "Details", X = Pos.Right(_listFrame) + 1, Y = 0 };
        _details = new TemplateDetails { X = Pos.Right(_listFrame) + 1, Y = 2, Width = Dim.Fill(), Height = Dim.Fill() };

        _extraOperations = [
            new ShortcutHint(Key.X, "Export", Export),
            new ShortcutHint(Key.O, "Import", Import),
        ];

        // Add()-order matches spatial top-down layout so Tab/Shift+Tab cycles in reading order -
        // same convention as StreamsTab/ValuesTab/ObjectsTab.
        Add(listLabel, _filterBox, _listFrame, detailsLabel, _details);
    }

    // No KeyBindings/AddCommand for specific keys here - that would hardcode which keys this tab
    // forwards. Instead this fires once Terminal.Gui has already tried the focused view (and its
    // own ancestors) and found no handler, at which point it's this tab's turn; whatever key
    // _listView's own TabOperations (plus this tab's own _extraOperations - Import/Export) happens
    // to expose is what gets dispatched - see SubscribeTab's identical single-list dispatch
    // (Templates has one list, like Subscribe, not two like Streams/Values/Objects).
    protected override bool OnKeyDownNotHandled(Key key)
    {
        if (_listView.TabOperations.Concat(_extraOperations).FirstOrDefault(h => h.Key == key) is { Action: { } action }) {
            action();
            return true;
        }

        return base.OnKeyDownNotHandled(key);
    }

    public IEnumerable<ShortcutHint> Shortcuts => _listView.TabOperations.Concat(_extraOperations);

    // "Selected tab" in this app is focus-driven (doc/terminal-gui-howto.md) - so this fires
    // exactly on tab entry/exit, which is what gates the one-time initial load per
    // nats-templates' "Template List" requirement (fetched when the tab is first activated, never
    // on a timer).
    protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView)
    {
        base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);

        if (newHasFocus && !_loaded) {
            _loaded = true;
            _ = RefreshListAsync();
        }

        _details.SetActive(newHasFocus);
    }

    private void OnHighlightChanged(Template? template)
    {
        _details.SetTarget(template);
        // The highlighted list item already carries the template's full value, so this shows
        // instantly rather than waiting on any fetch - see TemplateDetails' own comment.
        _details.Show(template);
    }

    private void OpenCreateDialog(Template? seed)
    {
        var dialog = new TemplateDialog(seed);
        App!.Run(dialog);
        if (dialog.Result is { } template) _ = WriteAsync(template, isEdit: false);
    }

    private void OpenEditDialog()
    {
        if (_listView.SelectedTemplate is { } template) OpenEditDialog(template);
    }

    private void OpenEditDialog(Template original)
    {
        var dialog = new TemplateDialog(original, isEdit: true);
        App!.Run(dialog);
        if (dialog.Result is { } template) _ = WriteAsync(template, isEdit: true);
    }

    private async Task WriteAsync(Template template, bool isEdit)
    {
        try {
            var store = await _kv.CreateStoreAsync(BucketConfig);
            var document = new TemplateDocument(
                template.Subject, new Dictionary<string, string>(template.Headers), template.PayloadType,
                TemplatePayloadCodec.ToNode(template.PayloadType, template.Payload));
            var bytes = JsonSerializer.SerializeToUtf8Bytes(document, TemplateJsonContext.Default.TemplateDocument);
            await store.PutAsync(template.Name, bytes);

            _ = RefreshListAsync(template.Name);
        } catch (Exception ex) {
            App?.Invoke(() => {
                var title = isEdit ? " Edit Template Failed " : " Create Template Failed ";
                MessageBox.ErrorQuery(App!, title, ex.Message.Pad(), "_Ok");
                if (isEdit) OpenEditDialog(template); else OpenCreateDialog(template);
            });
        }
    }

    private async Task TryDeleteAsync()
    {
        if (_listView.SelectedTemplate is not { } template) return;
        var name = template.Name;

        var choice = MessageBox.Query(
            App!, " Delete Template ",
            $"Delete template '{name}'? This cannot be undone.".Pad(),
            "_Delete", "_Cancel");
        if (choice != 0) return;

        var neighborName = _listView.NeighborIdentity(name);

        try {
            var store = await _kv.GetStoreAsync(BucketName);
            await store.DeleteAsync(name);
            _ = RefreshListAsync(neighborName);
        } catch (Exception ex) {
            App?.Invoke(() => MessageBox.ErrorQuery(App!, " Delete Template Failed ", ex.Message.Pad(), "_Ok"));
        }
    }

    // Bare X. Whole-bucket, not scoped to the highlighted template - no confirmation prompt for an
    // overwritten destination (SaveDialog is expected to prompt for that itself). See design.md's
    // "Export" decision.
    private void Export()
    {
        var picker = new SaveDialog { Title = " Export Templates ", AllowedTypes = [new AllowedType("JSON", ".json")] };
        App!.Run(picker);
        if (picker.Canceled) return;

        _ = ExportAsync(picker.Path);
    }

    private async Task ExportAsync(string path)
    {
        try {
            var templates = await FetchTemplatesAsync();
            var documents = templates.ToDictionary(
                template => template.Name,
                template => new TemplateDocument(
                    template.Subject, new Dictionary<string, string>(template.Headers), template.PayloadType,
                    TemplatePayloadCodec.ToNode(template.PayloadType, template.Payload)));
            var bytes = JsonSerializer.SerializeToUtf8Bytes(documents, typeof(Dictionary<string, TemplateDocument>), TemplateJsonContext.Default);
            await File.WriteAllBytesAsync(path, bytes);
        } catch (Exception ex) {
            App?.Invoke(() => MessageBox.ErrorQuery(App!, " Export Templates Failed ", ex.Message.Pad(), "_Ok"));
        }
    }

    // Bare O. Validate-then-write: every entry in the file is checked before any write happens, so
    // a failing entry leaves the bucket untouched - see design.md's "Import" decision.
    private void Import()
    {
        var picker = new OpenDialog {
            Title = " Import Templates ", OpenMode = OpenMode.File, MustExist = true,
            AllowedTypes = [new AllowedType("JSON", ".json")],
        };
        App!.Run(picker);
        if (picker.Canceled) return;

        _ = ImportAsync(picker.Path);
    }

    private async Task ImportAsync(string path)
    {
        Dictionary<string, TemplateDocument>? documents;
        try {
            var bytes = await File.ReadAllBytesAsync(path);
            documents = JsonSerializer.Deserialize(bytes, typeof(Dictionary<string, TemplateDocument>), TemplateJsonContext.Default)
                as Dictionary<string, TemplateDocument>;
            if (documents is null) throw new JsonException("The file does not contain a template object.");
        } catch (Exception ex) {
            App?.Invoke(() => MessageBox.ErrorQuery(App!, " Import Templates Failed ", ex.Message.Pad(), "_Ok"));
            return;
        }

        foreach (var (name, document) in documents) {
            var failure = ValidateImportEntry(name, document);
            if (failure is null) continue;

            App?.Invoke(() => MessageBox.ErrorQuery(App!, " Import Templates Failed ", failure.Pad(), "_Ok"));
            return;
        }

        try {
            var store = await _kv.CreateStoreAsync(BucketConfig);
            foreach (var (name, document) in documents) {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(document, TemplateJsonContext.Default.TemplateDocument);
                await store.PutAsync(name, bytes);
            }

            _ = RefreshListAsync();
        } catch (Exception ex) {
            App?.Invoke(() => MessageBox.ErrorQuery(App!, " Import Templates Failed ", ex.Message.Pad(), "_Ok"));
        }
    }

    // Same three checks "Create Template Field Validation" enforces per entry (non-empty name,
    // non-empty Subject, payload valid for its Payload Type), applied here to a whole imported
    // entry before any write happens.
    private static string? ValidateImportEntry(string name, TemplateDocument document)
    {
        var label = name.Trim().Length == 0 ? "(unnamed entry)" : name;

        if (name.Trim().Length == 0) return $"Entry '{label}' has an empty name.";
        if (document.Subject.Trim().Length == 0) return $"Entry '{label}' has an empty Subject.";

        var payload = TemplatePayloadCodec.ToText(document.PayloadType, document.Payload);
        if (!PayloadValidation.IsValid(document.PayloadType, payload)) return $"Entry '{label}' has an invalid Payload.";

        return null;
    }

    // Wrapped in one try/catch that yields an empty result on any failure - including the
    // overwhelmingly common case of the bucket not existing yet, before the first template is
    // ever saved. Deliberately does not distinguish "bucket missing" from any other failure (e.g.
    // a transient connectivity issue) - see design.md's "Missing bucket on read renders as empty"
    // decision and its accepted risk.
    private async Task<IReadOnlyList<Template>> FetchTemplatesAsync()
    {
        try {
            var store = await _kv.GetStoreAsync(BucketName);
            var templates = new List<Template>();
            await foreach (var key in store.GetKeysAsync()) {
                var entry = await store.TryGetEntryAsync<byte[]>(key);
                if (!entry.Success) continue;

                var document = JsonSerializer.Deserialize(entry.Value.Value ?? [], TemplateJsonContext.Default.TemplateDocument);
                if (document is null) continue;

                var payload = TemplatePayloadCodec.ToText(document.PayloadType, document.Payload);
                templates.Add(new Template(key, document.Subject, document.Headers, document.PayloadType, payload));
            }

            return templates;
        } catch {
            return [];
        }
    }

    // `selectName` highlights a specific template after the refresh (used right after a
    // create/edit/delete); omitted for a plain Ctrl+R, which instead preserves whatever was
    // already highlighted, if still present - handled by TemplateListView.ReplaceItems itself.
    private async Task RefreshListAsync(string? selectName = null)
    {
        try {
            var templates = await FetchTemplatesAsync();
            App?.Invoke(() => _listView.ReplaceItems(templates, selectName));
        } catch (Exception ex) {
            App?.Invoke(() => StatusChanged?.Invoke($"Templates: {ex.Message}"));
        }
    }
}
