// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Buffers;
using System.Runtime.CompilerServices;

namespace CP.Reactive.Quaternary.Internal;

/// <summary>
/// Provides batch change tracking for efficient event coalescing.
/// </summary>
/// <typeparam name="T">The type of items being tracked.</typeparam>
internal struct BatchChangeTracker<T> : IDisposable
{
    private T[]? _addedItems;
    private T[]? _removedItems;
    private int _addedCount;
    private int _removedCount;

    /// <summary>
    /// Gets a value indicating whether there are any tracked changes.
    /// </summary>
    public readonly bool HasChanges => _addedCount > 0 || _removedCount > 0;

    /// <summary>
    /// Gets the added items as a span.
    /// </summary>
    public readonly ReadOnlySpan<T> AddedItems => _addedItems.AsSpan(0, _addedCount);

    /// <summary>
    /// Gets the removed items as a span.
    /// </summary>
    public readonly ReadOnlySpan<T> RemovedItems => _removedItems.AsSpan(0, _removedCount);

    /// <summary>
    /// Tracks an added item.
    /// </summary>
    /// <param name="item">The item that was added.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackAdded(T item)
    {
        _addedItems ??= ArrayPool<T>.Shared.Rent(16);

        if (_addedCount >= _addedItems.Length)
        {
            GrowAdded();
        }

        _addedItems[_addedCount++] = item;
    }

    /// <summary>
    /// Tracks a removed item.
    /// </summary>
    /// <param name="item">The item that was removed.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackRemoved(T item)
    {
        _removedItems ??= ArrayPool<T>.Shared.Rent(16);

        if (_removedCount >= _removedItems.Length)
        {
            GrowRemoved();
        }

        _removedItems[_removedCount++] = item;
    }

    /// <summary>
    /// Releases pooled arrays back to the pool.
    /// </summary>
    public void Dispose()
    {
        if (_addedItems != null)
        {
            ArrayPool<T>.Shared.Return(_addedItems, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            _addedItems = null;
        }

        if (_removedItems != null)
        {
            ArrayPool<T>.Shared.Return(_removedItems, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            _removedItems = null;
        }

        _addedCount = 0;
        _removedCount = 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAdded()
    {
        var newArray = ArrayPool<T>.Shared.Rent(_addedItems!.Length * 2);
        _addedItems.AsSpan(0, _addedCount).CopyTo(newArray);
        ArrayPool<T>.Shared.Return(_addedItems, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        _addedItems = newArray;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowRemoved()
    {
        var newArray = ArrayPool<T>.Shared.Rent(_removedItems!.Length * 2);
        _removedItems.AsSpan(0, _removedCount).CopyTo(newArray);
        ArrayPool<T>.Shared.Return(_removedItems, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        _removedItems = newArray;
    }
}
#endif
