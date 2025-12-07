// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace CP.Reactive;

/// <summary>
/// Reactive 2D List.
/// </summary>
/// <typeparam name="T">The Type.</typeparam>
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
    /// Initializes a new instance of the <see cref="Reactive2DList{T}"/> class.
    /// </summary>
    /// <param name="items">The items.</param>
    public Reactive2DList(IEnumerable<IEnumerable<T>> items)
        : base(items?.Select(i => new ReactiveList<T>(i)) ?? throw new ArgumentNullException(nameof(items)))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reactive2DList{T}"/> class.
    /// </summary>
    /// <param name="items">The items.</param>
    public Reactive2DList(IEnumerable<ReactiveList<T>> items)
        : base(items)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reactive2DList{T}"/> class.
    /// </summary>
    /// <param name="items">The items.</param>
    public Reactive2DList(IEnumerable<T> items)
        : base(items?.Select(i => new ReactiveList<T>(i)) ?? throw new ArgumentNullException(nameof(items)))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reactive2DList{T}"/> class.
    /// </summary>
    /// <param name="item">The item.</param>
    public Reactive2DList(ReactiveList<T> item)
        : base([item ?? throw new ArgumentNullException(nameof(item))])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reactive2DList{T}"/> class.
    /// </summary>
    /// <param name="item">The item.</param>
    public Reactive2DList(T item)
        : base(new ReactiveList<T>(item))
    {
    }

    /// <summary>
    /// Adds the range.
    /// </summary>
    /// <param name="items">The items.</param>
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
    /// Adds the range.
    /// </summary>
    /// <param name="items">The items.</param>
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
    /// Adds items to the inner list at the specified outer index.
    /// </summary>
    /// <param name="outerIndex">The outer index.</param>
    /// <param name="items">The items to add.</param>
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
    /// Adds an item to the inner list at the specified outer index.
    /// </summary>
    /// <param name="outerIndex">The outer index.</param>
    /// <param name="item">The item to add.</param>
    public void AddToInner(int outerIndex, T item)
    {
        if (outerIndex < 0 || outerIndex >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(outerIndex));
        }

        this[outerIndex].Add(item);
    }

    /// <summary>
    /// Clears the inner list at the specified outer index.
    /// </summary>
    /// <param name="outerIndex">The outer index.</param>
    public void ClearInner(int outerIndex)
    {
        if (outerIndex < 0 || outerIndex >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(outerIndex));
        }

        this[outerIndex].Clear();
    }

    /// <summary>
    /// Gets the flattened list of all items.
    /// </summary>
    /// <returns>An enumerable of all items in all inner lists.</returns>
    public IEnumerable<T> Flatten() => Items.SelectMany(innerList => innerList.Items);

    /// <summary>
    /// Gets the item at the specified outer and inner index.
    /// </summary>
    /// <param name="outerIndex">The outer index.</param>
    /// <param name="innerIndex">The inner index.</param>
    /// <returns>The item at the specified indices.</returns>
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
    /// Inserts at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="items">The Items.</param>
    public void Insert(int index, IEnumerable<T> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        base.Insert(index, [.. items]);
    }

    /// <summary>
    /// Inserts at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="item">The Item.</param>
    public void Insert(int index, T item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        base.Insert(index, new ReactiveList<T>(item));
    }

    /// <summary>
    /// Inserts at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="items">The Items.</param>
    /// <param name="innerIndex">Index of the inner element.</param>
    public void Insert(int index, IEnumerable<T> items, int innerIndex)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        this[index].InsertRange(innerIndex, items);
    }

    /// <summary>
    /// Removes an item from the inner list at the specified indices.
    /// </summary>
    /// <param name="outerIndex">The outer index.</param>
    /// <param name="innerIndex">The inner index.</param>
    public void RemoveFromInner(int outerIndex, int innerIndex)
    {
        if (outerIndex < 0 || outerIndex >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(outerIndex));
        }

        this[outerIndex].RemoveAt(innerIndex);
    }

    /// <summary>
    /// Sets the item at the specified outer and inner index.
    /// </summary>
    /// <param name="outerIndex">The outer index.</param>
    /// <param name="innerIndex">The inner index.</param>
    /// <param name="value">The new value.</param>
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
    /// Gets the total count of all items across all inner lists.
    /// </summary>
    /// <returns>The total item count.</returns>
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
