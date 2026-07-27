// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Core;
#else
namespace CP.Primitives.Core;
#endif
/// <summary>Provides type-specific management for pooled <see cref="PooledEditableListWrapper{T}"/> instances.</summary>
/// <typeparam name="T">The wrapped element type.</typeparam>
public static class EditableListWrapperPool<T>
{
    /// <summary>The composed pool state for this element type.</summary>
    private static readonly PoolState State = new();

    /// <summary>Gets or sets the maximum number of wrappers retained for this element type.</summary>
    public static int MaxPoolSize
    {
        get => EditableListWrapperPool<T>.State.MaximumSize;
        set => EditableListWrapperPool<T>.State.MaximumSize = Math.Max(1, value);
    }

    /// <summary>Gets the current number of wrappers retained for this element type.</summary>
    public static int CurrentPoolSize => EditableListWrapperPool<T>.State.Count;

    /// <summary>Clears all wrappers retained for this element type.</summary>
    public static void Clear() => EditableListWrapperPool<T>.State.Clear();

    /// <summary>Attempts to rent a wrapper from the type-specific pool.</summary>
    /// <param name="wrapper">A wrapper from the pool when one is available; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a wrapper was rented; otherwise, <see langword="false"/>.</returns>
    internal static bool TryRent(out PooledEditableListWrapper<T>? wrapper) => State.TryRent(out wrapper);

    /// <summary>Returns a wrapper to the type-specific pool.</summary>
    /// <param name="wrapper">The wrapper to return.</param>
    internal static void Return(PooledEditableListWrapper<T> wrapper)
    {
        if (wrapper is null || State.Count >= State.MaximumSize)
        {
            return;
        }

        wrapper.Reset();
        State.Add(wrapper);
    }

    /// <summary>Holds pooled wrappers and metadata for this wrapper type.</summary>
    private sealed class PoolState
    {
        /// <summary>The available wrappers for this element type.</summary>
        private readonly ConcurrentBag<PooledEditableListWrapper<T>> _pool = [];

        /// <summary>The number of wrappers currently held in <see cref="_pool"/>.</summary>
        private int _count;

        /// <summary>Gets the current number of pooled wrappers for this type.</summary>
        public int Count => Volatile.Read(ref _count);

        /// <summary>Gets or sets the maximum number of wrappers to keep in the pool for this type. Defaults to 64.</summary>
        public int MaximumSize { get; set; } = 64;

        /// <summary>Attempts to rent a wrapper from the pool.</summary>
        /// <param name="wrapper">A wrapped instance from the pool if one is available; otherwise <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when a wrapper was rented from the pool; otherwise <see langword="false"/>.</returns>
        public bool TryRent(out PooledEditableListWrapper<T>? wrapper)
        {
            if (!_pool.TryTake(out wrapper))
            {
                return false;
            }

            _ = Interlocked.Decrement(ref _count);
            return true;
        }

        /// <summary>Adds a wrapper to the pool if it is not <see langword="null"/>.</summary>
        /// <param name="wrapper">The wrapper to add.</param>
        public void Add(PooledEditableListWrapper<T> wrapper)
        {
            _pool.Add(wrapper);
            _ = Interlocked.Increment(ref _count);
        }

        /// <summary>Clears all pooled wrappers for this type.</summary>
        public void Clear()
        {
            while (_pool.TryTake(out _))
            {
                _ = Interlocked.Decrement(ref _count);
            }
        }
    }
}
