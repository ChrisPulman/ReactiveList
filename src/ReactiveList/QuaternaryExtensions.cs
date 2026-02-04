// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Views;

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
        var snapshot = list.GetItemsBySecondaryIndex(indexName, key);

        // Create a filter that dynamically checks against the secondary index
        // This ensures new items are properly filtered based on their key value
        return new ReactiveView<T>(list.Stream, snapshot, item => list.ItemMatchesSecondaryIndex(indexName, item, key), TimeSpan.FromMilliseconds(throttleMs), scheduler);
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
        var snapshot = keys.SelectMany(key => list.GetItemsBySecondaryIndex(indexName, key));

        // Create a filter that dynamically checks against any of the keys
        var keySet = new HashSet<TKey>(keys);
        return new ReactiveView<T>(
            list.Stream,
            snapshot,
            item => keySet.Any(key => list.ItemMatchesSecondaryIndex(indexName, item, key)),
            TimeSpan.FromMilliseconds(throttleMs),
            scheduler);
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
    /// <returns>A <see cref="DynamicSecondaryIndexReactiveView{T, TKey}"/> that reflects the filtered contents of the source list matching the
    /// specified secondary index keys and updates when the keys change.</returns>
    public static DynamicSecondaryIndexReactiveView<T, TKey> CreateDynamicViewBySecondaryIndex<T, TKey>(this QuaternaryList<T> list, string indexName, IObservable<TKey[]> keysObservable, IScheduler scheduler, int throttleMs = 50)
        where T : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(indexName);
        ArgumentNullException.ThrowIfNull(keysObservable);

        return new DynamicSecondaryIndexReactiveView<T, TKey>(list, indexName, keysObservable, scheduler, TimeSpan.FromMilliseconds(throttleMs));
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
            var matchingItems = list.GetItemsBySecondaryIndex(indexName, key).Where(x => x != null).ToHashSet()!;

            return notification.Action switch
            {
                CacheAction.Added when notification.Item != null && matchingItems.Contains(notification.Item) => notification,
                CacheAction.Removed when notification.Item != null && matchingItems.Contains(notification.Item) => notification,
                CacheAction.BatchOperation when notification.Batch != null => ReactiveListExtensions.FilterBatch(notification, matchingItems!),
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
            var matchingItems = keys.SelectMany(key => list.GetItemsBySecondaryIndex(indexName, key)).Where(x => x != null).ToHashSet()!;

            return notification.Action switch
            {
                CacheAction.Added when notification.Item != null && matchingItems.Contains(notification.Item) => notification,
                CacheAction.Removed when notification.Item != null && matchingItems.Contains(notification.Item) => notification,
                CacheAction.BatchOperation when notification.Batch != null => ReactiveListExtensions.FilterBatch(notification, matchingItems!),
                CacheAction.Cleared => notification,
                _ => null
            };
        }).Where(n => n != null).Select(n => n!);
    }

    /// <summary>
    /// Creates a reactive view filtered by a secondary value index key.
    /// </summary>
    /// <remarks>The view uses the specified secondary value index for efficient filtering. The index must have been
    /// previously added using <see cref="QuaternaryDictionary{TKey, TValue}.AddValueIndex{TIndexKey}"/>.</remarks>
    /// <typeparam name="TKey">The type of keys in the dictionary. Must not be null.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
    /// <param name="dict">The source dictionary to observe for changes.</param>
    /// <param name="indexName">The name of the secondary value index to use for filtering.</param>
    /// <param name="indexKey">The key value to filter by.</param>
    /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
    /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
    /// milliseconds.</param>
    /// <returns>A <see cref="ReactiveView{T}"/> that reflects the filtered contents of the dictionary matching the specified
    /// secondary index key.</returns>
    public static ReactiveView<KeyValuePair<TKey, TValue>> CreateViewBySecondaryIndex<TKey, TValue, TIndexKey>(this QuaternaryDictionary<TKey, TValue> dict, string indexName, TIndexKey indexKey, IScheduler scheduler, int throttleMs = 50)
        where TKey : notnull
        where TIndexKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dict);
        ArgumentNullException.ThrowIfNull(indexName);

        // Get initial snapshot from the secondary index - map values back to key-value pairs
        var matchingValues = dict.GetValuesBySecondaryIndex(indexName, indexKey).ToHashSet();
        var snapshot = dict.Where(kvp => matchingValues.Contains(kvp.Value));

        // Create a filter that dynamically checks against the secondary index
        return new ReactiveView<KeyValuePair<TKey, TValue>>(
            dict.Stream,
            snapshot,
            kvp => dict.ValueMatchesSecondaryIndex(indexName, kvp.Value, indexKey),
            TimeSpan.FromMilliseconds(throttleMs),
            scheduler);
    }

    /// <summary>
    /// Creates a reactive view filtered by multiple secondary value index keys.
    /// </summary>
    /// <remarks>The view includes items whose values match any of the specified keys (OR logic). The index must have
    /// been previously added using <see cref="QuaternaryDictionary{TKey, TValue}.AddValueIndex{TIndexKey}"/>.</remarks>
    /// <typeparam name="TKey">The type of keys in the dictionary. Must not be null.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
    /// <param name="dict">The source dictionary to observe for changes.</param>
    /// <param name="indexName">The name of the secondary value index to use for filtering.</param>
    /// <param name="indexKeys">The key values to filter by. Items whose values match any of these keys are included.</param>
    /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
    /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
    /// milliseconds.</param>
    /// <returns>A <see cref="ReactiveView{T}"/> that reflects the filtered contents of the dictionary matching any of the
    /// specified secondary index keys.</returns>
    public static ReactiveView<KeyValuePair<TKey, TValue>> CreateViewBySecondaryIndex<TKey, TValue, TIndexKey>(this QuaternaryDictionary<TKey, TValue> dict, string indexName, TIndexKey[] indexKeys, IScheduler scheduler, int throttleMs = 50)
        where TKey : notnull
        where TIndexKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dict);
        ArgumentNullException.ThrowIfNull(indexName);
        ArgumentNullException.ThrowIfNull(indexKeys);

        // Get initial snapshot from the secondary index for all keys
        var matchingValues = indexKeys.SelectMany(k => dict.GetValuesBySecondaryIndex(indexName, k)).ToHashSet();
        var snapshot = dict.Where(kvp => matchingValues.Contains(kvp.Value));

        // Create a filter that dynamically checks against any of the keys
        var keySet = new HashSet<TIndexKey>(indexKeys);
        return new ReactiveView<KeyValuePair<TKey, TValue>>(
            dict.Stream,
            snapshot,
            kvp => keySet.Any(k => dict.ValueMatchesSecondaryIndex(indexName, kvp.Value, k)),
            TimeSpan.FromMilliseconds(throttleMs),
            scheduler);
    }

    /// <summary>
    /// Creates a reactive view with a dynamic secondary value index key filter that rebuilds when the key observable emits new keys.
    /// </summary>
    /// <remarks>The view automatically rebuilds its contents when the key observable emits new key values.
    /// This is useful for implementing dynamic filtering where the filter criteria can change over time.</remarks>
    /// <typeparam name="TKey">The type of keys in the dictionary. Must not be null.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
    /// <param name="dict">The source dictionary to observe for changes.</param>
    /// <param name="indexName">The name of the secondary value index to use for filtering.</param>
    /// <param name="keysObservable">An observable that emits arrays of key values. When new keys are emitted, the view rebuilds its contents.</param>
    /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
    /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
    /// milliseconds.</param>
    /// <returns>A <see cref="DynamicSecondaryIndexDictionaryReactiveView{TKey, TValue, TIndexKey}"/> that reflects the filtered contents of the dictionary matching the
    /// specified secondary index keys and updates when the keys change.</returns>
    public static DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue, TIndexKey> CreateDynamicViewBySecondaryIndex<TKey, TValue, TIndexKey>(this QuaternaryDictionary<TKey, TValue> dict, string indexName, IObservable<TIndexKey[]> keysObservable, IScheduler scheduler, int throttleMs = 50)
        where TKey : notnull
        where TIndexKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dict);
        ArgumentNullException.ThrowIfNull(indexName);
        ArgumentNullException.ThrowIfNull(keysObservable);

        return new DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue, TIndexKey>(dict, indexName, keysObservable, scheduler, TimeSpan.FromMilliseconds(throttleMs));
    }

    /// <summary>
    /// Filters the cache notification stream to only include key-value pairs whose values match the specified secondary index key.
    /// </summary>
    /// <remarks>This extension method enables efficient stream filtering using a pre-configured secondary value index.
    /// The returned observable emits filtered cache notifications that only contain items whose values match the specified key.</remarks>
    /// <typeparam name="TKey">The type of keys in the dictionary. Must not be null.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
    /// <param name="stream">The source cache notification stream to filter.</param>
    /// <param name="dict">The quaternary dictionary containing the secondary value index definition.</param>
    /// <param name="indexName">The name of the secondary value index to use for filtering.</param>
    /// <param name="indexKey">The key value to filter by.</param>
    /// <returns>An observable sequence of cache notifications that only includes items whose values match the specified secondary index key.</returns>
    public static IObservable<CacheNotify<KeyValuePair<TKey, TValue>>> FilterBySecondaryIndex<TKey, TValue, TIndexKey>(this IObservable<CacheNotify<KeyValuePair<TKey, TValue>>> stream, QuaternaryDictionary<TKey, TValue> dict, string indexName, TIndexKey indexKey)
        where TKey : notnull
        where TIndexKey : notnull
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(dict);
        ArgumentNullException.ThrowIfNull(indexName);

        return stream.Select(notification =>
        {
            // Get the current set of values matching the key from the secondary index
            var matchingValues = new HashSet<TValue>(dict.GetValuesBySecondaryIndex(indexName, indexKey));

            return notification.Action switch
            {
                CacheAction.Added when notification.Item.Value != null && matchingValues.Contains(notification.Item.Value) => notification,
                CacheAction.Removed when notification.Item.Value != null && matchingValues.Contains(notification.Item.Value) => notification,
                CacheAction.BatchAdded or CacheAction.BatchRemoved or CacheAction.BatchOperation when notification.Batch != null =>
                    ReactiveListExtensions.FilterBatch(notification, dict.Where(kvp => matchingValues.Contains(kvp.Value)).Select(kvp => new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value!)).ToHashSet()),
                CacheAction.Cleared => notification,
                _ => null
            };
        }).Where(n => n != null).Select(n => n!);
    }

    /// <summary>
    /// Filters the cache notification stream to only include key-value pairs whose values match any of the specified secondary index keys.
    /// </summary>
    /// <remarks>This extension method enables efficient stream filtering using a pre-configured secondary value index
    /// with multiple keys (OR logic). The returned observable emits filtered cache notifications that only contain
    /// items whose values match any of the specified keys.</remarks>
    /// <typeparam name="TKey">The type of keys in the dictionary. Must not be null.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
    /// <param name="stream">The source cache notification stream to filter.</param>
    /// <param name="dict">The quaternary dictionary containing the secondary value index definition.</param>
    /// <param name="indexName">The name of the secondary value index to use for filtering.</param>
    /// <param name="indexKeys">The key values to filter by. Items whose values match any of these keys are included.</param>
    /// <returns>An observable sequence of cache notifications that only includes items whose values match any of the specified
    /// secondary index keys.</returns>
    public static IObservable<CacheNotify<KeyValuePair<TKey, TValue>>> FilterBySecondaryIndex<TKey, TValue, TIndexKey>(this IObservable<CacheNotify<KeyValuePair<TKey, TValue>>> stream, QuaternaryDictionary<TKey, TValue> dict, string indexName, params TIndexKey[] indexKeys)
        where TKey : notnull
        where TIndexKey : notnull
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(dict);
        ArgumentNullException.ThrowIfNull(indexName);
        ArgumentNullException.ThrowIfNull(indexKeys);

        return stream.Select(notification =>
        {
            // Get the current set of values matching all keys from the secondary index
            var matchingValues = new HashSet<TValue>(indexKeys.SelectMany(key => dict.GetValuesBySecondaryIndex(indexName, key)));

            return notification.Action switch
            {
                CacheAction.Added when notification.Item.Value != null && matchingValues.Contains(notification.Item.Value) => notification,
                CacheAction.Removed when notification.Item.Value != null && matchingValues.Contains(notification.Item.Value) => notification,
                CacheAction.BatchAdded or CacheAction.BatchRemoved or CacheAction.BatchOperation when notification.Batch != null =>
                    ReactiveListExtensions.FilterBatch(notification, dict.Where(kvp => matchingValues.Contains(kvp.Value)).Select(kvp => new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value!)).ToHashSet()),
                CacheAction.Cleared => notification,
                _ => null
            };
        }).Where(n => n != null).Select(n => n!);
    }
}
#endif
