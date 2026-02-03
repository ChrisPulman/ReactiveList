// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace CP.Reactive;

/// <summary>
/// Represents a two-dimensional reactive list, where each element is itself a reactive list of items of type
/// <typeparamref name="T"/>. Provides methods for managing and observing changes to a collection of collections in a
/// reactive manner.
/// </summary>
/// <remarks>Use <see cref="Reactive2DList{T}"/> to manage tabular or matrix-like data structures where both rows
/// and individual items can be observed for changes. This class is useful in scenarios where you need to react to
/// modifications at either the outer or inner list level, such as in UI data binding or dynamic data grids. All
/// mutation operations are observable, enabling integration with reactive programming patterns.</remarks>
/// <typeparam name="T">The type of elements contained in the inner lists. Must be non-nullable.</typeparam>
public class Reactive2DList<T> : ReactiveList<ReactiveList<T>>
    where T : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Reactive2DList{T}"/> class.
    /// </summary>
    public Reactive2DList()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reactive2DList{T}"/> class with the specified collection of item sequences.
    /// </summary>
    /// <param name="items">A collection of sequences, where each inner sequence represents the items for a row in the two-dimensional list.</param>
    /// <exception cref="ArgumentNullException">Thrown if items is null.</exception>
    public Reactive2DList(IEnumerable<IEnumerable<T>> items)
        : base(items?.Select(i => new ReactiveList<T>(i)) ?? throw new ArgumentNullException(nameof(items)))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reactive2DList{T}"/> class with the specified collection of <see cref="ReactiveList{T}"/>
    /// items.
    /// </summary>
    /// <param name="items">The collection of <see cref="ReactiveList{T}"/> instances to include in the two-dimensional list. Cannot be null.</param>
    public Reactive2DList(IEnumerable<ReactiveList<T>> items)
        : base(items)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reactive2DList{T}"/> class with the specified collection of items.
    /// </summary>
    /// <param name="items">The collection of items to initialize the list with. Each item will be wrapped in a <see cref="ReactiveList{T}"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown if items is null.</exception>
    public Reactive2DList(IEnumerable<T> items)
        : base(items?.Select(i => new ReactiveList<T>(i)) ?? throw new ArgumentNullException(nameof(items)))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reactive2DList{T}"/> class containing a single row initialized with the specified
    /// reactive list.
    /// </summary>
    /// <param name="item">The reactive list to use as the initial row of the two-dimensional list. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="item"/> is null.</exception>
    public Reactive2DList(ReactiveList<T> item)
        : base([item ?? throw new ArgumentNullException(nameof(item))])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reactive2DList{T}"/> class containing a single item.
    /// </summary>
    /// <param name="item">The item to include as the initial element in the list.</param>
    public Reactive2DList(T item)
        : base(new ReactiveList<T>(item))
    {
    }

    /// <summary>
    /// Adds a collection of sequences to the current collection, appending each inner sequence as a separate item.
    /// </summary>
    /// <param name="items">A collection of sequences to add. Each inner sequence is added as a single item. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is null.</exception>
    public void AddRange(IEnumerable<IEnumerable<T>> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        foreach (var item in items)
        {
            Add([.. item]);
        }
    }

    /// <summary>
    /// Adds the elements of the specified collection to the current collection.
    /// </summary>
    /// <param name="items">The collection of items to add. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is null.</exception>
    public void AddRange(IEnumerable<T> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        foreach (var item in items)
        {
            Add(new ReactiveList<T>(item));
        }
    }

    /// <summary>
    /// Adds the specified items to the inner collection at the given outer index.
    /// </summary>
    /// <param name="outerIndex">The zero-based index of the outer collection whose inner collection will receive the new items. Must be greater
    /// than or equal to 0 and less than the total number of outer collections.</param>
    /// <param name="items">The sequence of items to add to the inner collection. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="outerIndex"/> is less than 0 or greater than or equal to the number of outer
    /// collections.</exception>
    public void AddToInner(int outerIndex, IEnumerable<T> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (outerIndex < 0 || outerIndex >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(outerIndex));
        }

        this[outerIndex].AddRange(items);
    }

    /// <summary>
    /// Adds the specified item to the inner collection at the given outer index.
    /// </summary>
    /// <param name="outerIndex">The zero-based index of the outer collection whose inner collection will receive the item. Must be greater than
    /// or equal to 0 and less than the total number of outer collections.</param>
    /// <param name="item">The item to add to the inner collection at the specified outer index.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="outerIndex"/> is less than 0 or greater than or equal to the number of outer
    /// collections.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddToInner(int outerIndex, T item)
    {
        if (outerIndex < 0 || outerIndex >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(outerIndex));
        }

        this[outerIndex].Add(item);
    }

    /// <summary>
    /// Removes all elements from the inner collection at the specified outer index.
    /// </summary>
    /// <param name="outerIndex">The zero-based index of the outer collection whose inner collection will be cleared. Must be greater than or
    /// equal to 0 and less than the value of Count.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when outerIndex is less than 0 or greater than or equal to Count.</exception>
    public void ClearInner(int outerIndex)
    {
        if (outerIndex < 0 || outerIndex >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(outerIndex));
        }

        this[outerIndex].Clear();
    }

    /// <summary>
    /// Returns a flattened sequence containing all items from each inner collection in the order they appear.
    /// </summary>
    /// <returns>An <see cref="IEnumerable{T}"/> that enumerates all items from the inner collections. The sequence is empty if
    /// there are no items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<T> Flatten() => Items.SelectMany(innerList => innerList.Items);

    /// <summary>
    /// Retrieves the element at the specified inner index within the list located at the specified outer index.
    /// </summary>
    /// <param name="outerIndex">The zero-based index of the outer list from which to retrieve the inner list. Must be greater than or equal to 0
    /// and less than the total number of outer lists.</param>
    /// <param name="innerIndex">The zero-based index of the item within the selected inner list. Must be greater than or equal to 0 and less
    /// than the number of items in the inner list.</param>
    /// <returns>The element of type T located at the specified inner index within the specified outer list.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either <paramref name="outerIndex"/> is less than 0 or greater than or equal to the number of outer
    /// lists, or when <paramref name="innerIndex"/> is less than 0 or greater than or equal to the number of items in
    /// the inner list.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetItem(int outerIndex, int innerIndex)
    {
        if (outerIndex < 0 || outerIndex >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(outerIndex));
        }

        var innerList = this[outerIndex];
        if (innerIndex < 0 || innerIndex >= innerList.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(innerIndex));
        }

        return innerList[innerIndex];
    }

    /// <summary>
    /// Inserts the elements of a collection into the list at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
    /// <param name="items">The collection of elements to insert into the list. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is null.</exception>
    public void Insert(int index, IEnumerable<T> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        base.Insert(index, [.. items]);
    }

    /// <summary>
    /// Inserts an item into the collection at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which the item should be inserted.</param>
    /// <param name="item">The item to insert into the collection. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="item"/> is null.</exception>
    public void Insert(int index, T item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        base.Insert(index, new ReactiveList<T>(item));
    }

    /// <summary>
    /// Inserts the specified collection of items into the inner list at the given position within the outer list.
    /// </summary>
    /// <param name="index">The zero-based index in the outer list at which to insert the items.</param>
    /// <param name="items">The collection of items to insert. Cannot be null.</param>
    /// <param name="innerIndex">The zero-based index in the inner list at which to begin inserting the items.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is null.</exception>
    public void Insert(int index, IEnumerable<T> items, int innerIndex)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        this[index].InsertRange(innerIndex, items);
    }

    /// <summary>
    /// Removes the element at the specified index from the inner collection at the given outer index.
    /// </summary>
    /// <param name="outerIndex">The zero-based index of the outer collection containing the inner collection from which to remove the element.
    /// Must be greater than or equal to 0 and less than the total number of outer collections.</param>
    /// <param name="innerIndex">The zero-based index of the element to remove from the specified inner collection.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="outerIndex"/> is less than 0 or greater than or equal to the number of outer
    /// collections.</exception>
    public void RemoveFromInner(int outerIndex, int innerIndex)
    {
        if (outerIndex < 0 || outerIndex >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(outerIndex));
        }

        this[outerIndex].RemoveAt(innerIndex);
    }

    /// <summary>
    /// Sets the element at the specified inner index within the inner list at the given outer index.
    /// </summary>
    /// <param name="outerIndex">The zero-based index of the outer list that contains the inner list to modify. Must be greater than or equal to
    /// 0 and less than the total number of outer lists.</param>
    /// <param name="innerIndex">The zero-based index of the element within the inner list to set. Must be greater than or equal to 0 and less
    /// than the number of elements in the specified inner list.</param>
    /// <param name="value">The new value to assign to the element at the specified inner index.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="outerIndex"/> is less than 0 or greater than or equal to the number of outer lists, or
    /// if <paramref name="innerIndex"/> is less than 0 or greater than or equal to the number of elements in the
    /// specified inner list.</exception>
    public void SetItem(int outerIndex, int innerIndex, T value)
    {
        if (outerIndex < 0 || outerIndex >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(outerIndex));
        }

        var innerList = this[outerIndex];
        if (innerIndex < 0 || innerIndex >= innerList.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(innerIndex));
        }

        innerList[innerIndex] = value;
    }

    /// <summary>
    /// Calculates the total number of items contained in all inner lists.
    /// </summary>
    /// <returns>The sum of the counts of all inner lists. Returns 0 if there are no items.</returns>
    public int TotalCount() => Items.Sum(innerList => innerList.Count);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose all inner lists before disposing base
            foreach (var innerList in Items)
            {
                innerList.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
