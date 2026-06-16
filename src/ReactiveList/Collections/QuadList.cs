// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK
using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;

namespace CP.Reactive.Collections;

/// <summary>Provides a high-performance, pooled list for storing elements with fast add, remove, and enumeration operations.</summary>
/// <remarks>
/// QuadList is an internal shard optimized for scenarios where frequent additions and removals are required.
/// It uses array pooling to minimize allocations. The list is not thread-safe and should not
/// be accessed concurrently from multiple threads. Call Dispose to return internal arrays to the pool.
/// Change notifications are NOT emitted from this class - the parent QuaternaryBase handles all notifications
/// through its Stream pipeline.
/// </remarks>
/// <typeparam name="T">The type of elements in the list.</typeparam>
[SkipLocalsInit]
public sealed class QuadList<T> : IDisposable, IQuad<T>
    where T : notnull
{
    private const int MinimumSize = 16;

    private T[] _items;

    private int _count;

    /// <summary>Initializes a new instance of the <see cref="QuadList{T}"/> class.</summary>
    public QuadList()
    {
        _items = ArrayPool<T>.Shared.Rent(MinimumSize);
        _count = 0;
    }

    /// <summary>Gets the number of elements contained in the list.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>Gets or sets the element at the specified index.</summary>
    /// <param name="index">The zero-based index of the element to get or set.</param>
    /// <returns>The element at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">index is less than 0 or greater than or equal to Count.</exception>
    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_count)
            {
                ThrowIndexOutOfRange();
            }

            return _items[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if ((uint)index >= (uint)_count)
            {
                ThrowIndexOutOfRange();
            }

            _items[index] = value;
        }
    }

    /// <summary>Adds an element to the end of the list.</summary>
    /// <param name="item">The element to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        var count = _count;
        var items = _items;

        if ((uint)count < (uint)items.Length)
        {
            items[count] = item;
            _count = count + 1;
        }
        else
        {
            AddWithResize(item);
        }
    }

    /// <summary>Removes the first occurrence of a specific element from the list.</summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>true if the element was found and removed; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(T item)
    {
        var index = IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    /// <summary>Removes the element at the specified index.</summary>
    /// <param name="index">The zero-based index of the element to remove.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_count)
        {
            ThrowIndexOutOfRange();
        }

        _count--;
        if (index < _count)
        {
            Array.Copy(_items, index + 1, _items, index, _count - index);
        }

        if (!CP.Reactive.Internal.ArrayPoolClearHelper.IsReferenceOrContainsReferences<T>())
        {
            return;
        }

        _items[_count] = default!;
    }

    /// <summary>Returns the zero-based index of the first occurrence of a value in the list.</summary>
    /// <param name="item">The element to locate.</param>
    /// <returns>The index of item if found; otherwise, -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(T item) => Array.IndexOf(_items, item, 0, _count);

    /// <summary>Determines whether an element is in the list.</summary>
    /// <param name="item">The element to locate.</param>
    /// <returns>true if item is found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item) => IndexOf(item) >= 0;

    /// <summary>Adds the elements of the specified span to the end of the list.</summary>
    /// <param name="items">The span of items to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRange(in ReadOnlySpan<T> items)
    {
        var count = items.Length;
        if (count == 0)
        {
            return;
        }

        EnsureCapacity(_count + count);
        items.CopyTo(_items.AsSpan(_count));
        _count += count;
    }

    /// <summary>Removes all elements from the list.</summary>
    public void Clear()
    {
        if (CP.Reactive.Internal.ArrayPoolClearHelper.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(_items, 0, _count);
        }

        _count = 0;
    }

    /// <summary>Ensures that the capacity of this list is at least the specified value.</summary>
    /// <param name="capacity">The minimum capacity to ensure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        if (_items.Length >= capacity)
        {
            return;
        }

        Grow(capacity);
    }

    /// <summary>Gets a span over the elements in the list.</summary>
    /// <returns>A span representing the elements in the list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan() => _items.AsSpan(0, _count);

    /// <summary>Copies the elements of the list to an array starting at a particular array index.</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] array, int arrayIndex) => Array.Copy(_items, 0, array, arrayIndex, _count);

    /// <summary>Returns an enumerator that iterates through the list.</summary>
    /// <returns>An enumerator for the list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new QuadListEnumerator(this);

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => new QuadListEnumerator(this);

    /// <summary>Returns the internal array to the pool and releases resources.</summary>
    public void Dispose()
    {
        if (_items is null)
        {
            return;
        }

        ArrayPool<T>.Shared.Return(_items, clearArray: CP.Reactive.Internal.ArrayPoolClearHelper.IsReferenceOrContainsReferences<T>());
        _items = null!;
    }

    /// <summary>Adds data for the AddAssumeCapacity operation.</summary>
    /// <param name="item">The item value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddAssumeCapacity(T item) => _items[_count++] = item;

    /// <summary>Removes data for the RemoveMatching operation.</summary>
    /// <param name="removeCounts">The removeCounts value.</param>
    /// <param name="removedItems">The removedItems value.</param>
    /// <returns>The number of items removed from the list.</returns>
    internal int RemoveMatching(Dictionary<T, int> removeCounts, List<T>? removedItems)
    {
        var items = _items;
        var count = _count;
        var writeIndex = 0;
        var removed = 0;

        for (var readIndex = 0; readIndex < count; readIndex++)
        {
            var item = items[readIndex];
            if (removeCounts.TryGetValue(item, out var remaining))
            {
                if (remaining == 1)
                {
                    removeCounts.Remove(item);
                }
                else
                {
                    removeCounts[item] = remaining - 1;
                }

                removedItems?.Add(item);
                removed++;
                continue;
            }

            if (writeIndex != readIndex)
            {
                items[writeIndex] = item;
            }

            writeIndex++;
        }

        if (removed == 0)
        {
            return 0;
        }

        if (CP.Reactive.Internal.ArrayPoolClearHelper.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(items, writeIndex, removed);
        }

        _count = writeIndex;
        return removed;
    }

    /// <summary>Performs the ThrowIndexOutOfRange operation.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIndexOutOfRange() => throw new ArgumentOutOfRangeException("index");

    /// <summary>Adds data for the AddWithResize operation.</summary>
    /// <param name="item">The item value.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddWithResize(T item)
    {
        var count = _count;
        Grow(count + 1);
        _items[count] = item;
        _count = count + 1;
    }

    /// <summary>Performs the Grow operation.</summary>
    /// <param name="minCapacity">The minCapacity value.</param>
    private void Grow(int minCapacity)
    {
        var newCapacity = _items.Length == 0 ? MinimumSize : _items.Length * 2;
        if (newCapacity < minCapacity)
        {
            newCapacity = (int)CP.Reactive.Internal.BitOperationsCompat.RoundUpToPowerOf2((uint)minCapacity);
        }

        var newItems = ArrayPool<T>.Shared.Rent(newCapacity);
        if (_count > 0)
        {
            Array.Copy(_items, newItems, _count);
        }

        ArrayPool<T>.Shared.Return(_items, clearArray: CP.Reactive.Internal.ArrayPoolClearHelper.IsReferenceOrContainsReferences<T>());
        _items = newItems;
    }

    /// <summary>Enumerates the elements of a <see cref="QuadList{T}"/>.</summary>
    public struct Enumerator : IEquatable<Enumerator>
    {
        private readonly QuadList<T> _list;

        private int _index;

        /// <summary>Initializes a new instance of the <see cref="Enumerator"/> struct.</summary>
        /// <param name="list">The list value.</param>
        internal Enumerator(QuadList<T> list)
        {
            _list = list;
            _index = -1;
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _list._items[_index];
        }

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

        /// <summary>Advances the enumerator to the next element of the list.</summary>
        /// <returns>true if the enumerator was successfully advanced; false if it has passed the end.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            var index = _index + 1;
            if (index >= _list._count)
            {
                return false;
            }

            _index = index;
            return true;
        }

        /// <inheritdoc/>
        public readonly bool Equals(Enumerator other) => ReferenceEquals(_list, other._list) && _index == other._index;

        /// <inheritdoc/>
        public override readonly bool Equals(object? obj) => obj is Enumerator other && Equals(other);

        /// <inheritdoc/>
        public override readonly int GetHashCode() => ((_list?.GetHashCode() ?? 0) * 397) ^ _index;
    }

    /// <summary>Wrapper to implement IEnumerator for foreach support.</summary>
    private struct QuadListEnumerator : IEnumerator<T>
    {
        private readonly QuadList<T> _list;

        private int _index;

        /// <summary>Initializes a new instance of the <see cref="QuadListEnumerator"/> struct.</summary>
        /// <param name="list">The list value.</param>
        internal QuadListEnumerator(QuadList<T> list)
        {
            _list = list;
            _index = -1;
        }

        /// <summary>Gets the current element.</summary>
        public readonly T Current => _list._items[_index];

        /// <summary>Gets the current element.</summary>
        readonly object IEnumerator.Current => Current!;

        /// <summary>Advances the enumerator to the next element.</summary>
        /// <returns>true if the enumerator was successfully advanced; false if it has passed the end.</returns>
        public bool MoveNext()
        {
            var index = _index + 1;
            if (index >= _list._count)
            {
                return false;
            }

            _index = index;
            return true;
        }

        /// <summary>Resets the enumerator to its initial position.</summary>
        public void Reset() => _index = -1;

        /// <summary>Disposes the enumerator.</summary>
        public readonly void Dispose()
        {
        }
    }
}
#endif
