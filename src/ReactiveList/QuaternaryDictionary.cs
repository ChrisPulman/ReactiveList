// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET6_0_OR_GREATER

using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using DynamicData;

namespace CP.Reactive;

/// <summary>
/// Represents a specialized dictionary that organizes key-value pairs into four separate internal dictionaries,
/// providing efficient storage and retrieval based on a quaternary partitioning strategy.
/// </summary>
/// <remarks>QuaternaryDictionary partitions its entries across four internal dictionaries to optimize certain
/// access patterns or storage scenarios. This structure is useful when data can be logically divided into four groups
/// or when parallel operations on each partition are required. Thread safety is not guaranteed; external
/// synchronization is required for concurrent access.</remarks>
/// <typeparam name="TKey">The type of keys in the dictionary. Must be non-nullable.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
public class QuaternaryDictionary<TKey, TValue> : QuaternaryBase<KeyValuePair<TKey, TValue>>, IQuaternaryDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly IQuaternaryDictionary<TKey, TValue>[] _quadrants = new IQuaternaryDictionary<TKey, TValue>[4];

    /// <summary>
    /// Initializes a new instance of the <see cref="QuaternaryDictionary{TKey, TValue}"/> class with an optional capacity limit for all quadrants.
    /// </summary>
    /// <remarks>When a capacity limit is set, each quadrant uses a least-recently-used (LRU) eviction policy
    /// to manage its items. Without a capacity limit, quadrants do not evict items automatically.</remarks>
    /// <param name="capacityLimit">The maximum total number of items that can be stored across all quadrants. If specified, the capacity is divided
    /// equally among the four quadrants. If null, no capacity limit is enforced.</param>
    public QuaternaryDictionary(int? capacityLimit = null)
    {
        var limitPerQuad = capacityLimit.HasValue ? capacityLimit.Value / 4 : 0;

        for (var i = 0; i < 4; i++)
        {
            _quadrants[i] = capacityLimit.HasValue
                ? new LruQuadrant(limitPerQuad)
                : new SimpleQuadrant();
        }
    }

    /// <summary>
    /// Gets the total number of items contained in all quads.
    /// </summary>
    public int Count => _quadrants.Sum(q =>
    {
        lock (q)
        {
            return q.Count;
        }
    });

    /// <summary>
    /// Gets a collection containing all keys in the spatial index.
    /// </summary>
    /// <remarks>The returned collection reflects the current set of keys across all quadrants. The order of
    /// the keys is not guaranteed. Modifying the collection does not affect the underlying spatial index.</remarks>
    public ICollection<TKey> Keys => [.. _quadrants.SelectMany(q => q.Keys)];

    /// <summary>
    /// Gets a collection containing all values stored in the collection.
    /// </summary>
    /// <remarks>The order of the values in the returned collection is not guaranteed. The returned collection
    /// is a snapshot and is not updated if the underlying collection changes.</remarks>
    public ICollection<TValue> Values => [.. _quadrants.SelectMany(q => q.Values)];

    /// <summary>
    /// Gets a value indicating whether the collection is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key whose value to get or set.</param>
    /// <returns>The value associated with the specified key.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when attempting to get a value and the specified key does not exist in the cache.</exception>
    public TValue this[TKey key]
    {
        get
        {
            var idx = GetIdx(key);
            Locks[idx].EnterReadLock();
            try
            {
                return _quadrants[idx][key];
            }
            finally
            {
                Locks[idx].ExitReadLock();
            }
        }
        set => Set(key, value);
    }

    /// <summary>
    /// Attempts to retrieve the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key whose value to retrieve.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise,
    /// the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns>true if the object that implements the method contains an element with the specified key; otherwise, false.</returns>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var idx = GetIdx(key);
        Locks[idx].EnterUpgradeableReadLock();
        try
        {
            return _quadrants[idx].TryGetValue(key, out value);
        }
        finally
        {
            Locks[idx].ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Adds a new entry to the cache or updates the value for an existing key.
    /// </summary>
    /// <remarks>If the specified key already exists in the cache, its value is updated. If the key does not
    /// exist, a new entry is added. Depending on the cache's eviction policy, adding a new entry may cause an existing
    /// entry to be evicted if the cache is at capacity.</remarks>
    /// <param name="key">The key of the entry to add or update. Cannot be null.</param>
    /// <param name="value">The value to associate with the specified key. May be null if the cache supports null values.</param>
    public void Set(TKey key, TValue value)
    {
        var idx = GetIdx(key);
        Locks[idx].EnterWriteLock();
        try
        {
            var q = _quadrants[idx];
            var isAdd = q.Set(key, value, out var oldValue);
            if (isAdd)
            {
                Emit(CacheAction.Adding, new(key, value));
                if (q.TryEvict(out var evictedKey, out var evictedValue))
                {
                    Emit(CacheAction.Evicted, new(evictedKey, evictedValue!));
                }
            }
            else
            {
                Emit(CacheAction.Updating, new(key, oldValue!));
                Emit(CacheAction.Updated, new(key, value));
            }
        }
        finally
        {
            Locks[idx].ExitWriteLock();
        }
    }

    /// <summary>
    /// Attempts to remove the value with the specified key from the collection.
    /// </summary>
    /// <remarks>If the key is not found, the value parameter is set to its default value. This method is
    /// thread-safe.</remarks>
    /// <param name="key">The key of the element to remove.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key if the key was found and removed;
    /// otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns>true if the element is successfully found and removed; otherwise, false.</returns>
    public bool TryRemove(TKey key, [MaybeNullWhen(false)] out TValue? value)
    {
        var idx = GetIdx(key);
        Locks[idx].EnterWriteLock();
        try
        {
            var removed = _quadrants[idx].Remove(key, out value);
            if (removed)
            {
                Emit(CacheAction.Removed, new(key, value!));
            }

            return removed;
        }
        finally
        {
            Locks[idx].ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes all items from the cache.
    /// </summary>
    /// <remarks>This method is thread-safe and can be called concurrently from multiple threads. After
    /// calling this method, the cache will be empty.</remarks>
    public void Clear()
    {
        Emit(CacheAction.Clearing, default);
        Parallel.For(0, 4, i =>
        {
            Locks[i].EnterWriteLock();
            try
            {
                _quadrants[i].Clear();
            }
            finally
            {
                Locks[i].ExitWriteLock();
            }
        });
        Emit(CacheAction.Cleared, default);
    }

    /// <summary>
    /// Attempts to retrieve the value associated with the specified key asynchronously.
    /// </summary>
    /// <param name="key">The key whose associated value is to be retrieved.</param>
    /// <returns>A value task that represents the asynchronous operation. The result contains the value associated with the
    /// specified key, or <see langword="null"/> if the key does not exist.</returns>
    public ValueTask<TValue?> TryGetAsync(TKey key)
    {
        var idx = GetIdx(key);
        Locks[idx].EnterReadLock();
        try
        {
            _quadrants[idx].TryGetValue(key, out var value);
            return new ValueTask<TValue?>(value);
        }
        finally
        {
            Locks[idx].ExitReadLock();
        }
    }

    /// <summary>
    /// Attempts to set the value associated with the specified key, returning a value that indicates whether the key
    /// was already present.
    /// </summary>
    /// <param name="key">The key whose value is to be set or updated.</param>
    /// <param name="value">The value to associate with the specified key.</param>
    /// <param name="oldValue">When this method returns, contains the previous value associated with the key if it was present; otherwise, the
    /// default value for the type.</param>
    /// <returns>true if the key was already present and its value was updated; otherwise, false if the key was newly added.</returns>
    public bool Set(TKey key, TValue value, out TValue? oldValue)
    {
        var idx = GetIdx(key);
        Locks[idx].EnterWriteLock();
        try
        {
            return _quadrants[idx].Set(key, value, out oldValue);
        }
        finally
        {
            Locks[idx].ExitWriteLock();
        }
    }

    /// <summary>
    /// Adds or updates multiple key-value pairs in the cache in a single operation.
    /// </summary>
    /// <remarks>This method processes the items in parallel to improve performance when handling large
    /// collections. For each key-value pair, appropriate cache events are emitted to signal additions, updates, and
    /// evictions. The operation is thread-safe.</remarks>
    /// <param name="items">An enumerable collection of key-value pairs to add or update in the cache. If a key already exists, its value is
    /// updated; otherwise, the key-value pair is added.</param>
    public void SetRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        var groups = items.GroupBy(kvp => GetIdx(kvp.Key));
        Parallel.ForEach(groups, group =>
        {
            var idx = group.Key;
            Locks[idx].EnterWriteLock();
            try
            {
                var quadrant = _quadrants[idx];
                foreach (var kvp in group)
                {
                    var isAdd = quadrant.Set(kvp.Key, kvp.Value, out var _);
                    if (isAdd)
                    {
                        Emit(CacheAction.Adding, kvp);
                        if (quadrant.TryEvict(out var evictedKey, out var evictedValue))
                        {
                            Emit(CacheAction.Evicted, new(evictedKey, evictedValue!));
                        }
                    }
                    else
                    {
                        Emit(CacheAction.Updating, kvp);
                        Emit(CacheAction.Updated, kvp);
                    }
                }
            }
            finally
            {
                Locks[idx].ExitWriteLock();
            }
        });
    }

    /// <summary>
    /// Removes the cache entries associated with the specified keys.
    /// </summary>
    /// <remarks>This method removes all entries matching the provided keys in parallel. Removal events are
    /// emitted for each entry that is successfully removed. The operation is thread-safe.</remarks>
    /// <param name="keys">An enumerable collection of keys whose corresponding cache entries will be removed. Cannot be null.</param>
    public void RemoveRange(IEnumerable<TKey> keys)
    {
        var groups = keys.GroupBy(key => GetIdx(key));
        Parallel.ForEach(groups, group =>
        {
            var idx = group.Key;
            Locks[idx].EnterWriteLock();
            try
            {
                var quadrant = _quadrants[idx];
                foreach (var key in group)
                {
                    if (quadrant.Remove(key, out var value))
                    {
                        Emit(CacheAction.Removed, new(key, value!));
                    }
                }
            }
            finally
            {
                Locks[idx].ExitWriteLock();
            }
        });
    }

    /// <summary>
    /// Removes the value with the specified key from the collection.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key if the key was found; otherwise,
    /// the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns>true if the element is successfully found and removed; otherwise, false.</returns>
    public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue? value)
    {
        var idx = GetIdx(key);
        Locks[idx].EnterWriteLock();
        try
        {
            return _quadrants[idx].Remove(key, out value);
        }
        finally
        {
            Locks[idx].ExitWriteLock();
        }
    }

    /// <summary>
    /// Attempts to evict an entry from the collection and retrieve its key and value.
    /// </summary>
    /// <param name="evictedKey">When this method returns, contains the key of the evicted entry if an entry was evicted; otherwise, the default
    /// value for the type of the key.</param>
    /// <param name="evictedValue">When this method returns, contains the value of the evicted entry if an entry was evicted; otherwise, the
    /// default value for the type of the value.</param>
    /// <returns>true if an entry was successfully evicted; otherwise, false.</returns>
    public bool TryEvict([MaybeNullWhen(false)] out TKey evictedKey, [MaybeNullWhen(false)] out TValue? evictedValue)
    {
        evictedKey = default;
        evictedValue = default;
        for (var i = 0; i < 4; i++)
        {
            Locks[i].EnterWriteLock();
            try
            {
                if (_quadrants[i].TryEvict(out evictedKey, out evictedValue))
                {
                    return true;
                }
            }
            finally
            {
                Locks[i].ExitWriteLock();
            }
        }

        return false;
    }

    /// <summary>
    /// Adds the specified key and value to the collection, overwriting the existing value if the key already exists.
    /// </summary>
    /// <param name="key">The key to add to the collection. Cannot be null.</param>
    /// <param name="value">The value to associate with the specified key.</param>
    public void Add(TKey key, TValue value) => Set(key, value);

    /// <summary>
    /// Determines whether the dictionary contains the specified key.
    /// </summary>
    /// <param name="key">The key to locate in the dictionary. Cannot be null if the dictionary implementation does not allow null keys.</param>
    /// <returns>true if the dictionary contains an element with the specified key; otherwise, false.</returns>
    public bool ContainsKey(TKey key)
    {
        var idx = GetIdx(key);
        Locks[idx].EnterReadLock();
        try
        {
            return _quadrants[idx].ContainsKey(key);
        }
        finally
        {
            Locks[idx].ExitReadLock();
        }
    }

    /// <summary>
    /// Removes the value with the specified key from the collection.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns>true if the element is successfully removed; otherwise, false. This method also returns false if the key was not
    /// found in the collection.</returns>
    public bool Remove(TKey key) => TryRemove(key, out var _);

    /// <summary>
    /// Adds the specified key/value pair to the collection.
    /// </summary>
    /// <param name="item">The key/value pair to add to the collection. The key must not already exist in the collection.</param>
    public void Add(KeyValuePair<TKey, TValue> item) => Set(item.Key, item.Value);

    /// <summary>
    /// Determines whether the collection contains the specified key/value pair.
    /// </summary>
    /// <remarks>The comparison checks both the key and value for equality. This method is thread-safe for
    /// concurrent read operations.</remarks>
    /// <param name="item">The key/value pair to locate in the collection.</param>
    /// <returns>true if the collection contains an element with the specified key and value; otherwise, false.</returns>
    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        var idx = GetIdx(item.Key);
        Locks[idx].EnterReadLock();
        try
        {
            return _quadrants[idx].Contains(item);
        }
        finally
        {
            Locks[idx].ExitReadLock();
        }
    }

    /// <summary>
    /// Copies the elements of the collection to the specified array, starting at the specified array index.
    /// </summary>
    /// <remarks>The elements are copied in no particular order. This method is thread-safe with respect to
    /// concurrent read operations.</remarks>
    /// <param name="array">The one-dimensional array of key/value pairs that is the destination of the elements copied from the collection.
    /// The array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in the destination array at which copying begins. Must be non-negative and less than or
    /// equal to the length of the array.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="array"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="arrayIndex"/> is less than 0 or greater than the length of <paramref name="array"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if the number of elements in the source collection is greater than the available space from <paramref
    /// name="arrayIndex"/> to the end of the destination <paramref name="array"/>.</exception>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (arrayIndex < 0 || arrayIndex > array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), "Array index is out of range.");
        }

        if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentException("The destination array has insufficient space to copy the elements.");
        }

        foreach (var quadrant in _quadrants)
        {
            Locks[GetIdx(quadrant.Keys.First())].EnterReadLock();
            try
            {
                foreach (var kvp in quadrant)
                {
                    array[arrayIndex++] = kvp;
                }
            }
            finally
            {
                Locks[GetIdx(quadrant.Keys.First())].ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Removes the specified key/value pair from the collection.
    /// </summary>
    /// <param name="item">The key/value pair to remove from the collection. The key and value must both match an existing element for the
    /// pair to be removed.</param>
    /// <returns>true if the key/value pair was successfully removed; otherwise, false. This method also returns false if the
    /// key/value pair was not found in the collection.</returns>
    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        var idx = GetIdx(item.Key);
        Locks[idx].EnterWriteLock();
        try
        {
            return _quadrants[idx].Remove(item);
        }
        finally
        {
            Locks[idx].ExitWriteLock();
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection of key/value pairs in the dictionary.
    /// </summary>
    /// <remarks>The enumerator provides a snapshot of the dictionary's contents at the time of enumeration.
    /// The collection should not be modified during enumeration, as this may result in undefined behavior or
    /// exceptions. The enumeration is thread-safe for reading, but concurrent modifications are not
    /// supported.</remarks>
    /// <returns>An enumerator for the collection of key/value pairs contained in the dictionary.</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var quadrant in _quadrants)
        {
            Locks[GetIdx(quadrant.Keys.First())].EnterReadLock();
            try
            {
                foreach (var kvp in quadrant)
                {
                    yield return kvp;
                }
            }
            finally
            {
                Locks[GetIdx(quadrant.Keys.First())].ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An <see cref="IEnumerator"/> that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Asynchronously writes the contents of the current instance to the specified binary writer.
    /// </summary>
    /// <remarks>This method serializes the internal data structure to the provided writer. The format of the
    /// output depends on whether the data is blittable. Callers should ensure that the writer is properly initialized
    /// and disposed after use.</remarks>
    /// <param name="writer">The binary writer to which the data will be written. Must not be null.</param>
    /// <returns>A value task that represents the asynchronous write operation.</returns>
    protected override ValueTask WriteDataAsync(BinaryWriter writer)
    {
        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        for (var i = 0; i < 4; i++)
        {
            var dict = _quadrants[i];
            writer.Write(dict.Count);

            // For dictionaries, we must copy to a temporary span to use MemoryMarshal
            if (IsBlittable && dict.Count > 0)
            {
                var arr = ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Rent(dict.Count);
                try
                {
                    var idx = 0;
                    foreach (var kvp in dict)
                    {
                        arr[idx++] = kvp;
                    }

                    ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(arr.AsSpan(0, dict.Count));
                    writer.Write(bytes);
                }
                finally
                {
                    ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Return(arr);
                }
            }
            else
            {
                foreach (var kvp in dict)
                {
                    SerializeFallback(writer, kvp);
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Asynchronously reads and populates internal data structures from the specified binary reader.
    /// </summary>
    /// <remarks>This method overrides a base implementation to efficiently restore data, using optimized
    /// memory operations when both the source and target support blittable types. The caller is responsible for
    /// ensuring that the reader contains valid and expected data.</remarks>
    /// <param name="reader">The binary reader from which to read the data. Must be positioned at the start of the data to be read.</param>
    /// <param name="wasBlittable">A value indicating whether the data was previously serialized in a blittable format. If <see langword="true"/>,
    /// optimized reading is used when possible.</param>
    /// <returns>A value task that represents the asynchronous read operation.</returns>
    protected override ValueTask ReadDataAsync(BinaryReader reader, bool wasBlittable)
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        for (var i = 0; i < 4; i++)
        {
            var dict = _quadrants[i];
            dict.Clear();
            var count = reader.ReadInt32();

            if (wasBlittable && IsBlittable)
            {
                var buffer = ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Rent(count);
                try
                {
                    var bytes = MemoryMarshal.AsBytes(buffer.AsSpan(0, count));
                    reader.BaseStream.ReadExactly(bytes);
                    for (var j = 0; j < count; j++)
                    {
                        dict.Add(buffer[j].Key, buffer[j].Value);
                    }
                }
                finally
                {
                    ArrayPool<KeyValuePair<TKey, TValue>>.Shared.Return(buffer);
                }
            }
            else
            {
                for (var j = 0; j < count; j++)
                {
                    var keyval = DeserializeFallback(reader);
                    dict.Add(keyval.Key, keyval.Value);
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    private static int GetIdx(TKey key) => Math.Abs(key.GetHashCode()) % 4;

    /// <summary>
    /// Standard Dictionary Wrapper. Low overhead.
    /// </summary>
    private class SimpleQuadrant : IQuaternaryDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _dict = [];

        public int Count => _dict.Count;

        public bool IsReadOnly => ((IDictionary<TKey, TValue?>)_dict).IsReadOnly;

        public ICollection<TKey> Keys => _dict.Keys;

        public ICollection<TValue> Values => _dict.Values;

        public TValue this[TKey key]
        {
            get => _dict[key];
            set => _dict[key] = value;
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
            => _dict.TryGetValue(key, out value);

        public bool Set(TKey key, TValue value, out TValue? oldValue)
        {
            var exists = _dict.TryGetValue(key, out oldValue);
            _dict[key] = value;
            return !exists; // True if Added, False if Updated
        }

        public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value)
            => _dict.Remove(key, out value);

        public bool TryEvict([MaybeNullWhen(false)] out TKey k, [MaybeNullWhen(false)] out TValue? v)
        {
            k = default;
            v = default;
            return false; // Simple dictionary never evicts
        }

        public void Clear() => _dict.Clear();

        // IDictionary<TKey, TValue> implementation
        public void Add(TKey key, TValue value) => _dict.Add(key, value);

        public bool ContainsKey(TKey key) => _dict.ContainsKey(key);

        public bool Remove(TKey key) => _dict.Remove(key);

        // ICollection<KeyValuePair<TKey, TValue>> implementation
        public void Add(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)_dict).Add(item);

        public bool Contains(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)_dict).Contains(item);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((IDictionary<TKey, TValue>)_dict).CopyTo(array, arrayIndex);

        public bool Remove(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)_dict).Remove(item);

        // IEnumerable<KeyValuePair<TKey, TValue>> implementation
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dict.GetEnumerator();

        // IEnumerable implementation
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _dict.GetEnumerator();
    }

    private class LruQuadrant(int capacity) : IQuaternaryDictionary<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _index = [];
        private readonly LinkedList<KeyValuePair<TKey, TValue>> _lruList = new();

        public int Count => _index.Count;

        public ICollection<TKey> Keys => [.. _index.Values.Select(node => node.Value.Key)];

        public ICollection<TValue> Values => [.. _index.Values.Select(node => node.Value.Value)];

        public bool IsReadOnly => false;

        public TValue this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }

                throw new KeyNotFoundException($"The given key '{key}' was not present in the cache.");
            }
            set => Set(key, value, out _);
        }

        /// <summary>
        /// Attempts to retrieve the value associated with the specified key and indicates whether the key was found in
        /// the cache.
        /// </summary>
        /// <remarks>If the key is found, the associated entry is marked as most recently used. This
        /// method does not throw an exception if the key is not present.</remarks>
        /// <param name="key">The key whose associated value is to be retrieved.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found;
        /// otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the cache contains an element with the specified key; otherwise, false.</returns>
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (_index.TryGetValue(key, out var node))
            {
                // Move to front of LRU list
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                value = node.Value.Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Adds a new key-value pair to the cache or updates the value for an existing key, and returns whether the key
        /// was newly added.
        /// </summary>
        /// <remarks>If the cache exceeds its capacity after adding a new key, the least recently used
        /// entry is evicted. The method updates the usage order so that the specified key becomes the most recently
        /// used.</remarks>
        /// <param name="key">The key to add or update in the cache.</param>
        /// <param name="value">The value to associate with the specified key. Can be null for reference types.</param>
        /// <param name="oldValue">When this method returns, contains the previous value associated with the key if the key already existed;
        /// otherwise, the default value for the type.</param>
        /// <returns>true if the key was newly added to the cache; false if the value for an existing key was updated.</returns>
        public bool Set(TKey key, TValue value, out TValue? oldValue)
        {
            if (_index.TryGetValue(key, out var node))
            {
                // Update existing
                oldValue = node.Value.Value;
                node.Value = new KeyValuePair<TKey, TValue>(key, value);
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return false; // Updated
            }

            // Add new
            var newNode = new LinkedListNode<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(key, value));
            _lruList.AddFirst(newNode);
            _index[key] = newNode;

            // Evict if over capacity
            if (_index.Count > _capacity)
            {
                TryEvict(out _, out _);
            }

            oldValue = default;

            return true; // Added
        }

        /// <summary>
        /// Removes the value with the specified key from the collection and retrieves the associated value if the key
        /// was found.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key if the key was found;
        /// otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the element is successfully found and removed; otherwise, false.</returns>
        public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue? value)
        {
            if (_index.TryGetValue(key, out var node))
            {
                value = node.Value.Value;
                _lruList.Remove(node);
                _index.Remove(key);
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Attempts to evict the least recently used entry from the cache and retrieve its key and value.
        /// </summary>
        /// <remarks>Use this method to remove the least recently used item when the cache exceeds its
        /// capacity. If the cache is not over capacity, no entries are evicted and the output parameters are set to
        /// their default values.</remarks>
        /// <param name="evictedKey">When this method returns <see langword="true"/>, contains the key of the evicted entry; otherwise, the
        /// default value for <typeparamref name="TKey"/>.</param>
        /// <param name="evictedValue">When this method returns <see langword="true"/>, contains the value of the evicted entry; otherwise, the
        /// default value for <typeparamref name="TValue"/>.</param>
        /// <returns><see langword="true"/> if an entry was evicted from the cache; otherwise, <see langword="false"/>.</returns>
        public bool TryEvict([MaybeNullWhen(false)] out TKey evictedKey, [MaybeNullWhen(false)] out TValue? evictedValue)
        {
            if (_lruList.Count > capacity)
            {
                // Remove LRU Head
                var lruNode = _lruList.First!;
                if (lruNode != null)
                {
                    evictedKey = lruNode.Value.Key;
                    evictedValue = lruNode.Value.Value;
                    _lruList.RemoveFirst();
                    _index.Remove(evictedKey);
                    return true;
                }
            }

            evictedKey = default;
            evictedValue = default;
            return false;
        }

        public void Clear()
        {
            _lruList.Clear();
            _index.Clear();
        }

        public void Add(TKey key, TValue value) => Set(key, value, out var _);

        public bool ContainsKey(TKey key) => Keys.Contains(key);

        public bool Remove(TKey key) => Remove(key, out var _);

        public void Add(KeyValuePair<TKey, TValue> item) => Set(item.Key, item.Value, out var _);

        public bool Contains(KeyValuePair<TKey, TValue> item)
            => _index.TryGetValue(item.Key, out var node) && EqualityComparer<TValue>.Default.Equals(node.Value.Value, item.Value);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (array is null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (arrayIndex < 0 || arrayIndex > array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), "Array index is out of range.");
            }

            if (array.Length - arrayIndex < Count)
            {
                throw new ArgumentException("The destination array has insufficient space to copy the elements.");
            }

            foreach (var kvp in _lruList)
            {
                array[arrayIndex++] = kvp;
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
            => Contains(item) && Remove(item.Key, out var _);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            => _lruList.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
#endif
