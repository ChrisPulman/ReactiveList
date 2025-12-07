// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
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
    private readonly ObservableCollection<T> _itemsAddedoc = [];
    private readonly ObservableCollection<T> _itemsChangedoc = [];
    private readonly ObservableCollection<T> _itemsRemovedoc = [];
    private readonly object _lock = new();
    private readonly ReplaySubject<IEnumerable<T>> _removed = new(1);
    private readonly SourceList<T> _sourceList = new();
    private readonly ManualResetEventSlim _clearedEvent = new(false);
    private readonly ManualResetEventSlim _addedRangeEvent = new(false);
    private ReadOnlyObservableCollection<T> _items;
    private volatile bool _replacingAll;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveList{T}"/> class.
    /// </summary>
    public ReactiveList()
    {
        _items = new([]);
        ItemsAdded = new(_itemsAddedoc);
        ItemsRemoved = new(_itemsRemovedoc);
        ItemsChanged = new(_itemsChangedoc);

        SetupSubscriptions();
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
    /// Gets the added during the last change as an Observeable.
    /// </summary>
    /// <value>The added.</value>
    public IObservable<IEnumerable<T>> Added => _added;

    /// <summary>
    /// Gets the changed during the last change as an Observeable.
    /// </summary>
    /// <value>The changed.</value>
    public IObservable<IEnumerable<T>> Changed => _changed;

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <summary>
    /// Gets the current items during the last change as an Observeable.
    /// </summary>
    /// <value>
    /// The current items.
    /// </value>
    public IObservable<IEnumerable<T>> CurrentItems => _currentItems;

    /// <inheritdoc/>
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
    /// Gets the items added during the last change.
    /// </summary>
    /// <value>The items added.</value>
    public ReadOnlyObservableCollection<T> ItemsAdded { get; }

    /// <summary>
    /// Gets the items changed during the last change.
    /// </summary>
    /// <value>The items changed.</value>
    public ReadOnlyObservableCollection<T> ItemsChanged { get; }

    /// <summary>
    /// Gets the items removed during the last change.
    /// </summary>
    /// <value>The items removed.</value>
    public ReadOnlyObservableCollection<T> ItemsRemoved { get; }

    /// <summary>
    /// Gets the removed items during the last change as an Observable.
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

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

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
        if (items is ICollection<T> c && c.Count == 0)
        {
            return;
        }

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
    public void CopyTo(T[] array, int arrayIndex) => ((ICollection<T>)_items).CopyTo(array, arrayIndex);

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
    /// Executes a batch edit operation on the list.
    /// </summary>
    /// <param name="editAction">The action to perform on the internal list.</param>
    public void Edit(Action<IExtendedList<T>> editAction)
    {
        if (editAction == null)
        {
            throw new ArgumentNullException(nameof(editAction));
        }

        lock (_lock)
        {
            _sourceList.Edit(editAction);
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
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

    /// <summary>
    /// Inserts the range.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="items">The items.</param>
    public void InsertRange(int index, IEnumerable<T> items)
    {
        if (items is ICollection<T> c && c.Count == 0)
        {
            return;
        }

        lock (_lock)
        {
            _sourceList.Edit(l => l.InsertRange(items, index));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <summary>
    /// Moves an item from one index to another.
    /// </summary>
    /// <param name="oldIndex">The current index of the item.</param>
    /// <param name="newIndex">The new index for the item.</param>
    public void Move(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(oldIndex));
        }

        if (newIndex < 0 || newIndex >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(newIndex));
        }

        if (oldIndex == newIndex)
        {
            return;
        }

        lock (_lock)
        {
            _sourceList.Edit(l => l.Move(oldIndex, newIndex));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <inheritdoc/>
    public bool Remove(T item)
    {
        lock (_lock)
        {
            var removed = false;
            _sourceList.Edit(l => removed = l.Remove(item));
            if (removed)
            {
                OnPropertyChanged(nameof(Count));
                OnPropertyChanged(ItemArray);
            }

            return removed;
        }
    }

    /// <inheritdoc/>
    public void Remove(IEnumerable<T> items)
    {
        if (items is ICollection<T> c && c.Count == 0)
        {
            return;
        }

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

    /// <inheritdoc/>
    public void RemoveRange(int index, int count)
    {
        if (count == 0)
        {
            return;
        }

        lock (_lock)
        {
            _sourceList.Edit(l => l.RemoveRange(index, count));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <inheritdoc/>
    public void ReplaceAll(IEnumerable<T> items)
    {
        lock (_lock)
        {
            ClearHistoryIfCountIsZero();
            _replacingAll = true;
            _clearedEvent.Reset();
            _addedRangeEvent.Reset();

            _sourceList.Edit(l =>
            {
                l.Clear();
                l.AddRange(items);
            });

            // Wait for both Clear and AddRange events using efficient signaling
            var timeout = TimeSpan.FromSeconds(5);
            if (!_clearedEvent.Wait(timeout) || !_addedRangeEvent.Wait(timeout))
            {
                // Timeout occurred - events were not signaled in time
                _replacingAll = false;
                throw new TimeoutException("ReplaceAll operation timed out waiting for change events.");
            }

            _replacingAll = false;
        }

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(ItemArray);
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
    /// Disposes the specified disposables.
    /// </summary>
    /// <param name="disposing">if set to <c>true</c> [disposing].</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // First, dispose subscriptions
            _cleanUp.Dispose();

            // Then dispose owned resources
            _added.Dispose();
            _changed.Dispose();
            _removed.Dispose();
            _currentItems.Dispose();
            _sourceList.Dispose();
            _clearedEvent.Dispose();
            _addedRangeEvent.Dispose();
        }
    }

    /// <summary>
    /// Raises a PropertyChanged event (per <see cref="INotifyPropertyChanged" />).
    /// </summary>
    /// <param name="propertyName">Name of the property.</param>
    protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static bool IsCompatibleObject(object? value) =>
        (value is T) || (value == null && default(T) == null);

    private static void UpdateObservableCollection(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    private void SetupSubscriptions()
    {
        var srcList = _sourceList.Connect();

        srcList
            .ObserveOn(Scheduler.Immediate)
            .Bind(out _items)
            .Subscribe()
            .DisposeWith(_cleanUp);

        _sourceList
            .CountChanged
            .Select(_ => _sourceList.Items)
            .ObserveOn(Scheduler.Immediate)
            .Do(_currentItems.OnNext)
            .Subscribe()
            .DisposeWith(_cleanUp);

        SetupAddedSubscriptions(srcList);
        SetupRemovedSubscriptions(srcList);
        SetupChangedSubscriptions(srcList);
        SetupClearSubscription(srcList);
    }

    private void SetupAddedSubscriptions(IObservable<IChangeSet<T>> srcList)
    {
        // Added - single items
        srcList
            .WhereReasonsAre(ListChangeReason.Add)
            .Select(t => t.Select(v => v.Item.Current))
            .Do(_added.OnNext)
            .ObserveOn(Scheduler.Immediate)
            .Subscribe(v =>
            {
                UpdateObservableCollection(_itemsAddedoc, v);
                _itemsRemovedoc.Clear();
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, v.ToList(), _items.Count - v.Count()));
            })
            .DisposeWith(_cleanUp);

        // Added range
        srcList
            .WhereReasonsAre(ListChangeReason.AddRange)
            .SelectMany(t => t.Select(v => v.Range))
            .Do(_added.OnNext)
            .ObserveOn(Scheduler.Immediate)
            .Subscribe(v =>
            {
                UpdateObservableCollection(_itemsAddedoc, v);
                if (!_replacingAll)
                {
                    _itemsRemovedoc.Clear();
                }
                else
                {
                    _addedRangeEvent.Set();
                }

                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            })
            .DisposeWith(_cleanUp);
    }

    private void SetupRemovedSubscriptions(IObservable<IChangeSet<T>> srcList)
    {
        // Removed - single items
        srcList
            .WhereReasonsAre(ListChangeReason.Remove)
            .Select(t => t.Select(v => v.Item.Current))
            .Do(_removed.OnNext)
            .ObserveOn(Scheduler.Immediate)
            .Subscribe(v =>
            {
                UpdateObservableCollection(_itemsRemovedoc, v);
                _itemsAddedoc.Clear();
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, v));
            })
            .DisposeWith(_cleanUp);

        // Removed range
        srcList
            .WhereReasonsAre(ListChangeReason.RemoveRange)
            .SelectMany(t => t.Select(v => v.Range))
            .Do(_removed.OnNext)
            .ObserveOn(Scheduler.Immediate)
            .Subscribe(v =>
            {
                UpdateObservableCollection(_itemsRemovedoc, v);
                _itemsAddedoc.Clear();
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, v.ToList()));
            })
            .DisposeWith(_cleanUp);
    }

    private void SetupChangedSubscriptions(IObservable<IChangeSet<T>> srcList)
    {
        // Changed: single item adds/removes/replaces
        srcList
            .WhereReasonsAre(ListChangeReason.Add, ListChangeReason.Remove, ListChangeReason.Replace)
            .Select(t => t.Select(v => v.Item.Current))
            .Do(_changed.OnNext)
            .ObserveOn(Scheduler.Immediate)
            .Subscribe(v => UpdateObservableCollection(_itemsChangedoc, v))
            .DisposeWith(_cleanUp);

        // Changed: add range -> skip updating when replacing all so Clear determines ItemsChanged
        srcList
            .WhereReasonsAre(ListChangeReason.AddRange)
            .SelectMany(t => t.Select(v => v.Range))
            .Do(_changed.OnNext)
            .ObserveOn(Scheduler.Immediate)
            .Subscribe(v =>
            {
                if (_replacingAll)
                {
                    return;
                }

                UpdateObservableCollection(_itemsChangedoc, v);
            })
            .DisposeWith(_cleanUp);

        // Changed: remove range
        srcList
            .WhereReasonsAre(ListChangeReason.RemoveRange)
            .SelectMany(t => t.Select(v => v.Range))
            .Do(_changed.OnNext)
            .ObserveOn(Scheduler.Immediate)
            .Subscribe(v => UpdateObservableCollection(_itemsChangedoc, v))
            .DisposeWith(_cleanUp);
    }

    private void SetupClearSubscription(IObservable<IChangeSet<T>> srcList) => srcList
            .WhereReasonsAre(ListChangeReason.Clear)
            .SelectMany(t => t.Select(v => v.Range))
            .ObserveOn(Scheduler.Immediate)
            .Subscribe(v =>
            {
                if (!_replacingAll)
                {
                    _itemsAddedoc.Clear();
                }
                else
                {
                    _clearedEvent.Set();
                }

                UpdateObservableCollection(_itemsChangedoc, v);
                UpdateObservableCollection(_itemsRemovedoc, v);
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            })
            .DisposeWith(_cleanUp);

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
