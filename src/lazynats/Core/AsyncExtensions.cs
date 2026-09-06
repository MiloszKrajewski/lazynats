using System.Reactive.Linq;
using Terminal.Gui.App;

namespace lazynats.Core;

// Small, deliberately generic Rx helpers - not streams-specific - so any future poll/async-to-UI
// need (e.g. the live feed pipeline) can adopt them without redesign. See
// openspec/changes/add-consumer-drilldown/design.md decisions 3-4 for the rationale.
internal static class AsyncExtensions
{
    // Select + Concat, not SelectMany/Merge: at most one projected async operation runs at a
    // time, and results are delivered in the order their source elements arrived. A slow
    // operation makes later ticks queue rather than overlap - this is what lets pollers built on
    // this drop the "is this response still the one I'm waiting for" staleness guard they'd
    // otherwise need with concurrent (SelectMany) semantics.
    public static IObservable<TResult> SelectAsync<TSource, TResult>(
        this IObservable<TSource> source, Func<TSource, Task<TResult>> selector) =>
        source.Select(item => Observable.FromAsync(() => selector(item))).Concat();
    
    // Terminal.Gui has no SynchronizationContext to hook a standard ObserveOn into - IApplication
    // exposes Invoke(Action) instead, so this wraps the observer to dispatch every notification
    // (value, error, completion) through it before it reaches downstream subscribers.
    public static IObservable<T> ObserveOnApp<T>(this IObservable<T> source, IApplication app) =>
        Observable.Create<T>(observer => source.Subscribe(
            ov => app.Invoke(ov, observer.OnNext),
            ex => app.Invoke(ex, observer.OnError),
            () => app.Invoke(observer.OnCompleted)));

    // System.Reactive 6.1.0's only ToObservable overload targets IEnumerable<T>, not
    // IAsyncEnumerable<T> - verified directly, it fails to compile against an async source. This
    // hand-rolled bridge avoids pulling in System.Linq.Async (Ix.NET) just for this one operator,
    // per openspec/changes/add-kv-filter-language/design.md Decision 3.
    public static IObservable<T> ToObservable<T>(this IAsyncEnumerable<T> source) =>
        Observable.Create<T>(async (observer, cancellationToken) => {
            await foreach (var item in source.WithCancellation(cancellationToken))
                observer.OnNext(item);
            observer.OnCompleted();
        });
}