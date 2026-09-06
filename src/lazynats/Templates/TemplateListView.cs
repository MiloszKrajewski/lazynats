using System.Collections.ObjectModel;
using lazynats.Components;

namespace lazynats.Templates;

// Flat (no descend/ascend - Templates has no second level), built on the same shared
// list-with-search/details-pane shape StreamListView/BucketListView/KeyListView already use,
// rather than the tab-scoped ListEditorView<T> hosting this originally shipped with - see
// design.md's "List-editor hosting" revision. TemplatesTab owns the actual KV read/write/delete
// calls (mirroring StreamsTab/ValuesTab), reacting to CreateRequested/EditRequested/
// DeleteRequested/RefreshRequested; this view only owns list/search/selection mechanics.
internal sealed class TemplateListView: DrillableListView<Template>
{
    private static readonly TemplateNamePresenter PresenterInstance = new();

    public TemplateListView(ObservableCollection<Template> items): base(items)
    {
        EnableCreate();
        EnableDelete();
        EnableEdit();
        EnableFilter();
    }

    protected override IValuePresenter<Template> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No templates — Ctrl+N to add one";
    protected override string GetIdentity(Template item) => item.Name;
    protected override string FilterDialogTitle => "Filter Templates";

    public Template? SelectedTemplate => SelectedItem;
}
