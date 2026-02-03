// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Internal;
using CP.Reactive.Views;

namespace CP.Reactive;

/// <summary>
/// Provides extension methods for reactive list operations including filtering, transforming, and observing changes.
/// </summary>
public static class ReactiveListExtensions
{
    /// <summary>
    /// Filters the change stream to only include changes matching the specified predicate.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of change sets.</param>
    /// <param name="predicate">A function to test each change for a condition.</param>
    /// <returns>An observable containing only changes that satisfy the predicate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<ChangeSet<T>> WhereChanges<T>(
        this IObservable<ChangeSet<T>> source,
        Func<Change<T>, bool> predicate)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return source.Select(changeSet =>
        {
            var filtered = new List<Change<T>>();
            for (var i = 0; i < changeSet.Count; i++)
            {
                var change = changeSet[i];
                if (predicate(change))
                {
                    filtered.Add(change);
                }
            }

            return filtered.Count > 0 ? new ChangeSet<T>([.. filtered]) : ChangeSet<T>.Empty;
        }).Where(cs => cs.Count > 0);
    }

    /// <summary>
    /// Filters the change stream to only include changes of a specific reason.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of change sets.</param>
    /// <param name="reason">The change reason to filter for.</param>
    /// <returns>An observable containing only changes with the specified reason.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<ChangeSet<T>> WhereReason<T>(
        this IObservable<ChangeSet<T>> source,
        ChangeReason reason) => source.WhereChanges(c => c.Reason == reason);

    /// <summary>
    /// Projects each item in a sequence of change sets into a new form using the specified selector
    /// function.
    /// </summary>
    /// <remarks>This method preserves the change reasons and indices from the source change sets while
    /// transforming the items. The selector function is applied to each item as changes are observed.</remarks>
    /// <typeparam name="T">The type of the elements contained in the source change set.</typeparam>
    /// <typeparam name="TResult">The type of the elements produced by the selector function.</typeparam>
    /// <param name="source">An observable sequence of change sets whose items will be transformed.</param>
    /// <param name="selector">A function that projects each item in the change set to a new result value.</param>
    /// <returns>An observable sequence of change sets containing the projected result items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<ChangeSet<TResult>> SelectChanges<T, TResult>(
    this IObservable<ChangeSet<T>> source,
    Func<T, TResult> selector) => source.Select(changes =>
        new ChangeSet<TResult>(
            changes.Select(c => new Change<TResult>(
                c.Reason,
                selector(c.Current),
                c.Previous != null ? selector(c.Previous) : default,
                c.CurrentIndex,
                c.PreviousIndex)).ToArray()));

    /// <summary>
    /// Projects each change in the change stream into a new form, including the change metadata.
    /// </summary>
    /// <typeparam name="TSource">The type of elements in the source collection.</typeparam>
    /// <typeparam name="TResult">The type of elements in the result.</typeparam>
    /// <param name="source">The source observable of change sets.</param>
    /// <param name="selector">A transform function to apply to each change.</param>
    /// <returns>An observable of transformed results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<TResult> SelectChanges<TSource, TResult>(
        this IObservable<ChangeSet<TSource>> source,
        Func<Change<TSource>, TResult> selector)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return source.SelectMany(changeSet =>
        {
            var results = new List<TResult>(changeSet.Count);
            for (var i = 0; i < changeSet.Count; i++)
            {
                results.Add(selector(changeSet[i]));
            }

            return results;
        });
    }

    /// <summary>
    /// Subscribes to adds only from the change stream.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of change sets.</param>
    /// <returns>An observable of added items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<T> OnAdd<T>(this IObservable<ChangeSet<T>> source) =>
        source.WhereReason(ChangeReason.Add).SelectChanges(c => c.Current);

    /// <summary>
    /// Subscribes to removes only from the change stream.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of change sets.</param>
    /// <returns>An observable of removed items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<T> OnRemove<T>(this IObservable<ChangeSet<T>> source) =>
        source.WhereReason(ChangeReason.Remove).SelectChanges(c => c.Current);

    /// <summary>
    /// Subscribes to updates only from the change stream.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of change sets.</param>
    /// <returns>An observable of update tuples containing (Previous, Current) values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<(T? Previous, T Current)> OnUpdate<T>(this IObservable<ChangeSet<T>> source) =>
        source.WhereReason(ChangeReason.Update).SelectChanges(c => (c.Previous, c.Current));

    /// <summary>
    /// Subscribes to moves only from the change stream.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of change sets.</param>
    /// <returns>An observable of move tuples containing (Item, OldIndex, NewIndex).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<(T Item, int OldIndex, int NewIndex)> OnMove<T>(this IObservable<ChangeSet<T>> source) =>
        source.WhereReason(ChangeReason.Move).SelectChanges(c => (c.Current, c.PreviousIndex, c.CurrentIndex));

    /// <summary>
    /// Creates a filtered view of the reactive list that updates automatically when the source changes.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The source reactive list.</param>
    /// <param name="filter">A predicate to filter items.</param>
    /// <param name="scheduler">Optional scheduler for dispatching updates. Defaults to CurrentThreadScheduler.</param>
    /// <param name="throttleMs">Throttle interval in milliseconds. Defaults to 50ms.</param>
    /// <returns>A read-only observable collection that stays synchronized with the filtered source.</returns>
    public static FilteredReactiveView<T> CreateView<T>(
        this IReactiveList<T> list,
        Func<T, bool> filter,
        IScheduler? scheduler = null,
        int throttleMs = 50)
        where T : notnull
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(filter);
#else
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }

        if (filter == null)
        {
            throw new ArgumentNullException(nameof(filter));
        }
#endif

        return new FilteredReactiveView<T>(list, filter, scheduler ?? Scheduler.CurrentThread, TimeSpan.FromMilliseconds(throttleMs));
    }

    /// <summary>
    /// Creates an unfiltered view of the reactive list that updates automatically when the source changes.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The source reactive list.</param>
    /// <param name="scheduler">Optional scheduler for dispatching updates. Defaults to CurrentThreadScheduler.</param>
    /// <param name="throttleMs">Throttle interval in milliseconds. Defaults to 50ms.</param>
    /// <returns>A read-only observable collection that stays synchronized with the source.</returns>
    public static FilteredReactiveView<T> CreateView<T>(
        this IReactiveList<T> list,
        IScheduler? scheduler = null,
        int throttleMs = 50)
        where T : notnull => list.CreateView(_ => true, scheduler, throttleMs);

    /// <summary>
    /// Creates a dynamically filtered view that rebuilds when the filter predicate changes.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The source reactive list.</param>
    /// <param name="filterObservable">An observable that emits filter predicates.</param>
    /// <param name="scheduler">Optional scheduler for dispatching updates. Defaults to CurrentThreadScheduler.</param>
    /// <param name="throttleMs">Throttle interval in milliseconds. Defaults to 50ms.</param>
    /// <returns>A read-only observable collection that updates when the source or filter changes.</returns>
    public static DynamicFilteredReactiveView<T> CreateView<T>(
        this IReactiveList<T> list,
        IObservable<Func<T, bool>> filterObservable,
        IScheduler? scheduler = null,
        int throttleMs = 50)
        where T : notnull
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(filterObservable);
#else
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }

        if (filterObservable == null)
        {
            throw new ArgumentNullException(nameof(filterObservable));
        }
#endif

        return new DynamicFilteredReactiveView<T>(list, filterObservable, scheduler ?? Scheduler.CurrentThread, TimeSpan.FromMilliseconds(throttleMs));
    }

    /// <summary>
    /// Creates a sorted view of the reactive list that updates automatically when the source changes.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The source reactive list.</param>
    /// <param name="comparer">The comparer to use for sorting.</param>
    /// <param name="scheduler">Optional scheduler for dispatching updates. Defaults to CurrentThreadScheduler.</param>
    /// <param name="throttleMs">Throttle interval in milliseconds. Defaults to 50ms.</param>
    /// <returns>A sorted view that stays synchronized with the source.</returns>
    public static SortedReactiveView<T> SortBy<T>(
        this IReactiveList<T> list,
        IComparer<T> comparer,
        IScheduler? scheduler = null,
        int throttleMs = 50)
        where T : notnull
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(comparer);
#else
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }

        if (comparer == null)
        {
            throw new ArgumentNullException(nameof(comparer));
        }
#endif

        return new SortedReactiveView<T>(list, comparer, scheduler ?? Scheduler.CurrentThread, TimeSpan.FromMilliseconds(throttleMs));
    }

    /// <summary>
    /// Creates a sorted view of the reactive list using a key selector.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <typeparam name="TKey">The type of the sort key.</typeparam>
    /// <param name="list">The source reactive list.</param>
    /// <param name="keySelector">A function to extract the sort key.</param>
    /// <param name="descending">Whether to sort in descending order.</param>
    /// <param name="scheduler">Optional scheduler for dispatching updates. Defaults to CurrentThreadScheduler.</param>
    /// <param name="throttleMs">Throttle interval in milliseconds. Defaults to 50ms.</param>
    /// <returns>A sorted view that stays synchronized with the source.</returns>
    public static SortedReactiveView<T> SortBy<T, TKey>(
        this IReactiveList<T> list,
        Func<T, TKey> keySelector,
        bool descending = false,
        IScheduler? scheduler = null,
        int throttleMs = 50)
        where T : notnull
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(keySelector);
#else
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }

        if (keySelector == null)
        {
            throw new ArgumentNullException(nameof(keySelector));
        }
#endif

        var comparer = descending
            ? Comparer<T>.Create((x, y) => Comparer<TKey>.Default.Compare(keySelector(y), keySelector(x)))
            : Comparer<T>.Create((x, y) => Comparer<TKey>.Default.Compare(keySelector(x), keySelector(y)));

        return new SortedReactiveView<T>(list, comparer, scheduler ?? Scheduler.CurrentThread, TimeSpan.FromMilliseconds(throttleMs));
    }

    /// <summary>
    /// Groups the change stream by a key selector.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <typeparam name="TKey">The type of the grouping key.</typeparam>
    /// <param name="source">The source observable of change sets.</param>
    /// <param name="keySelector">A function to extract the key for grouping.</param>
    /// <returns>An observable of grouped changes.</returns>
    public static IObservable<IGrouping<TKey, Change<T>>> GroupByChanges<T, TKey>(
        this IObservable<ChangeSet<T>> source,
        Func<T, TKey> keySelector)
        where TKey : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (keySelector == null)
        {
            throw new ArgumentNullException(nameof(keySelector));
        }

        return source.SelectMany(changeSet =>
        {
            var groups = new Dictionary<TKey, List<Change<T>>>();
            for (var i = 0; i < changeSet.Count; i++)
            {
                var change = changeSet[i];
                var key = keySelector(change.Current);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = [];
                    groups[key] = list;
                }

                list.Add(change);
            }

            return groups.Select(kvp => new ChangeGrouping<TKey, Change<T>>(kvp.Key, kvp.Value));
        });
    }

    /// <summary>
    /// Creates a grouped view of the reactive list.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <typeparam name="TKey">The type of the grouping key.</typeparam>
    /// <param name="list">The source reactive list.</param>
    /// <param name="keySelector">A function to extract the key for grouping.</param>
    /// <param name="scheduler">Optional scheduler for dispatching updates. Defaults to CurrentThreadScheduler.</param>
    /// <param name="throttleMs">Throttle interval in milliseconds. Defaults to 50ms.</param>
    /// <returns>A grouped view that stays synchronized with the source.</returns>
    public static GroupedReactiveView<T, TKey> GroupBy<T, TKey>(
        this IReactiveList<T> list,
        Func<T, TKey> keySelector,
        IScheduler? scheduler = null,
        int throttleMs = 50)
        where T : notnull
        where TKey : notnull
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(keySelector);
#else
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }

        if (keySelector == null)
        {
            throw new ArgumentNullException(nameof(keySelector));
        }
#endif

        return new GroupedReactiveView<T, TKey>(list, keySelector, scheduler ?? Scheduler.CurrentThread, TimeSpan.FromMilliseconds(throttleMs));
    }

    /// <summary>
    /// Automatically refreshes items when the specified property changes (via INotifyPropertyChanged).
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of change sets.</param>
    /// <param name="propertyName">The name of the property to watch for changes.</param>
    /// <returns>An observable that includes refresh notifications when property changes occur.</returns>
    public static IObservable<ChangeSet<T>> AutoRefresh<T>(
        this IObservable<ChangeSet<T>> source,
        string propertyName)
        where T : System.ComponentModel.INotifyPropertyChanged
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (string.IsNullOrEmpty(propertyName))
        {
            throw new ArgumentNullException(nameof(propertyName));
        }

        return source.SelectMany(changeSet =>
        {
            var results = new List<IObservable<ChangeSet<T>>> { Observable.Return(changeSet) };

            for (var i = 0; i < changeSet.Count; i++)
            {
                var change = changeSet[i];
                if (change.Reason == ChangeReason.Add || change.Reason == ChangeReason.Update)
                {
                    var item = change.Current;
                    var index = change.CurrentIndex;
                    var refreshObservable = Observable.FromEventPattern<System.ComponentModel.PropertyChangedEventHandler, System.ComponentModel.PropertyChangedEventArgs>(
                        h => item.PropertyChanged += h,
                        h => item.PropertyChanged -= h)
                        .Where(e => e.EventArgs.PropertyName == propertyName || string.IsNullOrEmpty(e.EventArgs.PropertyName))
                        .Select(_ => new ChangeSet<T>(new[] { Change<T>.CreateRefresh(item, index) }));

                    results.Add(refreshObservable);
                }
            }

            return results.Merge();
        });
    }

    /// <summary>
    /// Automatically refreshes items when any property changes (via INotifyPropertyChanged).
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of change sets.</param>
    /// <returns>An observable that includes refresh notifications when any property changes.</returns>
    public static IObservable<ChangeSet<T>> AutoRefresh<T>(this IObservable<ChangeSet<T>> source)
        where T : System.ComponentModel.INotifyPropertyChanged => source.AutoRefresh(string.Empty);

    /// <summary>
    /// Connects to the change stream and converts to ChangeSet format for compatibility with DynamicData-style processing.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source reactive list.</param>
    /// <returns>An observable stream of change sets representing all modifications to the list.</returns>
    /// <remarks>
    /// This is a convenience method that bridges the Stream-based notification model
    /// with ChangeSet-based processing patterns.
    /// </remarks>
    public static IObservable<ChangeSet<T>> Connect<T>(this IReactiveSource<T> source)
        where T : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Stream.ToChangeSets();
    }
}
