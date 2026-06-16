// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CP.Reactive.Core;

/// <summary>Provides a thread-safe object pool for reusing instances of <see cref="PooledEditableListWrapper{T}"/>.</summary>
/// <remarks>
/// Object pooling reduces GC pressure by reusing wrapper instances instead of allocating new ones.
/// The pool has a configurable maximum size to prevent unbounded memory growth.
/// </remarks>
public static class EditableListWrapperPool
{
    /// <summary>Gets or sets the maximum number of wrappers to keep in the pool for type <typeparamref name="T"/>. Default is 64.</summary>
    /// <typeparam name="T">The wrapped element type.</typeparam>
    /// <returns>The maximum number of pooled wrappers.</returns>
    public static int GetMaxPoolSize<T>()
    {
        return State.Get<T>().MaxPoolSize;
    }

    /// <summary>Sets the maximum number of wrappers to keep in the pool for type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The wrapped element type.</typeparam>
    /// <param name="value">Maximum number of wrappers to retain.</param>
    public static void SetMaxPoolSize<T>(int value)
    {
        State.Get<T>().MaxPoolSize = Math.Max(1, value);
    }

    /// <summary>Gets the current number of wrappers in the pool for type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The wrapped element type.</typeparam>
    /// <returns>The current number of pooled wrappers.</returns>
    public static int GetCurrentPoolSize<T>() => State.Get<T>().PoolSize;

    /// <summary>Rents a wrapper from the pool or creates a new one if the pool is empty.</summary>
    /// <typeparam name="T">The wrapped element type.</typeparam>
    /// <param name="list">The underlying list to wrap.</param>
    /// <param name="observableCollection">The optional observable collection to keep in sync.</param>
    /// <returns>A pooled wrapper instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PooledEditableListWrapper<T> Rent<T>(
        List<T> list,
        System.Collections.ObjectModel.ObservableCollection<T>? observableCollection = null)
    {
        if (State.Get<T>().TryRent(out var wrapper) && wrapper is not null)
        {
            wrapper.Initialize(list, observableCollection);
            return wrapper;
        }

        return new PooledEditableListWrapper<T>(list, observableCollection);
    }

    /// <summary>Returns a wrapper to the pool for reuse.</summary>
    /// <typeparam name="T">The wrapped element type.</typeparam>
    /// <param name="wrapper">The wrapper to return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return<T>(PooledEditableListWrapper<T> wrapper)
    {
        var state = State.Get<T>();
        if (wrapper is null || state.PoolSize >= state.MaxPoolSize)
        {
            return;
        }

        wrapper.Reset();
        state.Add(wrapper);
    }

    /// <summary>Clears all wrappers from the pool for type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The wrapped element type.</typeparam>
    public static void Clear<T>()
    {
        State.Get<T>().Clear();
    }

    /// <summary>Gets or creates per-type pooled state.</summary>
    private static class State
    {
        private static readonly ConcurrentDictionary<Type, object> Values = [];

        /// <summary>Gets or creates type-specific pool state.</summary>
        /// <typeparam name="T">The wrapped element type.</typeparam>
        /// <returns>The backing pool state for <typeparamref name="T"/>.</returns>
        public static State<T> Get<T>()
        {
            return (State<T>)Values.GetOrAdd(typeof(T), static _ => new State<T>());
        }
    }

    /// <summary>Holds pooled wrappers and metadata for a specific wrapper type.</summary>
    /// <typeparam name="T">The wrapped element type.</typeparam>
    private sealed class State<T>
    {
        private readonly ConcurrentBag<PooledEditableListWrapper<T>> _pool = [];

        private int _poolSize;

        /// <summary>Gets the current number of pooled wrappers for this type.</summary>
        public int PoolSize => _poolSize;

        /// <summary>Gets or sets the maximum number of wrappers to keep in the pool for this type. Defaults to 64.</summary>
        public int MaxPoolSize { get; set; } = 64;

        /// <summary>Attempts to rent a wrapper from the pool.</summary>
        /// <param name="wrapper">A wrapped instance from the pool if one is available; otherwise <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when a wrapper was rented from the pool; otherwise <see langword="false"/>.</returns>
        public bool TryRent(out PooledEditableListWrapper<T>? wrapper)
        {
            if (!_pool.TryTake(out wrapper))
            {
                return false;
            }

            Interlocked.Decrement(ref _poolSize);
            return true;
        }

        /// <summary>Adds a wrapper to the pool if it is not <see langword="null"/>.</summary>
        /// <param name="wrapper">The wrapper to add.</param>
        public void Add(PooledEditableListWrapper<T> wrapper)
        {
            _pool.Add(wrapper);
            Interlocked.Increment(ref _poolSize);
        }

        /// <summary>Clears all pooled wrappers for this type.</summary>
        public void Clear()
        {
            while (_pool.TryTake(out _))
            {
                Interlocked.Decrement(ref _poolSize);
            }
        }
    }
}
