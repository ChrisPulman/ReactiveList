// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;
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
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }
#endif

        return source.Select(changeSet =>
        {
            if (changeSet.Count == 0)
            {
                return ChangeSet<T>.Empty;
            }

            // Count matching items first to avoid resizing
            var matchCount = 0;
            for (var i = 0; i < changeSet.Count; i++)
            {
                if (predicate(changeSet[i]))
                {
                    matchCount++;
                }
            }

            if (matchCount == 0)
            {
                return ChangeSet<T>.Empty;
            }

            // If all match, return original (avoid copy)
            if (matchCount == changeSet.Count)
            {
                return changeSet;
            }

            // Allocate exactly what's needed
            var filtered = new Change<T>[matchCount];
            var idx = 0;
            for (var i = 0; i < changeSet.Count; i++)
            {
                var change = changeSet[i];
                if (predicate(change))
                {
                    filtered[idx++] = change;
                }
            }

            return new ChangeSet<T>(filtered);
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
        Func<T, TResult> selector)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
#else
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }
#endif

        return source.Select(changes =>
        {
            if (changes.Count == 0)
            {
                return ChangeSet<TResult>.Empty;
            }

            var transformed = new Change<TResult>[changes.Count];
            for (var i = 0; i < changes.Count; i++)
            {
                var c = changes[i];
                transformed[i] = new Change<TResult>(
                    c.Reason,
                    selector(c.Current),
                    c.Previous != null ? selector(c.Previous) : default,
                    c.CurrentIndex,
                    c.PreviousIndex);
            }

            return new ChangeSet<TResult>(transformed);
        });
    }

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
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
#else
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }
#endif

        return source.SelectMany(changeSet =>
        {
            if (changeSet.Count == 0)
            {
                return Array.Empty<TResult>();
            }

            var results = new TResult[changeSet.Count];
            for (var i = 0; i < changeSet.Count; i++)
            {
                results[i] = selector(changeSet[i]);
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
    /// Creates a filtered observable sequence where notifications are transformed based on a custom predicate that
    /// re-evaluates on each emission.
    /// </summary>
    /// <remarks>This extension provides dynamic filtering capabilities where the filter predicate is provided by
    /// an observable. When a new filter is emitted, subsequent cache notifications are filtered using that predicate.</remarks>
    /// <typeparam name="TKey">The type of keys in the dictionary. Must not be null.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="stream">The source cache notification stream to filter.</param>
    /// <param name="filterObservable">An observable that emits filter predicates. Each new predicate is used to filter subsequent notifications.</param>
    /// <returns>An observable sequence of cache notifications filtered by the most recent predicate.</returns>
    public static IObservable<CacheNotify<KeyValuePair<TKey, TValue>>> FilterDynamic<TKey, TValue>(this IObservable<CacheNotify<KeyValuePair<TKey, TValue>>> stream, IObservable<Func<KeyValuePair<TKey, TValue>, bool>> filterObservable)
        where TKey : notnull
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(filterObservable);
#else
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (filterObservable == null)
        {
            throw new ArgumentNullException(nameof(filterObservable));
        }
#endif

        return filterObservable
            .StartWith(static _ => true) // Default to include all items
            .Select(filter => stream.Select(notification => notification.Action switch
            {
                CacheAction.Added when filter(notification.Item) => notification,
                CacheAction.Removed => notification, // Always pass removed items
                CacheAction.BatchAdded or CacheAction.BatchRemoved or CacheAction.BatchOperation when notification.Batch != null =>
                    ReactiveListExtensions.FilterBatchByPredicate(notification, filter),
                CacheAction.Cleared => notification,
                _ => null
            }).Where(n => n != null).Select(n => n!))
            .Switch();
    }

    /// <summary>
    /// Creates a filtered observable sequence where notifications are transformed based on a custom predicate that
    /// re-evaluates on each emission.
    /// </summary>
    /// <remarks>This extension provides dynamic filtering capabilities where the filter predicate is provided by
    /// an observable. When a new filter is emitted, subsequent cache notifications are filtered using that predicate.</remarks>
    /// <typeparam name="T">The type of elements in the cache notification stream.</typeparam>
    /// <param name="stream">The source cache notification stream to filter.</param>
    /// <param name="filterObservable">An observable that emits filter predicates. Each new predicate is used to filter subsequent notifications.</param>
    /// <returns>An observable sequence of cache notifications filtered by the most recent predicate.</returns>
    public static IObservable<CacheNotify<T>> FilterDynamic<T>(this IObservable<CacheNotify<T>> stream, IObservable<Func<T, bool>> filterObservable)
        where T : notnull
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(filterObservable);
#else
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (filterObservable == null)
        {
            throw new ArgumentNullException(nameof(filterObservable));
        }
#endif

        return filterObservable
            .StartWith(static _ => true) // Default to include all items
            .Select(filter => stream.Select(notification => notification.Action switch
            {
                CacheAction.Added when notification.Item != null && filter(notification.Item) => notification,
                CacheAction.Removed when notification.Item != null => notification, // Always pass removed items
                CacheAction.BatchOperation when notification.Batch != null => FilterBatchByPredicate(notification, filter),
                CacheAction.Cleared => notification,
                _ => null
            }).Where(n => n != null).Select(n => n!))
            .Switch();
    }

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
    /// Creates a dynamic, reactive view of the list that updates in response to changes in a query observable and
    /// applies a filter to determine which items are included.
    /// </summary>
    /// <remarks>The returned view updates in response to both changes in the source list and changes in the
    /// query observable. Updates are throttled according to the specified interval to avoid excessive refreshes when
    /// queries change rapidly.</remarks>
    /// <typeparam name="T">The type of elements in the source list. Must be non-nullable.</typeparam>
    /// <typeparam name="TQuery">The type of the query values emitted by the observable that influence the filtering logic.</typeparam>
    /// <param name="list">The source list to create a dynamic view from. Cannot be null.</param>
    /// <param name="queryObservable">An observable sequence that emits query values used to update the filter applied to the list. Cannot be null.</param>
    /// <param name="filter">A function that determines whether a given item in the list matches the current query. The function receives the
    /// current query and an item, and returns <see langword="true"/> to include the item in the view; otherwise, <see
    /// langword="false"/>. Cannot be null.</param>
    /// <param name="scheduler">The scheduler used to observe and process updates to the view.</param>
    /// <param name="throttleMs">The minimum time, in milliseconds, to wait before applying updates after a query change. Defaults to 50
    /// milliseconds.</param>
    /// <returns>A <see cref="DynamicReactiveView{T}"/> that reflects the filtered view of the list and updates automatically as
    /// the query observable emits new values.</returns>
    public static DynamicReactiveView<T> CreateView<T, TQuery>(this IReactiveSource<T> list, IObservable<TQuery> queryObservable, Func<TQuery, T, bool> filter, IScheduler scheduler, int throttleMs = 50)
        where T : notnull
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(queryObservable);
        ArgumentNullException.ThrowIfNull(filter);
#else
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }

        if (queryObservable == null)
        {
            throw new ArgumentNullException(nameof(queryObservable));
        }

        if (filter == null)
        {
            throw new ArgumentNullException(nameof(filter));
        }
#endif

        // Convert query observable to a filter observable
        var filterObservable = queryObservable.Select<TQuery, Func<T, bool>>(query => item => filter(query, item));

        return new DynamicReactiveView<T>(list, filterObservable, TimeSpan.FromMilliseconds(throttleMs), scheduler);
    }

    /// <summary>
    /// Creates a reactive view with a dynamic filter that rebuilds when the filter observable emits a new predicate.
    /// </summary>
    /// <remarks>The view automatically rebuilds its contents when the filter observable emits a new predicate.
    /// This is useful for implementing dynamic search functionality where the search criteria can change over time.</remarks>
    /// <typeparam name="T">The type of elements contained in the quaternary list.</typeparam>
    /// <param name="list">The source quaternary list to observe for changes.</param>
    /// <param name="filterObservable">An observable that emits filter predicates. When a new predicate is emitted, the view rebuilds its contents.</param>
    /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
    /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
    /// milliseconds.</param>
    /// <returns>A <see cref="DynamicReactiveView{T}"/> that reflects the filtered contents of the source list and updates reactively
    /// as the list changes or the filter predicate changes.</returns>
    public static DynamicReactiveView<T> CreateView<T>(this IReactiveSource<T> list, IObservable<Func<T, bool>> filterObservable, IScheduler scheduler, int throttleMs = 50)
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

        return new DynamicReactiveView<T>(list, filterObservable, TimeSpan.FromMilliseconds(throttleMs), scheduler);
    }

    /// <summary>
    /// Creates a reactive view over the specified quaternary list that updates in response to changes, with optional
    /// throttling.
    /// </summary>
    /// <remarks>The returned view provides a thread-safe, observable snapshot of the list. Change
    /// notifications are throttled to avoid excessive updates when the list changes rapidly. Use this method to
    /// efficiently bind UI or other observers to dynamic data sources.</remarks>
    /// <typeparam name="T">The type of elements contained in the quaternary list. Must be non-nullable.</typeparam>
    /// <param name="list">The source quaternary list to observe for changes. Cannot be null.</param>
    /// <param name="scheduler">The scheduler used to dispatch change notifications to the reactive view. Cannot be null.</param>
    /// <param name="throttleMs">The minimum interval, in milliseconds, between consecutive change notifications. Must be non-negative. The
    /// default is 50 milliseconds.</param>
    /// <returns>A <see cref="ReactiveView{T}"/> that reflects the current state of the list and updates when the list changes, subject to the
    /// specified throttling.</returns>
    public static ReactiveView<T> CreateView<T>(this IReactiveSource<T> list, IScheduler scheduler, int throttleMs = 50)
        where T : notnull
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(list);
#else
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }
#endif

        // Thread-safe snapshot
        var snapshot = list.ToArray();
        return new ReactiveView<T>(list.Stream, snapshot, _ => true, TimeSpan.FromMilliseconds(throttleMs), scheduler);
    }

    /// <summary>
    /// Creates a reactive view of the specified quaternary list that updates in response to changes, applying the given
    /// filter and throttling updates as specified.
    /// </summary>
    /// <remarks>The returned view is updated in a thread-safe manner and only includes items that satisfy the
    /// specified filter. Updates are throttled to avoid excessive notifications when the source list changes
    /// rapidly.</remarks>
    /// <typeparam name="T">The type of elements contained in the quaternary list.</typeparam>
    /// <param name="list">The source quaternary list to observe for changes.</param>
    /// <param name="filter">A function that determines whether an element should be included in the view. Only elements for which this
    /// function returns <see langword="true"/> are included.</param>
    /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
    /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
    /// milliseconds.</param>
    /// <returns>A <see cref="ReactiveView{T}"/> that reflects the filtered contents of the source list and updates reactively as
    /// the list changes.</returns>
    public static ReactiveView<T> CreateView<T>(this IReactiveSource<T> list, Func<T, bool> filter, IScheduler scheduler, int throttleMs = 50)
        where T : notnull
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(list);
#else
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }
#endif

        // Thread-safe snapshot
        var snapshot = list.ToArray();
        return new ReactiveView<T>(list.Stream, snapshot, filter, TimeSpan.FromMilliseconds(throttleMs), scheduler);
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
    /// Projects each item in the source change set into groups based on a specified key, emitting an observable
    /// sequence of grouped observables as items change.
    /// </summary>
    /// <remarks>Each group is represented by an <see cref="IGroupedObservable{TKey, T}"/> that emits items
    /// sharing the same key. Groups are created and removed dynamically as items are added or removed from the source
    /// change set.</remarks>
    /// <typeparam name="T">The type of the items contained in the change set.</typeparam>
    /// <typeparam name="TKey">The type of the key used to group items.</typeparam>
    /// <param name="source">An observable sequence of change sets containing items to be grouped.</param>
    /// <param name="keySelector">A function that extracts the grouping key from each item.</param>
    /// <returns>An observable sequence of grouped observables, where each group corresponds to a unique key and emits items as
    /// they change.</returns>
    public static IObservable<IGroupedObservable<TKey, T>> GroupByChanges<T, TKey>(
    this IObservable<ChangeSet<T>> source,
    Func<T, TKey> keySelector) => source
            .SelectMany(set => set)
            .GroupBy(c => keySelector(c.Current), c => c.Current);

    /// <summary>
    /// Groups the change stream by a key selector.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <typeparam name="TKey">The type of the grouping key.</typeparam>
    /// <param name="source">The source observable of change sets.</param>
    /// <param name="keySelector">A function to extract the key for grouping.</param>
    /// <returns>An observable of grouped changes.</returns>
    public static IObservable<IGrouping<TKey, Change<T>>> GroupingByChanges<T, TKey>(
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
    /// <param name="propertyName">The name of the property to watch for changes. If null or empty, watches all properties.</param>
    /// <returns>An observable that includes refresh notifications when property changes occur.</returns>
    public static IObservable<ChangeSet<T>> AutoRefresh<T>(
        this IObservable<ChangeSet<T>> source,
        string? propertyName)
        where T : System.ComponentModel.INotifyPropertyChanged
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var watchAllProperties = string.IsNullOrEmpty(propertyName);

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
                        .Where(e => watchAllProperties || e.EventArgs.PropertyName == propertyName || string.IsNullOrEmpty(e.EventArgs.PropertyName))
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

    /// <summary>
    /// Filters the items in each cache notification emitted by the source observable according to the specified predicate.
    /// </summary>
    /// <remarks>This method produces a new sequence where each notification's item is checked against the predicate.
    /// Notifications with items that don't match are filtered out.</remarks>
    /// <typeparam name="T">The type of the items contained in the notification.</typeparam>
    /// <param name="source">The source observable sequence of cache notifications to filter.</param>
    /// <param name="predicate">A function that defines the conditions each item must satisfy to be included.</param>
    /// <returns>An observable sequence of cache notifications containing only notifications with items that satisfy the predicate.</returns>
    public static IObservable<CacheNotify<T>> WhereItems<T>(
    this IObservable<CacheNotify<T>> source,
    Func<T, bool> predicate)
        where T : notnull =>
        source.Where(notification =>
        {
            // Check if this is a single-item action (where Item property is meaningful)
            var isSingleItemAction = notification.Action is CacheAction.Added
                or CacheAction.Removed
                or CacheAction.Updated
                or CacheAction.Moved
                or CacheAction.Refreshed;

            if (isSingleItemAction && notification.Item is not null)
            {
                return predicate(notification.Item);
            }

            // For batch operations or cleared, pass through
            return notification.Action == CacheAction.Cleared ||
                   notification.Action == CacheAction.BatchOperation ||
                   notification.Action == CacheAction.BatchAdded ||
                   notification.Action == CacheAction.BatchRemoved;
        });

    /// <summary>
    /// Projects each change set in the observable sequence into a sorted batch using the specified key selector.
    /// </summary>
    /// <remarks>The sorting is applied to each batch as it is emitted by the source sequence. The method does
    /// not maintain a global sort order across batches.</remarks>
    /// <typeparam name="T">The type of the elements contained in the change set.</typeparam>
    /// <typeparam name="TKey">The type of the key used for sorting the elements.</typeparam>
    /// <param name="source">The observable sequence of change sets to be sorted.</param>
    /// <param name="keySelector">A function that extracts the key from each element for sorting purposes. Cannot be null.</param>
    /// <returns>An observable sequence of change sets, where each batch is sorted according to the specified key
    /// selector.</returns>
    public static IObservable<ChangeSet<T>> SortBy<T, TKey>(
    this IObservable<ChangeSet<T>> source,
    Func<T, TKey> keySelector) => source.Select(changes =>
    {
        var sorted = changes.OrderBy(c => keySelector(c.Current)).ToArray();
        return new ChangeSet<T>(sorted);
    });

    /// <summary>
    /// Creates an observable sequence that emits a refresh change set whenever the specified property of any item in
    /// the source list changes.
    /// </summary>
    /// <remarks>The returned observable emits a refresh change set for the entire list whenever the observed
    /// property changes on any item. This is useful for scenarios where downstream consumers need to be notified of
    /// property changes that may affect filtering, sorting, or other derived views.</remarks>
    /// <typeparam name="T">The type of elements contained in the source list.</typeparam>
    /// <param name="source">The source list to monitor for property changes.</param>
    /// <param name="property">An expression specifying the property of each item to observe for changes. Must refer to a property of type
    /// <typeparamref name="T"/>.</param>
    /// <returns>An observable sequence that produces a refresh notification each time the specified property changes on any item
    /// in the source list.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="property"/> does not refer to a property.</exception>
    public static IObservable<CacheNotify<T>> AutoRefresh<T>(
    this IReactiveSource<T> source,
    Expression<Func<T, object>> property)
        where T : notnull
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(property);
#else
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }
#endif

        var member = (property.Body as MemberExpression)?.Member ?? throw new ArgumentException("Expression must be a property", nameof(property));

        // Return the Stream directly - it already provides all change notifications
        // The caller can subscribe and handle refresh logic based on item property changes
        return source.Stream;
    }

    /// <summary>
    /// Filters the items in a batch notification based on a specified predicate.
    /// </summary>
    /// <remarks>The returned batch notification will contain only the filtered items. If the filter excludes
    /// all items, or if the input batch is null, the method returns null. The method uses an array pool to allocate
    /// storage for the filtered items, which may improve performance for large batches.</remarks>
    /// <typeparam name="T">The type of items contained in the batch notification. Must be non-nullable.</typeparam>
    /// <param name="notification">The batch notification containing the items to filter. The batch must not be null.</param>
    /// <param name="filter">A predicate function used to determine whether an item in the batch should be included in the result.</param>
    /// <returns>A new batch notification containing only the items that satisfy the predicate, or null if no items match or if
    /// the original batch is null.</returns>
    internal static CacheNotify<T>? FilterBatchByPredicate<T>(CacheNotify<T> notification, Func<T, bool> filter)
        where T : notnull
    {
        if (notification.Batch == null)
        {
            return null;
        }

        var filteredItems = new List<T>();
        for (var i = 0; i < notification.Batch.Count; i++)
        {
            var item = notification.Batch.Items[i];
            if (filter(item))
            {
                filteredItems.Add(item);
            }
        }

        if (filteredItems.Count == 0)
        {
            return null;
        }

        var pooledArray = System.Buffers.ArrayPool<T>.Shared.Rent(filteredItems.Count);
        for (var i = 0; i < filteredItems.Count; i++)
        {
            pooledArray[i] = filteredItems[i];
        }

        return new CacheNotify<T>(CacheAction.BatchOperation, default, new PooledBatch<T>(pooledArray, filteredItems.Count));
    }

    /// <summary>
    /// Filters the items in a batch notification to include only those that are present in the specified set of
    /// matching items.
    /// </summary>
    /// <typeparam name="T">The type of items contained in the batch. Must be non-nullable.</typeparam>
    /// <param name="notification">The batch notification containing the items to filter. The batch must not be null.</param>
    /// <param name="matchingItems">A set of items to match against. Only items present in this set will be included in the filtered batch.</param>
    /// <returns>A new batch notification containing only the items that are present in both the original batch and the matching
    /// set; or null if the original batch is null or if no items match.</returns>
    internal static CacheNotify<T>? FilterBatch<T>(CacheNotify<T> notification, HashSet<T> matchingItems)
        where T : notnull
    {
        if (notification.Batch == null)
        {
            return null;
        }

        var filteredItems = new List<T>();
        for (var i = 0; i < notification.Batch.Count; i++)
        {
            var item = notification.Batch.Items[i];
            if (matchingItems.Contains(item))
            {
                filteredItems.Add(item);
            }
        }

        if (filteredItems.Count == 0)
        {
            return null;
        }

        var pooledArray = System.Buffers.ArrayPool<T>.Shared.Rent(filteredItems.Count);
        for (var i = 0; i < filteredItems.Count; i++)
        {
            pooledArray[i] = filteredItems[i];
        }

        return new CacheNotify<T>(CacheAction.BatchOperation, default, new PooledBatch<T>(pooledArray, filteredItems.Count));
    }
}
