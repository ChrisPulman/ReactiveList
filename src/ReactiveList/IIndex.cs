// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CP.Reactive;

/// <summary>
/// Defines methods for maintaining an index of items, supporting addition, removal, and clearing of indexed elements.
/// </summary>
/// <remarks>Implementations of this interface are responsible for tracking the presence of items and updating the
/// index accordingly when items are added or removed. The interface does not specify thread safety; implementations
/// should document their own thread safety guarantees if applicable.</remarks>
/// <typeparam name="T">The type of items managed by the index.</typeparam>
internal interface IIndex<T>
{
    /// <summary>
    /// Handles logic to be performed when an item is added.
    /// </summary>
    /// <param name="item">The item that has been added. Cannot be null.</param>
    void OnAdded(T item);

    /// <summary>
    /// Handles logic to be performed when an item is removed from the collection.
    /// </summary>
    /// <param name="item">The item that has been removed from the collection.</param>
    void OnRemoved(T item);

    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    void Clear();
}
