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
        : base(items.Select(i => new ReactiveList<T>(i)))
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
        : base(items.Select(i => new ReactiveList<T>(i)))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reactive2DList{T}"/> class.
    /// </summary>
    /// <param name="item">The item.</param>
    public Reactive2DList(ReactiveList<T> item)
        : base([item])
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
        if (items is null)
        {
            return;
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
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            Add(new ReactiveList<T>(item));
        }
    }

    /// <summary>
    /// Inserts at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="items">The Items.</param>
    public void Insert(int index, IEnumerable<T> items)
    {
        if (items is null)
        {
            return;
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
        if (item is null)
        {
            return;
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
        if (items is null)
        {
            return;
        }

        this[index].InsertRange(innerIndex, items);
    }
}
