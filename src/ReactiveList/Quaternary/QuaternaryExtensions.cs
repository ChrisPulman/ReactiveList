// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace CP.Reactive;

/// <summary>
/// Provides extension methods for creating reactive views over quaternary collections with optional filtering and
/// throttling support.
/// </summary>
/// <remarks>These extension methods enable the creation of reactive views that reflect changes in the underlying
/// quaternary collections. The views can be filtered and updated at a specified throttling interval to balance
/// responsiveness and performance. All methods are thread-safe and return a snapshot of the collection at the time of
/// view creation.</remarks>
public static class QuaternaryExtensions
{
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
    public static ReactiveView<T> CreateView<T>(this QuaternaryList<T> list, IScheduler scheduler, int throttleMs = 50)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(list);

        // Thread-safe snapshot
        var snapshot = list.ToList();
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
    public static ReactiveView<T> CreateView<T>(this QuaternaryList<T> list, Func<T, bool> filter, IScheduler scheduler, int throttleMs = 50)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(list);

        // Thread-safe snapshot
        var snapshot = list.ToList();
        return new ReactiveView<T>(list.Stream, snapshot, filter, TimeSpan.FromMilliseconds(throttleMs), scheduler);
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
    public static DynamicReactiveView<T> CreateView<T>(this QuaternaryList<T> list, IObservable<Func<T, bool>> filterObservable, IScheduler scheduler, int throttleMs = 50)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(filterObservable);

        return new DynamicReactiveView<T>(list, filterObservable, TimeSpan.FromMilliseconds(throttleMs), scheduler);
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
    public static DynamicReactiveView<T> CreateView<T, TQuery>(this QuaternaryList<T> list, IObservable<TQuery> queryObservable, Func<TQuery, T, bool> filter, IScheduler scheduler, int throttleMs = 50)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(queryObservable);
        ArgumentNullException.ThrowIfNull(filter);

        // Convert query observable to a filter observable
        var filterObservable = queryObservable.Select<TQuery, Func<T, bool>>(query => item => filter(query, item));

        return new DynamicReactiveView<T>(list, filterObservable, TimeSpan.FromMilliseconds(throttleMs), scheduler);
    }

    /// <summary>
    /// Creates a reactive view filtered by a secondary index key.
    /// </summary>
    /// <remarks>The view uses the specified secondary index for efficient filtering. The index must have been
    /// previously added using <see cref="QuaternaryList{T}.AddIndex{TKey}"/>.</remarks>
    /// <typeparam name="T">The type of elements contained in the quaternary list.</typeparam>
    /// <typeparam name="TKey">The type of the secondary index key.</typeparam>
    /// <param name="list">The source quaternary list to observe for changes.</param>
    /// <param name="indexName">The name of the secondary index to use for filtering.</param>
    /// <param name="key">The key value to filter by.</param>
    /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
    /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
    /// milliseconds.</param>
    /// <returns>A <see cref="ReactiveView{T}"/> that reflects the filtered contents of the source list matching the specified
    /// secondary index key.</returns>
    public static ReactiveView<T> CreateViewBySecondaryIndex<T, TKey>(this QuaternaryList<T> list, string indexName, TKey key, IScheduler scheduler, int throttleMs = 50)
        where T : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(indexName);

        // Get initial snapshot from the secondary index
        var snapshot = list.GetItemsBySecondaryIndex(indexName, key).ToList();

        // Create a filter that uses the secondary index for the predicate
        // Note: The filter validates items match the key during stream processing
        var keySet = new HashSet<T>(snapshot);
        return new ReactiveView<T>(list.Stream, snapshot, item => keySet.Contains(item), TimeSpan.FromMilliseconds(throttleMs), scheduler);
    }

    /// <summary>
    /// Creates a reactive view filtered by multiple secondary index keys.
    /// </summary>
    /// <remarks>The view includes items that match any of the specified keys (OR logic). The index must have
    /// been previously added using <see cref="QuaternaryList{T}.AddIndex{TKey}"/>.</remarks>
    /// <typeparam name="T">The type of elements contained in the quaternary list.</typeparam>
    /// <typeparam name="TKey">The type of the secondary index key.</typeparam>
    /// <param name="list">The source quaternary list to observe for changes.</param>
    /// <param name="indexName">The name of the secondary index to use for filtering.</param>
    /// <param name="keys">The key values to filter by. Items matching any of these keys are included.</param>
    /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
    /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
    /// milliseconds.</param>
    /// <returns>A <see cref="ReactiveView{T}"/> that reflects the filtered contents of the source list matching any of the
    /// specified secondary index keys.</returns>
    public static ReactiveView<T> CreateViewBySecondaryIndex<T, TKey>(this QuaternaryList<T> list, string indexName, TKey[] keys, IScheduler scheduler, int throttleMs = 50)
        where T : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(indexName);
        ArgumentNullException.ThrowIfNull(keys);

        // Get initial snapshot from the secondary index for all keys
        var snapshot = new List<T>();
        foreach (var key in keys)
        {
            snapshot.AddRange(list.GetItemsBySecondaryIndex(indexName, key));
        }

        // Create a set for efficient lookup
        var keySet = new HashSet<T>(snapshot);
        return new ReactiveView<T>(list.Stream, snapshot, item => keySet.Contains(item), TimeSpan.FromMilliseconds(throttleMs), scheduler);
    }

    /// <summary>
    /// Creates a reactive view with a dynamic secondary index key filter that rebuilds when the key observable emits new keys.
    /// </summary>
    /// <remarks>The view automatically rebuilds its contents when the key observable emits new key values.
    /// This is useful for implementing dynamic filtering where the filter criteria can change over time.</remarks>
    /// <typeparam name="T">The type of elements contained in the quaternary list.</typeparam>
    /// <typeparam name="TKey">The type of the secondary index key.</typeparam>
    /// <param name="list">The source quaternary list to observe for changes.</param>
    /// <param name="indexName">The name of the secondary index to use for filtering.</param>
    /// <param name="keysObservable">An observable that emits arrays of key values. When new keys are emitted, the view rebuilds its contents.</param>
    /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
    /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
    /// milliseconds.</param>
    /// <returns>A <see cref="DynamicReactiveView{T}"/> that reflects the filtered contents of the source list matching the
    /// specified secondary index keys and updates when the keys change.</returns>
    public static DynamicReactiveView<T> CreateViewBySecondaryIndex<T, TKey>(this QuaternaryList<T> list, string indexName, IObservable<TKey[]> keysObservable, IScheduler scheduler, int throttleMs = 50)
        where T : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(indexName);
        ArgumentNullException.ThrowIfNull(keysObservable);

        // Convert keys observable to a filter observable
        var filterObservable = keysObservable.Select<TKey[], Func<T, bool>>(keys =>
        {
            // Build a hashset of items matching the keys for efficient lookup
            var matchingItems = new HashSet<T>();
            foreach (var key in keys)
            {
                foreach (var item in list.GetItemsBySecondaryIndex(indexName, key))
                {
                    matchingItems.Add(item);
                }
            }

            return item => matchingItems.Contains(item);
        });

        return new DynamicReactiveView<T>(list, filterObservable, TimeSpan.FromMilliseconds(throttleMs), scheduler);
    }

    /// <summary>
    /// Filters the cache notification stream to only include items that match the specified secondary index key.
    /// </summary>
    /// <remarks>This extension method enables efficient stream filtering using a pre-configured secondary index.
    /// The returned observable emits filtered cache notifications that only contain items matching the specified key.
    /// This is useful for reactive pipelines where you need to filter data changes by secondary index criteria.</remarks>
    /// <typeparam name="T">The type of elements in the cache notification stream.</typeparam>
    /// <typeparam name="TKey">The type of the secondary index key.</typeparam>
    /// <param name="stream">The source cache notification stream to filter.</param>
    /// <param name="list">The quaternary list containing the secondary index definition.</param>
    /// <param name="indexName">The name of the secondary index to use for filtering.</param>
    /// <param name="key">The key value to filter by.</param>
    /// <returns>An observable sequence of cache notifications that only includes items matching the specified secondary index key.</returns>
    public static IObservable<CacheNotify<T>> FilterBySecondaryIndex<T, TKey>(this IObservable<CacheNotify<T>> stream, QuaternaryList<T> list, string indexName, TKey key)
        where T : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(indexName);

        return stream.Select(notification =>
        {
            // Get the current set of items matching the key from the secondary index
            var matchingItems = new HashSet<T>(list.GetItemsBySecondaryIndex(indexName, key));

            return notification.Action switch
            {
                CacheAction.Added when notification.Item != null && matchingItems.Contains(notification.Item) => notification,
                CacheAction.Removed when notification.Item != null && matchingItems.Contains(notification.Item) => notification,
                CacheAction.BatchOperation when notification.Batch != null => FilterBatch(notification, matchingItems),
                CacheAction.Cleared => notification,
                _ => null
            };
        }).Where(n => n != null).Select(n => n!);
    }

    /// <summary>
    /// Filters the cache notification stream to only include items that match any of the specified secondary index keys.
    /// </summary>
    /// <remarks>This extension method enables efficient stream filtering using a pre-configured secondary index
    /// with multiple keys (OR logic). The returned observable emits filtered cache notifications that only contain
    /// items matching any of the specified keys.</remarks>
    /// <typeparam name="T">The type of elements in the cache notification stream.</typeparam>
    /// <typeparam name="TKey">The type of the secondary index key.</typeparam>
    /// <param name="stream">The source cache notification stream to filter.</param>
    /// <param name="list">The quaternary list containing the secondary index definition.</param>
    /// <param name="indexName">The name of the secondary index to use for filtering.</param>
    /// <param name="keys">The key values to filter by. Items matching any of these keys are included.</param>
    /// <returns>An observable sequence of cache notifications that only includes items matching any of the specified
    /// secondary index keys.</returns>
    public static IObservable<CacheNotify<T>> FilterBySecondaryIndex<T, TKey>(this IObservable<CacheNotify<T>> stream, QuaternaryList<T> list, string indexName, params TKey[] keys)
        where T : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(indexName);
        ArgumentNullException.ThrowIfNull(keys);

        return stream.Select(notification =>
        {
            // Get the current set of items matching all keys from the secondary index
            var matchingItems = new HashSet<T>();
            foreach (var key in keys)
            {
                foreach (var item in list.GetItemsBySecondaryIndex(indexName, key))
                {
                    matchingItems.Add(item);
                }
            }

            return notification.Action switch
            {
                CacheAction.Added when notification.Item != null && matchingItems.Contains(notification.Item) => notification,
                CacheAction.Removed when notification.Item != null && matchingItems.Contains(notification.Item) => notification,
                CacheAction.BatchOperation when notification.Batch != null => FilterBatch(notification, matchingItems),
                CacheAction.Cleared => notification,
                _ => null
            };
        }).Where(n => n != null).Select(n => n!);
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
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(filterObservable);

        return filterObservable
            .StartWith(static _ => true) // Default to include all items
            .Select(filter => stream.Select(notification =>
            {
                return notification.Action switch
                {
                    CacheAction.Added when notification.Item != null && filter(notification.Item) => notification,
                    CacheAction.Removed when notification.Item != null => notification, // Always pass removed items
                    CacheAction.BatchOperation when notification.Batch != null => FilterBatchByPredicate(notification, filter),
                    CacheAction.Cleared => notification,
                    _ => null
                };
            }).Where(n => n != null).Select(n => n!))
            .Switch();
    }

    /// <summary>
    /// Creates a reactive view of the dictionary that emits filtered key-value pairs as the dictionary changes.
    /// </summary>
    /// <remarks>The returned view reflects changes to the source dictionary in real time, subject to the
    /// specified filter and throttle interval. Use this method to create dynamic, filtered projections of the
    /// dictionary that update automatically as the underlying data changes.</remarks>
    /// <typeparam name="TKey">The type of keys in the dictionary. Must not be null.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="dict">The source dictionary to observe for changes.</param>
    /// <param name="filter">A predicate used to filter which key-value pairs are included in the view. Only pairs for which the predicate
    /// returns <see langword="true"/> are included.</param>
    /// <param name="scheduler">The scheduler used to dispatch notifications of changes to the view.</param>
    /// <param name="throttleMs">The minimum time, in milliseconds, to wait before emitting updates after a change. Defaults to 50 milliseconds.</param>
    /// <returns>A ReactiveView{KeyValuePair{TKey, TValue}} that provides a filtered, observable view of the dictionary's
    /// contents.</returns>
    public static ReactiveView<KeyValuePair<TKey, TValue>> CreateView<TKey, TValue>(this QuaternaryDictionary<TKey, TValue> dict, Func<KeyValuePair<TKey, TValue>, bool> filter, IScheduler scheduler, int throttleMs = 50)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dict);

        // Thread-safe snapshot
        var snapshot = dict.ToList();
        return new ReactiveView<KeyValuePair<TKey, TValue>>(dict.Stream, snapshot, filter, TimeSpan.FromMilliseconds(throttleMs), scheduler);
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
    private static CacheNotify<T>? FilterBatch<T>(CacheNotify<T> notification, HashSet<T> matchingItems)
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
    private static CacheNotify<T>? FilterBatchByPredicate<T>(CacheNotify<T> notification, Func<T, bool> filter)
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
}
#endif
