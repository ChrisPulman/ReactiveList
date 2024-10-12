// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;

namespace CP.Reactive;

/// <summary>
/// ReactiveList an Observable list with change tracking.
/// </summary>
/// <typeparam name="T">The Type.</typeparam>
public class ReactiveList<T> : IReactiveList<T>
    where T : notnull
{
    private const string ItemArray = "Item[]";
    private readonly ReplaySubject<IEnumerable<T>> _added = new(1);
    private readonly ReplaySubject<IEnumerable<T>> _changed = new(1);
    private readonly CompositeDisposable _cleanUp = [];
    private readonly ReplaySubject<IEnumerable<T>> _currentItems = new(1);
    private readonly ReadOnlyObservableCollection<T> _items;
    private readonly ObservableCollection<T> _itemsAddedoc = [];
    private readonly ObservableCollection<T> _itemsChangedoc = [];
    private readonly ObservableCollection<T> _itemsRemovedoc = [];
    private readonly object _lock = new();
    private readonly ReplaySubject<IEnumerable<T>> _removed = new(1);
    private readonly SourceList<T> _sourceList = new();
    private bool _addedRange;
    private bool _cleared;
    private bool _replacingAll;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveList{T}"/> class.
    /// </summary>
    public ReactiveList()
    {
        _items = new([]);
        ItemsAdded = new(_itemsAddedoc);
        ItemsRemoved = new(_itemsRemovedoc);
        ItemsChanged = new(_itemsChangedoc);
        var srcList = _sourceList.Connect();
        _cleanUp =
        [
            _sourceList,
            _added,
            _removed,
            _changed,
            _currentItems,
            srcList
                .ObserveOn(Scheduler.Immediate)
                .Bind(out _items)
                .Subscribe(),

            _sourceList
                .CountChanged
                .Select(_ => _sourceList.Items)
                .Do(_currentItems.OnNext)
                .Subscribe(),

            srcList
                .WhereReasonsAre(ListChangeReason.Add)
                .Select(t => t.Select(v => v.Item.Current))
                .Do(_added.OnNext)
                .ObserveOn(Scheduler.Immediate)
                .Subscribe(v =>
                {
                    _itemsAddedoc.Clear();
                    _itemsAddedoc.Add(v);
                    _itemsRemovedoc.Clear();
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, v.ToList(), _items.Count - v.Count()));
                }),

            srcList
                .WhereReasonsAre(ListChangeReason.AddRange)
                .SelectMany(t => t.Select(v => v.Range))
                .Do(_added.OnNext)
                .ObserveOn(Scheduler.Immediate)
                .Subscribe(v =>
                {
                    _itemsAddedoc.Clear();
                    _itemsAddedoc.Add(v);
                    if (!_replacingAll)
                    {
                        _itemsRemovedoc.Clear();
                    }
                    else
                    {
                        _addedRange = true;
                    }

                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }),

            srcList
                .WhereReasonsAre(ListChangeReason.Remove)
                .Select(t => t.Select(v => v.Item.Current))
                .Do(_removed.OnNext)
                .ObserveOn(Scheduler.Immediate)
                .Subscribe(v =>
                {
                    _itemsRemovedoc.Clear();
                    _itemsRemovedoc.Add(v);
                    _itemsAddedoc.Clear();
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, v));
                }),

            srcList
                .WhereReasonsAre(ListChangeReason.RemoveRange)
                .SelectMany(t => t.Select(v => v.Range))
                .Do(_removed.OnNext)
                .ObserveOn(Scheduler.Immediate)
                .Subscribe(v =>
                {
                    _itemsRemovedoc.Clear();
                    _itemsRemovedoc.Add(v);
                    _itemsAddedoc.Clear();
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, v.ToList()));
                }),

            srcList
                .WhereReasonsAre(ListChangeReason.Add, ListChangeReason.Remove, ListChangeReason.Replace)
                .Select(t => t.Select(v => v.Item.Current))
                .Do(_changed.OnNext)
                .ObserveOn(Scheduler.Immediate)
                .Subscribe(v =>
                {
                    _itemsChangedoc.Clear();
                    _itemsChangedoc.Add(v);
                }),

            srcList
                .WhereReasonsAre(ListChangeReason.RemoveRange, ListChangeReason.AddRange)
                .SelectMany(t => t.Select(v => v.Range))
                .Do(_changed.OnNext)
                .ObserveOn(Scheduler.Immediate)
                .Subscribe(v =>
                {
                    _itemsChangedoc.Clear();
                    _itemsChangedoc.Add(v);
                }),

            srcList
                .WhereReasonsAre(ListChangeReason.Clear)
                .SelectMany(t => t.Select(v => v.Range))
                .ObserveOn(Scheduler.Immediate)
                .Subscribe(v =>
                {
                    if (!_replacingAll)
                    {
                        _itemsAddedoc.Clear();
                    }

                    _itemsChangedoc.Clear();
                    _itemsChangedoc.Add(v);
                    _itemsRemovedoc.Clear();
                    _itemsRemovedoc.Add(v);
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

                    if (_replacingAll)
                    {
                        _cleared = true;
                    }
                }),
        ];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveList{T}"/> class.
    /// </summary>
    /// <param name="items">The items.</param>
    public ReactiveList(IEnumerable<T> items)
        : this() => AddRange(items);

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveList{T}"/> class.
    /// </summary>
    /// <param name="item">The item.</param>
    public ReactiveList(T item)
        : this() => Add(item);

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the added as an Observeable.
    /// </summary>
    /// <value>The added.</value>
    public IObservable<IEnumerable<T>> Added => _added;

    /// <summary>
    /// Gets the changed as an Observeable.
    /// </summary>
    /// <value>The changed.</value>
    public IObservable<IEnumerable<T>> Changed => _changed;

    /// <summary>
    /// Gets the count.
    /// </summary>
    /// <value>
    /// The count.
    /// </value>
    public int Count => _items.Count;

    /// <summary>
    /// Gets the current items as an Observeable.
    /// </summary>
    /// <value>
    /// The current items.
    /// </value>
    public IObservable<IEnumerable<T>> CurrentItems => _currentItems;

    /// <summary>
    /// Gets a value indicating whether gets a value that indicates whether the object is disposed.
    /// </summary>
    public bool IsDisposed => _cleanUp.IsDisposed;

    /// <inheritdoc/>
    public bool IsFixedSize => false;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public bool IsSynchronized => false;

    /// <summary>
    /// Gets the items.
    /// </summary>
    /// <value>The items.</value>
    public ReadOnlyObservableCollection<T> Items => _items;

    /// <summary>
    /// Gets the items added.
    /// </summary>
    /// <value>The items added.</value>
    public ReadOnlyObservableCollection<T> ItemsAdded { get; }

    /// <summary>
    /// Gets the items changed.
    /// </summary>
    /// <value>The items changed.</value>
    public ReadOnlyObservableCollection<T> ItemsChanged { get; }

    /// <summary>
    /// Gets the items removed.
    /// </summary>
    /// <value>The items removed.</value>
    public ReadOnlyObservableCollection<T> ItemsRemoved { get; }

    /// <summary>
    /// Gets the removed items as an Observable.
    /// </summary>
    /// <value>The removed.</value>
    public IObservable<IEnumerable<T>> Removed => _removed;

    /// <inheritdoc/>
    public object SyncRoot => this;

    /// <inheritdoc/>
    object? IList.this[int index]
    {
        get => _items[index];
        set
        {
            RemoveAt(index);
            Insert(index, (T)value!);
        }
    }

    /// <inheritdoc/>
    public T this[int index]
    {
        get => _items[index];
        set
        {
            RemoveAt(index);
            Insert(index, value);
        }
    }

    /// <inheritdoc/>
    public void Add(T item)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.Add(item));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <summary>
    /// Adds the specified items.
    /// </summary>
    /// <param name="items">The items.</param>
    public void AddRange(IEnumerable<T> items)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.AddRange(items));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
        }
    }

    /// <summary>
    /// Determines whether this instance contains the object.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>
    ///   <c>true</c> if [contains] [the specified item]; otherwise, <c>false</c>.
    /// </returns>
    public bool Contains(T item)
    {
        lock (_lock)
        {
            return _items.Contains(item);
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting
    /// unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Indexes the of.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The zero based index of the first occurrence of item within the entire collection.</returns>
    public int IndexOf(T item)
    {
        lock (_lock)
        {
            return _items.IndexOf(item);
        }
    }

    /// <summary>
    /// Replaces all existing items with new items.
    /// </summary>
    /// <param name="items">The new items.</param>
    public void ReplaceAll(IEnumerable<T> items)
    {
        lock (_lock)
        {
            ClearHistoryIfCountIsZero();
            _sourceList.Edit(l =>
            {
                _replacingAll = true;
                l.Clear();
                l.AddRange(items);
            });
            while (!_cleared && !_addedRange)
            {
                Thread.Sleep(1);
            }

            _replacingAll = false;
            _cleared = false;
            _addedRange = false;
        }
    }

    /// <summary>
    /// Removes the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>
    /// true if item was successfully removed from the System.Collections.Generic.ICollection
    /// otherwise, false. This method also returns false if item is not found in the
    /// original System.Collections.Generic.ICollection.
    /// </returns>
    public bool Remove(T item)
    {
        lock (_lock)
        {
            var removed = false;
            _sourceList.Edit(l => removed = l.Remove(item));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
            return removed;
        }
    }

    /// <summary>
    /// Removes the specified items.
    /// </summary>
    /// <param name="items">The items.</param>
    public void Remove(IEnumerable<T> items)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.Remove(items));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
        }
    }

    /// <summary>
    /// Removes the range.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="count">The count.</param>
    public void RemoveRange(int index, int count)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.RemoveRange(index, count));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
        }
    }

    /// <summary>
    /// Updates the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="newValue">The new value.</param>
    public void Update(T item, T newValue)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.Replace(item, newValue));
        }
    }

    /// <summary>
    /// Inserts the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="item">The item.</param>
    public void Insert(int index, T item)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.Insert(index, item));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
        }
    }

    /// <summary>
    /// Removes at.
    /// </summary>
    /// <param name="index">The index.</param>
    void IList<T>.RemoveAt(int index) => RemoveAt(index);

    /// <summary>
    /// Removes at.
    /// </summary>
    /// <param name="index">The index.</param>
    void IList.RemoveAt(int index) => RemoveAt(index);

    /// <summary>
    /// Removes at.
    /// </summary>
    /// <param name="index">The index.</param>
    public void RemoveAt(int index)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.RemoveAt(index));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
        }
    }

    /// <summary>
    /// Copies to.
    /// </summary>
    /// <param name="array">The array.</param>
    /// <param name="arrayIndex">Index of the array.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ((ICollection<T>)_items).CopyTo(array, arrayIndex);
        OnPropertyChanged("Item[]");
    }

    /// <summary>
    /// Gets the enumerator.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    /// <summary>
    /// Gets the enumerator.
    /// </summary>
    /// <returns>An System.Collections.IEnumerator object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();

    /// <summary>
    /// Adds the specified value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>An int of the length.</returns>
    /// <inheritdoc/>
    public int Add(object? value)
    {
        try
        {
            Add((T)value!);
        }
        catch (InvalidCastException)
        {
            throw;
        }

        return Count - 1;
    }

    /// <inheritdoc/>
    public void AddRange(IEnumerable<T> items)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.AddRange(items));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <inheritdoc/>
    void ICollection<T>.Clear() => Clear();

    /// <inheritdoc/>
    void IList.Clear() => Clear();

    /// <inheritdoc/>
    public void Clear()
    {
        lock (_lock)
        {
            ClearHistoryIfCountIsZero();
            _sourceList.Edit(l => l.Clear());
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <inheritdoc/>
    public bool Contains(T item)
    {
        lock (_lock)
        {
            return _items.Contains(item);
        }
    }

    /// <inheritdoc/>
    public bool Contains(object? value)
    {
        if (IsCompatibleObject(value))
        {
            return Contains((T)value!);
        }

        return false;
    }

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ((ICollection<T>)_items).CopyTo(array, arrayIndex);
        OnPropertyChanged(ItemArray);
    }

    /// <inheritdoc/>
    public void CopyTo(Array array, int index)
    {
        if (array == null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (array.Rank != 1)
        {
            throw new ArgumentException("Only single dimensional arrays are supported for the requested action.", nameof(array));
        }

        if (array.GetLowerBound(0) != 0)
        {
            throw new ArgumentException("The lower bound of target array must be zero.", nameof(array));
        }

        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index is less than zero.");
        }

        if (array.Length - index < Count)
        {
            throw new ArgumentException("The number of elements in the source collection is greater than the available space from index to the end of the destination array.", nameof(array));
        }

        if (array is T[] tArray)
        {
            CopyTo(tArray, index);
        }
        else if (array is object[] objects)
        {
            try
            {
                foreach (var item in this)
                {
                    objects[index++] = item;
                }
            }
            catch (ArrayTypeMismatchException)
            {
                throw new ArgumentException("Invalid array type.");
            }
        }
    }

    /// <summary>
    /// Clears this instance.
    /// </summary>
    void ICollection<T>.Clear() => Clear();
    /// <summary>
    /// Clears this instance.
    /// </summary>
    void IList.Clear() => Clear();

    /// <summary>
    /// Clears this instance.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            ClearHistoryIfCountIsZero();
            _sourceList.Edit(l => l.Clear());
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
        }
    }

    /// <summary>
    /// Raises a PropertyChanged event (per <see cref="INotifyPropertyChanged" />).
    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(T item)
    {
        lock (_lock)
        {
            return _items.IndexOf(item);
        }
    }

    /// <inheritdoc/>
    public int IndexOf(object? value)
    {
        if (IsCompatibleObject(value))
        {
            return IndexOf((T)value!);
        }

        return -1;
    }

    /// <inheritdoc/>
    public void Insert(int index, T item)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.Insert(index, item));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <inheritdoc/>
    public void Insert(int index, object? value)
    {
        try
        {
            Insert(index, (T)value!);
        }
        catch (InvalidCastException)
        {
            throw;
        }
    }

    /// <inheritdoc/>
    public bool Remove(T item)
    {
        lock (_lock)
        {
            var removed = false;
            _sourceList.Edit(l => removed = l.Remove(item));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
            return removed;
        }
    }

    /// <inheritdoc/>
    public void Remove(IEnumerable<T> items)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.Remove(items));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <inheritdoc/>
    public void Remove(object? value)
    {
        if (IsCompatibleObject(value))
        {
            Remove((T)value!);
        }
    }

    /// <inheritdoc/>
    void IList<T>.RemoveAt(int index) => RemoveAt(index);

    /// <inheritdoc/>
    void IList.RemoveAt(int index) => RemoveAt(index);

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.RemoveAt(index));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <summary>
    /// Removes a range of elements.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="count">The count.</param>
    public void RemoveRange(int index, int count)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.RemoveRange(index, count));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <summary>
    /// Replaces all existing items with new items.
    /// </summary>
    /// <param name="items">The new items.</param>
    public void ReplaceAll(IEnumerable<T> items)
    {
        lock (_lock)
        {
            ClearHistoryIfCountIsZero();
            _sourceList.Edit(l =>
            {
                _replacingAll = true;
                l.Clear();
                l.AddRange(items);
            });
            while (!_cleared && !_addedRange)
            {
                Thread.Sleep(1);
            }

            _replacingAll = false;
            _cleared = false;
            _addedRange = false;
        }
    }

    /// <summary>
    /// Subscribes the specified observer to the CurrentItems.
    /// </summary>
    /// <param name="observer">The observer.</param>
    /// <returns>An IDisposable to release the subscription.</returns>
    public IDisposable Subscribe(IObserver<IEnumerable<T>> observer) => _currentItems.Subscribe(observer);

    /// <inheritdoc/>
    public void Update(T item, T newValue)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.Replace(item, newValue));
        }
    }

    /// <summary>
    /// Disposes the specified disposing.
    /// </summary>
    /// <param name="disposing">if set to <c>true</c> [disposing].</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _added.Dispose();
            _changed.Dispose();
            _removed.Dispose();
            _currentItems.Dispose();
            _sourceList.Dispose();
            _cleanUp.Dispose();
        }
    }

    /// <summary>
    /// Raises a PropertyChanged event (per <see cref="INotifyPropertyChanged" />).
    /// </summary>
    /// <param name="propertyName">Name of the property.</param>
    protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Determines whether [is compatible object] [the specified value].
    /// Non-null values are fine.  Only accept nulls if T is a class or Nullable.
    /// Note that default(T) is not equal to null for value types except when T is Nullable.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>
    ///   <c>true</c> if [is compatible object] [the specified value]; otherwise, <c>false</c>.
    /// </returns>
    private static bool IsCompatibleObject(object? value) =>
        (value is T) || (value == null && default(T) == null);

    private void ClearHistoryIfCountIsZero()
    {
        if (_sourceList.Count == 0)
        {
            _itemsAddedoc.Clear();
            _itemsChangedoc.Clear();
            _itemsRemovedoc.Clear();
        }
    }
}
