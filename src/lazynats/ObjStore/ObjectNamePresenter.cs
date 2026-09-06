using lazynats.Components;

namespace lazynats.ObjStore;

internal sealed class ObjectNamePresenter: IValuePresenter<string>
{
    public string Format(string value) => value;
}
