using lazynats.Components;

namespace lazynats.Objects;

internal sealed class ObjectNamePresenter: IValuePresenter<string>
{
    public string Format(string value) => value;
}
