using System.Reactive.Linq;
using Terminal.Gui.App;

namespace lazynats.Core;

// Terminal.Gui-dependent half of AsyncExtensions - see lazynats.Core's AsyncExtensions.cs for the
// pure-Rx SelectAsync/ToObservable helpers this one is deliberately kept apart from.
internal static class AsyncExtensions
{
    // Terminal.Gui has no SynchronizationContext to hook a standard ObserveOn into - IApplication
    // exposes Invoke(Action) instead, so this wraps the observer to dispatch every notification
    // (value, error, completion) through it before it reaches downstream subscribers.
    public static IObservable<T> ObserveOnApp<T>(this IObservable<T> source, IApplication app) =>
        Observable.Create<T>(observer => source.Subscribe(
            ov => app.Invoke(ov, observer.OnNext),
            ex => app.Invoke(ex, observer.OnError),
            () => app.Invoke(observer.OnCompleted)));
}
