// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CP.Reactive.Core;

namespace CP.Reactive.Collections;

/// <summary>
/// Represents a reactive, observable list that notifies subscribers of changes to its items and supports batch
/// operations and advanced collection manipulation.
/// </summary>
/// <remarks>IReactiveList{T} extends standard list and collection interfaces with reactive capabilities, allowing
/// consumers to observe additions, removals, and changes to the list in real time. It is designed for scenarios where
/// changes to the collection need to be tracked and responded to, such as in data-binding or reactive programming
/// contexts. Implementations are expected to be thread-safe only if explicitly documented.</remarks>
/// <typeparam name="T">The type of elements contained in the list. Must be non-nullable.</typeparam>
public interface IReactiveList<T> : IList<T>, IList, IReadOnlyList<T>, IReactiveSource<T>, INotifyPropertyChanged
    where T : notnull
{
    /// <summary>
    /// Gets the added.
    /// </summary>
    /// <value>
    /// The added.
    /// </value>
    IObservable<IEnumerable<T>> Added { get; }

    /// <summary>
    /// Gets the changed.
    /// </summary>
    /// <value>
    /// The changed.
    /// </value>
    IObservable<IEnumerable<T>> Changed { get; }

    /// <summary>
    /// Gets the current items.
    /// </summary>
    /// <value>
    /// The current items.
    /// </value>
    IObservable<IEnumerable<T>> CurrentItems { get; }

    /// <summary>
    /// Gets the items.
    /// </summary>
    /// <value>
    /// The items.
    /// </value>
    ReadOnlyObservableCollection<T> Items { get; }

    /// <summary>
    /// Gets the items added.
    /// </summary>
    /// <value>
    /// The items added.
    /// </value>
    ReadOnlyObservableCollection<T> ItemsAdded { get; }

    /// <summary>
    /// Gets the items changed.
    /// </summary>
    /// <value>
    /// The items changed.
    /// </value>
    ReadOnlyObservableCollection<T> ItemsChanged { get; }

    /// <summary>
    /// Gets the items removed.
    /// </summary>
    /// <value>
    /// The items removed.
    /// </value>
    ReadOnlyObservableCollection<T> ItemsRemoved { get; }

    /// <summary>
    /// Gets the removed.
    /// </summary>
    /// <value>
    /// The removed.
    /// </value>
    IObservable<IEnumerable<T>> Removed { get; }

    /// <summary>
    /// Adds the range.
    /// </summary>
    /// <param name="items">The items.</param>
    void AddRange(IEnumerable<T> items);

    /// <summary>
    /// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1"></see>.
    /// </summary>
    new void Clear();

    /// <summary>
    /// Executes a batch edit operation on the list.
    /// </summary>
    /// <param name="editAction">The action to perform on the internal list.</param>
    void Edit(Action<IEditableList<T>> editAction);

    /// <summary>
    /// Inserts the range.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="items">The items.</param>
    void InsertRange(int index, IEnumerable<T> items);

    /// <summary>
    /// Moves an item from one index to another.
    /// </summary>
    /// <param name="oldIndex">The current index of the item.</param>
    /// <param name="newIndex">The new index for the item.</param>
    void Move(int oldIndex, int newIndex);

    /// <summary>
    /// Removes the specified items.
    /// </summary>
    /// <param name="items">The items.</param>
    void Remove(IEnumerable<T> items);

    /// <summary>
    /// Removes the <see cref="T:System.Collections.Generic.IList`1"></see> item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to remove.</param>
    new void RemoveAt(int index);

    /// <summary>
    /// Removes all elements that match the specified predicate from the collection.
    /// </summary>
    /// <remarks>This method provides an efficient way to remove multiple items based on a condition
    /// without having to enumerate the collection separately.</remarks>
    /// <param name="predicate">A function that returns true for elements that should be removed.</param>
    /// <returns>The number of elements removed from the collection.</returns>
    int RemoveMany(Func<T, bool> predicate);

    /// <summary>
    /// Removes the range.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="count">The count.</param>
    void RemoveRange(int index, int count);

    /// <summary>
    /// Replaces all existing items with new items.
    /// </summary>
    /// <param name="items">The new items.</param>
    void ReplaceAll(IEnumerable<T> items);

    /// <summary>
    /// Updates the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="newValue">The new value.</param>
    void Update(T item, T newValue);
}
