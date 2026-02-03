// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CP.Reactive;

/// <summary>
/// Provides a thread-safe object pool for reusing instances of <see cref="PooledEditableListWrapper{T}"/>.
/// </summary>
/// <typeparam name="T">The type of elements in the wrapped list.</typeparam>
/// <remarks>
/// Object pooling reduces GC pressure by reusing wrapper instances instead of allocating new ones.
/// The pool has a configurable maximum size to prevent unbounded memory growth.
/// </remarks>
public static class EditableListWrapperPool<T>
{
    private static readonly ConcurrentBag<PooledEditableListWrapper<T>> Pool = [];
    private static int _poolSize;
    private static int _maxPoolSize = 64;

    /// <summary>
    /// Gets or sets the maximum number of wrappers to keep in the pool.
    /// Default is 64.
    /// </summary>
    public static int MaxPoolSize
    {
        get => _maxPoolSize;
        set => _maxPoolSize = Math.Max(1, value);
    }

    /// <summary>
    /// Gets the current number of wrappers in the pool.
    /// </summary>
    public static int CurrentPoolSize => _poolSize;

    /// <summary>
    /// Rents a wrapper from the pool or creates a new one if the pool is empty.
    /// </summary>
    /// <param name="list">The underlying list to wrap.</param>
    /// <param name="observableCollection">The optional observable collection to keep in sync.</param>
    /// <returns>A pooled wrapper instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PooledEditableListWrapper<T> Rent(List<T> list, System.Collections.ObjectModel.ObservableCollection<T>? observableCollection = null)
    {
        if (Pool.TryTake(out var wrapper))
        {
            Interlocked.Decrement(ref _poolSize);
            wrapper.Initialize(list, observableCollection);
            return wrapper;
        }

        return new PooledEditableListWrapper<T>(list, observableCollection);
    }

    /// <summary>
    /// Returns a wrapper to the pool for reuse.
    /// </summary>
    /// <param name="wrapper">The wrapper to return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(PooledEditableListWrapper<T> wrapper)
    {
        if (wrapper == null || _poolSize >= _maxPoolSize)
        {
            return;
        }

        wrapper.Reset();
        Pool.Add(wrapper);
        Interlocked.Increment(ref _poolSize);
    }

    /// <summary>
    /// Clears all wrappers from the pool.
    /// </summary>
    public static void Clear()
    {
        while (Pool.TryTake(out _))
        {
            Interlocked.Decrement(ref _poolSize);
        }
    }
}
#endif
