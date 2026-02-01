// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CP.Reactive.Quaternary;

namespace CP.Reactive;

/// <summary>
/// Represents a high-performance, thread-safe list that partitions its elements across four internal shards for
/// efficient concurrent access and batch operations. Supports secondary indexing for fast lookups by custom keys.
/// </summary>
/// <remarks>QuaternaryList{T} is optimized for scenarios involving frequent additions, removals, and batch
/// operations on large datasets. Internally, items are distributed across four shards to reduce contention and improve
/// parallelism. The collection is not read-only and supports dynamic growth. Secondary indices can be added to enable
/// efficient lookups by arbitrary keys, which is useful for advanced querying scenarios. All public methods are
/// thread-safe. Direct index-based operations (such as Insert, RemoveAt, or setting by index) are not supported and
/// will throw exceptions; use Add, Remove, or batch methods instead.</remarks>
/// <typeparam name="T">The type of elements stored in the list.</typeparam>
public class QuaternaryList<T> : QuaternaryBase<T>, IQuaternaryList<T>
{
    private const int ParallelThreshold = 256; // Only parallelize for larger datasets

    private readonly QuadList<T>[] _quads =
    [
        new QuadList<T>(),
        new QuadList<T>(),
        new QuadList<T>(),
        new QuadList<T>()
    ];

    private readonly ConcurrentDictionary<string, ISecondaryIndex<T>> _indices = new();

    /// <summary>
    /// Gets the total number of items contained in all quads.
    /// </summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var count = 0;
            for (var i = 0; i < ShardCount; i++)
            {
                Locks[i].EnterReadLock();
                try
                {
                    count += _quads[i].Count;
                }
                finally
                {
                    Locks[i].ExitReadLock();
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the collection is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <returns>The element at the specified index.</returns>
    /// <exception cref="NotSupportedException">Setting an element by index is not supported.</exception>
    public T this[int index]
    {
        get => GetAtGlobalIndex(index);
        set => throw new NotSupportedException("Direct index replacement in sharded list is unstable. Use Remove/Add.");
    }

    /// <summary>
    /// Adds the specified item to the collection.
    /// </summary>
    /// <param name="item">The item to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        var idx = GetShardIndex(item);
        Locks[idx].EnterWriteLock();
        try
        {
            _quads[idx].Add(item);
            NotifyIndicesAdded(item);
        }
        finally
        {
            Locks[idx].ExitWriteLock();
        }

        Emit(CacheAction.Added, item);
    }

    /// <summary>
    /// Removes the specified item from the collection.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>true if the item was removed; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(T item)
    {
        var idx = GetShardIndex(item);
        bool removed;
        Locks[idx].EnterWriteLock();
        try
        {
            removed = _quads[idx].Remove(item);
            if (removed)
            {
                NotifyIndicesRemoved(item);
            }
        }
        finally
        {
            Locks[idx].ExitWriteLock();
        }

        if (removed)
        {
            Emit(CacheAction.Removed, item);
        }

        return removed;
    }

    /// <summary>
    /// Adds the elements of the specified collection to the current set.
    /// </summary>
    /// <param name="collection">The collection of elements to add.</param>
    public void AddRange(IEnumerable<T> collection)
    {
        // Fast path for arrays
        if (collection is T[] array)
        {
            AddRangeCore(array);
            return;
        }

        // Fast path for IList<T>
        if (collection is IList<T> list)
        {
            AddRangeCore(list);
            return;
        }

        // Slow path: materialize
        AddRangeCore(collection.ToArray());
    }

    /// <summary>
    /// Removes all elements in the specified collection from the current set.
    /// </summary>
    /// <param name="collection">The collection of elements to remove.</param>
    public void RemoveRange(IEnumerable<T> collection)
    {
        // Fast path for arrays
        if (collection is T[] array)
        {
            RemoveRangeCore(array);
            return;
        }

        // Fast path for IList<T>
        if (collection is IList<T> list)
        {
            RemoveRangeCore(list);
            return;
        }

        // Slow path: materialize
        RemoveRangeCore(collection.ToArray());
    }

    /// <summary>
    /// Removes all items from the cache.
    /// </summary>
    public void Clear()
    {
        // Acquire all locks first to ensure consistency
        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterWriteLock();
        }

        try
        {
            for (var i = 0; i < ShardCount; i++)
            {
                _quads[i].Clear();
            }
        }
        finally
        {
            for (var i = ShardCount - 1; i >= 0; i--)
            {
                Locks[i].ExitWriteLock();
            }
        }

        // Clear indices outside of locks
        if (!_indices.IsEmpty)
        {
            foreach (var idx in _indices.Values)
            {
                idx.Clear();
            }
        }

        Emit(CacheAction.Cleared, default);
    }

    /// <summary>
    /// Removes all elements that match the specified predicate from the collection.
    /// </summary>
    /// <param name="predicate">A function that returns true for elements that should be removed.</param>
    /// <returns>The number of elements removed from the collection.</returns>
    public int RemoveMany(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var totalRemoved = 0;
        var removedItems = new List<T>();

        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterWriteLock();
            try
            {
                var quad = _quads[i];
                for (var j = quad.Count - 1; j >= 0; j--)
                {
                    var item = quad[j];
                    if (predicate(item))
                    {
                        quad.RemoveAt(j);
                        NotifyIndicesRemoved(item);
                        removedItems.Add(item);
                        totalRemoved++;
                    }
                }
            }
            finally
            {
                Locks[i].ExitWriteLock();
            }
        }

        if (totalRemoved > 0)
        {
            EmitBatchRemovedFromList(removedItems, removedItems.Count);
        }

        return totalRemoved;
    }

    /// <summary>
    /// Performs a batch edit operation on the collection, ensuring only a single change notification is emitted.
    /// </summary>
    /// <param name="editAction">An action that receives an editable list interface to perform modifications.</param>
    public void Edit(Action<ICollection<T>> editAction)
    {
        ArgumentNullException.ThrowIfNull(editAction);

        // Acquire all locks for the edit operation
        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterWriteLock();
        }

        try
        {
            var wrapper = new QuaternaryEditWrapper(this);
            editAction(wrapper);
        }
        finally
        {
            for (var i = ShardCount - 1; i >= 0; i--)
            {
                Locks[i].ExitWriteLock();
            }
        }

        // Emit single batch notification after all changes
        Emit(CacheAction.BatchOperation, default);
    }

    /// <summary>
    /// Adds a secondary index to enable efficient lookups based on a specified key selector.
    /// </summary>
    /// <typeparam name="TKey">The type of the key used for indexing.</typeparam>
    /// <param name="name">The unique name of the index to add.</param>
    /// <param name="keySelector">A function that extracts the key from each item for indexing.</param>
    public void AddIndex<TKey>(string name, Func<T, TKey> keySelector)
        where TKey : notnull
    {
        var index = new SecondaryIndex<T, TKey>(keySelector);

        // Populate existing data
        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterReadLock();
            try
            {
                var quad = _quads[i];
                foreach (var item in quad)
                {
                    index.OnAdded(item);
                }
            }
            finally
            {
                Locks[i].ExitReadLock();
            }
        }

        _indices[name] = index;
    }

    /// <summary>
    /// Retrieves all entities of type T that match the specified key in the given secondary index.
    /// </summary>
    /// <typeparam name="TKey">The type of the key used to query the secondary index.</typeparam>
    /// <param name="indexName">The name of the secondary index to query.</param>
    /// <param name="key">The key value to search for within the specified index.</param>
    /// <returns>An enumerable collection of entities that match the specified key.</returns>
    public IEnumerable<T> GetItemsBySecondaryIndex<TKey>(string indexName, TKey key)
        where TKey : notnull
    {
        if (_indices.TryGetValue(indexName, out var idx) && idx is SecondaryIndex<T, TKey> typedIdx)
        {
            return typedIdx.Lookup(key);
        }

        return Array.Empty<T>();
    }

    /// <summary>
    /// Determines whether the specified item matches the given key in the specified secondary index.
    /// </summary>
    /// <typeparam name="TKey">The type of the key used in the secondary index.</typeparam>
    /// <param name="indexName">The name of the secondary index to use for matching.</param>
    /// <param name="item">The item to check.</param>
    /// <param name="key">The key value to match against.</param>
    /// <returns><see langword="true"/> if the item's indexed value matches the specified key; otherwise, <see langword="false"/>.</returns>
    public bool ItemMatchesSecondaryIndex<TKey>(string indexName, T item, TKey key)
        where TKey : notnull
    {
        if (_indices.TryGetValue(indexName, out var idx))
        {
            return idx.MatchesKey(item, key);
        }

        return false;
    }

    /// <summary>
    /// Determines whether the collection contains a specific value.
    /// </summary>
    /// <param name="item">The value to locate.</param>
    /// <returns>true if the item is found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item)
    {
        var idx = GetShardIndex(item);
        Locks[idx].EnterReadLock();
        try
        {
            // Use Span for faster iteration without enumerator allocation
            var span = _quads[idx].AsSpan();
            var comparer = EqualityComparer<T>.Default;
            for (var i = 0; i < span.Length; i++)
            {
                if (comparer.Equals(span[i], item))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            Locks[idx].ExitReadLock();
        }
    }

    /// <summary>
    /// Copies the elements of the collection to an array.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The index at which to start copying.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterReadLock();
            try
            {
                var sourceSpan = _quads[i].AsSpan();
                var destSpan = array.AsSpan(arrayIndex, sourceSpan.Length);
                sourceSpan.CopyTo(destSpan);
                arrayIndex += sourceSpan.Length;
            }
            finally
            {
                Locks[i].ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator for the collection.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterReadLock();
            try
            {
                foreach (var item in _quads[i])
                {
                    yield return item;
                }
            }
            finally
            {
                Locks[i].ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An <see cref="IEnumerator"/> that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Calculates the shard index for the specified item based on its hash code.
    /// </summary>
    /// <remarks>The shard index is determined by applying a hash function to the item. If the item is null, a
    /// default hash code of 0 is used. The result is always a non-negative integer less than ShardCount.</remarks>
    /// <param name="item">The item for which to compute the shard index. Can be null.</param>
    /// <returns>An integer representing the zero-based index of the shard to which the item is assigned.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetShardIndex(T? item) => ((item?.GetHashCode() ?? 0) & 0x7FFFFFFF) & (ShardCount - 1);

    /// <summary>
    /// Retrieves the element at the specified global index across all shards.
    /// </summary>
    /// <param name="index">The zero-based global index of the element to retrieve. Must be greater than or equal to 0 and less than the
    /// total number of elements.</param>
    /// <returns>The element of type T located at the specified global index.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when the specified index is less than 0 or greater than or equal to the total number of elements.</exception>
    private T GetAtGlobalIndex(int index)
    {
        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterReadLock();
            try
            {
                var quadCount = _quads[i].Count;
                if (index < quadCount)
                {
                    return _quads[i][index];
                }

                index -= quadCount;
            }
            finally
            {
                Locks[i].ExitReadLock();
            }
        }

        throw new IndexOutOfRangeException();
    }

    /// <summary>
    /// Notifies all registered indices that a new item has been added.
    /// </summary>
    /// <param name="item">The item that was added and should be communicated to all indices.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyIndicesAdded(T item)
    {
        if (_indices.IsEmpty)
        {
            return;
        }

        foreach (var kvp in _indices)
        {
            kvp.Value.OnAdded(item);
        }
    }

    /// <summary>
    /// Notifies all registered index listeners that the specified item has been removed.
    /// </summary>
    /// <param name="item">The item that was removed and for which index listeners should be notified.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyIndicesRemoved(T item)
    {
        if (_indices.IsEmpty)
        {
            return;
        }

        foreach (var kvp in _indices)
        {
            kvp.Value.OnRemoved(item);
        }
    }

    /// <summary>
    /// Adds the specified array of items to the collection, distributing them across internal shards as appropriate.
    /// </summary>
    /// <remarks>This method optimizes insertion by batching items and distributing them to shards in parallel
    /// when the number of items exceeds a predefined threshold. For smaller batches, insertion is performed
    /// sequentially. The method is intended for internal use and does not perform input validation; callers must ensure
    /// that the input array is not null.</remarks>
    /// <param name="items">The array of items to add to the collection. Cannot be null. Items are assigned to shards based on their shard
    /// index.</param>
    private void AddRangeCore(T[] items)
    {
        var count = items.Length;
        if (count == 0)
        {
            return;
        }

        // Use stack-allocated spans for small counts to avoid heap allocations (non-parallel path)
        // For parallel path, copy to array since Span cannot be captured in lambdas
        var bucketCountsArray = new int[ShardCount];
        var bucketIndicesArray = new int[ShardCount];
        var itemsSpan = items.AsSpan();

        for (var i = 0; i < count; i++)
        {
            var shardIdx = GetShardIndex(itemsSpan[i]);
            bucketCountsArray[shardIdx]++;
        }

        var bucketArrays = new T[ShardCount][];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCountsArray[i] > 0 ? ArrayPool<T>.Shared.Rent(bucketCountsArray[i]) : Array.Empty<T>();
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var item = itemsSpan[i];
                var shardIdx = GetShardIndex(item);
                bucketArrays[shardIdx][bucketIndicesArray[shardIdx]++] = item;
            }

            // Insert into shards - use parallel only for large datasets
            if (count >= ParallelThreshold)
            {
                Parallel.For(0, ShardCount, sIdx =>
                {
                    var bucketCount = bucketCountsArray[sIdx];
                    if (bucketCount == 0)
                    {
                        return;
                    }

                    var bucket = bucketArrays[sIdx].AsSpan(0, bucketCount);
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var quad = _quads[sIdx];
                        quad.AddRange(bucket);

                        if (!_indices.IsEmpty)
                        {
                            for (var i = 0; i < bucketCount; i++)
                            {
                                NotifyIndicesAdded(bucket[i]);
                            }
                        }
                    }
                    finally
                    {
                        Locks[sIdx].ExitWriteLock();
                    }
                });
            }
            else
            {
                for (var sIdx = 0; sIdx < ShardCount; sIdx++)
                {
                    var bucketCount = bucketCountsArray[sIdx];
                    if (bucketCount == 0)
                    {
                        continue;
                    }

                    var bucket = bucketArrays[sIdx].AsSpan(0, bucketCount);
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var quad = _quads[sIdx];
                        quad.AddRange(bucket);

                        if (!_indices.IsEmpty)
                        {
                            for (var i = 0; i < bucketCount; i++)
                            {
                                NotifyIndicesAdded(bucket[i]);
                            }
                        }
                    }
                    finally
                    {
                        Locks[sIdx].ExitWriteLock();
                    }
                }
            }

            EmitBatchDirect(items, count);
        }
        finally
        {
            for (var i = 0; i < ShardCount; i++)
            {
                if (bucketCountsArray[i] > 0)
                {
                    ArrayPool<T>.Shared.Return(bucketArrays[i], clearArray: true);
                }
            }
        }
    }

    /// <summary>
    /// Adds the elements of the specified list to the collection, distributing them across internal shards as
    /// appropriate.
    /// </summary>
    /// <remarks>This method optimizes batch addition by grouping items per shard and may perform the
    /// operation in parallel if the number of items meets a configured threshold. The method is not thread-safe and
    /// should be called only when appropriate synchronization is ensured by the caller.</remarks>
    /// <param name="items">The list of items to add to the collection. Cannot be null. Items are assigned to shards based on their shard
    /// index.</param>
    private void AddRangeCore(IList<T> items)
    {
        var count = items.Count;
        if (count == 0)
        {
            return;
        }

        var bucketCountsArray = new int[ShardCount];
        var bucketIndicesArray = new int[ShardCount];

        for (var i = 0; i < count; i++)
        {
            var shardIdx = GetShardIndex(items[i]);
            bucketCountsArray[shardIdx]++;
        }

        var bucketArrays = new T[ShardCount][];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCountsArray[i] > 0 ? ArrayPool<T>.Shared.Rent(bucketCountsArray[i]) : Array.Empty<T>();
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var item = items[i];
                var shardIdx = GetShardIndex(item);
                bucketArrays[shardIdx][bucketIndicesArray[shardIdx]++] = item;
            }

            if (count >= ParallelThreshold)
            {
                Parallel.For(0, ShardCount, sIdx =>
                {
                    var bucketCount = bucketCountsArray[sIdx];
                    if (bucketCount == 0)
                    {
                        return;
                    }

                    var bucket = bucketArrays[sIdx].AsSpan(0, bucketCount);
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var quad = _quads[sIdx];
                        quad.AddRange(bucket);

                        if (!_indices.IsEmpty)
                        {
                            for (var i = 0; i < bucketCount; i++)
                            {
                                NotifyIndicesAdded(bucket[i]);
                            }
                        }
                    }
                    finally
                    {
                        Locks[sIdx].ExitWriteLock();
                    }
                });
            }
            else
            {
                for (var sIdx = 0; sIdx < ShardCount; sIdx++)
                {
                    var bucketCount = bucketCountsArray[sIdx];
                    if (bucketCount == 0)
                    {
                        continue;
                    }

                    var bucket = bucketArrays[sIdx].AsSpan(0, bucketCount);
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var quad = _quads[sIdx];
                        quad.AddRange(bucket);

                        if (!_indices.IsEmpty)
                        {
                            for (var i = 0; i < bucketCount; i++)
                            {
                                NotifyIndicesAdded(bucket[i]);
                            }
                        }
                    }
                    finally
                    {
                        Locks[sIdx].ExitWriteLock();
                    }
                }
            }

            EmitBatchFromList(items, count);
        }
        finally
        {
            for (var i = 0; i < ShardCount; i++)
            {
                if (bucketCountsArray[i] > 0)
                {
                    ArrayPool<T>.Shared.Return(bucketArrays[i], clearArray: true);
                }
            }
        }
    }

    /// <summary>
    /// Removes the specified items from the collection, processing them in batches for efficiency.
    /// </summary>
    /// <remarks>This method distributes removal operations across internal shards and may perform removals in
    /// parallel for large input arrays to improve performance. Removal is performed in batch, and any associated
    /// indices are updated accordingly. The method is not thread-safe and should be called only when appropriate
    /// synchronization is ensured by the caller.</remarks>
    /// <param name="items">An array of items to remove from the collection. The array must not be null, but may be empty.</param>
    private void RemoveRangeCore(T[] items)
    {
        var count = items.Length;
        if (count == 0)
        {
            return;
        }

        var bucketCountsArray = new int[ShardCount];
        var bucketIndicesArray = new int[ShardCount];
        var itemsSpan = items.AsSpan();

        for (var i = 0; i < count; i++)
        {
            var shardIdx = GetShardIndex(itemsSpan[i]);
            bucketCountsArray[shardIdx]++;
        }

        var bucketArrays = new T[ShardCount][];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCountsArray[i] > 0 ? ArrayPool<T>.Shared.Rent(bucketCountsArray[i]) : Array.Empty<T>();
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var item = itemsSpan[i];
                var shardIdx = GetShardIndex(item);
                bucketArrays[shardIdx][bucketIndicesArray[shardIdx]++] = item;
            }

            if (count >= ParallelThreshold)
            {
                Parallel.For(0, ShardCount, sIdx =>
                {
                    var bucketCount = bucketCountsArray[sIdx];
                    if (bucketCount == 0)
                    {
                        return;
                    }

                    var bucket = bucketArrays[sIdx].AsSpan(0, bucketCount);
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var quad = _quads[sIdx];
                        var hasIndices = !_indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var item = bucket[i];
                            if (quad.Remove(item) && hasIndices)
                            {
                                NotifyIndicesRemoved(item);
                            }
                        }
                    }
                    finally
                    {
                        Locks[sIdx].ExitWriteLock();
                    }
                });
            }
            else
            {
                for (var sIdx = 0; sIdx < ShardCount; sIdx++)
                {
                    var bucketCount = bucketCountsArray[sIdx];
                    if (bucketCount == 0)
                    {
                        continue;
                    }

                    var bucket = bucketArrays[sIdx].AsSpan(0, bucketCount);
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var quad = _quads[sIdx];
                        var hasIndices = !_indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var item = bucket[i];
                            if (quad.Remove(item) && hasIndices)
                            {
                                NotifyIndicesRemoved(item);
                            }
                        }
                    }
                    finally
                    {
                        Locks[sIdx].ExitWriteLock();
                    }
                }
            }

            EmitBatchRemoved(items, count);
        }
        finally
        {
            for (var i = 0; i < ShardCount; i++)
            {
                if (bucketCountsArray[i] > 0)
                {
                    ArrayPool<T>.Shared.Return(bucketArrays[i], clearArray: true);
                }
            }
        }
    }

    private void RemoveRangeCore(IList<T> items)
    {
        var count = items.Count;
        if (count == 0)
        {
            return;
        }

        var bucketCountsArray = new int[ShardCount];
        var bucketIndicesArray = new int[ShardCount];

        for (var i = 0; i < count; i++)
        {
            var shardIdx = GetShardIndex(items[i]);
            bucketCountsArray[shardIdx]++;
        }

        var bucketArrays = new T[ShardCount][];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCountsArray[i] > 0 ? ArrayPool<T>.Shared.Rent(bucketCountsArray[i]) : Array.Empty<T>();
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var item = items[i];
                var shardIdx = GetShardIndex(item);
                bucketArrays[shardIdx][bucketIndicesArray[shardIdx]++] = item;
            }

            if (count >= ParallelThreshold)
            {
                Parallel.For(0, ShardCount, sIdx =>
                {
                    var bucketCount = bucketCountsArray[sIdx];
                    if (bucketCount == 0)
                    {
                        return;
                    }

                    var bucket = bucketArrays[sIdx].AsSpan(0, bucketCount);
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var quad = _quads[sIdx];
                        var hasIndices = !_indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var item = bucket[i];
                            if (quad.Remove(item) && hasIndices)
                            {
                                NotifyIndicesRemoved(item);
                            }
                        }
                    }
                    finally
                    {
                        Locks[sIdx].ExitWriteLock();
                    }
                });
            }
            else
            {
                for (var sIdx = 0; sIdx < ShardCount; sIdx++)
                {
                    var bucketCount = bucketCountsArray[sIdx];
                    if (bucketCount == 0)
                    {
                        continue;
                    }

                    var bucket = bucketArrays[sIdx].AsSpan(0, bucketCount);
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var quad = _quads[sIdx];
                        var hasIndices = !_indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var item = bucket[i];
                            if (quad.Remove(item) && hasIndices)
                            {
                                NotifyIndicesRemoved(item);
                            }
                        }
                    }
                    finally
                    {
                        Locks[sIdx].ExitWriteLock();
                    }
                }
            }

            EmitBatchRemovedFromList(items, count);
        }
        finally
        {
            for (var i = 0; i < ShardCount; i++)
            {
                if (bucketCountsArray[i] > 0)
                {
                    ArrayPool<T>.Shared.Return(bucketArrays[i], clearArray: true);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitBatchDirect(T[] items, int count)
    {
        var pool = ArrayPool<T>.Shared.Rent(count);
        Array.Copy(items, pool, count);
        Emit(CacheAction.BatchAdded, default, new PooledBatch<T>(pool, count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitBatchFromList(IList<T> items, int count)
    {
        var pool = ArrayPool<T>.Shared.Rent(count);
        for (var i = 0; i < count; i++)
        {
            pool[i] = items[i];
        }

        Emit(CacheAction.BatchAdded, default, new PooledBatch<T>(pool, count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitBatchRemoved(T[] items, int count)
    {
        var pool = ArrayPool<T>.Shared.Rent(count);
        Array.Copy(items, pool, count);
        Emit(CacheAction.BatchRemoved, default, new PooledBatch<T>(pool, count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitBatchRemovedFromList(IList<T> items, int count)
    {
        var pool = ArrayPool<T>.Shared.Rent(count);
        for (var i = 0; i < count; i++)
        {
            pool[i] = items[i];
        }

        Emit(CacheAction.BatchRemoved, default, new PooledBatch<T>(pool, count));
    }

    /// <summary>
    /// Internal wrapper for Edit operations that bypasses locking and notifications.
    /// </summary>
    private sealed class QuaternaryEditWrapper : ICollection<T>
    {
        private readonly QuaternaryList<T> _parent;
        private readonly bool _hasIndices;

        internal QuaternaryEditWrapper(QuaternaryList<T> parent)
        {
            _parent = parent;
            _hasIndices = !parent._indices.IsEmpty;
        }

        public int Count
        {
            get
            {
                var count = 0;
                for (var i = 0; i < ShardCount; i++)
                {
                    count += _parent._quads[i].Count;
                }

                return count;
            }
        }

        public bool IsReadOnly => false;

        public T this[int index]
        {
            get
            {
                for (var i = 0; i < ShardCount; i++)
                {
                    var quadCount = _parent._quads[i].Count;
                    if (index < quadCount)
                    {
                        return _parent._quads[i][index];
                    }

                    index -= quadCount;
                }

                throw new IndexOutOfRangeException();
            }

            set => throw new NotSupportedException("Direct index replacement in sharded list is unstable.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            var idx = GetShardIndex(item);
            _parent._quads[idx].Add(item);
            if (_hasIndices)
            {
                _parent.NotifyIndicesAdded(item);
            }
        }

        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }

        public bool Remove(T item)
        {
            var idx = GetShardIndex(item);
            if (_parent._quads[idx].Remove(item))
            {
                if (_hasIndices)
                {
                    _parent.NotifyIndicesRemoved(item);
                }

                return true;
            }

            return false;
        }

        public void Clear()
        {
            for (var i = 0; i < ShardCount; i++)
            {
                _parent._quads[i].Clear();
            }

            if (_hasIndices)
            {
                foreach (var idx in _parent._indices.Values)
                {
                    idx.Clear();
                }
            }
        }

        public bool Contains(T item)
        {
            var idx = GetShardIndex(item);
            return _parent._quads[idx].Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            for (var i = 0; i < ShardCount; i++)
            {
                var quad = _parent._quads[i];
                quad.CopyTo(array, arrayIndex);
                arrayIndex += quad.Count;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < ShardCount; i++)
            {
                foreach (var item in _parent._quads[i])
                {
                    yield return item;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
#endif
