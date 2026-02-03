// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

namespace CP.Reactive.Quaternary;

/// <summary>
/// Defines a contract for a tuple or structure that contains four elements of the specified type.
/// </summary>
/// <typeparam name="T">The type of each element in the quadruple.</typeparam>
public interface IQuad<T>
{
    /// <summary>
    /// Gets the number of elements contained in the collection.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets an observable sequence that emits change sets representing additions, removals, updates, and moves within
    /// the collection.
    /// </summary>
    /// <remarks>Subscribers receive notifications whenever the underlying collection changes. The sequence
    /// completes when the collection is disposed or no longer produces changes.</remarks>
    IObservable<QuaternaryChangeSet<T>> Changes { get; }

    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    /// <remarks>After calling this method, the collection will be empty. This method does not modify the
    /// capacity of the collection, if applicable.</remarks>
    void Clear();
}
#endif
