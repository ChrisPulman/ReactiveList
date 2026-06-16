// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.Reactive.Core;

/// <summary>Represents a generic list that supports batch addition and item reordering operations.</summary>
/// <remarks>In addition to standard list operations, this interface provides methods for adding multiple items at
/// once and for moving items within the list. Implementations may vary in thread safety and performance
/// characteristics.</remarks>
/// <typeparam name="T">The type of elements in the list.</typeparam>
public interface IEditableList<T> : IList<T>
{
    /// <summary>Adds a range of items to the list.</summary>
    /// <param name="items">The items to add.</param>
    void AddRange(IEnumerable<T> items);

    /// <summary>Moves an item from one index to another.</summary>
    /// <param name="oldIndex">The current index of the item.</param>
    /// <param name="newIndex">The new index for the item.</param>
    void Move(int oldIndex, int newIndex);
}
