// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace CP.Reactive.Internal;

/// <summary>Internal observable helpers used to keep ReactiveList source compact while depending on ReactiveUI.Primitives.</summary>
internal static class ObservableMixins
{
    /// <summary>Extensions for working with multiple observable sources.</summary>
    /// <typeparam name="TSource">The type of elements in each source sequence.</typeparam>
    /// <param name="sources">The sequence of observables to blend.</param>
    extension<TSource>(IEnumerable<IObservable<TSource>> sources)
    {
        /// <summary>Blends values from multiple sources as they arrive.</summary>
        /// <returns>An observable that emits values from all source sequences.</returns>
        public IObservable<TSource> Blend() => Signal.Blend([.. sources]);
    }

    /// <summary>Extensions for working with observable sequences.</summary>
    /// <typeparam name="TSource">The type of elements in the source sequence.</typeparam>
    /// <param name="source">The source observable sequence.</param>
    extension<TSource>(IObservable<TSource> source)
    {
        /// <summary>Projects each source value into zero or more output elements.</summary>
        /// <typeparam name="TResult">The output value type.</typeparam>
        /// <param name="selector">A function that returns an enumerable of output elements for each source value.</param>
        /// <returns>An observable sequence of flattened results.</returns>
        public IObservable<TResult> FlatMap<TResult>(Func<TSource, IEnumerable<TResult>> selector)
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

        /// <summary>Converts an observable sequence to a materialized list.</summary>
        /// <returns>A list containing all values from the source sequence.</returns>
        public IEnumerable<TSource> ToEnumerable()
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

        /// <summary>Buffers values from the source for a given interval using the default sequencer.</summary>
        /// <param name="timeSpan">The buffering period.</param>
        /// <returns>An observable producing buffered lists.</returns>
        public IObservable<IList<TSource>> Buffer(TimeSpan timeSpan) =>
            source.Buffer(timeSpan, Sequencer.Default);

        /// <summary>Buffers values from the source for a given interval using a custom sequencer.</summary>
        /// <param name="timeSpan">The buffering period.</param>
        /// <param name="sequencer">The scheduler that drives buffer flushes.</param>
        /// <returns>An observable producing buffered lists.</returns>
        public IObservable<IList<TSource>> Buffer(
            TimeSpan timeSpan,
            ISequencer sequencer)
        {
            ThrowHelper.ThrowIfNull(source);
            ThrowHelper.ThrowIfNull(sequencer);

            if (timeSpan <= TimeSpan.Zero)
            {
                return source.Map(static value => (IList<TSource>)[value]);
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

                        disposables.Add(sequencer.Schedule(timeSpan, Flush));
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

        /// <summary>Throttles notifications, using the default sequencer.</summary>
        /// <param name="dueTime">The delay before emitting the latest value.</param>
        /// <returns>An observable that emits throttled values.</returns>
        public IObservable<TSource> Throttle(TimeSpan dueTime) =>
            source.Throttle(dueTime, Sequencer.Default);

        /// <summary>Throttles notifications, using a custom sequencer.</summary>
        /// <param name="dueTime">The delay before emitting the latest value.</param>
        /// <param name="sequencer">The scheduler used to drive the timer.</param>
        /// <returns>An observable that emits throttled values.</returns>
        public IObservable<TSource> Throttle(
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

                        disposables.Add(sequencer.Schedule(dueTime, () =>
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
}
