// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Core;
#else
namespace CP.Primitives.Core;
#endif
/// <summary>Provides a thread-safe object pool for reusing instances of <see cref="PooledEditableListWrapper{T}"/>.</summary>
/// <remarks>
/// Object pooling reduces GC pressure by reusing wrapper instances instead of allocating new ones.
/// The pool has a configurable maximum size to prevent unbounded memory growth.
/// </remarks>
public static class EditableListWrapperPool
{
    /// <summary>Rents a wrapper from the pool or creates a new one if the pool is empty.</summary>
    /// <typeparam name="T">The wrapped element type.</typeparam>
    /// <param name="list">The underlying list to wrap.</param>
    /// <returns>A pooled wrapper instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PooledEditableListWrapper<T> Rent<T>(List<T> list) => Rent(list, null);

    /// <summary>Rents a wrapper from the pool or creates a new one if the pool is empty.</summary>
    /// <typeparam name="T">The wrapped element type.</typeparam>
    /// <param name="list">The underlying list to wrap.</param>
    /// <param name="observableCollection">The optional observable collection to keep in sync.</param>
    /// <returns>A pooled wrapper instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PooledEditableListWrapper<T> Rent<T>(
        List<T> list,
        ObservableCollection<T>? observableCollection)
    {
        if (EditableListWrapperPool<T>.TryRent(out var wrapper) && wrapper is not null)
        {
            wrapper.Initialize(list, observableCollection);
            return wrapper;
        }

        return new(list, observableCollection);
    }

    /// <summary>Returns a wrapper to the pool for reuse.</summary>
    /// <typeparam name="T">The wrapped element type.</typeparam>
    /// <param name="wrapper">The wrapper to return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return<T>(PooledEditableListWrapper<T> wrapper) => EditableListWrapperPool<T>.Return(wrapper);
}
