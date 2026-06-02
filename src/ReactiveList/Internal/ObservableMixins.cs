// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;
using System.Threading;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace CP.Reactive.Internal;

/// <summary>
/// Internal observable helpers used to keep ReactiveList source compact while depending on ReactiveUI.Primitives.
/// </summary>
internal static class ObservableMixins
{
    public static IObservable<TResult> Select<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, TResult> selector) =>
        source.Map(selector);

    public static IObservable<TSource> Where<TSource>(
        this IObservable<TSource> source,
        Func<TSource, bool> predicate) =>
        source.Keep(predicate);

    public static IObservable<TResult> SelectMany<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, IEnumerable<TResult>> selector)
    {
        ThrowHelper.ThrowIfNull(source);
        ThrowHelper.ThrowIfNull(selector);

        return Signal.Create<TResult>(observer =>
            source.Subscribe(
                value =>
                {
                    foreach (var item in selector(value))
                    {
                        observer.OnNext(item);
                    }
                },
                observer.OnError,
                observer.OnCompleted));
    }

    public static IObservable<TResult> SelectMany<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, IObservable<TResult>> selector) =>
        source.FlatMap(selector);

    public static IObservable<TResult> SelectMany<TSource, TCollection, TResult>(
        this IObservable<TSource> source,
        Func<TSource, IObservable<TCollection>> collectionSelector,
        Func<TSource, TCollection, TResult> resultSelector) =>
        source.FlatMap(collectionSelector, resultSelector);

    public static IObservable<TSource> StartWith<TSource>(
        this IObservable<TSource> source,
        TSource value) =>
        source.Lead(value);

    public static IObservable<TSource> Concat<TSource>(
        this IObservable<TSource> first,
        IObservable<TSource> second) =>
        first.Chain(second);

    public static IObservable<TSource> Merge<TSource>(
        this IEnumerable<IObservable<TSource>> sources) =>
        Signal.Blend([.. sources]);

    public static IObservable<TSource> Switch<TSource>(
        this IObservable<IObservable<TSource>> sources) =>
        sources.SwitchTo();

    public static IObservable<TSource> Do<TSource>(
        this IObservable<TSource> source,
        Action<TSource> onNext) =>
        source.Tap(onNext);

    public static IEnumerable<TSource> ToEnumerable<TSource>(this IObservable<TSource> source)
    {
        ThrowHelper.ThrowIfNull(source);

        var values = new List<TSource>();
        Exception? error = null;
        using var completed = new ManualResetEventSlim();
        using var subscription = source.Subscribe(
            values.Add,
            ex =>
            {
                error = ex;
                completed.Set();
            },
            completed.Set);

        completed.Wait();

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        return values;
    }

    public static IObservable<IList<TSource>> Buffer<TSource>(
        this IObservable<TSource> source,
        TimeSpan timeSpan) =>
        source.Buffer(timeSpan, Sequencer.Default);

    public static IObservable<IList<TSource>> Buffer<TSource>(
        this IObservable<TSource> source,
        TimeSpan timeSpan,
        ISequencer sequencer)
    {
        ThrowHelper.ThrowIfNull(source);
        ThrowHelper.ThrowIfNull(sequencer);

        if (timeSpan <= TimeSpan.Zero)
        {
            return source.Select(static value => (IList<TSource>)new[] { value });
        }

        return Signal.Create<IList<TSource>>(observer =>
        {
            var gate = new object();
            var values = new List<TSource>();
            var disposables = new MultipleDisposable();
            var flushScheduled = false;
            var stopped = false;

            void Flush()
            {
                TSource[] batch;
                lock (gate)
                {
                    if (values.Count == 0 || stopped)
                    {
                        flushScheduled = false;
                        return;
                    }

                    batch = [.. values];
                    values.Clear();
                    flushScheduled = false;
                }

                observer.OnNext(batch);
            }

            var subscription = source.Subscribe(
                value =>
                {
                    lock (gate)
                    {
                        if (stopped)
                        {
                            return;
                        }

                        values.Add(value);
                        if (flushScheduled)
                        {
                            return;
                        }

                        flushScheduled = true;
                    }

                    disposables.Add(Sequencer.Schedule(sequencer, timeSpan, Flush));
                },
                error =>
                {
                    lock (gate)
                    {
                        stopped = true;
                    }

                    observer.OnError(error);
                },
                () =>
                {
                    TSource[]? batch = null;
                    lock (gate)
                    {
                        stopped = true;
                        if (values.Count > 0)
                        {
                            batch = [.. values];
                            values.Clear();
                        }
                    }

                    if (batch is { Length: > 0 })
                    {
                        observer.OnNext(batch);
                    }

                    observer.OnCompleted();
                });

            disposables.Add(subscription);
            return disposables;
        });
    }

    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime) =>
        source.Throttle(dueTime, Sequencer.Default);

    public static IObservable<TSource> Throttle<TSource>(
        this IObservable<TSource> source,
        TimeSpan dueTime,
        ISequencer sequencer)
    {
        ThrowHelper.ThrowIfNull(source);
        ThrowHelper.ThrowIfNull(sequencer);

        if (dueTime <= TimeSpan.Zero)
        {
            return source;
        }

        return Signal.Create<TSource>(observer =>
        {
            var gate = new object();
            var disposables = new MultipleDisposable();
            var version = 0L;
            var hasValue = false;
            var latest = default(TSource);
            var stopped = false;

            var subscription = source.Subscribe(
                value =>
                {
                    long currentVersion;
                    lock (gate)
                    {
                        if (stopped)
                        {
                            return;
                        }

                        latest = value;
                        hasValue = true;
                        currentVersion = ++version;
                    }

                    disposables.Add(Sequencer.Schedule(sequencer, dueTime, () =>
                    {
                        TSource? valueToEmit;
                        lock (gate)
                        {
                            if (stopped || !hasValue || currentVersion != version)
                            {
                                return;
                            }

                            valueToEmit = latest;
                            hasValue = false;
                        }

                        observer.OnNext(valueToEmit!);
                    }));
                },
                error =>
                {
                    lock (gate)
                    {
                        stopped = true;
                    }

                    observer.OnError(error);
                },
                () =>
                {
                    TSource? valueToEmit = default;
                    var emit = false;
                    lock (gate)
                    {
                        stopped = true;
                        if (hasValue)
                        {
                            valueToEmit = latest;
                            emit = true;
                            hasValue = false;
                        }
                    }

                    if (emit)
                    {
                        observer.OnNext(valueToEmit!);
                    }

                    observer.OnCompleted();
                });

            disposables.Add(subscription);
            return disposables;
        });
    }
}
