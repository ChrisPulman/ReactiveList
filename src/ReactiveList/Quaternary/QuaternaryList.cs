// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Buffers;
using System.Collections;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace CP.Reactive.Quaternary;

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
/// <typeparam name="T">The type of elements stored in the list. Must be non-nullable.</typeparam>
[SkipLocalsInit]
public class QuaternaryList<T> : QuaternaryBase<T, QuadList<T>, T>, IQuaternaryList<T>
    where T : notnull
{
    private const int ParallelThreshold = 256; // Only parallelize for larger datasets

    /// <summary>
    /// Initializes a new instance of the <see cref="QuaternaryList{T}"/> class and sets up change tracking across all four underlying.
    /// quad lists.
    /// </summary>
    /// <remarks>The constructor merges the change notifications from each of the four quad lists into a
    /// single observable sequence. This allows consumers to subscribe to a unified stream of changes for the entire
    /// QuaternaryList. The merged observable is published and reference-counted to ensure efficient event propagation
    /// and resource management.</remarks>
    public QuaternaryList() => Changes = Observable.Merge(
            Quads[0].Changes,
            Quads[1].Changes,
            Quads[2].Changes,
            Quads[3].Changes)
        .Publish()
        .RefCount();

    /// <summary>
    /// Gets an observable sequence that emits change sets representing additions, removals, updates, and moves within
    /// the collection.
    /// </summary>
    /// <remarks>Subscribers receive notifications whenever the underlying collection changes. The sequence
    /// completes when the collection is disposed or no longer produces changes.</remarks>
    public IObservable<ChangeSet<T>> Changes { get; }

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
    /// Connects to the list and returns an observable of unified <see cref="CP.Reactive.ChangeSet{T}"/>.
    /// </summary>
    /// <remarks>
    /// This method provides a unified API compatible with the ReactiveList's Connect() method.
    /// Since QuaternaryList now uses the unified ChangeSet type internally, no conversion is needed.
    /// </remarks>
    /// <returns>An observable sequence of change sets representing collection modifications.</returns>
    public IObservable<CP.Reactive.ChangeSet<T>> Connect() => Changes;

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
            Quads[idx].Add(item);
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
            removed = Quads[idx].Remove(item);
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
    /// Removes all elements that match the specified predicate from the collection.
    /// </summary>
    /// <param name="predicate">A function that returns true for elements that should be removed.</param>
    /// <returns>The number of elements removed from the collection.</returns>
    public int RemoveMany(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var totalRemoved = 0;

        // Use pooled buffer for removed items
        var removedBuffer = ArrayPool<T>.Shared.Rent(64);
        var removedCount = 0;

        try
        {
            for (var i = 0; i < ShardCount; i++)
            {
                Locks[i].EnterWriteLock();
                try
                {
                    var quad = Quads[i];
                    for (var j = quad.Count - 1; j >= 0; j--)
                    {
                        var item = quad[j];
                        if (predicate(item))
                        {
                            quad.RemoveAt(j);
                            NotifyIndicesRemoved(item);

                            // Grow buffer if needed
                            if (removedCount >= removedBuffer.Length)
                            {
                                var newBuffer = ArrayPool<T>.Shared.Rent(removedBuffer.Length * 2);
                                removedBuffer.AsSpan(0, removedCount).CopyTo(newBuffer);
                                ArrayPool<T>.Shared.Return(removedBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                                removedBuffer = newBuffer;
                            }

                            removedBuffer[removedCount++] = item;
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
                EmitBatchRemoved(removedBuffer, removedCount);
            }
        }
        finally
        {
            ArrayPool<T>.Shared.Return(removedBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
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
    /// Replaces all existing items with new items atomically, emitting a single change notification.
    /// </summary>
    /// <remarks>This operation clears all existing items and adds the new items in a single atomic operation.
    /// Only one change notification is emitted for the entire operation. All indices are updated accordingly.</remarks>
    /// <param name="items">The new items to replace all existing items with. Cannot be null.</param>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var newItems = items as T[] ?? items.ToArray();

        // Acquire all locks
        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterWriteLock();
        }

        try
        {
            // Clear all shards and indices
            for (var i = 0; i < ShardCount; i++)
            {
                Quads[i].Clear();
            }

            foreach (var idx in Indices.Values)
            {
                idx.Clear();
            }

            // Add new items to appropriate shards
            if (newItems.Length > 0)
            {
                // Pre-calculate bucket assignments
                var bucketCountsArray = new int[ShardCount];
                var bucketIndicesArray = new int[ShardCount];

                for (var i = 0; i < newItems.Length; i++)
                {
                    var shardIdx = GetShardIndex(newItems[i]);
                    bucketCountsArray[shardIdx]++;
                }

                var bucketArrays = new T[ShardCount][];
                for (var i = 0; i < ShardCount; i++)
                {
                    bucketArrays[i] = bucketCountsArray[i] > 0 ? ArrayPool<T>.Shared.Rent(bucketCountsArray[i]) : Array.Empty<T>();
                }

                try
                {
                    for (var i = 0; i < newItems.Length; i++)
                    {
                        var item = newItems[i];
                        var shardIdx = GetShardIndex(item);
                        bucketArrays[shardIdx][bucketIndicesArray[shardIdx]++] = item;
                    }

                    for (var sIdx = 0; sIdx < ShardCount; sIdx++)
                    {
                        var bucketCount = bucketCountsArray[sIdx];
                        if (bucketCount == 0)
                        {
                            continue;
                        }

                        var bucket = bucketArrays[sIdx].AsSpan(0, bucketCount);
                        Quads[sIdx].AddRange(bucket);

                        if (!Indices.IsEmpty)
                        {
                            for (var i = 0; i < bucketCount; i++)
                            {
                                NotifyIndicesAdded(bucket[i]);
                            }
                        }
                    }
                }
                finally
                {
                    for (var i = 0; i < ShardCount; i++)
                    {
                        if (bucketCountsArray[i] > 0)
                        {
                            ArrayPool<T>.Shared.Return(bucketArrays[i], clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                        }
                    }
                }
            }
        }
        finally
        {
            for (var i = ShardCount - 1; i >= 0; i--)
            {
                Locks[i].ExitWriteLock();
            }
        }

        // Emit single batch notification
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
                var quad = Quads[i];
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

        Indices[name] = index;
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
        if (Indices.TryGetValue(indexName, out var idx) && idx is SecondaryIndex<T, TKey> typedIdx)
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
        if (Indices.TryGetValue(indexName, out var idx))
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
            var span = Quads[idx].AsSpan();
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
                var sourceSpan = Quads[i].AsSpan();
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
    public override IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterReadLock();
            try
            {
                foreach (var item in Quads[i])
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
    /// Creates a snapshot of the current items as a read-only list.
    /// </summary>
    /// <returns>A read-only list containing the items at the time of the call. The returned list is independent of subsequent
    /// changes to the original collection.</returns>
    public IReadOnlyList<T> Snapshot()
    {
        var total = Quads.Sum(q => q.Count);
        var result = new T[total];

        var offset = 0;
        foreach (var quad in Quads)
        {
            quad.CopyTo(result, offset);
            offset += quad.Count;
        }

        return result;
    }

    /// <summary>
    /// Calculates the shard index for the specified item based on its hash code.
    /// </summary>
    /// <remarks>The shard index is determined by applying an optimized hash function using the golden ratio
    /// for better distribution. If the item is null, a default hash code of 0 is used. The result is always a
    /// non-negative integer less than ShardCount (0-3).</remarks>
    /// <param name="item">The item for which to compute the shard index. Can be null.</param>
    /// <returns>An integer representing the zero-based index of the shard to which the item is assigned.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetShardIndex(T? item)
    {
        // Use golden ratio multiplication for better hash distribution
        // Then shift right by 30 bits to get 2 bits (0-3) for 4 shards
        var hash = item?.GetHashCode() ?? 0;
        return (int)((uint)(hash * 0x9E3779B9) >> 30);
    }

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
                var quadCount = Quads[i].Count;
                if (index < quadCount)
                {
                    return Quads[i][index];
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
                        var quad = Quads[sIdx];
                        quad.AddRange(bucket);

                        if (!Indices.IsEmpty)
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
                        var quad = Quads[sIdx];
                        quad.AddRange(bucket);

                        if (!Indices.IsEmpty)
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

            EmitBatchAddedDirect(items, count);
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
                        var quad = Quads[sIdx];
                        quad.AddRange(bucket);

                        if (!Indices.IsEmpty)
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
                        var quad = Quads[sIdx];
                        quad.AddRange(bucket);

                        if (!Indices.IsEmpty)
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

            EmitBatchAddedFromList(items, count);
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
                        var quad = Quads[sIdx];
                        var hasIndices = !Indices.IsEmpty;
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
                        var quad = Quads[sIdx];
                        var hasIndices = !Indices.IsEmpty;
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

    /// <summary>
    /// Removes the specified collection of items from the data structure in a batch operation.
    /// </summary>
    /// <remarks>This method performs removals in a sharded and potentially parallelized manner for improved
    /// performance with large collections. Removal notifications are triggered for each item that is successfully
    /// removed. The operation is thread-safe.</remarks>
    /// <param name="items">The list of items to remove. Cannot be null. Items that are not present in the data structure are ignored.</param>
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
                        var quad = Quads[sIdx];
                        var hasIndices = !Indices.IsEmpty;
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
                        var quad = Quads[sIdx];
                        var hasIndices = !Indices.IsEmpty;
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
            _hasIndices = !parent.Indices.IsEmpty;
        }

        /// <summary>
        /// Gets the total number of items contained in all shards.
        /// </summary>
        public int Count
        {
            get
            {
                var count = 0;
                for (var i = 0; i < ShardCount; i++)
                {
                    count += _parent.Quads[i].Count;
                }

                return count;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the collection is read-only.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Gets the element at the specified index across all shards.
        /// </summary>
        /// <remarks>This indexer provides read-only access to elements as if the sharded collection were
        /// a single contiguous list. Setting elements by index is not supported and will throw an exception.</remarks>
        /// <param name="index">The zero-based index of the element to get. Must be greater than or equal to 0 and less than the total
        /// number of elements in all shards.</param>
        /// <returns>The element at the specified index in the combined sharded collection.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="index"/> is less than 0 or greater than or equal to the total number of elements
        /// in all shards.</exception>
        /// <exception cref="NotSupportedException">Thrown when attempting to set an element by index, as direct replacement is not supported in the sharded
        /// list.</exception>
        public T this[int index]
        {
            get
            {
                for (var i = 0; i < ShardCount; i++)
                {
                    var quadCount = _parent.Quads[i].Count;
                    if (index < quadCount)
                    {
                        return _parent.Quads[i][index];
                    }

                    index -= quadCount;
                }

                throw new IndexOutOfRangeException();
            }

            set => throw new NotSupportedException("Direct index replacement in sharded list is unstable.");
        }

        /// <summary>
        /// Adds the specified item to the collection.
        /// </summary>
        /// <param name="item">The item to add to the collection.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            var idx = GetShardIndex(item);
            _parent.Quads[idx].Add(item);
            if (_hasIndices)
            {
                _parent.NotifyIndicesAdded(item);
            }
        }

        /// <summary>
        /// Adds the elements of the specified collection to the end of the collection.
        /// </summary>
        /// <remarks>The order of the elements in the input collection is preserved. If any element in the
        /// input collection is invalid for the collection, an exception may be thrown when adding that
        /// element.</remarks>
        /// <param name="items">The collection whose elements should be added to the end of the collection. Cannot be null.</param>
        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }

        /// <summary>
        /// Removes the specified item from the collection.
        /// </summary>
        /// <param name="item">The item to remove from the collection.</param>
        /// <returns>true if the item was successfully removed; otherwise, false.</returns>
        public bool Remove(T item)
        {
            var idx = GetShardIndex(item);
            if (_parent.Quads[idx].Remove(item))
            {
                if (_hasIndices)
                {
                    _parent.NotifyIndicesRemoved(item);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes all items from the collection, including all shards and associated indices.
        /// </summary>
        /// <remarks>After calling this method, the collection will be empty and any indices will also be
        /// cleared. This operation affects all shards managed by the parent object.</remarks>
        public void Clear()
        {
            for (var i = 0; i < ShardCount; i++)
            {
                _parent.Quads[i].Clear();
            }

            if (_hasIndices)
            {
                foreach (var idx in _parent.Indices.Values)
                {
                    idx.Clear();
                }
            }
        }

        /// <summary>
        /// Determines whether the collection contains a specific value.
        /// </summary>
        /// <param name="item">The value to locate in the collection.</param>
        /// <returns>true if the item is found in the collection; otherwise, false.</returns>
        public bool Contains(T item)
        {
            var idx = GetShardIndex(item);
            return _parent.Quads[idx].Contains(item);
        }

        /// <summary>
        /// Copies the elements of the collection to the specified array, starting at the specified array index.
        /// </summary>
        /// <remarks>The destination array must be large enough to contain all the elements of the
        /// collection starting at the specified index. If the array is not large enough, an exception will be
        /// thrown.</remarks>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from the collection. The array must
        /// have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in the destination array at which copying begins.</param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            for (var i = 0; i < ShardCount; i++)
            {
                var quad = _parent.Quads[i];
                quad.CopyTo(array, arrayIndex);
                arrayIndex += quad.Count;
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection of items contained in all shards.
        /// </summary>
        /// <remarks>The enumeration traverses the items in each shard in order, starting from the first
        /// shard to the last. The order of items within each shard is preserved.</remarks>
        /// <returns>An enumerator that can be used to iterate through the items in the collection.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < ShardCount; i++)
            {
                foreach (var item in _parent.Quads[i])
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
#endif
