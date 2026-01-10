// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET6_0_OR_GREATER

using System.Buffers;
using System.Collections;

namespace CP.Reactive;

/// <summary>
/// Represents a collection that organizes elements into four separate lists, allowing efficient storage and retrieval
/// of items in each quadrant.
/// </summary>
/// <remarks>QuaternaryList of T is useful for scenarios where data needs to be partitioned into four distinct
/// groups, such as spatial partitioning or multi-channel data processing. Each quadrant is internally managed as a
/// separate list, and the class provides serialization support for efficient persistence. Thread safety is not
/// guaranteed; external synchronization is required if accessed concurrently.</remarks>
/// <typeparam name="T">The type of elements contained in the quaternary lists. Must be non-nullable.</typeparam>
public class QuaternaryList<T> : QuaternaryBase<T>, IQuaternaryList<T>
    where T : notnull
{
    private readonly Dictionary<string, IIndex<T>> _indices = [];
    private readonly IQuaternaryList<T>[] _quadrants = new IQuaternaryList<T>[4];

    /// <summary>
    /// Initializes a new instance of the <see cref="QuaternaryList{T}"/> class with an optional capacity limit per quadrant.
    /// </summary>
    /// <remarks>When a capacity limit is provided, the list enforces per-quadrant limits and evicts items
    /// using an LRU strategy. Without a limit, quadrants use a standard list implementation with no automatic eviction.
    /// This affects memory usage and item retention behavior.</remarks>
    /// <param name="capacityLimit">The maximum total number of items allowed across all quadrants. If specified, each quadrant will be limited to
    /// approximately one quarter of this value using a least-recently-used (LRU) eviction strategy. If null, quadrants
    /// have no capacity limit.</param>
    public QuaternaryList(int? capacityLimit = null)
    {
        // Strategy Selection:
        // If a limit is provided, use the LRU strategy.
        // Otherwise use the lightweight List strategy.
        var limitPerQuadrant = capacityLimit.HasValue
            ? capacityLimit.Value / 4
            : 0;

        for (var i = 0; i < 4; i++)
        {
            _quadrants[i] = capacityLimit.HasValue
                ? new LruQuadrant(limitPerQuadrant) // Strategy A: LRU
                : new SimpleQuadrant();             // Strategy B: Standard List
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
    /// Gets a value indicating whether the collection is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Adds the specified item to the cache, potentially triggering eviction if the cache is full.
    /// </summary>
    /// <remarks>If adding the item causes the cache to exceed its capacity, the least recently used item in
    /// the relevant quadrant may be evicted. Eviction notifications are sent before and after the item is removed. This
    /// method is thread-safe.</remarks>
    /// <param name="item">The item to add to the cache. Cannot be null if the cache does not support null values.</param>
    public void Add(T item)
    {
        var idx = QuaternaryList<T>.GetIdx(item);

        Emit(CacheAction.Adding, item);

        Locks[idx].EnterWriteLock();
        try
        {
            var quad = _quadrants[idx];

            // 1. Perform Add
            quad.Add(item);

            // 2. Check LRU Eviction
            // If the quadrant is full, it will return the item it wants to kill
            var evicted = quad.CheckEviction();
            if (evicted is not null)
            {
                quad.Remove(evicted);

                Emit(CacheAction.Evicted, evicted);
                foreach (var index in _indices.Values)
                {
                    index.OnRemoved(evicted);
                }
            }
        }
        finally
        {
            Locks[idx].ExitWriteLock();
        }

        foreach (var index in _indices.Values)
        {
            index.OnAdded(item);
        }

        Emit(CacheAction.Added, item);
    }

    /// <summary>
    /// Adds a collection of items to the cache, distributing them to their appropriate quadrants based on their index.
    /// </summary>
    /// <remarks>Items are grouped and added according to their calculated index. After addition, items may be
    /// evicted from their quadrant if eviction conditions are met. Events are emitted for both addition and eviction
    /// actions.</remarks>
    /// <param name="items">The collection of items to add to the cache. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is null.</exception>
    public void AddRange(IEnumerable<T> items)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var groups = items.GroupBy(GetIdx);
        foreach (var group in groups)
        {
            var idx = group.Key;
            Locks[idx].EnterWriteLock();
            try
            {
                var quad = _quadrants[idx];
                foreach (var item in group)
                {
                    Emit(CacheAction.Adding, item);
                    quad.Add(item);
                    foreach (var index in _indices.Values)
                    {
                        index.OnAdded(item);
                    }

                    Emit(CacheAction.Added, item);
                }

                // After adding all items, check for evictions
                T? evicted;
                while ((evicted = quad.CheckEviction()) is not null)
                {
                    quad.Remove(evicted);
                    foreach (var index in _indices.Values)
                    {
                        index.OnRemoved(evicted);
                    }

                    Emit(CacheAction.Evicted, evicted);
                }
            }
            finally
            {
                Locks[idx].ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// Removes all specified items from the collection.
    /// </summary>
    /// <param name="items">The sequence of items to remove from the collection. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is null.</exception>
    public void RemoveRange(IEnumerable<T> items)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var groups = items.GroupBy(GetIdx);
        foreach (var group in groups)
        {
            var idx = group.Key;
            Locks[idx].EnterWriteLock();
            try
            {
                var quad = _quadrants[idx];
                foreach (var item in group)
                {
                    var success = quad.Remove(item);
                    if (success)
                    {
                        foreach (var index in _indices.Values)
                        {
                            index.OnRemoved(item);
                        }

                        Emit(CacheAction.Removed, item);
                    }
                }
            }
            finally
            {
                Locks[idx].ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// Removes the specified item from the collection.
    /// </summary>
    /// <remarks>This method is thread-safe and can be called concurrently from multiple threads. If the item
    /// appears multiple times in the collection, only the first occurrence is removed.</remarks>
    /// <param name="item">The item to remove from the collection.</param>
    /// <returns>true if the item was successfully removed; otherwise, false. This method also returns false if the item was not
    /// found in the collection.</returns>
    public bool Remove(T item)
    {
        var idx = QuaternaryList<T>.GetIdx(item);

        bool success;
        Locks[idx].EnterWriteLock();
        try
        {
            success = _quadrants[idx].Remove(item);
        }
        finally
        {
            Locks[idx].ExitWriteLock();
        }

        if (success)
        {
            foreach (var index in _indices.Values)
            {
                index.OnRemoved(item);
            }

            Emit(CacheAction.Removed, item);
        }

        return success;
    }

    /// <summary>
    /// Determines whether the specified item exists in the list.
    /// </summary>
    /// <remarks>If the item is found, its position in the list may be updated to reflect recent access. This
    /// method is thread-safe.</remarks>
    /// <param name="item">The item to locate in the list.</param>
    /// <returns>true if the item is found in the list; otherwise, false.</returns>
    public bool Contains(T item)
    {
        var idx = QuaternaryList<T>.GetIdx(item);

        // We use UpgradeableReadLock because if we find the item,
        // we might need to write to the linked list to move it to the front.
        Locks[idx].EnterUpgradeableReadLock();
        try
        {
            var exists = _quadrants[idx].Contains(item);
            if (exists)
            {
                Locks[idx].EnterWriteLock();
                try
                {
                    _quadrants[idx].Touch(item);
                }
                finally
                {
                    Locks[idx].ExitWriteLock();
                }
            }

            return exists;
        }
        finally
        {
            Locks[idx].ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Determines whether the collection contains a specific value asynchronously.
    /// </summary>
    /// <param name="item">The value to locate in the collection.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if the item
    /// is found in the collection; otherwise, <see langword="false"/>.</returns>
    public ValueTask<bool> ContainsAsync(T item) => new(Contains(item));

    /// <summary>
    /// Removes all items from the cache.
    /// </summary>
    /// <remarks>This method is thread-safe and can be called concurrently with other cache operations. After
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
        foreach (var index in _indices.Values)
        {
            index.Clear();
        }

        Emit(CacheAction.Cleared, default);
    }

    /// <summary>
    /// Checks all quadrants for an item that should be evicted, returning the first such item found.
    /// </summary>
    /// <remarks>
    /// Iterates through each quadrant and calls <see cref="IQuaternaryList{T}.CheckEviction"/>. Returns the first non-null item that should be evicted, or <c>null</c> if no eviction is needed.
    /// </remarks>
    /// <returns>The item to be evicted if any quadrant requires eviction; otherwise, <c>null</c>.</returns>
    public T? CheckEviction()
    {
        for (var i = 0; i < _quadrants.Length; i++)
        {
            var evicted = _quadrants[i].CheckEviction();
            if (evicted is not null)
            {
                return evicted;
            }
        }

        return default;
    }

    /// <summary>
    /// Updates the access metadata for the specified item, marking it as recently used within the collection.
    /// </summary>
    /// <param name="item">The item whose access metadata is to be updated. Cannot be null.</param>
    public void Touch(T item)
    {
        var idx = QuaternaryList<T>.GetIdx(item);
        Locks[idx].EnterWriteLock();
        try
        {
            _quadrants[idx].Touch(item);
        }
        finally
        {
            Locks[idx].ExitWriteLock();
        }
    }

    /// <summary>
    /// Ensures that the total capacity of the collection is at least the specified value, expanding storage if
    /// necessary.
    /// </summary>
    /// <remarks>If the current capacity is less than the specified value, the method increases the capacity
    /// to accommodate at least the specified number of elements. Capacity is distributed evenly across internal
    /// quadrants. This method is thread-safe.</remarks>
    /// <param name="newcapacity">The minimum total capacity to ensure for the collection. Must be a non-negative integer.</param>
    public void EnsureCapacity(int newcapacity)
    {
        var limitPerQuadrant = newcapacity / 4;
        for (var i = 0; i < 4; i++)
        {
            Locks[i].EnterWriteLock();
            try
            {
                _quadrants[i].EnsureCapacity(limitPerQuadrant);
            }
            finally
            {
                Locks[i].ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// Copies the elements of the collection to the specified array, starting at the specified array index.
    /// </summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the collection. The array must
    /// have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in the destination array at which copying begins.</param>
    /// <exception cref="NotImplementedException">The method is not implemented.</exception>
    public void CopyTo(T[] array, int arrayIndex) => throw new NotImplementedException();

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <remarks>Enumeration is thread-safe and reflects the state of the collection at the time of
    /// enumeration. The order of items is determined by the internal quadrant structure and may not correspond to
    /// insertion order.</remarks>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < 4; i++)
        {
            Locks[i].EnterReadLock();
            try
            {
                foreach (var item in _quadrants[i])
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
    /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Adds a secondary index to enable efficient lookups based on a specified key selector.
    /// </summary>
    /// <remarks>If an index with the specified name already exists, it will be replaced. The index is
    /// immediately populated with all existing items.</remarks>
    /// <typeparam name="TKey">The type of the key used for indexing. Must be non-nullable.</typeparam>
    /// <param name="name">The unique name of the index to add. Cannot be null.</param>
    /// <param name="keySelector">A function that extracts the key from each item for indexing. Cannot be null.</param>
    public void AddIndex<TKey>(string name, Func<T, TKey> keySelector)
        where TKey : notnull
    {
        var index = new SecondaryIndex<TKey, T>(keySelector);
        _indices[name] = index;

        // Populate with existing items
        for (var i = 0; i < 4; i++)
        {
            foreach (var item in _quadrants[i])
            {
                index.OnAdded(item);
            }
        }
    }

    /// <summary>
    /// Asynchronously retrieves an item from the specified secondary index using the provided key.
    /// </summary>
    /// <remarks>If the specified index does not exist or the key is not found, the method returns null. This
    /// method does not perform any asynchronous I/O and completes synchronously.</remarks>
    /// <typeparam name="TKey">The type of the key used to look up the item in the index. Must be non-nullable.</typeparam>
    /// <param name="indexName">The name of the secondary index to search. Cannot be null.</param>
    /// <param name="key">The key value to locate within the specified index.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result contains the item of type T if found;
    /// otherwise, null.</returns>
    public ValueTask<T?> FindByIndexAsync<TKey>(string indexName, TKey key)
        where TKey : notnull
    {
        if (_indices.TryGetValue(indexName, out var idx) && idx is SecondaryIndex<TKey, T> sidx)
        {
            return new ValueTask<T?>(sidx.Find(key));
        }

        return new ValueTask<T?>(default(T));
    }

    /// <summary>
    /// Returns an enumerable collection of elements that satisfy the specified predicate.
    /// </summary>
    /// <remarks>Enumeration is thread-safe and reflects the state of the collection at the time of iteration.
    /// The returned enumerable evaluates the predicate lazily as elements are iterated.</remarks>
    /// <param name="predicate">A function to test each element for a condition. The method returns elements for which this function returns
    /// <see langword="true"/>.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> containing the elements that match the specified predicate. The collection may
    /// be empty if no elements satisfy the condition.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is <see langword="null"/>.</exception>
    public IEnumerable<T> Query(Func<T, bool> predicate)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        for (var i = 0; i < 4; i++)
        {
            Locks[i].EnterReadLock();
            try
            {
                foreach (var item in _quadrants[i])
                {
                    if (predicate(item))
                    {
                        yield return item;
                    }
                }
            }
            finally
            {
                Locks[i].ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Creates a new transaction for performing a series of cache operations atomically.
    /// </summary>
    /// <remarks>Use the returned transaction to group multiple cache operations into a single atomic unit.
    /// All changes made within the transaction are either committed together or rolled back if the transaction is not
    /// completed successfully. This method acquires global locks to ensure transactional consistency; concurrent
    /// transactions may be blocked until the current transaction is completed.</remarks>
    /// <returns>A <see cref="CacheTransaction{T}"/> instance that represents the new transaction.</returns>
    public CacheTransaction<T> CreateTransaction()
    {
        AcquireGlobalLocks();
        return new(this);
    }

    internal void AcquireGlobalLocks()
    {
        for (var i = 0; i < 4; i++)
        {
            Locks[i].EnterWriteLock();
        }
    }

    internal void ReleaseGlobalLocks()
    {
        for (var i = 0; i < 4; i++)
        {
            Locks[i].ExitWriteLock();
        }
    }

    internal void InternalAddWithoutLock(T item)
    {
        var idx = QuaternaryList<T>.GetIdx(item);
        _quadrants[idx].Add(item);
        foreach (var index in _indices.Values)
        {
            index.OnAdded(item);
        }

        var evicted = _quadrants[idx].CheckEviction();
        if (evicted is not null)
        {
            _quadrants[idx].Remove(evicted);
            foreach (var index in _indices.Values)
            {
                index.OnRemoved(evicted);
            }

            Emit(CacheAction.Evicted, evicted);
        }
    }

    /// <summary>
    /// Asynchronously writes the data for all quads to the specified binary writer.
    /// </summary>
    /// <remarks>If the data type is blittable and the quad list is not empty, the method writes the data
    /// using a zero-allocation memory copy for improved performance. Otherwise, it serializes each item individually.
    /// This method does not perform any I/O asynchronously; the returned ValueTask is always completed.</remarks>
    /// <param name="writer">The binary writer to which the quad data will be written. Must not be null.</param>
    /// <returns>A value task that represents the asynchronous write operation.</returns>
    protected override ValueTask WriteDataAsync(BinaryWriter writer)
    {
        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        for (var i = 0; i < 4; i++)
        {
            var list = _quadrants[i];
            writer.Write(list.Count);

            if (IsBlittable && list.Count > 0 && typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) is null)
            {
                // Call MemoryMarshal.AsBytes<T> for non-nullable value types
            }
            else
            {
                foreach (var item in list)
                {
                    SerializeFallback(writer, item);
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Asynchronously reads and deserializes data from the specified binary reader into the internal data structure.
    /// </summary>
    /// <remarks>If both <paramref name="wasBlittable"/> and the current instance's blittable flag are <see
    /// langword="true"/>, the method uses a more efficient deserialization path. Otherwise, a fallback deserialization
    /// is used. The method does not advance the reader beyond the data it consumes.</remarks>
    /// <param name="reader">The binary reader from which to read the serialized data.</param>
    /// <param name="wasBlittable">A value indicating whether the data was written in a blittable format, which may enable optimized
    /// deserialization.</param>
    /// <returns>A value task that represents the asynchronous read operation.</returns>
    protected override ValueTask ReadDataAsync(BinaryReader reader, bool wasBlittable)
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        for (var i = 0; i < 4; i++)
        {
            var list = _quadrants[i];
            list.Clear();
            var count = reader.ReadInt32();
            if (count == 0)
            {
                continue;
            }

            if (wasBlittable && IsBlittable && typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) is null)
            {
                // Pre-size the list
                list.EnsureCapacity(count);

                // Create a temporary span-compatible array to read the whole block
                var buffer = ArrayPool<T>.Shared.Rent(count);
                try
                {
                    // Only call MemoryMarshal.AsBytes<T> for non-nullable value types
                    var tSpan = buffer.AsSpan(0, count);
                    ////Span<byte> byteBuffer = MemoryMarshal.AsBytes(tSpan);
                    ////reader.BaseStream.ReadExactly(byteBuffer);

                    ////for (var j = 0; j < count; j++)
                    ////{
                    ////    list.Add(buffer[j]);
                    ////}
                }
                finally
                {
                    ArrayPool<T>.Shared.Return(buffer);
                }
            }
            else
            {
                for (var j = 0; j < count; j++)
                {
                    list.Add(DeserializeFallback(reader));
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    private static int GetIdx(T item) => Math.Abs(item.GetHashCode()) % 4;

    /// <summary>
    /// Represents a quadrant that manages items using a Least Recently Used (LRU) eviction policy, maintaining a fixed
    /// maximum capacity.
    /// </summary>
    /// <remarks>When the number of items exceeds the specified capacity, the least recently used item is
    /// identified for eviction. This class is typically used as an internal component of a larger cache or spatial
    /// partitioning system to efficiently manage item lifetimes based on usage patterns.</remarks>
    /// <param name="capacity">The maximum number of items that the quadrant can hold before eviction is considered. Must be a non-negative
    /// integer.</param>
    private class LruQuadrant(int capacity) : IQuaternaryList<T>
    {
        private readonly Dictionary<T, LinkedListNode<T>> _map = []; // C# 12
        private readonly LinkedList<T> _list = new();

        public int Capacity => capacity;

        public int Count => _map.Count;

        public bool IsReadOnly => ((ICollection<T>)_list).IsReadOnly;

        public T this[int index]
        {
            get
            {
                var current = _list.First;
                for (var i = 0; i < index; i++)
                {
                    current = current!.Next;
                }

                return current!.Value;
            }

            set
            {
                var current = _list.First;
                for (var i = 0; i < index; i++)
                {
                    current = current!.Next;
                }

                if (current is not null)
                {
                    // Update map
                    _map.Remove(current.Value);
                    _map[value] = current;

                    // Update list node
                    current.Value = value;
                }
            }
        }

        public void Add(T item)
        {
            if (_map.TryGetValue(item, out var node))
            {
                // Item exists, just update value and move to MRU (Most Recently Used)
                _list.Remove(node);
                _list.AddLast(node);
            }
            else
            {
                // New item
                var newNode = new LinkedListNode<T>(item);
                _map[item] = newNode;
                _list.AddLast(newNode);
            }
        }

        public T? CheckEviction()
        {
            if (_map.Count > Capacity)
            {
                // Return the Least Recently Used (Head of list)
                return _list.First!.Value;
            }

            return default;
        }

        public bool Remove(T item)
        {
            if (_map.TryGetValue(item, out var node))
            {
                _map.Remove(item);
                _list.Remove(node);
                return true;
            }

            return false;
        }

        public bool Contains(T item) => _map.ContainsKey(item);

        public void Touch(T item)
        {
            if (_map.TryGetValue(item, out var node))
            {
                _list.Remove(node);
                _list.AddLast(node);
            }
        }

        public void Clear()
        {
            _map.Clear();
            _list.Clear();
        }

        public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        public void EnsureCapacity(int newcapacity) => capacity = newcapacity;

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _list.GetEnumerator();
    }

    /// <summary>
    /// Provides a simple, non-evicting implementation of the <see cref="IQuaternaryList{T}"/> interface using a list to store
    /// items.
    /// </summary>
    /// <remarks>This class does not perform automatic eviction or usage tracking. Items are retained until
    /// explicitly removed or the collection is cleared. Suitable for scenarios where eviction policies are not
    /// required.</remarks>
    private class SimpleQuadrant : IQuaternaryList<T>
    {
        private readonly List<T> _list = [];

        public int Count => _list.Count;

        public bool IsReadOnly => ((ICollection<T>)_list).IsReadOnly;

        public T this[int index]
        {
            get => _list[index];
            set => _list[index] = value;
        }

        public void Add(T item) => _list.Add(item);

        // Simple lists don't evict automatically
        public T? CheckEviction() => default;

        public bool Remove(T item) => _list.Remove(item);

        public bool Contains(T item) => _list.Contains(item);

        // Simple lists don't track usage
        public void Touch(T item)
        {
        }

        public void Clear() => _list.Clear();

        public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        public void EnsureCapacity(int newcapacity) => _list.EnsureCapacity(newcapacity);

        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

        public int IndexOf(T item) => _list.IndexOf(item);

        public void Insert(int index, T item) => _list.Insert(index, item);

        public void RemoveAt(int index) => _list.RemoveAt(index);
    }

    private class SecondaryIndex<TKey, TItem> : IIndex<TItem>
        where TItem : notnull
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TItem> _map = [];
        private readonly Func<TItem, TKey> _keySelector;

        public SecondaryIndex(Func<TItem, TKey> keySelector) => _keySelector = keySelector;

        public void OnAdded(TItem item) => _map[_keySelector(item)] = item;

        public void OnRemoved(TItem item) => _map.Remove(_keySelector(item));

        public void Clear() => _map.Clear();

        public TItem? Find(TKey key) => _map.TryGetValue(key, out var item) ? item : default;
    }
}
#endif
