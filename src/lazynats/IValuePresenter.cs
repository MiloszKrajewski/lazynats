namespace lazynats;

internal interface IValuePresenter<T>
{
    string Format(T value);
    bool TryParse(string raw, out T value, out string? error);
}
