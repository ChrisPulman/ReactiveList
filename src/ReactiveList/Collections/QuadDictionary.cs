// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Collections;
#else
namespace CP.Primitives.Collections;
#endif
/// <summary>
/// Provides a high-performance, pooled hash table for storing key/value pairs, supporting fast lookup and insertion
/// operations with customizable key equality comparison.
/// </summary>
/// <remarks>
/// QuadDictionary is an internal shard optimized for scenarios where frequent additions and lookups are required,
/// and memory efficiency is important. It uses array pooling to minimize allocations and supports custom equality
/// comparers for keys. The dictionary is not thread-safe and should not be accessed concurrently from multiple threads.
/// Call Dispose to return internal arrays to the pool when the dictionary is no longer needed.
/// Change notifications are NOT emitted from this class - the parent QuaternaryBase handles all notifications
/// through its Stream pipeline.
/// </remarks>
/// <typeparam name="TKey">The type of keys in the dictionary. Can be null if the comparer supports it.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
[SkipLocalsInit]
public sealed class QuadDictionary<TKey, TValue> : IQuad<KeyValuePair<TKey, TValue>>
{
    /// <summary>Minimum ArrayPool size as a power of two.</summary>
    private const int MinimumSize = 16;

    private const double LoadFactor = 0.72;

    /// <summary>Capacity multiplier used when growing pooled storage.</summary>
    private const int CapacityGrowthFactor = 2;

    /// <summary>Base value for encoding the freelist.</summary>
    private const int FreeListSentinel = -3;

    /// <summary>Marks the end of a bucket chain.</summary>
    private const int EndOfChain = -1;

    private readonly IEqualityComparer<TKey> _comparer;

    private Entry[] _entries;

    /// <summary>Bucket value is the 1-based entry index, with zero representing empty.</summary>
    private int[] _buckets;

    /// <summary>Current bucket array length as a power of two.</summary>
    private int _bucketsLength;

    private int _entryIndex;

    private int _resizeThreshold;

    /// <summary>Head of the free list, or -1 when there are no free entries.</summary>
    private int _freeList;

    private int _freeCount;

    /// <summary>Initializes a new instance of the <see cref="QuadDictionary{TKey, TValue}"/> class with default settings.</summary>
    /// <remarks>This constructor creates an empty QuadDictionary using default configuration values. Use this
    /// overload when no custom comparer or options are required.</remarks>
    public QuadDictionary()
        : this(null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="QuadDictionary{TKey, TValue}"/> class.</summary>
    /// <param name="comparer">Optional equality comparer for keys.</param>
    public QuadDictionary(IEqualityComparer<TKey>? comparer = null)
    {
        _comparer = comparer ?? EqualityComparer<TKey>.Default;
        _buckets = ArrayPool<int>.Shared.Rent(MinimumSize);
        _entries = ArrayPool<Entry>.Shared.Rent(MinimumSize);
        _bucketsLength = MinimumSize;
        _resizeThreshold = (int)(_bucketsLength * LoadFactor);
        _freeList = -1;
        _freeCount = 0;
        _buckets.AsSpan().Clear();
    }

    /// <summary>Gets the number of key/value pairs contained in the dictionary.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _entryIndex - _freeCount;
    }

    /// <summary>Gets all keys in the dictionary.</summary>
    public IEnumerable<TKey> Keys
    {
        get
        {
            for (var i = 0; i < _entryIndex; i++)
            {
                ref var entry = ref _entries[i];
                if (entry.GetNext() < EndOfChain)
                {
                    continue; // Skip free entries (Next < -1 means in freelist)
                }

                yield return entry.GetKey();
            }
        }
    }

    /// <summary>Gets all values in the dictionary.</summary>
    public IEnumerable<TValue> Values
    {
        get
        {
            for (var i = 0; i < _entryIndex; i++)
            {
                ref var entry = ref _entries[i];
                if (entry.GetNext() < EndOfChain)
                {
                    continue; // Skip free entries (Next < -1 means in freelist)
                }

                yield return entry.GetValue();
            }
        }
    }

    /// <summary>Gets or sets the value associated with the specified key.</summary>
    /// <param name="key">The key of the value to get or set.</param>
    /// <returns>The value associated with the specified key.</returns>
    /// <exception cref="KeyNotFoundException">The key does not exist in the dictionary.</exception>
    public TValue this[TKey key]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            ref var valueRef = ref GetValueRefOrAddDefault(key, out _);
            valueRef = value;
        }
    }

    /// <summary>Gets a reference to the value for the specified key, adding a default entry if the key does not exist.</summary>
    /// <param name="key">The key to look up or add.</param>
    /// <param name="exists">When this method returns, true if the key existed; otherwise, false.</param>
    /// <returns>A reference to the value associated with the key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue? GetValueRefOrAddDefault(TKey key, out bool exists)
    {
        var hashCode = InternalGetHashCode(key);
        ref var bucket = ref _buckets[GetBucketIndex(hashCode)];
        var index = bucket - 1;

        // lookup phase
        while (index >= 0)
        {
            ref var entry = ref _entries[index];
            if (entry.GetHashCodeValue() == hashCode && _comparer.Equals(entry.GetKey(), key))
            {
                exists = true;
                return ref Entry.ValueRef(ref entry);
            }

            index = entry.GetNext();
        }

        // add phase
        exists = false;
        return ref AddNewEntry(key, hashCode, ref bucket);
    }

    /// <summary>Attempts to add the specified key and value to the dictionary.</summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <returns>true if the key/value pair was added; false if the key already exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(TKey key, TValue value)
    {
        var hashCode = InternalGetHashCode(key);
        ref var bucket = ref _buckets[GetBucketIndex(hashCode)];
        var index = bucket - 1;

        // Check if key exists
        while (index >= 0)
        {
            ref var entry = ref _entries[index];
            if (entry.GetHashCodeValue() == hashCode && _comparer.Equals(entry.GetKey(), key))
            {
                return false; // Key already exists
            }

            index = entry.GetNext();
        }

        // Add new entry
        ref var newEntry = ref AddNewEntry(key, hashCode, ref bucket);
        newEntry = value;
        return true;
    }

    /// <summary>Adds the specified key and value to the dictionary.</summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <exception cref="ArgumentException">An element with the same key already exists.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TKey key, TValue value)
    {
        if (TryAdd(key, value))
        {
            return;
        }

        throw new ArgumentException("An element with the same key already exists.");
    }

    /// <summary>Gets the value associated with the specified key.</summary>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">When this method returns, the value associated with the specified key, if found.</param>
    /// <returns>true if the key was found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var hashCode = InternalGetHashCode(key);
        var index = _buckets[GetBucketIndex(hashCode)] - 1;

        while (index >= 0)
        {
            ref var entry = ref _entries[index];
            if (entry.GetHashCodeValue() == hashCode && _comparer.Equals(entry.GetKey(), key))
            {
                value = entry.GetValue();
                return true;
            }

            index = entry.GetNext();
        }

        value = default;
        return false;
    }

    /// <summary>Determines whether the dictionary contains the specified key.</summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>true if the dictionary contains an element with the specified key; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(TKey key) => TryGetValue(key, out _);

    /// <summary>Removes the value with the specified key from the dictionary.</summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns>true if the element was removed; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(TKey key) => Remove(key, out _);

    /// <summary>Removes the value with the specified key from the dictionary, returning the removed value.</summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <param name="value">When this method returns, the removed value, if the key was found.</param>
    /// <returns>true if the element was removed; otherwise, false.</returns>
    public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var hashCode = InternalGetHashCode(key);
        var bucketIndex = GetBucketIndex(hashCode);
        ref var bucket = ref _buckets[bucketIndex];
        var index = bucket - 1;
        var lastIndex = -1;

        while (index >= 0)
        {
            ref var entry = ref _entries[index];
            if (entry.GetHashCodeValue() == hashCode && _comparer.Equals(entry.GetKey(), key))
            {
                value = entry.GetValue();

                // Remove from bucket chain
                if (lastIndex < 0)
                {
                    bucket = entry.GetNext() + 1;
                }
                else
                {
                    _entries[lastIndex].SetNext(entry.GetNext());
                }

                // Clear entry and add to free list
                if (ArrayPoolClearHelper.IsReferenceOrContainsReferences<TKey>())
                {
                    entry.SetKey(default!);
                }

                if (ArrayPoolClearHelper.IsReferenceOrContainsReferences<TValue>())
                {
                    entry.SetValue(default);
                }

                entry.SetNext(FreeListSentinel - _freeList);
                _freeList = index;
                _freeCount++;

                return true;
            }

            lastIndex = index;
            index = entry.GetNext();
        }

        value = default;
        return false;
    }

    /// <summary>Removes all keys and values from the dictionary.</summary>
    public void Clear()
    {
        if (_entryIndex <= 0)
        {
            return;
        }

        _buckets.AsSpan(0, _bucketsLength).Clear();
        if (ArrayPoolClearHelper.IsReferenceOrContainsReferences<Entry>())
        {
            _entries.AsSpan(0, _entryIndex).Clear();
        }

        _entryIndex = 0;
        _freeList = -1;
        _freeCount = 0;
    }

    /// <summary>Ensures that the dictionary can hold up to the specified number of entries without resizing.</summary>
    /// <param name="capacity">The number of entries the dictionary should be able to hold.</param>
    public void EnsureCapacity(int capacity)
    {
        if (capacity <= _resizeThreshold)
        {
            return;
        }

        var requiredBucketCount = (int)Math.Ceiling(capacity / LoadFactor);
        var newSize = BitOperationsCompat.RoundUpToPowerOf2((uint)requiredBucketCount);
        ResizeTo((int)newSize);
    }

    /// <summary>Returns an enumerator that iterates through the dictionary.</summary>
    /// <returns>An enumerator for the dictionary.</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc/>
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => new EnumeratorWrapper(this);

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => new EnumeratorWrapper(this);

    /// <summary>Copies all keys to a list.</summary>
    /// <param name="list">The list to copy to.</param>
    public void CopyKeysTo(List<TKey> list)
    {
        ThrowHelper.ThrowIfNull(list);

        for (var i = 0; i < _entryIndex; i++)
        {
            ref var entry = ref _entries[i];
            if (entry.GetNext() < EndOfChain)
            {
                continue; // Skip free entries (Next < -1 means in freelist)
            }

            list.Add(entry.GetKey());
        }
    }

    /// <summary>Copies all values to a list.</summary>
    /// <param name="list">The list to copy to.</param>
    public void CopyValuesTo(List<TValue> list)
    {
        ThrowHelper.ThrowIfNull(list);

        for (var i = 0; i < _entryIndex; i++)
        {
            ref var entry = ref _entries[i];
            if (entry.GetNext() < EndOfChain)
            {
                continue; // Skip free entries (Next < -1 means in freelist)
            }

            list.Add(entry.GetValue());
        }
    }

    /// <summary>Copies all key/value pairs to a list.</summary>
    /// <param name="list">The list to copy to.</param>
    public void CopyTo(List<KeyValuePair<TKey, TValue>> list)
    {
        ThrowHelper.ThrowIfNull(list);

        for (var i = 0; i < _entryIndex; i++)
        {
            ref var entry = ref _entries[i];
            if (entry.GetNext() < EndOfChain)
            {
                continue; // Skip free entries (Next < -1 means in freelist)
            }

            list.Add(new KeyValuePair<TKey, TValue>(entry.GetKey(), entry.GetValue()));
        }
    }

    /// <summary>Returns arrays to pool and cleans up resources.</summary>
    public void Dispose()
    {
        if (_buckets is not null)
        {
            ArrayPool<int>.Shared.Return(_buckets, clearArray: false);
            _buckets = null!;
        }

        if (_entries is null)
        {
            return;
        }

        ArrayPool<Entry>.Shared.Return(_entries, clearArray: ArrayPoolClearHelper.IsReferenceOrContainsReferences<Entry>());
        _entries = null!;
    }

    /// <summary>
    /// Adds a new entry for the specified key and hash code to the underlying collection and returns a reference to the
    /// value slot for assignment.
    /// </summary>
    /// <remarks>If the underlying storage requires resizing, the method will update the bucket reference to
    /// reflect the new layout. The returned reference points to an uninitialized value slot, which should be assigned
    /// by the caller before further use.</remarks>
    /// <param name="key">The key to associate with the new entry.</param>
    /// <param name="hashCode">The hash code computed for the key, used to determine the bucket placement.</param>
    /// <param name="bucket">A reference to the bucket index where the new entry will be inserted. This value may be updated if the
    /// underlying storage is resized.</param>
    /// <returns>A reference to the value field of the newly added entry, allowing direct assignment.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref TValue? AddNewEntry(TKey key, uint hashCode, ref int bucket)
    {
        int index;

        // Check if we can reuse a free slot
        if (_freeCount > 0)
        {
            index = _freeList;
            _freeList = FreeListSentinel - _entries[index].GetNext();
            _freeCount--;
        }
        else
        {
            // Check if resize needed
            if (_entryIndex >= _resizeThreshold)
            {
                Resize();
                bucket = ref _buckets[GetBucketIndex(hashCode)];
            }

            index = _entryIndex++;
        }

        ref var newEntry = ref _entries[index];
        newEntry.Initialize(hashCode, key, default, bucket - 1);
        bucket = index + 1;

        return ref Entry.ValueRef(ref newEntry);
    }

    /// <summary>Doubles the capacity of the internal entries array to accommodate additional elements.</summary>
    /// <remarks>The new capacity is rounded up to the next power of two to optimize memory usage and
    /// performance. This method is intended for internal use and should not be called directly by consumers of the
    /// class.</remarks>
    private void Resize() => ResizeTo((int)BitOperationsCompat.RoundUpToPowerOf2((uint)_entries.Length * CapacityGrowthFactor));

    /// <summary>Resizes the internal storage arrays to accommodate the specified number of entries.</summary>
    /// <remarks>This method reinitializes the internal buckets and entries arrays to the specified size and
    /// rehashes existing entries. Existing data is preserved and reindexed to maintain correct lookup behavior. This
    /// operation may impact performance due to memory allocation and data copying.</remarks>
    /// <param name="newSize">The new size for the internal storage arrays. Must be greater than zero.</param>
    private void ResizeTo(int newSize)
    {
        var newEntries = ArrayPool<Entry>.Shared.Rent(newSize);
        var newBuckets = ArrayPool<int>.Shared.Rent(newSize);
        _bucketsLength = newSize;
        _resizeThreshold = (int)(_bucketsLength * LoadFactor);
        newBuckets.AsSpan().Clear();
        Array.Copy(_entries, newEntries, _entryIndex);

        for (var i = 0; i < _entryIndex; i++)
        {
            ref var entry = ref newEntries[i];

            if (entry.GetNext() < EndOfChain)
            {
                continue;
            }

            var bucketIndex = GetBucketIndex(entry.GetHashCodeValue());
            ref var bucket = ref newBuckets[bucketIndex];
            entry.SetNext(bucket - 1);
            bucket = i + 1;
        }

        ArrayPool<int>.Shared.Return(_buckets, clearArray: false);
        ArrayPool<Entry>.Shared.Return(_entries, clearArray: ArrayPoolClearHelper.IsReferenceOrContainsReferences<Entry>());

        _entries = newEntries;
        _buckets = newBuckets;
    }

    /// <summary>Calculates a non-negative hash code for the specified key using the configured comparer.</summary>
    /// <param name="key">The key for which to compute the hash code. Can be null.</param>
    /// <returns>A non-negative 32-bit unsigned integer representing the hash code of the key. Returns 0 if the key is null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint InternalGetHashCode(TKey key) => (uint)(key is null ? 0 : _comparer.GetHashCode(key) & 0x7FFFFFFF);

    /// <summary>Calculates the index of the bucket corresponding to the specified hash code within the current bucket array.</summary>
    /// <remarks>This method assumes that the bucket array length is a power of two, which enables efficient
    /// computation of the index using a bitwise operation.</remarks>
    /// <param name="hashCode">The hash code for which to determine the bucket index. Typically generated from a key to be stored or retrieved.</param>
    /// <returns>The zero-based index of the bucket that corresponds to the specified hash code.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetBucketIndex(uint hashCode) => (int)(hashCode & ((uint)_bucketsLength - 1));

    /// <summary>Enumerates the elements of a <see cref="QuadDictionary{TKey, TValue}"/>.</summary>
    public struct Enumerator : IEquatable<Enumerator>
    {
        private readonly QuadDictionary<TKey, TValue> _dictionary;

        private int _index;

        private KeyValuePair<TKey, TValue> _current;

        /// <summary>Initializes a new instance of the <see cref="Enumerator"/> struct.</summary>
        /// <param name="dictionary">The QuadDictionary to enumerate. Must not be null.</param>
        internal Enumerator(QuadDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _index = 0;
            _current = default;
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly KeyValuePair<TKey, TValue> Current => _current;

        /// <summary>Determines whether two enumerators are equal.</summary>
        /// <param name="left">The left enumerator.</param>
        /// <param name="right">The right enumerator.</param>
        /// <returns>true if the enumerators are equal; otherwise, false.</returns>
        public static bool operator ==(Enumerator left, Enumerator right) => left.Equals(right);

        /// <summary>Determines whether two enumerators are not equal.</summary>
        /// <param name="left">The left enumerator.</param>
        /// <param name="right">The right enumerator.</param>
        /// <returns>true if the enumerators are not equal; otherwise, false.</returns>
        public static bool operator !=(Enumerator left, Enumerator right) => !left.Equals(right);

        /// <summary>Advances the enumerator to the next element of the dictionary.</summary>
        /// <returns>true if the enumerator successfully advanced; false if it has passed the end.</returns>
        public bool MoveNext()
        {
            while (_index < _dictionary._entryIndex)
            {
                ref var entry = ref _dictionary._entries[_index++];

                // Skip free entries (Next < -1 means in freelist)
                if (entry.GetNext() < EndOfChain)
                {
                    continue;
                }

                _current = new(entry.GetKey(), entry.GetValue());
                return true;
            }

            _current = default;
            return false;
        }

        /// <summary>Attempts to get the next element in the dictionary.</summary>
        /// <param name="current">When this method returns, the current key/value pair if available.</param>
        /// <returns>true if a next element was found; otherwise, false.</returns>
        public bool TryGetNext(out KeyValuePair<TKey, TValue> current)
        {
            if (MoveNext())
            {
                current = _current;
                return true;
            }

            current = default;
            return false;
        }

        /// <inheritdoc/>
        public readonly bool Equals(Enumerator other) =>
            ReferenceEquals(_dictionary, other._dictionary) &&
            _index == other._index &&
            EqualityComparer<KeyValuePair<TKey, TValue>>.Default.Equals(_current, other._current);

        /// <inheritdoc/>
        public override readonly bool Equals(object? obj) => obj is Enumerator other && Equals(other);

        /// <inheritdoc/>
        public override readonly int GetHashCode() => _dictionary?.GetHashCode() ?? 0;
    }

    /// <summary>Provides an enumerator for iterating through the elements of a QuadDictionary.</summary>
    /// <remarks>The enumerator exposes each key/value pair in the dictionary in sequence. If the dictionary
    /// is modified after the enumerator is created, the behavior of the enumerator is undefined. This struct is
    /// intended for internal use and is not thread-safe.</remarks>
    private struct EnumeratorWrapper : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly QuadDictionary<TKey, TValue> _dictionary;

        private int _index;

        private KeyValuePair<TKey, TValue> _current;

        /// <summary>Initializes a new instance of the <see cref="EnumeratorWrapper"/> struct.</summary>
        /// <param name="dictionary">The QuadDictionary to enumerate. Must not be null.</param>
        internal EnumeratorWrapper(QuadDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _index = 0;
            _current = default;
        }

        /// <summary>Gets the element in the collection at the current position of the enumerator.</summary>
        public readonly KeyValuePair<TKey, TValue> Current => _current;

        /// <summary>Gets the current element in the collection.</summary>
        readonly object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next element in the dictionary.</summary>
        /// <remarks>After calling MoveNext, the Current property contains the next element in the
        /// dictionary if MoveNext returned true. If the collection is modified after the enumerator is created, the
        /// behavior of MoveNext is undefined.</remarks>
        /// <returns>true if the enumerator was successfully advanced to the next element; otherwise, false if the enumerator has
        /// passed the end of the collection.</returns>
        public bool MoveNext()
        {
            while (_index < _dictionary._entryIndex)
            {
                ref var entry = ref _dictionary._entries[_index++];

                // Skip free entries (Next < -1 means in freelist)
                if (entry.GetNext() < EndOfChain)
                {
                    continue;
                }

                _current = new(entry.GetKey(), entry.GetValue());
                return true;
            }

            return false;
        }

        /// <summary>Resets the enumerator to its initial position, before the first element in the collection.</summary>
        /// <remarks>After calling this method, the enumerator is positioned before the first element. You
        /// must call MoveNext to advance the enumerator to the first element before reading the value of
        /// Current.</remarks>
        public void Reset()
        {
            _index = 0;
            _current = default;
        }

        /// <summary>Releases all resources used by the current instance.</summary>
        /// <remarks>Call this method when you are finished using the object to free unmanaged resources
        /// and perform other cleanup operations. After calling <see cref="Dispose"/>, the object should not be
        /// used.</remarks>
        public readonly void Dispose()
        {
        }
    }

    /// <summary>
    /// Represents a single entry in a hash-based collection, containing the key, value, hash code, and linkage
    /// information for bucket chaining.
    /// </summary>
    /// <remarks>This structure is typically used internally by hash table implementations to store key-value
    /// pairs and manage collision resolution through chaining. The fields provide direct access to the entry's key,
    /// value, computed hash code, and the index of the next entry in the chain.</remarks>
    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay("HashCode = {_hashCode}, Key = {_key}, Value = {_value}, Next = {_next}")]
    private struct Entry
    {
        private uint _hashCode;

        private TKey _key;

        private TValue _value;

        private int _next;

        /// <summary>Gets a mutable reference to the stored value.</summary>
        /// <param name="entry">The entry whose value is referenced.</param>
        /// <returns>A mutable reference to the stored value.</returns>
        internal static ref TValue? ValueRef(ref Entry entry) => ref Unsafe.As<TValue, TValue?>(ref entry._value);

        /// <summary>Initializes the entry with stored hash, key, value, and chain link data.</summary>
        /// <param name="hashCode">The stored key hash code.</param>
        /// <param name="key">The stored key.</param>
        /// <param name="value">The stored value.</param>
        /// <param name="next">The next entry index in the bucket chain.</param>
        internal void Initialize(uint hashCode, TKey key, TValue? value, int next)
        {
            _hashCode = hashCode;
            _key = key;
            _value = Unsafe.As<TValue?, TValue>(ref value);
            _next = next;
        }

        /// <summary>Gets the stored hash code.</summary>
        /// <returns>The stored hash code.</returns>
        internal readonly uint GetHashCodeValue() => _hashCode;

        /// <summary>Gets the stored key.</summary>
        /// <returns>The stored key.</returns>
        internal readonly TKey GetKey() => _key;

        /// <summary>Sets the stored key.</summary>
        /// <param name="key">The new key value.</param>
        internal void SetKey(TKey key) => _key = key;

        /// <summary>Gets the stored value.</summary>
        /// <returns>The stored value.</returns>
        internal readonly TValue GetValue() => _value;

        /// <summary>Sets the stored value.</summary>
        /// <param name="value">The new value.</param>
        internal void SetValue(TValue? value) => _value = Unsafe.As<TValue?, TValue>(ref value);

        /// <summary>Gets the next entry index in the bucket chain.</summary>
        /// <returns>The next entry index.</returns>
        internal readonly int GetNext() => _next;

        /// <summary>Sets the next entry index in the bucket chain.</summary>
        /// <param name="next">The next entry index.</param>
        internal void SetNext(int next) => _next = next;
    }
}
