using lazynats.Components;

namespace lazynats;

// Proves the IValuePresenter<T> abstraction against the existing HeaderPair type from
// HeaderListDataSource.cs. Not wired into PublishView in this change - see design.md.
internal sealed class HeaderColonPresenter: IValuePresenter<HeaderPair>
{
    public string Format(HeaderPair value) => $"{value.Key}: {value.Value}";

    public bool TryParse(string raw, out HeaderPair value, out string? error)
    {
        var separator = raw.IndexOf(':');
        if (separator < 0) {
            value = default;
            error = "expected \"key: value\"";
            return false;
        }

        var key = raw[..separator].Trim();
        var headerValue = raw[(separator + 1)..].Trim();
        if (key.Length == 0) {
            value = default;
            error = "key must not be empty";
            return false;
        }

        value = new HeaderPair(key, headerValue);
        error = null;
        return true;
    }
}
