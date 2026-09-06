using System.Collections.ObjectModel;
using lazynats.Components;

namespace lazynats.Publish;

internal readonly record struct HeaderPair(string Key, string Value);

// PublishDialog's header list, edited via the shared ListEditorView<T> pattern (mirroring
// SubscriptionsView) instead of the always-visible key/value input row it used to be. Add/Replace/
// Delete stay the base class defaults - unlike subscriptions, headers have no external registry to
// keep in sync with, PublishDialog only reads the collection at Send time.
internal sealed class HeaderEditorView: ListEditorView<HeaderPair>
{
    private static readonly HeaderColonPresenter Presenter = new();

    public HeaderEditorView(ObservableCollection<HeaderPair> items): base(items, Presenter) { }

    protected override string EmptyHint => "No headers — Ctrl+N to add one";

    protected override bool TryCreate(out HeaderPair result) =>
        TryEditHeader("New Header", string.Empty, out result);

    protected override bool TryEdit(HeaderPair original, out HeaderPair result) =>
        TryEditHeader("Edit Header", Presenter.Format(original), out result);

    private bool TryEditHeader(string title, string initialText, out HeaderPair result)
    {
        var dialog = new HeaderDialog(title, initialText);
        App!.Run(dialog);

        if (dialog.Result is { } text) {
            result = ParseHeader(text);
            return true;
        }

        result = default;
        return false;
    }

    // No colon means the whole text becomes the key with an empty value - the user's
    // responsibility, not validated, per the agreed "type it like curl -H" compromise.
    private static HeaderPair ParseHeader(string text)
    {
        var separator = text.IndexOf(':');
        if (separator < 0) return new HeaderPair(text.Trim(), string.Empty);

        var key = text[..separator].Trim();
        var value = text[(separator + 1)..].Trim();
        return new HeaderPair(key, value);
    }
}
