// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET6_0_OR_GREATER

using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace CP.Reactive;

/// <summary>
/// Represents a thread-safe, sharded dictionary that distributes key-value pairs across four internal partitions for
/// improved concurrency and scalability.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary. Must be non-nullable.</typeparam>
/// <typeparam name="TValue">The type of values stored in the dictionary.</typeparam>
public class QuaternaryDictionary<TKey, TValue> : QuaternaryBase<KeyValuePair<TKey, TValue>>, IQuaternaryDictionary<TKey, TValue>
    where TKey : notnull
{
    private const int ParallelThreshold = 256;

    private readonly Dictionary<TKey, TValue>[] _quads =
    [
        new Dictionary<TKey, TValue>(),
        new Dictionary<TKey, TValue>(),
        new Dictionary<TKey, TValue>(),
        new Dictionary<TKey, TValue>()
    ];

    private readonly ConcurrentDictionary<string, ISecondaryIndex<TValue>> _valueIndices = new();

    /// <summary>
    /// Gets a collection containing all keys from the underlying quads.
    /// </summary>
    public ICollection<TKey> Keys
    {
        get
        {
            var result = new List<TKey>();
            for (var i = 0; i < ShardCount; i++)
            {
                Locks[i].EnterReadLock();
                try
                {
                    result.AddRange(_quads[i].Keys);
                }
                finally
                {
                    Locks[i].ExitReadLock();
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Gets a collection containing all values from the underlying quads.
    /// </summary>
    public ICollection<TValue> Values
    {
        get
        {
            var result = new List<TValue>();
            for (var i = 0; i < ShardCount; i++)
            {
                Locks[i].EnterReadLock();
                try
                {
                    result.AddRange(_quads[i].Values);
                }
                finally
                {
                    Locks[i].ExitReadLock();
                }
            }

            return result;
        }
    }

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
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key whose value to get or set.</param>
    /// <returns>The value associated with the specified key.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found.</exception>
    public TValue this[TKey key]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (TryGetValue(key, out var val))
            {
                return val;
            }

            throw new KeyNotFoundException();
        }

        set => AddOrUpdate(key, value);
    }

    /// <summary>
    /// Adds the specified key and value to the collection.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <exception cref="ArgumentException">Thrown if the key already exists.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TKey key, TValue value)
    {
        if (!TryAdd(key, value))
        {
            throw new ArgumentException("Key exists");
        }
    }

    /// <summary>
    /// Attempts to add the specified key and value to the cache if the key does not already exist.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <returns>true if the key and value were added; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(TKey key, TValue value)
    {
        var idx = GetShard(key);
        Locks[idx].EnterWriteLock();
        try
        {
            if (_quads[idx].TryAdd(key, value))
            {
                NotifyIndicesAdded(value);
                Emit(CacheAction.Added, new(key, value));
                return true;
            }

            return false;
        }
        finally
        {
            Locks[idx].ExitWriteLock();
        }
    }

    /// <summary>
    /// Adds a new entry or updates existing value.
    /// </summary>
    /// <param name="key">The key to add or update.</param>
    /// <param name="value">The value to associate with the key.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOrUpdate(TKey key, TValue value)
    {
        var idx = GetShard(key);
        Locks[idx].EnterWriteLock();
        try
        {
            var dict = _quads[idx];
            if (dict.TryGetValue(key, out var oldVal))
            {
                NotifyIndicesRemoved(oldVal);
                dict[key] = value;
                NotifyIndicesAdded(value);
                Emit(CacheAction.Updated, new(key, value));
            }
            else
            {
                dict[key] = value;
                NotifyIndicesAdded(value);
                Emit(CacheAction.Added, new(key, value));
            }
        }
        finally
        {
            Locks[idx].ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes the value with the specified key from the collection.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns>true if the element was removed; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(TKey key)
    {
        var idx = GetShard(key);
        Locks[idx].EnterWriteLock();
        try
        {
            if (_quads[idx].Remove(key, out var val))
            {
                NotifyIndicesRemoved(val);
                Emit(CacheAction.Removed, new(key, val));
                return true;
            }

            return false;
        }
        finally
        {
            Locks[idx].ExitWriteLock();
        }
    }

    /// <summary>
    /// Attempts to retrieve the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key whose value to retrieve.</param>
    /// <param name="value">When this method returns, contains the value if found.</param>
    /// <returns>true if the key was found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var idx = GetShard(key);
        Locks[idx].EnterReadLock();
        try
        {
            return _quads[idx].TryGetValue(key, out value);
        }
        finally
        {
            Locks[idx].ExitReadLock();
        }
    }

    /// <summary>
    /// Adds a collection of key/value pairs to the cache in a single batch operation.
    /// </summary>
    /// <param name="pairs">The collection of key/value pairs to add.</param>
    public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> pairs)
    {
        if (pairs is KeyValuePair<TKey, TValue>[] array)
        {
            AddRangeCore(array);
            return;
        }

        if (pairs is IList<KeyValuePair<TKey, TValue>> list)
        {
            AddRangeCore(list);
            return;
        }

        AddRangeCore(pairs.ToArray());
    }

    /// <summary>
    /// Adds a secondary index for values using the specified key selector function.
    /// </summary>
    /// <typeparam name="TIndexKey">The type of the index key.</typeparam>
    /// <param name="name">The unique name of the index to add.</param>
    /// <param name="keySelector">A function that extracts the index key from a value.</param>
    public void AddValueIndex<TIndexKey>(string name, Func<TValue, TIndexKey> keySelector)
        where TIndexKey : notnull
    {
        var index = new SecondaryIndex<TValue, TIndexKey>(keySelector);

        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterReadLock();
            try
            {
                foreach (var val in _quads[i].Values)
                {
                    index.OnAdded(val);
                }
            }
            finally
            {
                Locks[i].ExitReadLock();
            }
        }

        _valueIndices[name] = index;
    }

    /// <summary>
    /// Adds the specified key/value pair to the collection.
    /// </summary>
    /// <param name="item">The key/value pair to add.</param>
    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    /// <summary>
    /// Determines whether the dictionary contains the specified key and value pair.
    /// </summary>
    /// <param name="item">The key/value pair to locate.</param>
    /// <returns>true if found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(KeyValuePair<TKey, TValue> item) => TryGetValue(item.Key, out var v) && EqualityComparer<TValue>.Default.Equals(v, item.Value);

    /// <summary>
    /// Removes the specified key/value pair from the collection.
    /// </summary>
    /// <param name="item">The key/value pair to remove.</param>
    /// <returns>true if removed; otherwise, false.</returns>
    public bool Remove(KeyValuePair<TKey, TValue> item) => Contains(item) && Remove(item.Key);

    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    public void Clear()
    {
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

        if (!_valueIndices.IsEmpty)
        {
            foreach (var idx in _valueIndices.Values)
            {
                idx.Clear();
            }
        }

        Emit(CacheAction.Cleared, default);
    }

    /// <summary>
    /// Copies the elements of the collection to an array.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The starting index in the array.</param>
    /// <exception cref="ArgumentNullException">Thrown when array is null.</exception>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterReadLock();
            try
            {
                foreach (var kvp in _quads[i])
                {
                    array[arrayIndex++] = kvp;
                }
            }
            finally
            {
                Locks[i].ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Determines whether the dictionary contains the specified key.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>true if the key exists; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(TKey key) => TryGetValue(key, out _);

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator for the collection.</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterReadLock();
            try
            {
                foreach (var kvp in _quads[i])
                {
                    yield return kvp;
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
    private static int GetShard(TKey key) => (key.GetHashCode() & 0x7FFFFFFF) % ShardCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyIndicesAdded(TValue item)
    {
        if (_valueIndices.IsEmpty)
        {
            return;
        }

        foreach (var index in _valueIndices.Values)
        {
            index.OnAdded(item);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyIndicesRemoved(TValue item)
    {
        if (_valueIndices.IsEmpty)
        {
            return;
        }

        foreach (var index in _valueIndices.Values)
        {
            index.OnRemoved(item);
        }
    }

    private void AddRangeCore(KeyValuePair<TKey, TValue>[] items)
    {
        var count = items.Length;
        if (count == 0)
        {
            return;
        }

        var bucketCounts = new int[ShardCount];

        for (var i = 0; i < count; i++)
        {
            var shardIdx = GetShard(items[i].Key);
            bucketCounts[shardIdx]++;
        }

        var bucketArrays = new KeyValuePair<TKey, TValue>[ShardCount][];
        var bucketIndices = new int[ShardCount];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCounts[i] > 0 ? ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Rent(bucketCounts[i]) : [];
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var kvp = items[i];
                var shardIdx = GetShard(kvp.Key);
                bucketArrays[shardIdx][bucketIndices[shardIdx]++] = kvp;
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
                        var dict = _quads[sIdx];
                        dict.EnsureCapacity(dict.Count + bucketCount);

                        var hasIndices = !_valueIndices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var kvp = bucket[i];
                            dict[kvp.Key] = kvp.Value;
                            if (hasIndices)
                            {
                                NotifyIndicesAdded(kvp.Value);
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
                        var dict = _quads[sIdx];
                        dict.EnsureCapacity(dict.Count + bucketCount);

                        var hasIndices = !_valueIndices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var kvp = bucket[i];
                            dict[kvp.Key] = kvp.Value;
                            if (hasIndices)
                            {
                                NotifyIndicesAdded(kvp.Value);
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
                    ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Return(bucketArrays[i], clearArray: true);
                }
            }
        }
    }

    private void AddRangeCore(IList<KeyValuePair<TKey, TValue>> items)
    {
        var count = items.Count;
        if (count == 0)
        {
            return;
        }

        var bucketCounts = new int[ShardCount];

        for (var i = 0; i < count; i++)
        {
            var shardIdx = GetShard(items[i].Key);
            bucketCounts[shardIdx]++;
        }

        var bucketArrays = new KeyValuePair<TKey, TValue>[ShardCount][];
        var bucketIndices = new int[ShardCount];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCounts[i] > 0 ? ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Rent(bucketCounts[i]) : [];
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var kvp = items[i];
                var shardIdx = GetShard(kvp.Key);
                bucketArrays[shardIdx][bucketIndices[shardIdx]++] = kvp;
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
                        var dict = _quads[sIdx];
                        dict.EnsureCapacity(dict.Count + bucketCount);

                        var hasIndices = !_valueIndices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var kvp = bucket[i];
                            dict[kvp.Key] = kvp.Value;
                            if (hasIndices)
                            {
                                NotifyIndicesAdded(kvp.Value);
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
                        var dict = _quads[sIdx];
                        dict.EnsureCapacity(dict.Count + bucketCount);

                        var hasIndices = !_valueIndices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var kvp = bucket[i];
                            dict[kvp.Key] = kvp.Value;
                            if (hasIndices)
                            {
                                NotifyIndicesAdded(kvp.Value);
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
                    ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Return(bucketArrays[i], clearArray: true);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitBatchDirect(KeyValuePair<TKey, TValue>[] items, int count)
    {
        var pool = ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Rent(count);
        Array.Copy(items, pool, count);
        Emit(CacheAction.BatchOperation, default, new PooledBatch<KeyValuePair<TKey, TValue>>(pool, count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitBatchFromList(IList<KeyValuePair<TKey, TValue>> items, int count)
    {
        var pool = ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Rent(count);
        for (var i = 0; i < count; i++)
        {
            pool[i] = items[i];
        }

        Emit(CacheAction.BatchOperation, default, new PooledBatch<KeyValuePair<TKey, TValue>>(pool, count));
    }
}
#endif
