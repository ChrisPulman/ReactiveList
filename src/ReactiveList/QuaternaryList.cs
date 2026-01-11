// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET6_0_OR_GREATER

using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CP.Reactive;

/// <summary>
/// Represents a thread-safe, sharded list that partitions elements across four internal lists to improve concurrency
/// and scalability.
/// </summary>
/// <typeparam name="T">The type of elements stored in the list.</typeparam>
public class QuaternaryList<T> : QuaternaryBase<T>, IQuaternaryList<T>
{
    private const int ParallelThreshold = 256; // Only parallelize for larger datasets

    private readonly List<T>[] _quads =
    [
        new List<T>(),
        new List<T>(),
        new List<T>(),
        new List<T>()
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
    public IEnumerable<T> Query<TKey>(string indexName, TKey key)
        where TKey : notnull
    {
        if (_indices.TryGetValue(indexName, out var idx) && idx is SecondaryIndex<T, TKey> typedIdx)
        {
            return typedIdx.Lookup(key);
        }

        return Array.Empty<T>();
    }

    /// <summary>
    /// Returns the zero-based index of the first occurrence of the specified item.
    /// </summary>
    /// <param name="item">The item to locate.</param>
    /// <returns>The index of the item, or -1 if not found.</returns>
    /// <exception cref="NotSupportedException">This method is not supported.</exception>
    public int IndexOf(T item) => throw new NotSupportedException("Global IndexOf is slow. Use Contains.");

    /// <summary>
    /// Inserts an item at the specified index.
    /// </summary>
    /// <param name="index">The index at which to insert.</param>
    /// <param name="item">The item to insert.</param>
    /// <exception cref="NotSupportedException">This method is not supported.</exception>
    public void Insert(int index, T item) => throw new NotSupportedException("Use Add.");

    /// <summary>
    /// Removes the item at the specified index.
    /// </summary>
    /// <param name="index">The index of the item to remove.</param>
    /// <exception cref="NotSupportedException">This method is not supported.</exception>
    public void RemoveAt(int index) => throw new NotSupportedException("Use Remove(item).");

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
            return _quads[idx].Contains(item);
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
                var quad = _quads[i];
                quad.CopyTo(array, arrayIndex);
                arrayIndex += quad.Count;
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

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetShardIndex(T? item) => (item?.GetHashCode() ?? 0) & 0x7FFFFFFF % ShardCount;

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

    private void AddRangeCore(T[] items)
    {
        var count = items.Length;
        if (count == 0)
        {
            return;
        }

        // Use array instead of Span for compatibility with lambdas
        var bucketCounts = new int[ShardCount];

        for (var i = 0; i < count; i++)
        {
            var shardIdx = GetShardIndex(items[i]);
            bucketCounts[shardIdx]++;
        }

        var bucketArrays = new T[ShardCount][];
        var bucketIndices = new int[ShardCount];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCounts[i] > 0 ? ArrayPool<T>.Shared.Rent(bucketCounts[i]) : Array.Empty<T>();
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var item = items[i];
                var shardIdx = GetShardIndex(item);
                bucketArrays[shardIdx][bucketIndices[shardIdx]++] = item;
            }

            // Insert into shards - use parallel only for large datasets
            if (count >= ParallelThreshold)
            {
                Parallel.For(0, ShardCount, sIdx =>
                {
                    var bucketCount = bucketCounts[sIdx];
                    if (bucketCount == 0)
                    {
                        return;
                    }

                    var bucket = bucketArrays[sIdx];
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var quad = _quads[sIdx];
                        quad.EnsureCapacity(quad.Count + bucketCount);

                        var hasIndices = !_indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var item = bucket[i];
                            quad.Add(item);
                            if (hasIndices)
                            {
                                NotifyIndicesAdded(item);
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
                    var bucketCount = bucketCounts[sIdx];
                    if (bucketCount == 0)
                    {
                        continue;
                    }

                    var bucket = bucketArrays[sIdx];
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var quad = _quads[sIdx];
                        quad.EnsureCapacity(quad.Count + bucketCount);

                        var hasIndices = !_indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var item = bucket[i];
                            quad.Add(item);
                            if (hasIndices)
                            {
                                NotifyIndicesAdded(item);
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
                if (bucketCounts[i] > 0)
                {
                    ArrayPool<T>.Shared.Return(bucketArrays[i], clearArray: true);
                }
            }
        }
    }

    private void AddRangeCore(IList<T> items)
    {
        var count = items.Count;
        if (count == 0)
        {
            return;
        }

        var bucketCounts = new int[ShardCount];

        for (var i = 0; i < count; i++)
        {
            var shardIdx = GetShardIndex(items[i]);
            bucketCounts[shardIdx]++;
        }

        var bucketArrays = new T[ShardCount][];
        var bucketIndices = new int[ShardCount];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCounts[i] > 0 ? ArrayPool<T>.Shared.Rent(bucketCounts[i]) : Array.Empty<T>();
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var item = items[i];
                var shardIdx = GetShardIndex(item);
                bucketArrays[shardIdx][bucketIndices[shardIdx]++] = item;
            }

            if (count >= ParallelThreshold)
            {
                Parallel.For(0, ShardCount, sIdx =>
                {
                    var bucketCount = bucketCounts[sIdx];
                    if (bucketCount == 0)
                    {
                        return;
                    }

                    var bucket = bucketArrays[sIdx];
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var quad = _quads[sIdx];
                        quad.EnsureCapacity(quad.Count + bucketCount);

                        var hasIndices = !_indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var item = bucket[i];
                            quad.Add(item);
                            if (hasIndices)
                            {
                                NotifyIndicesAdded(item);
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
                    var bucketCount = bucketCounts[sIdx];
                    if (bucketCount == 0)
                    {
                        continue;
                    }

                    var bucket = bucketArrays[sIdx];
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var quad = _quads[sIdx];
                        quad.EnsureCapacity(quad.Count + bucketCount);

                        var hasIndices = !_indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var item = bucket[i];
                            quad.Add(item);
                            if (hasIndices)
                            {
                                NotifyIndicesAdded(item);
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
                if (bucketCounts[i] > 0)
                {
                    ArrayPool<T>.Shared.Return(bucketArrays[i], clearArray: true);
                }
            }
        }
    }

    private void RemoveRangeCore(T[] items)
    {
        var count = items.Length;
        if (count == 0)
        {
            return;
        }

        var bucketCounts = new int[ShardCount];

        for (var i = 0; i < count; i++)
        {
            var shardIdx = GetShardIndex(items[i]);
            bucketCounts[shardIdx]++;
        }

        var bucketArrays = new T[ShardCount][];
        var bucketIndices = new int[ShardCount];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCounts[i] > 0 ? ArrayPool<T>.Shared.Rent(bucketCounts[i]) : Array.Empty<T>();
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var item = items[i];
                var shardIdx = GetShardIndex(item);
                bucketArrays[shardIdx][bucketIndices[shardIdx]++] = item;
            }

            if (count >= ParallelThreshold)
            {
                Parallel.For(0, ShardCount, sIdx =>
                {
                    var bucketCount = bucketCounts[sIdx];
                    if (bucketCount == 0)
                    {
                        return;
                    }

                    var bucket = bucketArrays[sIdx];
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
                    var bucketCount = bucketCounts[sIdx];
                    if (bucketCount == 0)
                    {
                        continue;
                    }

                    var bucket = bucketArrays[sIdx];
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

            Emit(CacheAction.BatchOperation, default);
        }
        finally
        {
            for (var i = 0; i < ShardCount; i++)
            {
                if (bucketCounts[i] > 0)
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

        var bucketCounts = new int[ShardCount];

        for (var i = 0; i < count; i++)
        {
            var shardIdx = GetShardIndex(items[i]);
            bucketCounts[shardIdx]++;
        }

        var bucketArrays = new T[ShardCount][];
        var bucketIndices = new int[ShardCount];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCounts[i] > 0 ? ArrayPool<T>.Shared.Rent(bucketCounts[i]) : Array.Empty<T>();
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var item = items[i];
                var shardIdx = GetShardIndex(item);
                bucketArrays[shardIdx][bucketIndices[shardIdx]++] = item;
            }

            if (count >= ParallelThreshold)
            {
                Parallel.For(0, ShardCount, sIdx =>
                {
                    var bucketCount = bucketCounts[sIdx];
                    if (bucketCount == 0)
                    {
                        return;
                    }

                    var bucket = bucketArrays[sIdx];
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
                    var bucketCount = bucketCounts[sIdx];
                    if (bucketCount == 0)
                    {
                        continue;
                    }

                    var bucket = bucketArrays[sIdx];
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

            Emit(CacheAction.BatchOperation, default);
        }
        finally
        {
            for (var i = 0; i < ShardCount; i++)
            {
                if (bucketCounts[i] > 0)
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
        Emit(CacheAction.BatchOperation, default, new PooledBatch<T>(pool, count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitBatchFromList(IList<T> items, int count)
    {
        var pool = ArrayPool<T>.Shared.Rent(count);
        for (var i = 0; i < count; i++)
        {
            pool[i] = items[i];
        }

        Emit(CacheAction.BatchOperation, default, new PooledBatch<T>(pool, count));
    }
}
#endif
