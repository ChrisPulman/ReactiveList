// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;
#else
namespace CP.Primitives.Internal;
#endif
/// <summary>
/// Provides a stack-allocated buffer builder that minimizes allocations for small collections
/// and falls back to pooled arrays for larger sizes.
/// </summary>
/// <typeparam name="T">The type of elements in the buffer.</typeparam>
internal ref struct ValueBuffer<T>
{
    private readonly Span<T> _stackBuffer;

    private T[]? _rentedArray;

    private int _count;

    /// <summary>Initializes a new instance of the <see cref="ValueBuffer{T}"/> struct with a stack-allocated buffer.</summary>
    /// <param name="stackBuffer">The stack-allocated buffer to use initially.</param>
    public ValueBuffer(in Span<T> stackBuffer)
    {
        _stackBuffer = stackBuffer;
        _rentedArray = null;
        _count = 0;
    }

    /// <summary>Gets the number of elements in the buffer.</summary>
    public readonly int Count => _count;

    /// <summary>Gets a span over the valid elements in the buffer.</summary>
    public readonly ReadOnlySpan<T> Span => _rentedArray is not null
        ? _rentedArray.AsSpan(0, _count)
        : _stackBuffer.Slice(0, _count);

    /// <summary>Adds an item to the buffer, growing if necessary.</summary>
    /// <param name="item">The item to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        var count = _count;

        if (_rentedArray is not null)
        {
            if (count >= _rentedArray.Length)
            {
                GrowRented();
            }

            _rentedArray[count] = item;
        }
        else if (count < _stackBuffer.Length)
        {
            _stackBuffer[count] = item;
        }
        else
        {
            MoveToRented();
            _rentedArray![count] = item;
        }

        _count = count + 1;
    }

    /// <summary>Returns the rented array to the pool if one was used.</summary>
    public void Dispose()
    {
        if (_rentedArray is null)
        {
            return;
        }

        ArrayPool<T>.Shared.Return(_rentedArray, clearArray: ArrayPoolClearHelper.IsReferenceOrContainsReferences<T>());
        _rentedArray = null;
    }

    /// <summary>Transfers ownership from the stack buffer to a rented array.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void MoveToRented()
    {
        var newCapacity = _stackBuffer.Length * 2;
        _rentedArray = ArrayPool<T>.Shared.Rent(newCapacity);
        _stackBuffer.Slice(0, _count).CopyTo(_rentedArray);
    }

    /// <summary>Grows the rented array to accommodate additional elements.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowRented()
    {
        var newCapacity = _rentedArray!.Length * 2;
        var newArray = ArrayPool<T>.Shared.Rent(newCapacity);
        _rentedArray.AsSpan(0, _count).CopyTo(newArray);
        ArrayPool<T>.Shared.Return(_rentedArray, clearArray: ArrayPoolClearHelper.IsReferenceOrContainsReferences<T>());
        _rentedArray = newArray;
    }
}
