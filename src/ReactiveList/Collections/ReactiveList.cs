// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Collections;
#else
namespace CP.Primitives.Collections;
#endif
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

    private static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new(nameof(Count));

    private static readonly PropertyChangedEventArgs ItemArrayPropertyChangedEventArgs = new(ItemArray);

    private static readonly NotifyCollectionChangedEventArgs ResetCollectionChangedEventArgs = new(NotifyCollectionChangedAction.Reset);

    private readonly List<T> _internalList = [];

    [NonSerialized]
    private Lock _lock = new();

    [NonSerialized]
    private IObservable<IEnumerable<T>>? _added;

    [NonSerialized]
    private IObservable<IEnumerable<T>>? _changed;

    [NonSerialized]
    private BehaviorSignal<IEnumerable<T>>? _currentItems;

    [NonSerialized]
    private IObservable<IEnumerable<T>>? _removed;

    [NonSerialized]
    private ReadOnlyObservableCollection<T>? _items;

    [NonSerialized]
    private ReadOnlyObservableCollection<T>? _itemsAdded;

    [NonSerialized]
    private RangeObservableCollection? _itemsAddedCollection;

    [NonSerialized]
    private ReadOnlyObservableCollection<T>? _itemsChanged;

    [NonSerialized]
    private RangeObservableCollection? _itemsChangedCollection;

    [NonSerialized]
    private ReadOnlyObservableCollection<T>? _itemsRemoved;

    [NonSerialized]
    private RangeObservableCollection? _itemsRemovedCollection;

    [NonSerialized]
    private RangeObservableCollection? _observableItems;

    [NonSerialized]
    private Signal<CacheNotify<T>>? _streamPipeline;

    [NonSerialized]
    private bool _disposed;

    [NonSerialized]
    private long _version;

    /// <summary>Initializes a new instance of the <see cref="ReactiveList{T}"/> class.</summary>
    public ReactiveList() => InitializeNonSerializedFields();

    /// <summary>Initializes a new instance of the <see cref="ReactiveList{T}"/> class.</summary>
    /// <param name="items">The items.</param>
    public ReactiveList(IEnumerable<T> items)
    {
        var itemArray = (items as T[]) ?? [.. items];
        if (itemArray.Length > 0)
        {
            _internalList.AddRange(itemArray);
        }

        InitializeNonSerializedFields();
        if (itemArray.Length == 0)
        {
            return;
        }

        _currentItems!.OnNext(_internalList);
    }

    /// <summary>Initializes a new instance of the <see cref="ReactiveList{T}"/> class.</summary>
    /// <param name="item">The item.</param>
    public ReactiveList(T item)
    {
        _internalList.Add(item);
        InitializeNonSerializedFields();
        _currentItems!.OnNext(_internalList);
    }

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the added during the last change as an Observable.</summary>
    /// <value>The added.</value>
    public IObservable<IEnumerable<T>> Added => _added ??= Stream
        .Keep(n => n.Action is CacheAction.Added or CacheAction.BatchAdded)
        .Map(GetItemsFromNotification);

    /// <summary>Gets the changed during the last change as an Observable.</summary>
    /// <value>The changed.</value>
    public IObservable<IEnumerable<T>> Changed => _changed ??= Stream.Map(GetItemsFromNotification);

    /// <summary>Gets the current items during the last change as an Observable.</summary>
    /// <value>
    /// The current items.
    /// </value>
    public IObservable<IEnumerable<T>> CurrentItems => _currentItems!;

    /// <summary>Gets the removed items during the last change as an Observable.</summary>
    /// <value>The removed.</value>
    public IObservable<IEnumerable<T>> Removed => _removed ??= Stream
        .Keep(n => n.Action is CacheAction.Removed or CacheAction.BatchRemoved or CacheAction.Cleared)
        .Map(GetItemsFromNotification);

    /// <inheritdoc/>
    public int Count => _internalList.Count;

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <inheritdoc/>
    public bool IsFixedSize => false;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public bool IsSynchronized => false;

    /// <summary>Gets the items.</summary>
    /// <value>The items.</value>
    public ReadOnlyObservableCollection<T> Items => _items ??= new(_observableItems!);

    /// <summary>Gets the items added during the last change.</summary>
    /// <value>The items added.</value>
    public ReadOnlyObservableCollection<T> ItemsAdded => _itemsAdded ??= new(_itemsAddedCollection!);

    /// <summary>Gets the items changed during the last change.</summary>
    /// <value>The items changed.</value>
    public ReadOnlyObservableCollection<T> ItemsChanged => _itemsChanged ??= new(_itemsChangedCollection!);

    /// <summary>Gets the items removed during the last change.</summary>
    /// <value>The items removed.</value>
    public ReadOnlyObservableCollection<T> ItemsRemoved => _itemsRemoved ??= new(_itemsRemovedCollection!);

    /// <summary>Gets an observable sequence that emits cache change notifications as they occur.</summary>
    /// <remarks>
    /// This is the primary observable for change notifications. It provides all change information
    /// including single item changes and batch operations. The Stream uses a channel-based pipeline
    /// for efficient, low-allocation event delivery.
    /// </remarks>
    public IObservable<CacheNotify<T>> Stream => _streamPipeline!.AsObservable();

    /// <summary>Gets the current version number of the collection, which is incremented on each modification.</summary>
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
        set => SetItem(index, (T)value!);
    }

    /// <inheritdoc/>
    public T this[int index]
    {
        get => _internalList[index];
        set => SetItem(index, value);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        lock (_lock)
        {
            _internalList.Add(item);
            _observableItems!.Add(item);
            NotifyAdded(item);
        }
    }

    /// <summary>Creates a snapshot of current items as an array.</summary>
    /// <returns>An array containing all current items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T[] ToArray()
    {
        lock (_lock)
        {
            return [.. _internalList];
        }
    }

    /// <summary>Adds a range of items from a <see cref="ReadOnlySpan{T}"/>.</summary>
    /// <param name="items">The items to add.</param>
    public void AddRange(ReadOnlySpan<T> items)
    {
        if (items.IsEmpty)
        {
            return;
        }

        var itemArray = items.ToArray();
        lock (_lock)
        {
            var requiredCapacity = _internalList.Count + itemArray.Length;
            if (_internalList.Capacity < requiredCapacity)
            {
                _internalList.Capacity = requiredCapacity;
            }

            _internalList.AddRange(itemArray);
            _observableItems!.AddRange(itemArray);
            NotifyAddedRange(itemArray);
        }
    }

    /// <summary>Copies items to the specified span.</summary>
    /// <param name="destination">The destination span.</param>
    /// <exception cref="ArgumentException">Thrown when destination is too small.</exception>
    public void CopyTo(Span<T> destination)
    {
        lock (_lock)
        {
            if (destination.Length < _internalList.Count)
            {
                throw new ArgumentException("Destination span is too small.", nameof(destination));
            }

#if NET6_0_OR_GREATER
            CollectionsMarshal.AsSpan(_internalList).CopyTo(destination);
#else
            _internalList.ToArray().AsSpan().CopyTo(destination);
#endif
        }
    }

    /// <summary>Gets a read-only span over the internal list for zero-copy access.</summary>
    /// <remarks>
    /// WARNING: This method does not acquire a lock. The caller must ensure thread safety.
    /// The returned span is only valid while no modifications are made to the list.
    /// </remarks>
    /// <returns>A read-only span over the internal items.</returns>
    public ReadOnlySpan<T> AsSpan()
    {
#if NET6_0_OR_GREATER
        return CollectionsMarshal.AsSpan(_internalList);
#else
        return new ReadOnlySpan<T>([.. _internalList]);
#endif
    }

    /// <summary>Gets a memory region over the internal list for async operations.</summary>
    /// <remarks>
    /// WARNING: This method does not acquire a lock. The caller must ensure thread safety.
    /// The returned memory is only valid while no modifications are made to the list.
    /// </remarks>
    /// <returns>A read-only memory region over the internal items.</returns>
    public ReadOnlyMemory<T> AsMemory() => _internalList.ToArray().AsMemory();

    /// <summary>Clears all items from the list without releasing the internal array capacity.</summary>
    /// <remarks>
    /// This method is more efficient than Clear() when you plan to add items back to the list,
    /// as it avoids the overhead of reallocating the internal array. The capacity is preserved.
    /// </remarks>
    /// <param name="notifyChange">Whether to emit change notifications. Defaults to true.</param>
    public void ClearWithoutDeallocation(bool notifyChange = true)
    {
        lock (_lock)
        {
            if (_internalList.Count == 0)
            {
                if (notifyChange)
                {
                    ClearHistory();
                }

                return;
            }

            var clearedItems = notifyChange ? [.. _internalList] : Array.Empty<T>();
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
        var itemArray = (items as T[]) ?? [.. items];
        if (itemArray.Length == 0)
        {
            return;
        }

        lock (_lock)
        {
#if NET6_0_OR_GREATER
            // Use AddRange with capacity hint for List
            _internalList.EnsureCapacity(_internalList.Count + itemArray.Length);
#endif
            _internalList.AddRange(itemArray);
            _observableItems!.AddRange(itemArray);

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
        lock (_lock)
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
        lock (_lock)
        {
            return _internalList.Contains(item);
        }
    }

    /// <inheritdoc/>
    public bool Contains(object? value)
    {
        if (!IsCompatibleObject(value))
        {
            return false;
        }

        return Contains((T)value!);
    }

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex)
    {
        lock (_lock)
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
        if (array is null)
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
                lock (_lock)
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

    /// <summary>Executes a batch edit operation on the list.</summary>
    /// <param name="editAction">The action to perform on the internal list.</param>
    public void Edit(Action<IEditableList<T>> editAction)
    {
        if (editAction is null)
        {
            throw new ArgumentNullException(nameof(editAction));
        }

        lock (_lock)
        {
            var snapshot = _internalList.ToArray();
            var wrapper = new EditableListWrapper<T>(_internalList);
            editAction(wrapper);

            _observableItems!.ReplaceAll(_internalList);

            var added = GetMultisetDifference(_internalList, snapshot);
            var removed = GetMultisetDifference(snapshot, _internalList);

            if (added.Length > 0)
            {
                NotifyAddedRange(added, notifyINPC: false);
            }

            if (removed.Length > 0)
            {
                NotifyRemovedRange(removed, notifyINPC: false);
            }

            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        lock (_lock)
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
        lock (_lock)
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
        lock (_lock)
        {
            _internalList.Insert(index, item);
            _observableItems?.Insert(index, item);
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

    /// <summary>Inserts the range.</summary>
    /// <param name="index">The index.</param>
    /// <param name="items">The items.</param>
    public void InsertRange(int index, IEnumerable<T> items)
    {
        var itemArray = (items as T[]) ?? [.. items];
        if (itemArray.Length == 0)
        {
            return;
        }

        lock (_lock)
        {
            _internalList.InsertRange(index, itemArray);
            _observableItems?.InsertRange(index, itemArray);

            NotifyAddedRange(itemArray, index);
        }
    }

    /// <summary>Moves an item from one index to another.</summary>
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

        lock (_lock)
        {
            var item = _internalList[oldIndex];
            _internalList.RemoveAt(oldIndex);
            _internalList.Insert(newIndex, item);
            _observableItems!.Move(oldIndex, newIndex);
            NotifyChangedSingle(item, ChangeReason.Move, newIndex, oldIndex);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(T item)
    {
        lock (_lock)
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
        var itemArray = (items as T[]) ?? [.. items];
        if (itemArray.Length == 0)
        {
            return;
        }

        lock (_lock)
        {
            var removed = new List<T>();
            foreach (var item in itemArray)
            {
                var index = _internalList.IndexOf(item);
                if (index >= 0)
                {
                    _internalList.RemoveAt(index);
                    removed.Add(item);
                }
            }

            if (removed.Count > 0)
            {
                _observableItems!.ReplaceAll(_internalList);
                NotifyRemovedRange([.. removed]);
            }
        }
    }

    /// <inheritdoc/>
    public void Remove(object? value)
    {
        if (!IsCompatibleObject(value))
        {
            return;
        }

        Remove((T)value!);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RemoveMany(Func<T, bool> predicate)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        lock (_lock)
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
                    _observableItems!.ReplaceAll(_internalList);

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
                    removed.Add(item);
                }
            }

            if (removed.Count > 0)
            {
                _observableItems!.ReplaceAll(_internalList);

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
        lock (_lock)
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

        lock (_lock)
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
            var removed = new T[count];
            _internalList.CopyTo(index, removed, 0, count);
            _internalList.RemoveRange(index, count);
            _observableItems!.RemoveRange(index, count);
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
        var itemArray = (items as T[]) ?? [.. items];

        lock (_lock)
        {
            var oldItems = _internalList.ToArray();
            _internalList.Clear();

#if NET6_0_OR_GREATER
            _internalList.EnsureCapacity(itemArray.Length);
#endif
            _internalList.AddRange(itemArray);
            _observableItems!.ReplaceAll(itemArray);

            Interlocked.Increment(ref _version);

            // For ReplaceAll, directly update tracking collections with expected semantics:
            // - ItemsAdded = new items
            // - ItemsRemoved = old items
            // - ItemsChanged = old items (the items that were replaced)
            UpdateTrackingCollection(_itemsAddedCollection!, itemArray);
            UpdateTrackingCollection(_itemsRemovedCollection!, oldItems);
            UpdateTrackingCollection(_itemsChangedCollection!, oldItems);
            _currentItems!.OnNext(_internalList);

            if (_streamPipeline?.HasObservers == true && oldItems.Length > 0)
            {
                _streamPipeline?.OnNext(new CacheNotify<T>(CacheAction.BatchRemoved, default, CreateBatch(oldItems)));
            }

            if (_streamPipeline?.HasObservers == true && itemArray.Length > 0)
            {
                _streamPipeline?.OnNext(new CacheNotify<T>(CacheAction.BatchAdded, default, CreateBatch(itemArray)));
            }

            if (oldItems.Length > 0 || itemArray.Length > 0)
            {
                RaiseCollectionReset();
            }

            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(ItemArray);
        }
    }

    /// <summary>Subscribes the specified observer to the CurrentItems.</summary>
    /// <param name="observer">The observer.</param>
    /// <returns>An IDisposable to release the subscription.</returns>
    public IDisposable Subscribe(IObserver<IEnumerable<T>> observer) => _currentItems!.Subscribe(observer);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(T item, T newValue)
    {
        lock (_lock)
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

    /// <summary>Disposes the specified disposables.</summary>
    /// <param name="disposing">if set to <c>true</c> [disposing].</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _disposed = true;
        _currentItems?.Dispose();
        _streamPipeline?.OnCompleted();
        _streamPipeline?.Dispose();
    }

    /// <summary>Raises a PropertyChanged event (per <see cref="INotifyPropertyChanged" />).</summary>
    /// <param name="propertyName">Name of the property.</param>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChangedEventArgs args;
        if (propertyName == nameof(Count))
        {
            args = CountPropertyChangedEventArgs;
        }
        else if (propertyName == ItemArray)
        {
            args = ItemArrayPropertyChangedEventArgs;
        }
        else
        {
            args = new PropertyChangedEventArgs(propertyName);
        }

        PropertyChanged?.Invoke(this, args);
    }

    /// <summary>Determines whether the specified object is compatible with the generic type parameter T.</summary>
    /// <param name="value">The object to test for compatibility with type T. May be null.</param>
    /// <returns>true if the object is of type T, or if both the object and the default value of T are null; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCompatibleObject(object? value) =>
        (value is T) || (value is null && default(T) is null);

    /// <summary>Replaces all items in the specified observable collection with the elements from the provided array.</summary>
    /// <remarks>The method clears the target collection before adding the new items. The order of items in
    /// the collection will match the order in the provided array.</remarks>
    /// <param name="target">The observable collection to update. All existing items in this collection will be removed and replaced.</param>
    /// <param name="items">The array of items to add to the collection. The collection will contain these items after the operation
    /// completes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateTrackingCollection(RangeObservableCollection target, T[] items) => target.ReplaceAll(items);

    /// <summary>Creates data for the CreateBatch operation.</summary>
    /// <param name="items">The items value.</param>
    /// <returns>A pooled batch wrapping a copy of the specified items.</returns>
    private static PooledBatch<T> CreateBatch(T[] items)
    {
        var batchItems = new T[items.Length];
        Array.Copy(items, batchItems, items.Length);
        return new PooledBatch<T>(batchItems, items.Length, ReturnToPool: false);
    }

    /// <summary>Gets data for the GetMultisetDifference operation.</summary>
    /// <param name="source">The source value.</param>
    /// <param name="subtract">The subtract value.</param>
    /// <returns>The source items after removing matching occurrences from the subtract list.</returns>
    private static T[] GetMultisetDifference(IReadOnlyList<T> source, IReadOnlyList<T> subtract)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var counts = new Dictionary<T, int>(subtract.Count);
        for (var i = 0; i < subtract.Count; i++)
        {
            var item = subtract[i];
            counts.TryGetValue(item, out var count);
            counts[item] = count + 1;
        }

        List<T>? difference = null;
        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            if (counts.TryGetValue(item, out var count) && count > 0)
            {
                counts[item] = count - 1;
                continue;
            }

            difference ??= [];
            difference.Add(item);
        }

        return difference is null || difference.Count == 0 ? [] : [.. difference];
    }

    /// <summary>Extracts items from a cache notification.</summary>
    /// <param name="notification">The cache notification.</param>
    /// <returns>The items from the notification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T[] GetItemsFromNotification(CacheNotify<T> notification)
    {
        if (notification.Batch is not null)
        {
            // Return a copy to avoid issues with pooled array disposal
            var batch = notification.Batch;
            var result = new T[batch.Count];
            Array.Copy(batch.Items, result, batch.Count);
            return result;
        }

        if (notification.Item is not null)
        {
            return [notification.Item];
        }

        return [];
    }

    /// <summary>Sets data for the SetItem operation.</summary>
    /// <param name="index">The index value.</param>
    /// <param name="value">The new value.</param>
    private void SetItem(int index, T value)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _internalList.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var oldValue = _internalList[index];
            _internalList[index] = value;
            _observableItems![index] = value;
            NotifyChangedSingle(value, ChangeReason.Update, index, index, previous: oldValue);
        }
    }

    /// <summary>Handles post-deserialization processing to restore the object's state after deserialization is complete.</summary>
    /// <remarks>This method is automatically invoked by the deserialization infrastructure after the object
    /// has been deserialized. It is used to reinitialize fields or properties that are not serialized.</remarks>
    /// <param name="context">The streaming context for the deserialization operation. Provides contextual information about the source or
    /// destination of the serialization stream.</param>
    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) => InitializeNonSerializedFields();

    /// <summary>Initializes fields that are not serialized during deserialization or object construction.</summary>
    /// <remarks>Call this method after deserialization to ensure that all non-serialized fields are properly
    /// initialized and the object is in a valid state. This method is intended for internal use and should not be
    /// called directly in normal application code.</remarks>
    private void InitializeNonSerializedFields()
    {
        _lock = new();
        _disposed = false;
        _observableItems = new(_internalList);
        _itemsAddedCollection = [];
        _itemsChangedCollection = [];
        _itemsRemovedCollection = [];
        _items = null;
        _itemsAdded = null;
        _itemsChanged = null;
        _itemsRemoved = null;
        _currentItems = new([]);
        _streamPipeline = new();
        _added = null;
        _removed = null;
        _changed = null;

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(ItemArray);
    }

    /// <summary>Raises collectionreset notifications.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RaiseCollectionReset()
    {
        CollectionChanged?.Invoke(this, ResetCollectionChangedEventArgs);
    }

    /// <summary>Raises collectionadded notifications.</summary>
    /// <param name="item">The item value.</param>
    /// <param name="index">The index value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RaiseCollectionAdded(T item, int index)
    {
        CollectionChanged?.Invoke(
            this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
    }

    /// <summary>Raises collectionremoved notifications.</summary>
    /// <param name="item">The item value.</param>
    /// <param name="index">The index value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RaiseCollectionRemoved(T item, int index)
    {
        CollectionChanged?.Invoke(
            this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
    }

    /// <summary>Raises collectionchanged notifications.</summary>
    /// <param name="item">The item value.</param>
    /// <param name="previous">The previous value.</param>
    /// <param name="index">The index value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RaiseCollectionChanged(T item, T? previous, int index)
    {
        CollectionChanged?.Invoke(
            this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, previous, index));
    }

    /// <summary>Raises collectionmoved notifications.</summary>
    /// <param name="item">The item value.</param>
    /// <param name="currentIndex">The currentIndex value.</param>
    /// <param name="previousIndex">The previousIndex value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RaiseCollectionMoved(T item, int currentIndex, int previousIndex)
    {
        CollectionChanged?.Invoke(
            this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, currentIndex, previousIndex));
    }

    /// <summary>Performs the TrackAdded operation.</summary>
    /// <param name="item">The item value.</param>
    /// <param name="index">The index value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrackAdded(T item, int index)
    {
        _itemsAddedCollection!.ReplaceWithSingle(item);
        _itemsRemovedCollection!.Clear();
        _itemsChangedCollection!.ReplaceWithSingle(item);
        _currentItems!.OnNext(_internalList);
        RaiseCollectionAdded(item, index);
    }

    /// <summary>Performs the TrackAddedRange operation.</summary>
    /// <param name="items">The items value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrackAddedRange(T[] items)
    {
        UpdateTrackingCollection(_itemsAddedCollection!, items);
        _itemsRemovedCollection!.Clear();
        UpdateTrackingCollection(_itemsChangedCollection!, items);
        _currentItems!.OnNext(_internalList);
        RaiseCollectionReset();
    }

    /// <summary>Performs the TrackRemoved operation.</summary>
    /// <param name="item">The item value.</param>
    /// <param name="index">The index value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrackRemoved(T item, int index)
    {
        _itemsRemovedCollection!.ReplaceWithSingle(item);
        _itemsAddedCollection!.Clear();
        _itemsChangedCollection!.ReplaceWithSingle(item);
        _currentItems!.OnNext(_internalList);
        RaiseCollectionRemoved(item, index);
    }

    /// <summary>Performs the TrackRemovedRange operation.</summary>
    /// <param name="items">The items value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrackRemovedRange(T[] items)
    {
        UpdateTrackingCollection(_itemsRemovedCollection!, items);
        _itemsAddedCollection!.Clear();
        UpdateTrackingCollection(_itemsChangedCollection!, items);
        _currentItems!.OnNext(_internalList);
        RaiseCollectionReset();
    }

    /// <summary>Performs the TrackChanged operation.</summary>
    /// <param name="item">The item value.</param>
    /// <param name="action">The action value.</param>
    /// <param name="currentIndex">The currentIndex value.</param>
    /// <param name="previousIndex">The previousIndex value.</param>
    /// <param name="previous">The previous value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrackChanged(T item, CacheAction action, int currentIndex, int previousIndex, T? previous)
    {
        _itemsChangedCollection!.ReplaceWithSingle(item);
        _currentItems!.OnNext(_internalList);
        if (action == CacheAction.Moved)
        {
            RaiseCollectionMoved(item, currentIndex, previousIndex);
        }
        else
        {
            RaiseCollectionChanged(item, previous, currentIndex);
        }
    }

    /// <summary>Notifies subscribers that an item has been added to the collection and raises the appropriate collection changed event.</summary>
    /// <param name="item">The item that was added to the collection.</param>
    /// <param name="index">The zero-based index at which the item was added, or -1 to indicate the item was added at the end of the
    /// collection.</param>
    /// <param name="notifyINPC">True to notify INotifyPropertyChanged subscribers; otherwise, false.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyAdded(T item, int index = -1, bool notifyINPC = true)
    {
        Interlocked.Increment(ref _version);
        var startIndex = index >= 0 ? index : _internalList.Count - 1;
        TrackAdded(item, startIndex);
        EmitStream(CacheAction.Added, item, currentIndex: startIndex);

        if (!notifyINPC)
        {
            return;
        }

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(ItemArray);
    }

    /// <summary>Notifies observers that a range of items has been added to the collection and updates tracking collections accordingly.</summary>
    /// <param name="items">The array of items that were added to the collection. Cannot be null.</param>
    /// <param name="index">The zero-based index at which the items were added, or -1 if the index is not specified.</param>
    /// <param name="notifyINPC">True to notify INotifyPropertyChanged subscribers; otherwise, false.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyAddedRange(T[] items, int index = -1, bool notifyINPC = true)
    {
        Interlocked.Increment(ref _version);
        var startIndex = index >= 0 ? index : _internalList.Count - items.Length;
        TrackAddedRange(items);
        if (_streamPipeline?.HasObservers == true)
        {
            EmitStream(CacheAction.BatchAdded, default, CreateBatch(items), startIndex);
        }

        if (!notifyINPC)
        {
            return;
        }

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(ItemArray);
    }

    /// <summary>Notifies subscribers that an item has been removed from the collection at the specified index.</summary>
    /// <param name="item">The item that was removed from the collection.</param>
    /// <param name="index">The zero-based index at which the item was removed.</param>
    /// <param name="notifyINPC">True to notify INotifyPropertyChanged subscribers; otherwise, false.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyRemoved(T item, int index, bool notifyINPC = true)
    {
        Interlocked.Increment(ref _version);
        TrackRemoved(item, index);
        EmitStream(CacheAction.Removed, item, currentIndex: index);

        if (!notifyINPC)
        {
            return;
        }

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(ItemArray);
    }

    /// <summary>Notifies observers that a range of items has been removed from the collection.</summary>
    /// <remarks>This method updates internal tracking collections and raises collection change notifications
    /// to observers. It should be called after items have been removed to ensure that all observers receive the
    /// appropriate notifications.</remarks>
    /// <param name="items">The array of items that were removed from the collection. Cannot be null.</param>
    /// <param name="notifyINPC">True to notify INotifyPropertyChanged subscribers; otherwise, false.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyRemovedRange(T[] items, bool notifyINPC = true)
    {
        Interlocked.Increment(ref _version);
        TrackRemovedRange(items);
        if (_streamPipeline?.HasObservers == true)
        {
            EmitStream(CacheAction.BatchRemoved, default, CreateBatch(items));
        }

        if (!notifyINPC)
        {
            return;
        }

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(ItemArray);
    }

    /// <summary>Notifies subscribers that the collection has been cleared and provides the items that were removed.</summary>
    /// <remarks>This method raises collection change notifications and updates internal tracking collections
    /// to reflect the cleared state. It should be called after the collection is cleared to ensure that observers
    /// receive accurate updates.</remarks>
    /// <param name="clearedItems">An array containing the items that were removed from the collection when it was cleared. Cannot be null.</param>
    /// <param name="notifyINPC">True to notify INotifyPropertyChanged subscribers; otherwise, false.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyCleared(T[] clearedItems, bool notifyINPC = true)
    {
        Interlocked.Increment(ref _version);
        TrackRemovedRange(clearedItems);
        if (_streamPipeline?.HasObservers == true)
        {
            if (clearedItems.Length > 0)
            {
                EmitStream(CacheAction.Cleared, default, CreateBatch(clearedItems));
            }
            else
            {
                EmitStream(CacheAction.Cleared, default);
            }
        }

        if (!notifyINPC)
        {
            return;
        }

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(ItemArray);
    }

    /// <summary>Notifies observers that a single item has changed by updating the change collection and publishing the change event.</summary>
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
        TrackChanged(item, cacheAction, currentIndex, previousIndex, previous);
        EmitStream(cacheAction, item, currentIndex: currentIndex, previousIndex: previousIndex, previous: previous);
        OnPropertyChanged(ItemArray);
    }

    /// <summary>Clears all tracked item history, removing records of added, changed, and removed items.</summary>
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

    /// <summary>Emits a cache event to the Stream pipeline.</summary>
    /// <param name="action">The cache action type.</param>
    /// <param name="item">The item associated with the action.</param>
    /// <param name="batch">An optional batch of items.</param>
    /// <param name="currentIndex">The current index of the item, or -1 if not applicable.</param>
    /// <param name="previousIndex">The previous index of the item, or -1 if not applicable.</param>
    /// <param name="previous">The previous item value (for update operations), or default if not applicable.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitStream(CacheAction action, T? item, PooledBatch<T>? batch = null, int currentIndex = -1, int previousIndex = -1, T? previous = default)
    {
        if (_streamPipeline?.HasObservers != true)
        {
            batch?.Dispose();
            return;
        }

        _streamPipeline.OnNext(new CacheNotify<T>(action, item, batch, currentIndex, previousIndex, previous));
    }

    /// <summary>Provides the RangeObservableCollection implementation.</summary>
    private sealed class RangeObservableCollection : ObservableCollection<T>
    {
        /// <summary>Initializes a new instance of the <see cref="RangeObservableCollection"/> class.</summary>
        public RangeObservableCollection()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="RangeObservableCollection"/> class.</summary>
        /// <param name="items">The items value.</param>
        public RangeObservableCollection(IEnumerable<T> items)
            : base([.. items])
        {
        }

        /// <summary>Adds data for the AddRange operation.</summary>
        /// <param name="items">The items value.</param>
        public void AddRange(T[] items)
        {
            if (items.Length == 0)
            {
                return;
            }

            CheckReentrancy();
            EnsureCapacity(Items.Count + items.Length);
            for (var i = 0; i < items.Length; i++)
            {
                Items.Add(items[i]);
            }

            RaiseReset();
        }

        /// <summary>Inserts data for the InsertRange operation.</summary>
        /// <param name="index">The index value.</param>
        /// <param name="items">The items value.</param>
        public void InsertRange(int index, T[] items)
        {
            if (items.Length == 0)
            {
                return;
            }

            CheckReentrancy();
            EnsureCapacity(Items.Count + items.Length);
            for (var i = 0; i < items.Length; i++)
            {
                Items.Insert(index + i, items[i]);
            }

            RaiseReset();
        }

        /// <summary>Removes data for the RemoveRange operation.</summary>
        /// <param name="index">The index value.</param>
        /// <param name="count">The count value.</param>
        public void RemoveRange(int index, int count)
        {
            if (count == 0)
            {
                return;
            }

            CheckReentrancy();
            for (var i = 0; i < count; i++)
            {
                Items.RemoveAt(index);
            }

            RaiseReset();
        }

        /// <summary>Performs the ReplaceAll operation.</summary>
        /// <param name="items">The items value.</param>
        public void ReplaceAll(IReadOnlyList<T> items)
        {
            CheckReentrancy();
            Items.Clear();
            EnsureCapacity(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                Items.Add(items[i]);
            }

            RaiseReset();
        }

        /// <summary>Performs the ReplaceWithSingle operation.</summary>
        /// <param name="item">The item value.</param>
        public void ReplaceWithSingle(T item)
        {
            CheckReentrancy();
            Items.Clear();
            Items.Add(item);
            RaiseReset();
        }

        /// <summary>Raises reset notifications.</summary>
        private void RaiseReset()
        {
            OnPropertyChanged(CountPropertyChangedEventArgs);
            OnPropertyChanged(ItemArrayPropertyChangedEventArgs);
            OnCollectionChanged(ResetCollectionChangedEventArgs);
        }

        /// <summary>Ensures state for the EnsureCapacity operation.</summary>
        /// <param name="capacity">The capacity value.</param>
        private void EnsureCapacity(int capacity)
        {
            if (Items is not List<T> list || list.Capacity >= capacity)
            {
                return;
            }

            list.Capacity = capacity;
        }
    }
}
