// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace CP.Reactive;

/// <summary>
/// Represents a thread-safe, observable list that provides reactive notifications for item additions, removals, and
/// changes.
/// </summary>
/// <remarks><see cref="ReactiveList{T}"/> combines the features of a standard list with reactive extensions, allowing consumers
/// to observe changes to the collection in real time. It implements standard collection interfaces and supports batch
/// operations, making it suitable for scenarios where both collection manipulation and change tracking are required.
/// The class raises collection and property change notifications, and exposes observables for added, removed, and
/// changed items. All public methods are thread-safe. This type is not read-only or fixed-size, and supports dynamic
/// modification.</remarks>
/// <typeparam name="T">The type of elements in the list. Must be non-nullable.</typeparam>
[Serializable]
[DebuggerDisplay("Count = {Count}")]
public class ReactiveList<T> : IReactiveList<T>
    where T : notnull
{
    private const string ItemArray = "Item[]";
    private readonly List<T> _internalList = [];

    [NonSerialized]
    private BehaviorSubject<IEnumerable<T>>? _added;

    [NonSerialized]
    private BehaviorSubject<IEnumerable<T>>? _changed;

    [NonSerialized]
    private CompositeDisposable? _cleanUp;

    [NonSerialized]
    private BehaviorSubject<IEnumerable<T>>? _currentItems;

    [NonSerialized]
    private ReadOnlyObservableCollection<T>? _items;

    [NonSerialized]
    private ReadOnlyObservableCollection<T>? _itemsAdded;

    [NonSerialized]
    private ObservableCollection<T>? _itemsAddedCollection;

    [NonSerialized]
    private ReadOnlyObservableCollection<T>? _itemsChanged;

    [NonSerialized]
    private ObservableCollection<T>? _itemsChangedCollection;

    [NonSerialized]
    private ReadOnlyObservableCollection<T>? _itemsRemoved;

    [NonSerialized]
    private ObservableCollection<T>? _itemsRemovedCollection;

#if NET9_0_OR_GREATER
    [NonSerialized]
    private Lock? _lock;
#else
    [NonSerialized]
    private object? _lock;
#endif

    [NonSerialized]
    private ObservableCollection<T>? _observableItems;

    [NonSerialized]
    private BehaviorSubject<IEnumerable<T>>? _removed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveList{T}"/> class.
    /// </summary>
    public ReactiveList() => InitializeNonSerializedFields();

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
    public IObservable<IEnumerable<T>> Added => _added!.Skip(1);

    /// <summary>
    /// Gets the changed during the last change as an Observeable.
    /// </summary>
    /// <value>The changed.</value>
    public IObservable<IEnumerable<T>> Changed => _changed!.Skip(1);

    /// <inheritdoc/>
    public int Count => _internalList.Count;

    /// <summary>
    /// Gets the current items during the last change as an Observeable.
    /// </summary>
    /// <value>
    /// The current items.
    /// </value>
    public IObservable<IEnumerable<T>> CurrentItems => _currentItems!;

    /// <inheritdoc/>
    public bool IsDisposed => _cleanUp?.IsDisposed ?? true;

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
    public ReadOnlyObservableCollection<T> Items => _items!;

    /// <summary>
    /// Gets the items added during the last change.
    /// </summary>
    /// <value>The items added.</value>
    public ReadOnlyObservableCollection<T> ItemsAdded => _itemsAdded!;

    /// <summary>
    /// Gets the items changed during the last change.
    /// </summary>
    /// <value>The items changed.</value>
    public ReadOnlyObservableCollection<T> ItemsChanged => _itemsChanged!;

    /// <summary>
    /// Gets the items removed during the last change.
    /// </summary>
    /// <value>The items removed.</value>
    public ReadOnlyObservableCollection<T> ItemsRemoved => _itemsRemoved!;

    /// <summary>
    /// Gets the removed items during the last change as an Observable.
    /// </summary>
    /// <value>The removed.</value>
    public IObservable<IEnumerable<T>> Removed => _removed!.Skip(1);

    /// <inheritdoc/>
    public object SyncRoot => this;

    /// <inheritdoc/>
    object? IList.this[int index]
    {
        get => _internalList[index];
        set
        {
            RemoveAt(index);
            Insert(index, (T)value!);
        }
    }

    /// <inheritdoc/>
    public T this[int index]
    {
        get => _internalList[index];
        set
        {
            RemoveAt(index);
            Insert(index, value);
        }
    }

    /// <inheritdoc/>
    public void Add(T item)
    {
        lock (_lock!)
        {
            _internalList.Add(item);
            _observableItems!.Add(item);
            NotifyAdded(item);
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
        var itemArray = items as T[] ?? items.ToArray();
        if (itemArray.Length == 0)
        {
            return;
        }

        lock (_lock!)
        {
#if NET6_0_OR_GREATER
            // Use AddRange with capacity hint for List
            _internalList.EnsureCapacity(_internalList.Count + itemArray.Length);
#endif
            _internalList.AddRange(itemArray);
            foreach (var item in itemArray)
            {
                _observableItems!.Add(item);
            }

            NotifyAddedRange(itemArray);
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
        lock (_lock!)
        {
            if (_internalList.Count == 0)
            {
                ClearHistory();
                OnPropertyChanged(nameof(Count));
                OnPropertyChanged(ItemArray);
                return;
            }

            var clearedItems = _internalList.ToArray();
            _internalList.Clear();
            _observableItems!.Clear();
            NotifyCleared(clearedItems);
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item)
    {
        lock (_lock!)
        {
            return _internalList.Contains(item);
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
        lock (_lock!)
        {
#if NET6_0_OR_GREATER
            CollectionsMarshal.AsSpan(_internalList).CopyTo(array.AsSpan(arrayIndex));
#else
            _internalList.CopyTo(array, arrayIndex);
#endif
        }
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
                lock (_lock!)
                {
                    foreach (var item in _internalList)
                    {
                        objects[index++] = item;
                    }
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
    public void Edit(Action<IEditableList<T>> editAction)
    {
        if (editAction == null)
        {
            throw new ArgumentNullException(nameof(editAction));
        }

        lock (_lock!)
        {
            var snapshotSet = new HashSet<T>(_internalList);
            var wrapper = new EditableListWrapper<T>(_internalList, _observableItems);
            editAction(wrapper);

            var currentSet = new HashSet<T>(_internalList);
            var added = new List<T>();
            var removed = new List<T>();

            foreach (var item in _internalList)
            {
                if (!snapshotSet.Contains(item))
                {
                    added.Add(item);
                }
            }

            foreach (var item in snapshotSet)
            {
                if (!currentSet.Contains(item))
                {
                    removed.Add(item);
                }
            }

            if (added.Count > 0)
            {
                NotifyAddedRange([.. added]);
            }

            if (removed.Count > 0)
            {
                NotifyRemovedRange([.. removed]);
            }

            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        lock (_lock!)
        {
            return _internalList.ToList().GetEnumerator();
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(T item)
    {
        lock (_lock!)
        {
            return _internalList.IndexOf(item);
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
        lock (_lock!)
        {
            _internalList.Insert(index, item);
            _observableItems!.Insert(index, item);
            NotifyAdded(item, index);
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
        var itemArray = items as T[] ?? items.ToArray();
        if (itemArray.Length == 0)
        {
            return;
        }

        lock (_lock!)
        {
            _internalList.InsertRange(index, itemArray);
            for (var i = 0; i < itemArray.Length; i++)
            {
                _observableItems!.Insert(index + i, itemArray[i]);
            }

            NotifyAddedRange(itemArray, index);
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

        lock (_lock!)
        {
            var item = _internalList[oldIndex];
            _internalList.RemoveAt(oldIndex);
            _internalList.Insert(newIndex, item);
            _observableItems!.Move(oldIndex, newIndex);
            NotifyChangedSingle(item);
            OnPropertyChanged(ItemArray);
        }
    }

    /// <inheritdoc/>
    public bool Remove(T item)
    {
        lock (_lock!)
        {
            var index = _internalList.IndexOf(item);
            if (index < 0)
            {
                return false;
            }

            _internalList.RemoveAt(index);
            _observableItems!.RemoveAt(index);
            NotifyRemoved(item, index);
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
            return true;
        }
    }

    /// <inheritdoc/>
    public void Remove(IEnumerable<T> items)
    {
        var itemArray = items as T[] ?? items.ToArray();
        if (itemArray.Length == 0)
        {
            return;
        }

        lock (_lock!)
        {
            var removed = new List<T>();
            foreach (var item in itemArray)
            {
                var index = _internalList.IndexOf(item);
                if (index >= 0)
                {
                    _internalList.RemoveAt(index);
                    _observableItems!.RemoveAt(index);
                    removed.Add(item);
                }
            }

            if (removed.Count > 0)
            {
                NotifyRemovedRange([.. removed]);
                OnPropertyChanged(nameof(Count));
                OnPropertyChanged(ItemArray);
            }
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
        lock (_lock!)
        {
            if (index < 0 || index >= _internalList.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var item = _internalList[index];
            _internalList.RemoveAt(index);
            _observableItems!.RemoveAt(index);
            NotifyRemoved(item, index);
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

        lock (_lock!)
        {
            if (index < 0 || index >= _internalList.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (index + count > _internalList.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

#if NET6_0_OR_GREATER
            // Use GetRange + RemoveRange for better performance
            var removed = _internalList.GetRange(index, count).ToArray();
            _internalList.RemoveRange(index, count);
            for (var i = 0; i < count; i++)
            {
                _observableItems!.RemoveAt(index);
            }

            NotifyRemovedRange(removed);
#else
            var removed = new T[count];
            for (var i = 0; i < count; i++)
            {
                removed[i] = _internalList[index];
                _internalList.RemoveAt(index);
                _observableItems!.RemoveAt(index);
            }

            NotifyRemovedRange(removed);
#endif
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <inheritdoc/>
    public void ReplaceAll(IEnumerable<T> items)
    {
        var itemArray = items as T[] ?? items.ToArray();

        lock (_lock!)
        {
            var oldItems = _internalList.ToArray();
            _internalList.Clear();
            _observableItems!.Clear();

#if NET6_0_OR_GREATER
            _internalList.EnsureCapacity(itemArray.Length);
#endif
            _internalList.AddRange(itemArray);
            foreach (var item in itemArray)
            {
                _observableItems.Add(item);
            }

            // Update tracking collections
            UpdateTrackingCollection(_itemsRemovedCollection!, oldItems);
            _removed!.OnNext(oldItems);

            UpdateTrackingCollection(_itemsAddedCollection!, itemArray);
            _added!.OnNext(itemArray);

            UpdateTrackingCollection(_itemsChangedCollection!, oldItems);
            _changed!.OnNext(oldItems);

            _currentItems!.OnNext(_internalList);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <summary>
    /// Subscribes the specified observer to the CurrentItems.
    /// </summary>
    /// <param name="observer">The observer.</param>
    /// <returns>An IDisposable to release the subscription.</returns>
    public IDisposable Subscribe(IObserver<IEnumerable<T>> observer) => _currentItems!.Subscribe(observer);

    /// <inheritdoc/>
    public void Update(T item, T newValue)
    {
        lock (_lock!)
        {
            var index = _internalList.IndexOf(item);
            if (index >= 0)
            {
                _internalList[index] = newValue;
                _observableItems![index] = newValue;
                NotifyChangedSingle(newValue);
            }
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
            _cleanUp?.Dispose();
            _added?.Dispose();
            _changed?.Dispose();
            _removed?.Dispose();
            _currentItems?.Dispose();
        }
    }

    /// <summary>
    /// Raises a PropertyChanged event (per <see cref="INotifyPropertyChanged" />).
    /// </summary>
    /// <param name="propertyName">Name of the property.</param>
    protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCompatibleObject(object? value) =>
        (value is T) || (value == null && default(T) == null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateTrackingCollection(ObservableCollection<T> target, T[] items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) => InitializeNonSerializedFields();

    private void InitializeNonSerializedFields()
    {
#if NET9_0_OR_GREATER
        _lock = new Lock();
#else
        _lock = new object();
#endif
        _cleanUp = [];
        _observableItems = new(_internalList);
        _itemsAddedCollection = [];
        _itemsChangedCollection = [];
        _itemsRemovedCollection = [];
        _items = new(_observableItems);
        _itemsAdded = new(_itemsAddedCollection);
        _itemsChanged = new(_itemsChangedCollection);
        _itemsRemoved = new(_itemsRemovedCollection);
        _added = new(Array.Empty<T>());
        _changed = new(Array.Empty<T>());
        _removed = new(Array.Empty<T>());
        _currentItems = new(Array.Empty<T>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyAdded(T item, int index = -1)
    {
        _itemsAddedCollection!.Clear();
        _itemsAddedCollection.Add(item);
        _itemsRemovedCollection!.Clear();
        _itemsChangedCollection!.Clear();
        _itemsChangedCollection.Add(item);

        _added!.OnNext([item]);
        _changed!.OnNext([item]);
        _currentItems!.OnNext(_internalList);

        var startIndex = index >= 0 ? index : _internalList.Count - 1;
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, startIndex));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyAddedRange(T[] items, int index = -1)
    {
        UpdateTrackingCollection(_itemsAddedCollection!, items);
        _itemsRemovedCollection!.Clear();
        UpdateTrackingCollection(_itemsChangedCollection!, items);

        _added!.OnNext(items);
        _changed!.OnNext(items);
        _currentItems!.OnNext(_internalList);

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyRemoved(T item, int index)
    {
        _itemsRemovedCollection!.Clear();
        _itemsRemovedCollection.Add(item);
        _itemsAddedCollection!.Clear();
        _itemsChangedCollection!.Clear();
        _itemsChangedCollection.Add(item);

        _removed!.OnNext([item]);
        _changed!.OnNext([item]);
        _currentItems!.OnNext(_internalList);

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyRemovedRange(T[] items)
    {
        UpdateTrackingCollection(_itemsRemovedCollection!, items);
        _itemsAddedCollection!.Clear();
        UpdateTrackingCollection(_itemsChangedCollection!, items);

        _removed!.OnNext(items);
        _changed!.OnNext(items);
        _currentItems!.OnNext(_internalList);

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyCleared(T[] clearedItems)
    {
        _itemsAddedCollection!.Clear();
        UpdateTrackingCollection(_itemsChangedCollection!, clearedItems);
        UpdateTrackingCollection(_itemsRemovedCollection!, clearedItems);

        _removed!.OnNext(clearedItems);
        _changed!.OnNext(clearedItems);
        _currentItems!.OnNext(_internalList);

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyChangedSingle(T item)
    {
        _itemsChangedCollection!.Clear();
        _itemsChangedCollection.Add(item);

        _changed!.OnNext([item]);
        _currentItems!.OnNext(_internalList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearHistory()
    {
        _itemsAddedCollection!.Clear();
        _itemsChangedCollection!.Clear();
        _itemsRemovedCollection!.Clear();
    }
}
