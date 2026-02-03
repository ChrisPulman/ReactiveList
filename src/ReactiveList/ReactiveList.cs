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
using System.Buffers;
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

    [NonSerialized]
    private Subject<ChangeSet<T>>? _changeSubject;

    [NonSerialized]
    private long _version;

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

    /// <summary>
    /// Gets the current version number of the collection, which is incremented on each modification.
    /// </summary>
    /// <remarks>
    /// This property can be used for efficient change detection without acquiring locks.
    /// The version is incremented atomically using <see cref="Interlocked.Increment(ref long)"/>.
    /// </remarks>
    public long Version => Interlocked.Read(ref _version);

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    /// <summary>
    /// Connects to the change stream. Similar to DynamicData's Connect().
    /// </summary>
    /// <returns>An observable stream of change sets representing all modifications to the list.</returns>
    public IObservable<ChangeSet<T>> Connect() => _changeSubject!.AsObservable();

#if NET6_0_OR_GREATER
    /// <summary>
    /// Creates a snapshot of current items as an array.
    /// </summary>
    /// <returns>An array containing all current items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T[] ToArray()
    {
        lock (_lock!)
        {
            return [.. _internalList];
        }
    }

    /// <summary>
    /// Adds a range of items from a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <param name="items">The items to add.</param>
    public void AddRange(ReadOnlySpan<T> items)
    {
        if (items.IsEmpty)
        {
            return;
        }

        lock (_lock!)
        {
            _internalList.EnsureCapacity(_internalList.Count + items.Length);
            foreach (var item in items)
            {
                _internalList.Add(item);
                _observableItems!.Add(item);
            }

            // Notify with array copy
            var itemArray = items.ToArray();
            NotifyAddedRange(itemArray);
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <summary>
    /// Copies items to the specified span.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <exception cref="ArgumentException">Thrown when destination is too small.</exception>
    public void CopyTo(Span<T> destination)
    {
        lock (_lock!)
        {
            if (destination.Length < _internalList.Count)
            {
                throw new ArgumentException("Destination span is too small.", nameof(destination));
            }

            CollectionsMarshal.AsSpan(_internalList).CopyTo(destination);
        }
    }

    /// <summary>
    /// Gets a read-only span over the internal list for zero-copy access.
    /// </summary>
    /// <remarks>
    /// WARNING: This method does not acquire a lock. The caller must ensure thread safety.
    /// The returned span is only valid while no modifications are made to the list.
    /// </remarks>
    /// <returns>A read-only span over the internal items.</returns>
    public ReadOnlySpan<T> AsSpan()
    {
        return CollectionsMarshal.AsSpan(_internalList);
    }

    /// <summary>
    /// Gets a memory region over the internal list for async operations.
    /// </summary>
    /// <remarks>
    /// WARNING: This method does not acquire a lock. The caller must ensure thread safety.
    /// The returned memory is only valid while no modifications are made to the list.
    /// </remarks>
    /// <returns>A read-only memory region over the internal items.</returns>
    public ReadOnlyMemory<T> AsMemory()
    {
        return _internalList.ToArray().AsMemory();
    }

    /// <summary>
    /// Clears all items from the list without releasing the internal array capacity.
    /// </summary>
    /// <remarks>
    /// This method is more efficient than Clear() when you plan to add items back to the list,
    /// as it avoids the overhead of reallocating the internal array. The capacity is preserved.
    /// </remarks>
    /// <param name="notifyChange">Whether to emit change notifications. Defaults to true.</param>
    public void ClearWithoutDeallocation(bool notifyChange = true)
    {
        lock (_lock!)
        {
            if (_internalList.Count == 0)
            {
                if (notifyChange)
                {
                    ClearHistory();
                    OnPropertyChanged(nameof(Count));
                    OnPropertyChanged(ItemArray);
                }

                return;
            }

            var clearedItems = notifyChange ? _internalList.ToArray() : Array.Empty<T>();
            var capacity = _internalList.Capacity;

            _internalList.Clear();
            _observableItems!.Clear();

            // Restore capacity to avoid reallocation on next add
            _internalList.Capacity = capacity;

            if (notifyChange)
            {
                NotifyCleared(clearedItems);
                OnPropertyChanged(nameof(Count));
                OnPropertyChanged(ItemArray);
            }
        }
    }
#endif

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            NotifyChangedSingle(item, ChangeReason.Move, newIndex, oldIndex);
            OnPropertyChanged(ItemArray);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RemoveMany(Func<T, bool> predicate)
    {
        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        lock (_lock!)
        {
#if NET6_0_OR_GREATER
            // Use pooled buffer for better memory efficiency
            var removedBuffer = ArrayPool<T>.Shared.Rent(Math.Max(16, _internalList.Count / 4));
            var removedCount = 0;

            try
            {
                // Iterate in reverse to avoid index shifting issues
                for (var i = _internalList.Count - 1; i >= 0; i--)
                {
                    var item = _internalList[i];
                    if (predicate(item))
                    {
                        _internalList.RemoveAt(i);
                        _observableItems!.RemoveAt(i);

                        // Grow buffer if needed
                        if (removedCount >= removedBuffer.Length)
                        {
                            var newBuffer = ArrayPool<T>.Shared.Rent(removedBuffer.Length * 2);
                            removedBuffer.AsSpan(0, removedCount).CopyTo(newBuffer);
                            ArrayPool<T>.Shared.Return(removedBuffer);
                            removedBuffer = newBuffer;
                        }

                        removedBuffer[removedCount++] = item;
                    }
                }

                if (removedCount > 0)
                {
                    // Reverse in-place to maintain original order
                    removedBuffer.AsSpan(0, removedCount).Reverse();
                    NotifyRemovedRange(removedBuffer.AsSpan(0, removedCount).ToArray());
                    OnPropertyChanged(nameof(Count));
                    OnPropertyChanged(ItemArray);
                }

                return removedCount;
            }
            finally
            {
                ArrayPool<T>.Shared.Return(removedBuffer, clearArray: true);
            }
#else
            var removed = new List<T>();

            // Iterate in reverse to avoid index shifting issues
            for (var i = _internalList.Count - 1; i >= 0; i--)
            {
                var item = _internalList[i];
                if (predicate(item))
                {
                    _internalList.RemoveAt(i);
                    _observableItems!.RemoveAt(i);
                    removed.Add(item);
                }
            }

            if (removed.Count > 0)
            {
                // Reverse to maintain original order in notification
                removed.Reverse();
                NotifyRemovedRange([.. removed]);
                OnPropertyChanged(nameof(Count));
                OnPropertyChanged(ItemArray);
            }

            return removed.Count;
#endif
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(T item, T newValue)
    {
        lock (_lock!)
        {
            var index = _internalList.IndexOf(item);
            if (index >= 0)
            {
                _internalList[index] = newValue;
                _observableItems![index] = newValue;
                NotifyChangedSingle(newValue, ChangeReason.Update, index, index);
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
            _changeSubject?.OnCompleted();
            _changeSubject?.Dispose();
        }
    }

    /// <summary>
    /// Raises a PropertyChanged event (per <see cref="INotifyPropertyChanged" />).
    /// </summary>
    /// <param name="propertyName">Name of the property.</param>
    protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Determines whether the specified object is compatible with the generic type parameter T.
    /// </summary>
    /// <param name="value">The object to test for compatibility with type T. May be null.</param>
    /// <returns>true if the object is of type T, or if both the object and the default value of T are null; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCompatibleObject(object? value) =>
        (value is T) || (value == null && default(T) == null);

    /// <summary>
    /// Replaces all items in the specified observable collection with the elements from the provided array.
    /// </summary>
    /// <remarks>The method clears the target collection before adding the new items. The order of items in
    /// the collection will match the order in the provided array.</remarks>
    /// <param name="target">The observable collection to update. All existing items in this collection will be removed and replaced.</param>
    /// <param name="items">The array of items to add to the collection. The collection will contain these items after the operation
    /// completes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateTrackingCollection(ObservableCollection<T> target, T[] items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    /// <summary>
    /// Handles post-deserialization processing to restore the object's state after deserialization is complete.
    /// </summary>
    /// <remarks>This method is automatically invoked by the deserialization infrastructure after the object
    /// has been deserialized. It is used to reinitialize fields or properties that are not serialized.</remarks>
    /// <param name="context">The streaming context for the deserialization operation. Provides contextual information about the source or
    /// destination of the serialization stream.</param>
    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) => InitializeNonSerializedFields();

    /// <summary>
    /// Initializes fields that are not serialized during deserialization or object construction.
    /// </summary>
    /// <remarks>Call this method after deserialization to ensure that all non-serialized fields are properly
    /// initialized and the object is in a valid state. This method is intended for internal use and should not be
    /// called directly in normal application code.</remarks>
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
        _changeSubject = new();
    }

    /// <summary>
    /// Notifies subscribers that an item has been added to the collection and raises the appropriate collection changed
    /// event.
    /// </summary>
    /// <param name="item">The item that was added to the collection.</param>
    /// <param name="index">The zero-based index at which the item was added, or -1 to indicate the item was added at the end of the
    /// collection.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyAdded(T item, int index = -1)
    {
        Interlocked.Increment(ref _version);

        _itemsAddedCollection!.Clear();
        _itemsAddedCollection.Add(item);
        _itemsRemovedCollection!.Clear();
        _itemsChangedCollection!.Clear();
        _itemsChangedCollection.Add(item);

        _added!.OnNext([item]);
        _changed!.OnNext([item]);
        _currentItems!.OnNext(_internalList);

        var startIndex = index >= 0 ? index : _internalList.Count - 1;

        // Emit to unified change stream
        _changeSubject!.OnNext(new ChangeSet<T>(Change<T>.CreateAdd(item, startIndex)));

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, startIndex));
    }

    /// <summary>
    /// Notifies observers that a range of items has been added to the collection and updates tracking collections
    /// accordingly.
    /// </summary>
    /// <param name="items">The array of items that were added to the collection. Cannot be null.</param>
    /// <param name="index">The zero-based index at which the items were added, or -1 if the index is not specified.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyAddedRange(T[] items, int index = -1)
    {
        Interlocked.Increment(ref _version);

        UpdateTrackingCollection(_itemsAddedCollection!, items);
        _itemsRemovedCollection!.Clear();
        UpdateTrackingCollection(_itemsChangedCollection!, items);

        _added!.OnNext(items);
        _changed!.OnNext(items);
        _currentItems!.OnNext(_internalList);

        // Emit to unified change stream
        var startIndex = index >= 0 ? index : _internalList.Count - items.Length;
        var changes = new Change<T>[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            changes[i] = Change<T>.CreateAdd(items[i], startIndex + i);
        }

        _changeSubject!.OnNext(new ChangeSet<T>(changes));

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Notifies subscribers that an item has been removed from the collection at the specified index.
    /// </summary>
    /// <param name="item">The item that was removed from the collection.</param>
    /// <param name="index">The zero-based index at which the item was removed.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyRemoved(T item, int index)
    {
        Interlocked.Increment(ref _version);

        _itemsRemovedCollection!.Clear();
        _itemsRemovedCollection.Add(item);
        _itemsAddedCollection!.Clear();
        _itemsChangedCollection!.Clear();
        _itemsChangedCollection.Add(item);

        _removed!.OnNext([item]);
        _changed!.OnNext([item]);
        _currentItems!.OnNext(_internalList);

        // Emit to unified change stream
        _changeSubject!.OnNext(new ChangeSet<T>(Change<T>.CreateRemove(item, index)));

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
    }

    /// <summary>
    /// Notifies observers that a range of items has been removed from the collection.
    /// </summary>
    /// <remarks>This method updates internal tracking collections and raises collection change notifications
    /// to observers. It should be called after items have been removed to ensure that all observers receive the
    /// appropriate notifications.</remarks>
    /// <param name="items">The array of items that were removed from the collection. Cannot be null.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyRemovedRange(T[] items)
    {
        Interlocked.Increment(ref _version);

        UpdateTrackingCollection(_itemsRemovedCollection!, items);
        _itemsAddedCollection!.Clear();
        UpdateTrackingCollection(_itemsChangedCollection!, items);

        _removed!.OnNext(items);
        _changed!.OnNext(items);
        _currentItems!.OnNext(_internalList);

        // Emit to unified change stream
        var changes = new Change<T>[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            changes[i] = Change<T>.CreateRemove(items[i], -1);
        }

        _changeSubject!.OnNext(new ChangeSet<T>(changes));

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Notifies subscribers that the collection has been cleared and provides the items that were removed.
    /// </summary>
    /// <remarks>This method raises collection change notifications and updates internal tracking collections
    /// to reflect the cleared state. It should be called after the collection is cleared to ensure that observers
    /// receive accurate updates.</remarks>
    /// <param name="clearedItems">An array containing the items that were removed from the collection when it was cleared. Cannot be null.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyCleared(T[] clearedItems)
    {
        Interlocked.Increment(ref _version);

        _itemsAddedCollection!.Clear();
        UpdateTrackingCollection(_itemsChangedCollection!, clearedItems);
        UpdateTrackingCollection(_itemsRemovedCollection!, clearedItems);

        _removed!.OnNext(clearedItems);
        _changed!.OnNext(clearedItems);
        _currentItems!.OnNext(_internalList);

        // Emit to unified change stream - single Clear change
        _changeSubject!.OnNext(new ChangeSet<T>(new Change<T>(ChangeReason.Clear, default!)));

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Notifies observers that a single item has changed by updating the change collection and publishing the change
    /// event.
    /// </summary>
    /// <remarks>This method is intended for scenarios where only one item has changed and should be
    /// communicated as such to subscribers. It clears any previous change notifications before reporting the new
    /// change.</remarks>
    /// <param name="item">The item that has changed and should be reported to observers.</param>
    /// <param name="reason">The reason for the change. Defaults to Refresh.</param>
    /// <param name="currentIndex">The current index, if applicable.</param>
    /// <param name="previousIndex">The previous index, if applicable (for moves).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyChangedSingle(T item, ChangeReason reason = ChangeReason.Refresh, int currentIndex = -1, int previousIndex = -1)
    {
        Interlocked.Increment(ref _version);

        _itemsChangedCollection!.Clear();
        _itemsChangedCollection.Add(item);

        _changed!.OnNext([item]);
        _currentItems!.OnNext(_internalList);

        // Emit to unified change stream
        _changeSubject!.OnNext(new ChangeSet<T>(new Change<T>(reason, item, currentIndex: currentIndex, previousIndex: previousIndex)));
    }

    /// <summary>
    /// Clears all tracked item history, removing records of added, changed, and removed items.
    /// </summary>
    /// <remarks>Call this method to reset the internal collections that track item changes. After calling
    /// this method, any previous history of item additions, modifications, or removals will be lost.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearHistory()
    {
        _itemsAddedCollection!.Clear();
        _itemsChangedCollection!.Clear();
        _itemsRemovedCollection!.Clear();
    }
}
