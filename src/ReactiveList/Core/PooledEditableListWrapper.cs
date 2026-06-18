// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.Reactive.Core;

/// <summary>A pooled version of <see cref="EditableListWrapper{T}"/> that supports reuse through object pooling.</summary>
/// <typeparam name="T">The type of elements in the wrapped list.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="PooledEditableListWrapper{T}"/> class.
/// </remarks>
/// <param name="list">The underlying list to wrap.</param>
/// <param name="observableCollection">The observable collection to keep in sync (optional).</param>
public sealed class PooledEditableListWrapper<T>(List<T> list, ObservableCollection<T>? observableCollection = null) : IEditableList<T>, IResettable, IDisposable
{
    private List<T>? _list = list;

    private ObservableCollection<T>? _observableCollection = observableCollection;

    private bool _isReturned;

    /// <inheritdoc/>
    public int Count => _list?.Count ?? 0;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public T this[int index]
    {
        get
        {
            ThrowIfReturned();
            return _list![index];
        }

        set
        {
            ThrowIfReturned();
            _list![index] = value;
            _observableCollection?[index] = value;
        }
    }

    /// <inheritdoc/>
    public void Add(T item)
    {
        ThrowIfReturned();
        _list!.Add(item);
        _observableCollection?.Add(item);
    }

    /// <inheritdoc/>
    public void AddRange(IEnumerable<T> items)
    {
        ThrowIfReturned();
        var itemArray = (items as T[]) ?? [.. items];
        _list!.AddRange(itemArray);
        if (_observableCollection is null)
        {
            return;
        }

        foreach (var item in itemArray)
        {
            _observableCollection.Add(item);
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        ThrowIfReturned();
        _list!.Clear();
        _observableCollection?.Clear();
    }

    /// <inheritdoc/>
    public bool Contains(T item)
    {
        ThrowIfReturned();
        return _list!.Contains(item);
    }

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ThrowIfReturned();
        _list!.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isReturned)
        {
            return;
        }

        EditableListWrapperPool.Return(this);
    }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        ThrowIfReturned();
        return _list!.GetEnumerator();
    }

    /// <inheritdoc/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(T item)
    {
        ThrowIfReturned();
        return _list!.IndexOf(item);
    }

    /// <summary>Initializes the wrapper with new list references.</summary>
    /// <param name="list">The underlying list to wrap.</param>
    /// <param name="observableCollection">The observable collection to keep in sync (optional).</param>
    public void Initialize(List<T> list, ObservableCollection<T>? observableCollection)
    {
        _list = list;
        _observableCollection = observableCollection;
        _isReturned = false;
    }

    /// <inheritdoc/>
    public void Insert(int index, T item)
    {
        ThrowIfReturned();
        _list!.Insert(index, item);
        _observableCollection?.Insert(index, item);
    }

    /// <inheritdoc/>
    public void Move(int oldIndex, int newIndex)
    {
        ThrowIfReturned();

        if (oldIndex < 0 || oldIndex >= _list!.Count)
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
        ThrowIfReturned();
        var index = _list!.IndexOf(item);
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
        ThrowIfReturned();
        _list!.RemoveAt(index);
        _observableCollection?.RemoveAt(index);
    }

    /// <inheritdoc/>
    public void Reset()
    {
        _list = null;
        _observableCollection = null;
        _isReturned = true;
    }

    /// <summary>Throws an <see cref="ObjectDisposedException"/> when the wrapper is no longer usable.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfReturned()
    {
        if (!_isReturned)
        {
            return;
        }

        throw new ObjectDisposedException(nameof(PooledEditableListWrapper<T>), "This wrapper has been returned to the pool and cannot be used.");
    }
}
