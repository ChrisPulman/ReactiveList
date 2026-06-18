// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// <summary>Extensions for key-value cache notification streams.</summary>
    /// <typeparam name="TKey">The key type carried by the receiver.</typeparam>
    /// <typeparam name="TValue">The value type carried by the receiver.</typeparam>
    /// <param name="stream">The source sequence.</param>
    extension<TKey, TValue>(IObservable<CacheNotify<KeyValuePair<TKey, TValue>>> stream)
        where TKey : notnull
    {
        /// <summary>
        /// Filters the cache notification stream to only include key-value pairs whose values match the specified secondary index key.
        /// </summary>
        /// <remarks>This extension method enables efficient stream filtering using a pre-configured secondary value index.
        /// The returned observable emits filtered cache notifications that only contain items whose values match the specified key.</remarks>
        /// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
        /// <param name="dict">The quaternary dictionary containing the secondary value index definition.</param>
        /// <param name="indexName">The name of the secondary value index to use for filtering.</param>
        /// <param name="indexKey">The key value to filter by.</param>
        /// <returns>An observable sequence of cache notifications that only includes items whose values match the specified secondary index key.</returns>
        public IObservable<CacheNotify<KeyValuePair<TKey, TValue>>> FilterBySecondaryIndex<TIndexKey>(QuaternaryDictionary<TKey, TValue> dict, string indexName, TIndexKey indexKey)
            where TIndexKey : notnull
        {
            ThrowHelper.ThrowIfNull(stream);
            ThrowHelper.ThrowIfNull(dict);
            ThrowHelper.ThrowIfNull(indexName);

            return stream.Map(notification =>
            {
                bool Matches(KeyValuePair<TKey, TValue> item) =>
                    item.Value is not null && dict.ValueMatchesSecondaryIndex(indexName, item.Value, indexKey);

                return notification.Action switch
                {
                    CacheAction.Added when Matches(notification.Item) => notification,
                    CacheAction.Removed when Matches(notification.Item) => notification,
                    CacheAction.BatchAdded or CacheAction.BatchRemoved or CacheAction.BatchOperation when notification.Batch is not null =>
                        ReactiveListExtensions.FilterBatchByPredicate(notification, Matches),
                    CacheAction.Cleared => notification,
                    _ => null
                };
            }).Where(n => n is not null).Map(n => n!);
        }

        /// <summary>
        /// Filters the cache notification stream to only include key-value pairs whose values match any of the specified secondary index keys.
        /// </summary>
        /// <remarks>This extension method enables efficient stream filtering using a pre-configured secondary value index
        /// with multiple keys (OR logic). The returned observable emits filtered cache notifications that only contain
        /// items whose values match any of the specified keys.</remarks>
        /// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
        /// <param name="dict">The quaternary dictionary containing the secondary value index definition.</param>
        /// <param name="indexName">The name of the secondary value index to use for filtering.</param>
        /// <param name="indexKeys">The key values to filter by. Items whose values match any of these keys are included.</param>
        /// <returns>An observable sequence of cache notifications that only includes items whose values match any of the specified
        /// secondary index keys.</returns>
        public IObservable<CacheNotify<KeyValuePair<TKey, TValue>>> FilterBySecondaryIndex<TIndexKey>(QuaternaryDictionary<TKey, TValue> dict, string indexName, params TIndexKey[] indexKeys)
            where TIndexKey : notnull
        {
            ThrowHelper.ThrowIfNull(stream);
            ThrowHelper.ThrowIfNull(dict);
            ThrowHelper.ThrowIfNull(indexName);
            ThrowHelper.ThrowIfNull(indexKeys);

            return stream.Map(notification =>
            {
                bool Matches(KeyValuePair<TKey, TValue> item) =>
                    item.Value is not null && indexKeys.Any(key => dict.ValueMatchesSecondaryIndex(indexName, item.Value, key));

                return notification.Action switch
                {
                    CacheAction.Added when Matches(notification.Item) => notification,
                    CacheAction.Removed when Matches(notification.Item) => notification,
                    CacheAction.BatchAdded or CacheAction.BatchRemoved or CacheAction.BatchOperation when notification.Batch is not null =>
                        ReactiveListExtensions.FilterBatchByPredicate(notification, Matches),
                    CacheAction.Cleared => notification,
                    _ => null
                };
            }).Where(n => n is not null).Map(n => n!);
        }
    }

    /// <summary>Extensions for cache notification streams.</summary>
    /// <typeparam name="T">The item type carried by the receiver.</typeparam>
    /// <param name="stream">The source sequence.</param>
    extension<T>(IObservable<CacheNotify<T>> stream)
        where T : notnull
    {
        /// <summary>Filters the cache notification stream to only include items that match the specified secondary index key.</summary>
        /// <remarks>This extension method enables efficient stream filtering using a pre-configured secondary index.
        /// The returned observable emits filtered cache notifications that only contain items matching the specified key.
        /// This is useful for reactive pipelines where you need to filter data changes by secondary index criteria.</remarks>
        /// <typeparam name="TKey">The type of the secondary index key.</typeparam>
        /// <param name="list">The quaternary list containing the secondary index definition.</param>
        /// <param name="indexName">The name of the secondary index to use for filtering.</param>
        /// <param name="key">The key value to filter by.</param>
        /// <returns>An observable sequence of cache notifications that only includes items matching the specified secondary index key.</returns>
        public IObservable<CacheNotify<T>> FilterBySecondaryIndex<TKey>(QuaternaryList<T> list, string indexName, TKey key)
            where TKey : notnull
        {
            ThrowHelper.ThrowIfNull(stream);
            ThrowHelper.ThrowIfNull(list);
            ThrowHelper.ThrowIfNull(indexName);

            return stream.Map(notification =>
            {
                bool Matches(T item) => list.ItemMatchesSecondaryIndex(indexName, item, key);

                return notification.Action switch
                {
                    CacheAction.Added when notification.Item is not null && Matches(notification.Item) => notification,
                    CacheAction.Removed when notification.Item is not null && Matches(notification.Item) => notification,
                    CacheAction.BatchAdded or CacheAction.BatchRemoved or CacheAction.BatchOperation when notification.Batch is not null =>
                        ReactiveListExtensions.FilterBatchByPredicate(notification, Matches),
                    CacheAction.Cleared => notification,
                    _ => null
                };
            }).Where(n => n is not null).Map(n => n!);
        }

        /// <summary>Filters the cache notification stream to only include items that match any of the specified secondary index keys.</summary>
        /// <remarks>This extension method enables efficient stream filtering using a pre-configured secondary index
        /// with multiple keys (OR logic). The returned observable emits filtered cache notifications that only contain
        /// items matching any of the specified keys.</remarks>
        /// <typeparam name="TKey">The type of the secondary index key.</typeparam>
        /// <param name="list">The quaternary list containing the secondary index definition.</param>
        /// <param name="indexName">The name of the secondary index to use for filtering.</param>
        /// <param name="keys">The key values to filter by. Items matching any of these keys are included.</param>
        /// <returns>An observable sequence of cache notifications that only includes items matching any of the specified
        /// secondary index keys.</returns>
        public IObservable<CacheNotify<T>> FilterBySecondaryIndex<TKey>(QuaternaryList<T> list, string indexName, params TKey[] keys)
            where TKey : notnull
        {
            ThrowHelper.ThrowIfNull(stream);
            ThrowHelper.ThrowIfNull(list);
            ThrowHelper.ThrowIfNull(indexName);
            ThrowHelper.ThrowIfNull(keys);

            return stream.Map(notification =>
            {
                bool Matches(T item) => keys.Any(key => list.ItemMatchesSecondaryIndex(indexName, item, key));

                return notification.Action switch
                {
                    CacheAction.Added when notification.Item is not null && Matches(notification.Item) => notification,
                    CacheAction.Removed when notification.Item is not null && Matches(notification.Item) => notification,
                    CacheAction.BatchAdded or CacheAction.BatchRemoved or CacheAction.BatchOperation when notification.Batch is not null =>
                        ReactiveListExtensions.FilterBatchByPredicate(notification, Matches),
                    CacheAction.Cleared => notification,
                    _ => null
                };
            }).Where(n => n is not null).Map(n => n!);
        }
    }

    /// <summary>Extensions for quaternary dictionaries.</summary>
    /// <typeparam name="TKey">The key type carried by the receiver.</typeparam>
    /// <typeparam name="TValue">The value type carried by the receiver.</typeparam>
    /// <param name="dict">The source dictionary.</param>
    extension<TKey, TValue>(QuaternaryDictionary<TKey, TValue> dict)
        where TKey : notnull
    {
        /// <summary>Creates a reactive view filtered by a secondary value index key.</summary>
        /// <remarks>The view uses the specified secondary value index for efficient filtering. The index must have been
        /// previously added using <see cref="QuaternaryDictionary{TKey, TValue}.AddValueIndex{TIndexKey}"/>.</remarks>
        /// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
        /// <param name="indexName">The name of the secondary value index to use for filtering.</param>
        /// <param name="indexKey">The key value to filter by.</param>
        /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
        /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
        /// milliseconds.</param>
        /// <returns>A <see cref="ReactiveView{T}"/> that reflects the filtered contents of the dictionary matching the specified
        /// secondary index key.</returns>
        public ReactiveView<KeyValuePair<TKey, TValue>> CreateViewBySecondaryIndex<TIndexKey>(string indexName, TIndexKey indexKey, ISequencer scheduler, int throttleMs = 50)
            where TIndexKey : notnull
        {
            ThrowHelper.ThrowIfNull(dict);
            ThrowHelper.ThrowIfNull(indexName);

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

        /// <summary>Creates a reactive view filtered by multiple secondary value index keys.</summary>
        /// <remarks>The view includes items whose values match any of the specified keys (OR logic). The index must have
        /// been previously added using <see cref="QuaternaryDictionary{TKey, TValue}.AddValueIndex{TIndexKey}"/>.</remarks>
        /// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
        /// <param name="indexName">The name of the secondary value index to use for filtering.</param>
        /// <param name="indexKeys">The key values to filter by. Items whose values match any of these keys are included.</param>
        /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
        /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
        /// milliseconds.</param>
        /// <returns>A <see cref="ReactiveView{T}"/> that reflects the filtered contents of the dictionary matching any of the
        /// specified secondary index keys.</returns>
        public ReactiveView<KeyValuePair<TKey, TValue>> CreateViewBySecondaryIndex<TIndexKey>(string indexName, TIndexKey[] indexKeys, ISequencer scheduler, int throttleMs = 50)
            where TIndexKey : notnull
        {
            ThrowHelper.ThrowIfNull(dict);
            ThrowHelper.ThrowIfNull(indexName);
            ThrowHelper.ThrowIfNull(indexKeys);

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
        /// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
        /// <param name="indexName">The name of the secondary value index to use for filtering.</param>
        /// <param name="keysObservable">An observable that emits arrays of key values. When new keys are emitted, the view rebuilds its contents.</param>
        /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
        /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
        /// milliseconds.</param>
        /// <returns>A <see cref="DynamicSecondaryIndexDictionaryReactiveView{TKey, TValue}"/> that reflects the filtered contents of the dictionary matching the
        /// specified secondary index keys and updates when the keys change.</returns>
        public DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue> CreateDynamicViewBySecondaryIndex<TIndexKey>(string indexName, IObservable<TIndexKey[]> keysObservable, ISequencer scheduler, int throttleMs = 50)
            where TIndexKey : notnull
        {
            ThrowHelper.ThrowIfNull(dict);
            ThrowHelper.ThrowIfNull(indexName);
            ThrowHelper.ThrowIfNull(keysObservable);

            return DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue>.Create(dict, indexName, keysObservable, scheduler, TimeSpan.FromMilliseconds(throttleMs));
        }
    }

    /// <summary>Extensions for quaternary lists.</summary>
    /// <typeparam name="T">The item type carried by the receiver.</typeparam>
    /// <param name="list">The source.</param>
    extension<T>(QuaternaryList<T> list)
        where T : notnull
    {
        /// <summary>Creates a reactive view filtered by a secondary index key.</summary>
        /// <remarks>The view uses the specified secondary index for efficient filtering. The index must have been
        /// previously added using <see cref="QuaternaryList{T}.AddIndex{TKey}"/>.</remarks>
        /// <typeparam name="TKey">The type of the secondary index key.</typeparam>
        /// <param name="indexName">The name of the secondary index to use for filtering.</param>
        /// <param name="key">The key value to filter by.</param>
        /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
        /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
        /// milliseconds.</param>
        /// <returns>A <see cref="ReactiveView{T}"/> that reflects the filtered contents of the source matching the specified
        /// secondary index key.</returns>
        public ReactiveView<T> CreateViewBySecondaryIndex<TKey>(string indexName, TKey key, ISequencer scheduler, int throttleMs = 50)
            where TKey : notnull
        {
            ThrowHelper.ThrowIfNull(list);
            ThrowHelper.ThrowIfNull(indexName);

            // Get initial snapshot from the secondary index
            var snapshot = list.GetItemsBySecondaryIndex(indexName, key);

            // Create a filter that dynamically checks against the secondary index
            // This ensures new items are properly filtered based on their key value
            return new ReactiveView<T>(list.Stream, snapshot, item => list.ItemMatchesSecondaryIndex(indexName, item, key), TimeSpan.FromMilliseconds(throttleMs), scheduler);
        }

        /// <summary>Creates a reactive view filtered by multiple secondary index keys.</summary>
        /// <remarks>The view includes items that match any of the specified keys (OR logic). The index must have
        /// been previously added using <see cref="QuaternaryList{T}.AddIndex{TKey}"/>.</remarks>
        /// <typeparam name="TKey">The type of the secondary index key.</typeparam>
        /// <param name="indexName">The name of the secondary index to use for filtering.</param>
        /// <param name="keys">The key values to filter by. Items matching any of these keys are included.</param>
        /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
        /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
        /// milliseconds.</param>
        /// <returns>A <see cref="ReactiveView{T}"/> that reflects the filtered contents of the source matching any of the
        /// specified secondary index keys.</returns>
        public ReactiveView<T> CreateViewBySecondaryIndex<TKey>(string indexName, TKey[] keys, ISequencer scheduler, int throttleMs = 50)
            where TKey : notnull
        {
            ThrowHelper.ThrowIfNull(list);
            ThrowHelper.ThrowIfNull(indexName);
            ThrowHelper.ThrowIfNull(keys);

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

        /// <summary>Creates a reactive view with a dynamic secondary index key filter that rebuilds when the key observable emits new keys.</summary>
        /// <remarks>The view automatically rebuilds its contents when the key observable emits new key values.
        /// This is useful for implementing dynamic filtering where the filter criteria can change over time.</remarks>
        /// <typeparam name="TKey">The type of the secondary index key.</typeparam>
        /// <param name="indexName">The name of the secondary index to use for filtering.</param>
        /// <param name="keysObservable">An observable that emits arrays of key values. When new keys are emitted, the view rebuilds its contents.</param>
        /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
        /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
        /// milliseconds.</param>
        /// <returns>A <see cref="DynamicSecondaryIndexReactiveView{T, TKey}"/> that reflects the filtered contents of the source matching the
        /// specified secondary index keys and updates when the keys change.</returns>
        public DynamicSecondaryIndexReactiveView<T, TKey> CreateDynamicViewBySecondaryIndex<TKey>(string indexName, IObservable<TKey[]> keysObservable, ISequencer scheduler, int throttleMs = 50)
            where TKey : notnull
        {
            ThrowHelper.ThrowIfNull(list);
            ThrowHelper.ThrowIfNull(indexName);
            ThrowHelper.ThrowIfNull(keysObservable);

            return new DynamicSecondaryIndexReactiveView<T, TKey>(list, indexName, keysObservable, scheduler, TimeSpan.FromMilliseconds(throttleMs));
        }
    }
}
