// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;
#else
namespace CP.Primitives.Internal;
#endif
/// <summary>Internal observable helpers used to keep ReactiveList source compact while depending on ReactiveUI.Primitives.</summary>
internal static class ObservableMixins
{
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
    }
}
