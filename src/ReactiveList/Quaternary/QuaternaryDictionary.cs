// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace CP.Reactive.Quaternary;

/// <summary>
/// Represents a thread-safe, sharded dictionary that distributes key-value pairs across four internal partitions for
/// improved concurrency and scalability.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary. Must be non-nullable.</typeparam>
/// <typeparam name="TValue">The type of values stored in the dictionary.</typeparam>
[SkipLocalsInit]
public class QuaternaryDictionary<TKey, TValue> : QuaternaryBase<KeyValuePair<TKey, TValue>, QuadDictionary<TKey, TValue>, TValue>, IQuaternaryDictionary<TKey, TValue>
    where TKey : notnull
{
    private const int ParallelThreshold = 256;

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
                    result.AddRange(Quads[i].GetKeys());
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
                    result.AddRange(Quads[i].GetValues());
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
            if (Quads[idx].TryAdd(key, value))
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
            var dict = Quads[idx];

            // Use direct ref access - avoids double lookup
            ref var valueRef = ref dict.GetValueRefOrAddDefault(key, out var exists);

            if (exists)
            {
                NotifyIndicesRemoved(valueRef!);
                valueRef = value;
                NotifyIndicesAdded(value);
                Emit(CacheAction.Updated, new(key, value));
            }
            else
            {
                valueRef = value;
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
            if (Quads[idx].Remove(key, out var val))
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
            return Quads[idx].TryGetValue(key, out value);
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
    public void AddValueIndex<TIndexKey>(string name, Func<TValue?, TIndexKey> keySelector)
        where TIndexKey : notnull
    {
        var index = new SecondaryIndex<TValue, TIndexKey>(keySelector);

        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterReadLock();
            try
            {
                foreach (var val in Quads[i].GetValues())
                {
                    index.OnAdded(val);
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
    /// Retrieves all values that match the specified key in the given secondary value index.
    /// </summary>
    /// <typeparam name="TIndexKey">The type of the key used to query the secondary index.</typeparam>
    /// <param name="indexName">The name of the secondary index to query.</param>
    /// <param name="key">The key value to search for within the specified index.</param>
    /// <returns>An enumerable collection of values that match the specified key.</returns>
    public IEnumerable<TValue> GetValuesBySecondaryIndex<TIndexKey>(string indexName, TIndexKey key)
        where TIndexKey : notnull
    {
        if (Indices.TryGetValue(indexName, out var idx) && idx is SecondaryIndex<TValue, TIndexKey> typedIdx)
        {
            return typedIdx.Lookup(key);
        }

        return [];
    }

    /// <summary>
    /// Creates a reactive view filtered by a secondary index key.
    /// </summary>
    /// <typeparam name="TIndexKey">The type of the key used in the secondary index.</typeparam>
    /// <param name="indexName">The name of the secondary index to filter by.</param>
    /// <param name="key">The key value to filter on.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttleMs">Throttle interval in milliseconds. Defaults to 50ms.</param>
    /// <returns>A reactive view containing values matching the secondary index key.</returns>
    public SecondaryIndexReactiveView<TKey, TValue, TIndexKey> CreateViewBySecondaryIndex<TIndexKey>(
        string indexName,
        TIndexKey key,
        System.Reactive.Concurrency.IScheduler scheduler,
        int throttleMs = 50)
        where TIndexKey : notnull
    {
        if (!Indices.TryGetValue(indexName, out var idx) || idx is not SecondaryIndex<TValue, TIndexKey>)
        {
            throw new InvalidOperationException($"Secondary index '{indexName}' does not exist or has incompatible type.");
        }

        return new SecondaryIndexReactiveView<TKey, TValue, TIndexKey>(
            this,
            indexName,
            key,
            scheduler,
            TimeSpan.FromMilliseconds(throttleMs));
    }

    /// <summary>
    /// Determines whether the specified value matches the given key in the specified secondary index.
    /// </summary>
    /// <typeparam name="TIndexKey">The type of the key used in the secondary index.</typeparam>
    /// <param name="indexName">The name of the secondary index to use for matching.</param>
    /// <param name="value">The value to check.</param>
    /// <param name="key">The key value to match against.</param>
    /// <returns><see langword="true"/> if the value's indexed property matches the specified key; otherwise, <see langword="false"/>.</returns>
    public bool ValueMatchesSecondaryIndex<TIndexKey>(string indexName, TValue value, TIndexKey key)
        where TIndexKey : notnull
    {
        if (Indices.TryGetValue(indexName, out var idx))
        {
            return idx.MatchesKey(value, key);
        }

        return false;
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
    /// Looks up the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>A tuple containing a boolean indicating if the key was found and the value if present.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (bool HasValue, TValue? Value) Lookup(TKey key)
    {
        if (TryGetValue(key, out var value))
        {
            return (true, value);
        }

        return (false, default);
    }

    /// <summary>
    /// Removes all entries with keys in the specified collection from the dictionary.
    /// </summary>
    /// <param name="keys">The collection of keys to remove.</param>
    public void RemoveKeys(IEnumerable<TKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        // Fast path for arrays
        if (keys is TKey[] array)
        {
            RemoveKeysCore(array);
            return;
        }

        // Fast path for IList<TKey>
        if (keys is IList<TKey> list)
        {
            RemoveKeysCore(list);
            return;
        }

        // Slow path: materialize
        RemoveKeysCore(keys.ToArray());
    }

    /// <summary>
    /// Removes all entries that match the specified predicate from the dictionary.
    /// </summary>
    /// <param name="predicate">A function that returns true for entries that should be removed.</param>
    /// <returns>The number of entries removed from the dictionary.</returns>
    public int RemoveMany(Func<KeyValuePair<TKey, TValue>, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var totalRemoved = 0;

        // Use pooled arrays for keys to remove
        var keysBuffer = ArrayPool<TKey>.Shared.Rent(64);
        var keysCount = 0;

        try
        {
            for (var i = 0; i < ShardCount; i++)
            {
                Locks[i].EnterWriteLock();
                try
                {
                    var dict = Quads[i];

                    // First pass: identify keys to remove
                    keysCount = 0;
                    foreach (var kvp in dict)
                    {
                        if (predicate(kvp))
                        {
                            // Grow buffer if needed
                            if (keysCount >= keysBuffer.Length)
                            {
                                var newBuffer = ArrayPool<TKey>.Shared.Rent(keysBuffer.Length * 2);
                                keysBuffer.AsSpan(0, keysCount).CopyTo(newBuffer);
                                ArrayPool<TKey>.Shared.Return(keysBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TKey>());
                                keysBuffer = newBuffer;
                            }

                            keysBuffer[keysCount++] = kvp.Key;
                        }
                    }

                    // Second pass: remove identified keys
                    var hasIndices = !Indices.IsEmpty;
                    for (var j = 0; j < keysCount; j++)
                    {
                        var key = keysBuffer[j];
                        if (dict.Remove(key, out var val))
                        {
                            if (hasIndices)
                            {
                                NotifyIndicesRemoved(val);
                            }

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
                Emit(CacheAction.BatchOperation, default);
            }
        }
        finally
        {
            ArrayPool<TKey>.Shared.Return(keysBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TKey>());
        }

        return totalRemoved;
    }

    /// <summary>
    /// Performs a batch edit operation on the dictionary, ensuring only a single change notification is emitted.
    /// </summary>
    /// <param name="editAction">An action that receives the dictionary interface to perform modifications.</param>
    public void Edit(Action<IDictionary<TKey, TValue>> editAction)
    {
        ArgumentNullException.ThrowIfNull(editAction);

        // Acquire all locks for the edit operation
        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterWriteLock();
        }

        try
        {
            var wrapper = new QuaternaryDictEditWrapper(this);
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
                foreach (var kvp in Quads[i])
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
    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterReadLock();
            try
            {
                foreach (var kvp in Quads[i])
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

    /// <summary>
    /// Calculates the shard index for the specified key.
    /// </summary>
    /// <remarks>Uses golden ratio multiplication for better hash distribution across shards.
    /// The result is always a value between 0 and 3 (inclusive) for 4 shards.</remarks>
    /// <param name="key">The key for which to determine the shard index. Must not be null.</param>
    /// <returns>The zero-based index of the shard to which the key is assigned.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetShard(TKey key)
    {
        // Use golden ratio multiplication for better hash distribution
        // Then shift right by 30 bits to get 2 bits (0-3) for 4 shards
        var hash = key.GetHashCode();
        return (int)((uint)(hash * 0x9E3779B9) >> 30);
    }

    /// <summary>
    /// Adds the specified key/value pairs to the collection, distributing them across internal shards for optimized
    /// storage and concurrency.
    /// </summary>
    /// <remarks>This method batches additions for efficiency and may perform operations in parallel if the
    /// number of items exceeds a predefined threshold. If a key already exists, its value is overwritten. Index
    /// notifications are triggered for each added value if indices are present.</remarks>
    /// <param name="items">An array of key/value pairs to add to the collection. Each key must be non-null and conform to the requirements
    /// of the underlying dictionary.</param>
    private void AddRangeCore(KeyValuePair<TKey, TValue>[] items)
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
            var shardIdx = GetShard(itemsSpan[i].Key);
            bucketCountsArray[shardIdx]++;
        }

        var bucketArrays = new KeyValuePair<TKey, TValue>[ShardCount][];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCountsArray[i] > 0 ? ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Rent(bucketCountsArray[i]) : [];
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var kvp = itemsSpan[i];
                var shardIdx = GetShard(kvp.Key);
                bucketArrays[shardIdx][bucketIndicesArray[shardIdx]++] = kvp;
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
                        var dict = Quads[sIdx];
                        dict.EnsureCapacity(dict.Count + bucketCount);

                        var hasIndices = !Indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var kvp = bucket[i];
                            ref var valueRef = ref dict.GetValueRefOrAddDefault(kvp.Key, out _);
                            valueRef = kvp.Value;
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
                    var bucketCount = bucketCountsArray[sIdx];
                    if (bucketCount == 0)
                    {
                        continue;
                    }

                    var bucket = bucketArrays[sIdx].AsSpan(0, bucketCount);
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var dict = Quads[sIdx];
                        dict.EnsureCapacity(dict.Count + bucketCount);

                        var hasIndices = !Indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var kvp = bucket[i];
                            ref var valueRef = ref dict.GetValueRefOrAddDefault(kvp.Key, out _);
                            valueRef = kvp.Value;
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

            EmitBatchAddedDirect(items, count);
        }
        finally
        {
            for (var i = 0; i < ShardCount; i++)
            {
                if (bucketCountsArray[i] > 0)
                {
                    ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Return(bucketArrays[i], clearArray: true);
                }
            }
        }
    }

    /// <summary>
    /// Adds a collection of key/value pairs to the underlying data structure, distributing them across shards as
    /// appropriate.
    /// </summary>
    /// <remarks>If the number of items meets or exceeds a parallelization threshold, the addition is
    /// performed in parallel for improved performance. The method acquires write locks on affected shards during the
    /// operation. Indices, if present, are updated for each added value.</remarks>
    /// <param name="items">The list of key/value pairs to add. Cannot be null. Each key is assigned to a shard based on its value.</param>
    private void AddRangeCore(IList<KeyValuePair<TKey, TValue>> items)
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
            var shardIdx = GetShard(items[i].Key);
            bucketCountsArray[shardIdx]++;
        }

        var bucketArrays = new KeyValuePair<TKey, TValue>[ShardCount][];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCountsArray[i] > 0 ? ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Rent(bucketCountsArray[i]) : [];
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var kvp = items[i];
                var shardIdx = GetShard(kvp.Key);
                bucketArrays[shardIdx][bucketIndicesArray[shardIdx]++] = kvp;
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
                        var dict = Quads[sIdx];
                        dict.EnsureCapacity(dict.Count + bucketCount);

                        var hasIndices = !Indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var kvp = bucket[i];
                            ref var valueRef = ref dict.GetValueRefOrAddDefault(kvp.Key, out _);
                            valueRef = kvp.Value;
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
                    var bucketCount = bucketCountsArray[sIdx];
                    if (bucketCount == 0)
                    {
                        continue;
                    }

                    var bucket = bucketArrays[sIdx].AsSpan(0, bucketCount);
                    Locks[sIdx].EnterWriteLock();
                    try
                    {
                        var dict = Quads[sIdx];
                        dict.EnsureCapacity(dict.Count + bucketCount);

                        var hasIndices = !Indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var kvp = bucket[i];
                            ref var valueRef = ref dict.GetValueRefOrAddDefault(kvp.Key, out _);
                            valueRef = kvp.Value;
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

            EmitBatchAddedFromList(items, count);
        }
        finally
        {
            for (var i = 0; i < ShardCount; i++)
            {
                if (bucketCountsArray[i] > 0)
                {
                    ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Return(bucketArrays[i], clearArray: true);
                }
            }
        }
    }

    /// <summary>
    /// Removes the specified keys from the underlying data store, updating all relevant shards and indices as
    /// necessary.
    /// </summary>
    /// <remarks>This method distributes key removals across internal shards for efficiency. For large numbers
    /// of keys, removals may be processed in parallel to improve performance. Any associated indices are updated
    /// accordingly when keys are removed.</remarks>
    /// <param name="keys">An array of keys to remove from the data store. Cannot be null. If the array is empty, no action is taken.</param>
    private void RemoveKeysCore(TKey[] keys)
    {
        var count = keys.Length;
        if (count == 0)
        {
            return;
        }

        var bucketCountsArray = new int[ShardCount];
        var bucketIndicesArray = new int[ShardCount];
        var keysSpan = keys.AsSpan();

        for (var i = 0; i < count; i++)
        {
            var shardIdx = GetShard(keysSpan[i]);
            bucketCountsArray[shardIdx]++;
        }

        var bucketArrays = new TKey[ShardCount][];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCountsArray[i] > 0 ? ArrayPool<TKey>.Shared.Rent(bucketCountsArray[i]) : [];
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var key = keysSpan[i];
                var shardIdx = GetShard(key);
                bucketArrays[shardIdx][bucketIndicesArray[shardIdx]++] = key;
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
                        var dict = Quads[sIdx];
                        var hasIndices = !Indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var key = bucket[i];
                            if (dict.Remove(key, out var val) && hasIndices)
                            {
                                NotifyIndicesRemoved(val);
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
                        var dict = Quads[sIdx];
                        var hasIndices = !Indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var key = bucket[i];
                            if (dict.Remove(key, out var val) && hasIndices)
                            {
                                NotifyIndicesRemoved(val);
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
                if (bucketCountsArray[i] > 0)
                {
                    ArrayPool<TKey>.Shared.Return(bucketArrays[i], clearArray: true);
                }
            }
        }
    }

    /// <summary>
    /// Removes the specified keys from the underlying data store, updating any associated indices as needed.
    /// </summary>
    /// <remarks>This method processes removals in parallel if the number of keys meets or exceeds a
    /// predefined threshold for improved performance. Associated indices are updated for each successfully removed key.
    /// The method is not thread-safe and should be called with appropriate synchronization if used in a multithreaded
    /// context.</remarks>
    /// <param name="keys">A list of keys to remove from the data store. Cannot be null. Keys that do not exist are ignored.</param>
    private void RemoveKeysCore(IList<TKey> keys)
    {
        var count = keys.Count;
        if (count == 0)
        {
            return;
        }

        var bucketCountsArray = new int[ShardCount];
        var bucketIndicesArray = new int[ShardCount];

        for (var i = 0; i < count; i++)
        {
            var shardIdx = GetShard(keys[i]);
            bucketCountsArray[shardIdx]++;
        }

        var bucketArrays = new TKey[ShardCount][];

        for (var i = 0; i < ShardCount; i++)
        {
            bucketArrays[i] = bucketCountsArray[i] > 0 ? ArrayPool<TKey>.Shared.Rent(bucketCountsArray[i]) : [];
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var key = keys[i];
                var shardIdx = GetShard(key);
                bucketArrays[shardIdx][bucketIndicesArray[shardIdx]++] = key;
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
                        var dict = Quads[sIdx];
                        var hasIndices = !Indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var key = bucket[i];
                            if (dict.Remove(key, out var val) && hasIndices)
                            {
                                NotifyIndicesRemoved(val);
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
                        var dict = Quads[sIdx];
                        var hasIndices = !Indices.IsEmpty;
                        for (var i = 0; i < bucketCount; i++)
                        {
                            var key = bucket[i];
                            if (dict.Remove(key, out var val) && hasIndices)
                            {
                                NotifyIndicesRemoved(val);
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
                if (bucketCountsArray[i] > 0)
                {
                    ArrayPool<TKey>.Shared.Return(bucketArrays[i], clearArray: true);
                }
            }
        }
    }

    /// <summary>
    /// Internal wrapper for Edit operations that bypasses locking and notifications.
    /// </summary>
    private sealed class QuaternaryDictEditWrapper : IDictionary<TKey, TValue>
    {
        private readonly QuaternaryDictionary<TKey, TValue> _parent;

        internal QuaternaryDictEditWrapper(QuaternaryDictionary<TKey, TValue> parent) => _parent = parent;

        /// <summary>
        /// Gets a collection containing all keys in the dictionary.
        /// </summary>
        /// <remarks>The returned collection reflects the current set of keys in the dictionary at the
        /// time of the call. The order of the keys in the collection is not guaranteed. Modifications to the dictionary
        /// after retrieving the collection are not reflected in the returned collection.</remarks>
        public ICollection<TKey> Keys
        {
            get
            {
                var result = new List<TKey>();
                for (var i = 0; i < ShardCount; i++)
                {
                    result.AddRange(_parent.Quads[i].GetKeys());
                }

                return result;
            }
        }

        /// <summary>
        /// Gets a collection containing all values in the dictionary.
        /// </summary>
        /// <remarks>The order of the values in the returned collection is not guaranteed. The returned
        /// collection is a snapshot and is not updated if the dictionary changes after the property is
        /// accessed.</remarks>
        public ICollection<TValue> Values
        {
            get
            {
                var result = new List<TValue>();
                for (var i = 0; i < ShardCount; i++)
                {
                    result.AddRange(_parent.Quads[i].GetValues());
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the total number of elements contained in all shards.
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
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <remarks>Setting a value for an existing key may trigger index updates. Getting a value for a
        /// key that does not exist will throw a KeyNotFoundException.</remarks>
        /// <param name="key">The key whose value to get or set.</param>
        /// <returns>The value associated with the specified key.</returns>
        public TValue this[TKey key]
        {
            get
            {
                var idx = GetShard(key);
                return _parent.Quads[idx][key];
            }

            set
            {
                var idx = GetShard(key);
                var dict = _parent.Quads[idx];
                if (dict.TryGetValue(key, out var oldVal))
                {
                    _parent.NotifyIndicesRemoved(oldVal);
                }

                dict[key] = value;
                _parent.NotifyIndicesAdded(value);
            }
        }

        /// <summary>
        /// Adds the specified key and value to the collection.
        /// </summary>
        /// <param name="key">The key associated with the value to add. Cannot be null.</param>
        /// <param name="value">The value to add to the collection.</param>
        public void Add(TKey key, TValue value)
        {
            var idx = GetShard(key);
            _parent.Quads[idx].Add(key, value);
            _parent.NotifyIndicesAdded(value);
        }

        /// <summary>
        /// Determines whether the dictionary contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the dictionary. Cannot be null.</param>
        /// <returns><see langword="true"/> if the dictionary contains an element with the specified key; otherwise, <see
        /// langword="false"/>.</returns>
        public bool ContainsKey(TKey key)
        {
            var idx = GetShard(key);
            return _parent.Quads[idx].ContainsKey(key);
        }

        /// <summary>
        /// Removes the value with the specified key from the collection.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>true if the element is successfully found and removed; otherwise, false.</returns>
        public bool Remove(TKey key)
        {
            var idx = GetShard(key);
            if (_parent.Quads[idx].Remove(key, out var val))
            {
                _parent.NotifyIndicesRemoved(val);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to retrieve the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key whose value to retrieve.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found;
        /// otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the object that implements the method contains an element with the specified key; otherwise, false.</returns>
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            var idx = GetShard(key);
            return _parent.Quads[idx].TryGetValue(key, out value);
        }

        /// <summary>
        /// Adds the specified key/value pair to the collection.
        /// </summary>
        /// <param name="item">The key/value pair to add to the collection. The key must not already exist in the collection.</param>
        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        /// <summary>
        /// Removes all items from the collection, resetting it to an empty state.
        /// </summary>
        /// <remarks>This method clears all data from the collection, including any associated indices.
        /// After calling this method, the collection will contain no items and all indices will be empty. This
        /// operation is not reversible.</remarks>
        public void Clear()
        {
            for (var i = 0; i < ShardCount; i++)
            {
                _parent.Quads[i].Clear();
            }

            if (!_parent.Indices.IsEmpty)
            {
                foreach (var idx in _parent.Indices.Values)
                {
                    idx.Clear();
                }
            }
        }

        /// <summary>
        /// Determines whether the dictionary contains the specified key and value pair.
        /// </summary>
        /// <remarks>The value comparison uses the default equality comparer for the value type. If the
        /// key is not found, the method returns false.</remarks>
        /// <param name="item">The key and value pair to locate in the dictionary. The key is used to locate the entry, and the value is
        /// compared to the value associated with the key.</param>
        /// <returns>true if the dictionary contains an element with the specified key and value; otherwise, false.</returns>
        public bool Contains(KeyValuePair<TKey, TValue> item) =>
            TryGetValue(item.Key, out var v) && EqualityComparer<TValue>.Default.Equals(v, item.Value);

        /// <summary>
        /// Copies the elements of the collection to the specified array, starting at the given array index.
        /// </summary>
        /// <remarks>The destination array must be large enough to contain all the elements of the
        /// collection, starting at the specified index. The order of the copied elements matches the enumeration order
        /// of the collection.</remarks>
        /// <param name="array">The one-dimensional array of key/value pairs that is the destination of the elements copied from the
        /// collection. The array must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in the destination array at which copying begins.</param>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            for (var i = 0; i < ShardCount; i++)
            {
                foreach (var kvp in _parent.Quads[i])
                {
                    array[arrayIndex++] = kvp;
                }
            }
        }

        /// <summary>
        /// Removes the first occurrence of the specified key/value pair from the collection.
        /// </summary>
        /// <param name="item">The key/value pair to remove from the collection. The pair is removed only if both the key and value match
        /// an entry in the collection.</param>
        /// <returns>true if the key/value pair was found and removed; otherwise, false.</returns>
        public bool Remove(KeyValuePair<TKey, TValue> item) => Contains(item) && Remove(item.Key);

        /// <summary>
        /// Returns an enumerator that iterates through the collection of key/value pairs in the dictionary.
        /// </summary>
        /// <returns>An enumerator for the collection of key/value pairs contained in the dictionary.</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (var i = 0; i < ShardCount; i++)
            {
                foreach (var kvp in _parent.Quads[i])
                {
                    yield return kvp;
                }
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
#endif
