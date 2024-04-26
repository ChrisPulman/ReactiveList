// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Reactive.Disposables;

namespace CP.Reactive;

/// <summary>
/// Interface for Reactive List.
/// </summary>
/// <typeparam name="T">The type stored in the list.</typeparam>
/// <seealso cref="ICancelable" />
public interface IReactiveList<T> : ICancelable, IList<T>
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
    /// Replaces all existing items with new items.
    /// </summary>
    /// <param name="items">The new items.</param>
    void ReplaceAll(IEnumerable<T> items);

    /// <summary>
    /// Removes the specified items.
    /// </summary>
    /// <param name="items">The items.</param>
    void Remove(IEnumerable<T> items);

    /// <summary>
    /// Removes the range.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="count">The count.</param>
    void RemoveRange(int index, int count);

    /// <summary>
    /// Updates the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="newValue">The new value.</param>
    void Update(T item, T newValue);
}
