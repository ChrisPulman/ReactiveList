// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
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
using CP.Reactive.Core;
using CP.Reactive.Internal;

#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace CP.Reactive.Collections;

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
    private CompositeDisposable? _cleanUp;

    [NonSerialized]
    private IObservable<IEnumerable<T>>? _added;

    [NonSerialized]
    private IObservable<IEnumerable<T>>? _changed;

    [NonSerialized]
    private BehaviorSubject<IEnumerable<T>>? _currentItems;

    [NonSerialized]
    private IObservable<IEnumerable<T>>? _removed;

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
    private Subject<CacheNotify<T>>? _streamPipeline;

    [NonSerialized]
    private bool _skipInternalSubscription;

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
    /// Gets the added during the last change as an Observable.
    /// </summary>
    /// <value>The added.</value>
    public IObservable<IEnumerable<T>> Added => _added!;

    /// <summary>
    /// Gets the changed during the last change as an Observable.
    /// </summary>
    /// <value>The changed.</value>
    public IObservable<IEnumerable<T>> Changed => _changed!;

    /// <summary>
    /// Gets the current items during the last change as an Observable.
    /// </summary>
    /// <value>
    /// The current items.
    /// </value>
    public IObservable<IEnumerable<T>> CurrentItems => _currentItems!;

    /// <summary>
    /// Gets the removed items during the last change as an Observable.
    /// </summary>
    /// <value>The removed.</value>
    public IObservable<IEnumerable<T>> Removed => _removed!;

    /// <inheritdoc/>
    public int Count => _internalList.Count;

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
    /// Gets an observable sequence that emits cache change notifications as they occur.
    /// </summary>
    /// <remarks>
    /// This is the primary observable for change notifications. It provides all change information
    /// including single item changes and batch operations. The Stream uses a channel-based pipeline
    /// for efficient, low-allocation event delivery.
    /// </remarks>
    public IObservable<CacheNotify<T>> Stream => _streamPipeline!.AsObservable();

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
        }
    }

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
    public ReadOnlySpan<T> AsSpan() => CollectionsMarshal.AsSpan(_internalList);

    /// <summary>
    /// Gets a memory region over the internal list for async operations.
    /// </summary>
    /// <remarks>
    /// WARNING: This method does not acquire a lock. The caller must ensure thread safety.
    /// The returned memory is only valid while no modifications are made to the list.
    /// </remarks>
    /// <returns>A read-only memory region over the internal items.</returns>
    public ReadOnlyMemory<T> AsMemory() => _internalList.ToArray().AsMemory();

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
                return;
            }

            var clearedItems = _internalList.ToArray();
            _internalList.Clear();
            _observableItems!.Clear();
            NotifyCleared(clearedItems);
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
                NotifyAddedRange([.. added], notifyINPC: false);
            }

            if (removed.Count > 0)
            {
                NotifyRemovedRange([.. removed], notifyINPC: false);
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
#else
            var removed = new T[count];
            for (var i = 0; i < count; i++)
            {
                removed[i] = _internalList[index];
                _internalList.RemoveAt(index);
                _observableItems!.RemoveAt(index);
            }
#endif

            NotifyRemovedRange(removed);
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

            Interlocked.Increment(ref _version);

            // For ReplaceAll, directly update tracking collections with expected semantics:
            // - ItemsAdded = new items
            // - ItemsRemoved = old items
            // - ItemsChanged = old items (the items that were replaced)
            UpdateTrackingCollection(_itemsAddedCollection!, itemArray);
            UpdateTrackingCollection(_itemsRemovedCollection!, oldItems);
            UpdateTrackingCollection(_itemsChangedCollection!, oldItems);
            _currentItems!.OnNext(_internalList);

            // Set flag to skip internal subscription processing for the following events
            _skipInternalSubscription = true;

            // Emit events to Stream for external subscribers
            if (oldItems.Length > 0)
            {
                var removedPool = ArrayPool<T>.Shared.Rent(oldItems.Length);
                Array.Copy(oldItems, removedPool, oldItems.Length);
                _streamPipeline?.OnNext(new CacheNotify<T>(CacheAction.BatchRemoved, default, new PooledBatch<T>(removedPool, oldItems.Length)));
            }

            // Keep flag set for the second event
            _skipInternalSubscription = true;

            if (itemArray.Length > 0)
            {
                var addedPool = ArrayPool<T>.Shared.Rent(itemArray.Length);
                Array.Copy(itemArray, addedPool, itemArray.Length);
                _streamPipeline?.OnNext(new CacheNotify<T>(CacheAction.BatchAdded, default, new PooledBatch<T>(addedPool, itemArray.Length)));
            }

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
                var oldValue = _internalList[index];
                _internalList[index] = newValue;
                _observableItems![index] = newValue;
                NotifyChangedSingle(newValue, ChangeReason.Update, index, index, previous: oldValue);
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
            _currentItems?.Dispose();
            _streamPipeline?.OnCompleted();
            _streamPipeline?.Dispose();
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
    /// Extracts items from a cache notification.
    /// </summary>
    /// <param name="notification">The cache notification.</param>
    /// <returns>The items from the notification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T[] GetItemsFromNotification(CacheNotify<T> notification)
    {
        if (notification.Batch != null)
        {
            // Return a copy to avoid issues with pooled array disposal
            var batch = notification.Batch;
            var result = new T[batch.Count];
            Array.Copy(batch.Items, result, batch.Count);
            return result;
        }

        if (notification.Item != null)
        {
            return [notification.Item];
        }

        return Array.Empty<T>();
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
        _currentItems = new(Array.Empty<T>());
        _streamPipeline = new();

        // Create a shared observable from the stream pipeline
        var sharedStream = _streamPipeline.Publish().RefCount();

        // Derive Added observable from Stream
        _added = sharedStream
            .Where(n => n.Action == CacheAction.Added || n.Action == CacheAction.BatchAdded)
            .Select(GetItemsFromNotification);

        // Derive Removed observable from Stream
        _removed = sharedStream
            .Where(n => n.Action == CacheAction.Removed || n.Action == CacheAction.BatchRemoved || n.Action == CacheAction.Cleared)
            .Select(GetItemsFromNotification);

        // Derive Changed observable from Stream (all actions represent changes)
        _changed = sharedStream
            .Select(GetItemsFromNotification);

        // Internal subscription to update tracking collections, CurrentItems, and raise events
        var internalSubscription = _streamPipeline
            .Subscribe(notification =>
            {
                // Skip if tracking collections were already updated directly (e.g., ReplaceAll)
                if (_skipInternalSubscription)
                {
                    _skipInternalSubscription = false;
                    RaiseCollectionChanged(notification);
                    return;
                }

                // Update tracking collections based on action type
                var items = GetItemsFromNotification(notification);
                var itemsArray = items as T[] ?? items.ToArray();

                switch (notification.Action)
                {
                    case CacheAction.Added:
                    case CacheAction.BatchAdded:
                        UpdateTrackingCollection(_itemsAddedCollection!, itemsArray);
                        _itemsRemovedCollection!.Clear();
                        UpdateTrackingCollection(_itemsChangedCollection!, itemsArray);
                        break;

                    case CacheAction.Removed:
                    case CacheAction.BatchRemoved:
                    case CacheAction.Cleared:
                        UpdateTrackingCollection(_itemsRemovedCollection!, itemsArray);
                        _itemsAddedCollection!.Clear();
                        UpdateTrackingCollection(_itemsChangedCollection!, itemsArray);
                        break;

                    case CacheAction.Updated:
                    case CacheAction.Moved:
                    case CacheAction.Refreshed:
                        // For update/move/refresh, only update ItemsChanged - leave ItemsAdded/ItemsRemoved unchanged
                        UpdateTrackingCollection(_itemsChangedCollection!, itemsArray);
                        break;
                }

                // Always update CurrentItems
                _currentItems!.OnNext(_internalList);

                // Raise CollectionChanged event
                RaiseCollectionChanged(notification);
            });

        _cleanUp.Add(internalSubscription);
    }

    /// <summary>
    /// Raises the CollectionChanged event based on the cache notification.
    /// </summary>
    /// <param name="notification">The cache notification.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RaiseCollectionChanged(CacheNotify<T> notification)
    {
        if (CollectionChanged == null)
        {
            return;
        }

        var args = notification.Action switch
        {
            CacheAction.Added => new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add,
                notification.Item,
                notification.CurrentIndex),
            CacheAction.Removed => new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove,
                notification.Item,
                notification.CurrentIndex),
            CacheAction.Moved => new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Move,
                notification.Item,
                notification.CurrentIndex,
                notification.PreviousIndex),
            _ => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)
        };

        CollectionChanged.Invoke(this, args);
    }

    /// <summary>
    /// Notifies subscribers that an item has been added to the collection and raises the appropriate collection changed
    /// event.
    /// </summary>
    /// <param name="item">The item that was added to the collection.</param>
    /// <param name="index">The zero-based index at which the item was added, or -1 to indicate the item was added at the end of the
    /// collection.</param>
    /// <param name="notifyINPC">True to notify INotifyPropertyChanged subscribers; otherwise, false.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyAdded(T item, int index = -1, bool notifyINPC = true)
    {
        Interlocked.Increment(ref _version);
        var startIndex = index >= 0 ? index : _internalList.Count - 1;
        EmitStream(CacheAction.Added, item, currentIndex: startIndex);

        if (notifyINPC)
        {
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <summary>
    /// Notifies observers that a range of items has been added to the collection and updates tracking collections
    /// accordingly.
    /// </summary>
    /// <param name="items">The array of items that were added to the collection. Cannot be null.</param>
    /// <param name="index">The zero-based index at which the items were added, or -1 if the index is not specified.</param>
    /// <param name="notifyINPC">True to notify INotifyPropertyChanged subscribers; otherwise, false.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyAddedRange(T[] items, int index = -1, bool notifyINPC = true)
    {
        Interlocked.Increment(ref _version);
        var startIndex = index >= 0 ? index : _internalList.Count - items.Length;
        var pool = ArrayPool<T>.Shared.Rent(items.Length);
        Array.Copy(items, pool, items.Length);
        EmitStream(CacheAction.BatchAdded, default, new PooledBatch<T>(pool, items.Length), startIndex);

        if (notifyINPC)
        {
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <summary>
    /// Notifies subscribers that an item has been removed from the collection at the specified index.
    /// </summary>
    /// <param name="item">The item that was removed from the collection.</param>
    /// <param name="index">The zero-based index at which the item was removed.</param>
    /// <param name="notifyINPC">True to notify INotifyPropertyChanged subscribers; otherwise, false.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyRemoved(T item, int index, bool notifyINPC = true)
    {
        Interlocked.Increment(ref _version);
        EmitStream(CacheAction.Removed, item, currentIndex: index);

        if (notifyINPC)
        {
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <summary>
    /// Notifies observers that a range of items has been removed from the collection.
    /// </summary>
    /// <remarks>This method updates internal tracking collections and raises collection change notifications
    /// to observers. It should be called after items have been removed to ensure that all observers receive the
    /// appropriate notifications.</remarks>
    /// <param name="items">The array of items that were removed from the collection. Cannot be null.</param>
    /// <param name="notifyINPC">True to notify INotifyPropertyChanged subscribers; otherwise, false.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyRemovedRange(T[] items, bool notifyINPC = true)
    {
        Interlocked.Increment(ref _version);
        var pool = ArrayPool<T>.Shared.Rent(items.Length);
        Array.Copy(items, pool, items.Length);
        EmitStream(CacheAction.BatchRemoved, default, new PooledBatch<T>(pool, items.Length));

        if (notifyINPC)
        {
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <summary>
    /// Notifies subscribers that the collection has been cleared and provides the items that were removed.
    /// </summary>
    /// <remarks>This method raises collection change notifications and updates internal tracking collections
    /// to reflect the cleared state. It should be called after the collection is cleared to ensure that observers
    /// receive accurate updates.</remarks>
    /// <param name="clearedItems">An array containing the items that were removed from the collection when it was cleared. Cannot be null.</param>
    /// <param name="notifyINPC">True to notify INotifyPropertyChanged subscribers; otherwise, false.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyCleared(T[] clearedItems, bool notifyINPC = true)
    {
        Interlocked.Increment(ref _version);
        if (clearedItems.Length > 0)
        {
            var pool = ArrayPool<T>.Shared.Rent(clearedItems.Length);
            Array.Copy(clearedItems, pool, clearedItems.Length);
            EmitStream(CacheAction.Cleared, default, new PooledBatch<T>(pool, clearedItems.Length));
        }
        else
        {
            EmitStream(CacheAction.Cleared, default);
        }

        if (notifyINPC)
        {
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
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
    /// <param name="previous">The previous item value (for update operations), or default if not applicable.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyChangedSingle(T item, ChangeReason reason = ChangeReason.Refresh, int currentIndex = -1, int previousIndex = -1, T? previous = default)
    {
        Interlocked.Increment(ref _version);
        var cacheAction = reason switch
        {
            ChangeReason.Update => CacheAction.Updated,
            ChangeReason.Move => CacheAction.Moved,
            ChangeReason.Refresh => CacheAction.Refreshed,
            _ => CacheAction.Updated
        };
        EmitStream(cacheAction, item, currentIndex: currentIndex, previousIndex: previousIndex, previous: previous);
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
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(ItemArray);
    }

    /// <summary>
    /// Emits a cache event to the Stream pipeline.
    /// </summary>
    /// <param name="action">The cache action type.</param>
    /// <param name="item">The item associated with the action.</param>
    /// <param name="batch">An optional batch of items.</param>
    /// <param name="currentIndex">The current index of the item, or -1 if not applicable.</param>
    /// <param name="previousIndex">The previous index of the item, or -1 if not applicable.</param>
    /// <param name="previous">The previous item value (for update operations), or default if not applicable.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitStream(CacheAction action, T? item, PooledBatch<T>? batch = null, int currentIndex = -1, int previousIndex = -1, T? previous = default) =>

        // Always emit to pipeline - internal subscription needs events for tracking collections
        _streamPipeline?.OnNext(new CacheNotify<T>(action, item, batch, currentIndex, previousIndex, previous));
}
