namespace lazynats.Components;

internal interface IValuePresenter<T>
{
    string Format(T value);
}
