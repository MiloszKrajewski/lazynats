using lazynats.Components;

namespace lazynats.KVStore;

internal sealed class KeyNamePresenter: IValuePresenter<string>
{
    public string Format(string value) => value;
}
