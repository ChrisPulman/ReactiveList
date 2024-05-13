// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;

namespace CP.Reactive;

/// <summary>
/// Rx List.
/// </summary>
/// <typeparam name="T">The Type.</typeparam>
public class ReactiveList<T> : IReactiveList<T>
    where T : notnull
{
    private readonly ReplaySubject<IEnumerable<T>> _added = new(1);
    private readonly ReplaySubject<IEnumerable<T>> _changed = new(1);
    private readonly ReplaySubject<IEnumerable<T>> _removed = new(1);
    private readonly ReplaySubject<IEnumerable<T>> _currentItems = new(1);
    private readonly ReadOnlyObservableCollection<T> _items;
    private readonly ObservableCollection<T> _itemsAddedoc = [];
    private readonly ObservableCollection<T> _itemsChangedoc = [];
    private readonly ObservableCollection<T> _itemsRemovedoc = [];
    private readonly CompositeDisposable _cleanUp = [];
    private readonly SourceList<T> _sourceList = new();
    private readonly object _lock = new();
    private bool _cleared;
    private bool _addedRange;
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
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, v));
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

                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, v.ToList()));
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

    /// <summary>
    /// Gets the added.
    /// </summary>
    /// <value>The added.</value>
    public IObservable<IEnumerable<T>> Added => _added;

    /// <summary>
    /// Gets the changed.
    /// </summary>
    /// <value>The changed.</value>
    public IObservable<IEnumerable<T>> Changed => _changed;

    /// <summary>
    /// Gets the current items.
    /// </summary>
    /// <value>
    /// The current items.
    /// </value>
    public IObservable<IEnumerable<T>> CurrentItems => _currentItems;

    /// <summary>
    /// Gets a value indicating whether gets a value that indicates whether the object is disposed.
    /// </summary>
    public bool IsDisposed => _cleanUp.IsDisposed;

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
    /// Gets the removed.
    /// </summary>
    /// <value>The removed.</value>
    public IObservable<IEnumerable<T>> Removed => _removed;

    /// <summary>
    /// Gets the count.
    /// </summary>
    /// <value>
    /// The count.
    /// </value>
    public int Count => _items.Count;

    /// <summary>
    /// Gets a value indicating whether this instance is read only.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is read only; otherwise, <c>false</c>.
    /// </value>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the value of T at the specified index.
    /// </summary>
    /// <value>
    /// The value of T.
    /// </value>
    /// <param name="index">The index.</param>
    /// <returns>An instance of T.</returns>
    public T this[int index]
    {
        get => _items[index];
        set => Insert(index, value);
    }

    /// <summary>
    /// Adds the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Add(T item)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.Add(item));
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
        }
    }

    /// <summary>
    /// Clears this instance.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            ClearHistoryIfCountIsZero();
            _sourceList.Edit(l => l.Clear());
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
        }
    }

    /// <summary>
    /// Updates the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="newValue">The new value.</param>
    public void Update(T item, T newValue) => _sourceList.Edit(l => l.Replace(item, newValue));

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
        }
    }

    /// <summary>
    /// Removes at.
    /// </summary>
    /// <param name="index">The index.</param>
    public void RemoveAt(int index)
    {
        lock (_lock)
        {
            _sourceList.Edit(l => l.RemoveAt(index));
        }
    }

    /// <summary>
    /// Copies to.
    /// </summary>
    /// <param name="array">The array.</param>
    /// <param name="arrayIndex">Index of the array.</param>
    public void CopyTo(T[] array, int arrayIndex) => ((ICollection<T>)_items).CopyTo(array, arrayIndex);

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
