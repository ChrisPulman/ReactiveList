// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET6_0_OR_GREATER

namespace CP.Reactive;

/// <summary>
/// Represents a batch operation for adding items to a cache-backed collection, allowing multiple additions to be
/// grouped and committed together.
/// </summary>
/// <remarks>Use this struct to group multiple additions into a single batch operation, improving performance and
/// consistency when working with cache-backed collections. After completing all additions, call Dispose to commit the
/// batched actions. The struct should not be used after disposal.</remarks>
/// <typeparam name="T">The type of elements contained in the collection. Must be non-nullable.</typeparam>
/// <param name="parent">The parent collection to which items are added during the transaction.</param>
public readonly ref struct CacheTransaction<T>(QuaternaryList<T> parent) : IDisposable
    where T : notnull
{
    private readonly List<T> _addedBuffer = [];

    /// <summary>
    /// Adds the specified item to the collection.
    /// </summary>
    /// <param name="item">The item to add to the collection.</param>
    public void Add(T item)
    {
        parent.InternalAddWithoutLock(item);
        _addedBuffer.Add(item);
    }

    /// <summary>
    /// Releases resources used by the batch operation and emits any buffered cache actions.
    /// </summary>
    /// <remarks>Call this method when the batch operation is complete to ensure that all pending cache
    /// actions are processed. After calling this method, the batch operation should not be used.</remarks>
    public void Dispose()
    {
        parent.Emit(CacheAction.BatchOperation, default, new([.. _addedBuffer], _addedBuffer.Count));
        parent.ReleaseGlobalLocks();
    }
}
#endif
