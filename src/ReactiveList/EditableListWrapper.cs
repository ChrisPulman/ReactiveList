// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;

namespace CP.Reactive;

/// <summary>
/// A wrapper around a List that provides IEditableList functionality.
/// </summary>
/// <typeparam name="T">The type of elements in the list.</typeparam>
internal sealed class EditableListWrapper<T> : IEditableList<T>
{
    private readonly List<T> _list;
    private readonly ObservableCollection<T>? _observableCollection;

    /// <summary>
    /// Initializes a new instance of the <see cref="EditableListWrapper{T}"/> class.
    /// </summary>
    /// <param name="list">The underlying list to wrap.</param>
    /// <param name="observableCollection">The observable collection to keep in sync (optional).</param>
    public EditableListWrapper(List<T> list, ObservableCollection<T>? observableCollection = null)
    {
        _list = list;
        _observableCollection = observableCollection;
    }

    /// <inheritdoc/>
    public int Count => _list.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public T this[int index]
    {
        get => _list[index];
        set
        {
            _list[index] = value;
            if (_observableCollection != null)
            {
                _observableCollection[index] = value;
            }
        }
    }

    /// <inheritdoc/>
    public void Add(T item)
    {
        _list.Add(item);
        _observableCollection?.Add(item);
    }

    /// <inheritdoc/>
    public void AddRange(IEnumerable<T> items)
    {
        var itemArray = items as T[] ?? items.ToArray();
        _list.AddRange(itemArray);
        if (_observableCollection != null)
        {
            foreach (var item in itemArray)
            {
                _observableCollection.Add(item);
            }
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _list.Clear();
        _observableCollection?.Clear();
    }

    /// <inheritdoc/>
    public bool Contains(T item) => _list.Contains(item);

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(T item) => _list.IndexOf(item);

    /// <inheritdoc/>
    public void Insert(int index, T item)
    {
        _list.Insert(index, item);
        _observableCollection?.Insert(index, item);
    }

    /// <inheritdoc/>
    public void Move(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= _list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(oldIndex));
        }

        if (newIndex < 0 || newIndex >= _list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(newIndex));
        }

        if (oldIndex == newIndex)
        {
            return;
        }

        var item = _list[oldIndex];
        _list.RemoveAt(oldIndex);
        _list.Insert(newIndex, item);
        _observableCollection?.Move(oldIndex, newIndex);
    }

    /// <inheritdoc/>
    public bool Remove(T item)
    {
        var index = _list.IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        _list.RemoveAt(index);
        _observableCollection?.RemoveAt(index);
        return true;
    }

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        _list.RemoveAt(index);
        _observableCollection?.RemoveAt(index);
    }
}
