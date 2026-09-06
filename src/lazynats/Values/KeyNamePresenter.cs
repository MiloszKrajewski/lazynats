using lazynats.Components;

namespace lazynats.Values;

internal sealed class KeyNamePresenter: IValuePresenter<string>
{
    public string Format(string value) => value;
}
