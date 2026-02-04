// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using CP.Reactive.Core;

namespace CP.Reactive.Internal;

/// <summary>
/// Provides a wrapper around a generic list that supports editable operations and optional synchronization with an
/// observable collection.
/// </summary>
/// <remarks>This class enables editing operations such as adding, removing, and moving items in the underlying
/// list. If an observable collection is provided, all changes made through this wrapper are also applied to the
/// observable collection, keeping both collections in sync. This is useful when you need to maintain consistency
/// between a standard list and an observable collection, such as for data binding scenarios.</remarks>
/// <typeparam name="T">The type of elements in the list.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="EditableListWrapper{T}"/> class.
/// </remarks>
/// <param name="list">The underlying list to wrap.</param>
/// <param name="observableCollection">The observable collection to keep in sync (optional).</param>
internal sealed class EditableListWrapper<T>(List<T> list, ObservableCollection<T>? observableCollection = null) : IEditableList<T>
{
    /// <inheritdoc/>
    public int Count => list.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public T this[int index]
    {
        get => list[index];
        set
        {
            list[index] = value;
            if (observableCollection != null)
            {
                observableCollection[index] = value;
            }
        }
    }

    /// <inheritdoc/>
    public void Add(T item)
    {
        list.Add(item);
        observableCollection?.Add(item);
    }

    /// <inheritdoc/>
    public void AddRange(IEnumerable<T> items)
    {
        var itemArray = items as T[] ?? items.ToArray();
        list.AddRange(itemArray);
        if (observableCollection != null)
        {
            foreach (var item in itemArray)
            {
                observableCollection.Add(item);
            }
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        list.Clear();
        observableCollection?.Clear();
    }

    /// <inheritdoc/>
    public bool Contains(T item) => list.Contains(item);

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => list.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(T item) => list.IndexOf(item);

    /// <inheritdoc/>
    public void Insert(int index, T item)
    {
        list.Insert(index, item);
        observableCollection?.Insert(index, item);
    }

    /// <inheritdoc/>
    public void Move(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(oldIndex));
        }

        if (newIndex < 0 || newIndex >= list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(newIndex));
        }

        if (oldIndex == newIndex)
        {
            return;
        }

        var item = list[oldIndex];
        list.RemoveAt(oldIndex);
        list.Insert(newIndex, item);
        observableCollection?.Move(oldIndex, newIndex);
    }

    /// <inheritdoc/>
    public bool Remove(T item)
    {
        var index = list.IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        list.RemoveAt(index);
        observableCollection?.RemoveAt(index);
        return true;
    }

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        list.RemoveAt(index);
        observableCollection?.RemoveAt(index);
    }
}
