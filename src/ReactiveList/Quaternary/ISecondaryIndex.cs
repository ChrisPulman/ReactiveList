// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

namespace CP.Reactive;

/// <summary>
/// Defines methods for maintaining a secondary index that tracks changes to a collection of items.
/// </summary>
/// <remarks>Implementations of this interface are notified when items are added, removed, or updated in the
/// primary collection, allowing the secondary index to stay synchronized. This interface is typically used to support
/// efficient lookups or queries based on alternative keys or properties.</remarks>
/// <typeparam name="T">The type of items managed by the secondary index.</typeparam>
public interface ISecondaryIndex<T>
{
    /// <summary>
    /// Handles logic to be performed when an item is added.
    /// </summary>
    /// <param name="item">The item that was added. Cannot be null.</param>
    void OnAdded(T item);

    /// <summary>
    /// Called when an item is removed from the collection.
    /// </summary>
    /// <param name="item">The item that was removed from the collection.</param>
    void OnRemoved(T item);

    /// <summary>
    /// Handles an update event by providing the previous and current values of the item.
    /// </summary>
    /// <param name="oldItem">The previous value of the item before the update occurred.</param>
    /// <param name="newItem">The new value of the item after the update.</param>
    void OnUpdated(T oldItem, T newItem);

    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    void Clear();

    /// <summary>
    /// Determines whether the specified item's key matches the provided key object.
    /// </summary>
    /// <param name="item">The item whose key should be compared.</param>
    /// <param name="key">The key to compare against. Must be of the correct type for this index.</param>
    /// <returns><see langword="true"/> if the item's key matches the specified key; otherwise, <see langword="false"/>.</returns>
    bool MatchesKey(T item, object key);
}
#endif
