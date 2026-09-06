using lazynats.Components;

namespace lazynats.Kv;

internal sealed class KeyNamePresenter: IValuePresenter<string>
{
    public string Format(string value) => value;
}
