using System.Reactive.Linq;

namespace lazynats.Core;

// Small, deliberately generic Rx helpers - not streams-specific - so any future poll/async-to-UI
// need (e.g. the live feed pipeline) can adopt them without redesign. See
// openspec/changes/add-consumer-drilldown/design.md decisions 3-4 for the rationale.
//
// UI-framework-agnostic half of the original AsyncExtensions - ObserveOnApp (Terminal.Gui's
// IApplication dispatch) stays in the app project's lazynats.Core namespace since it can't be
// exercised without Terminal.Gui running.
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
