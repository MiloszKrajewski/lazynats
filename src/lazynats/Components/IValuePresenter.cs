namespace lazynats.Components;

internal interface IValuePresenter<in T>
{
    string Format(T value);
}
