using Terminal.Gui.App;

namespace lazynats.Core;

public static class ApplicationExtensions
{
    public static void Invoke<T>(this IApplication app, T value, Action<T> action) =>
        app.Invoke(() => action(value));
}
