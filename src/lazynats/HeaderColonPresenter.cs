using lazynats.Components;

namespace lazynats;

// Row/dialog-seed formatting for HeaderEditorView - "key: value" - used by PresenterListDataSource
// for list rows and by HeaderEditorView to seed HeaderDialog's text on edit.
internal sealed class HeaderColonPresenter: IValuePresenter<HeaderPair>
{
    public string Format(HeaderPair value) => $"{value.Key}: {value.Value}";
}
