// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Disposables;

namespace CP.Reactive;

/// <summary>
/// Interface for Reactive List.
/// </summary>
/// <typeparam name="T">The type stored in the list.</typeparam>
/// <seealso cref="ICancelable" />
public interface IReactiveList<T> : IList<T>, IList, IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged, ICancelable
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
