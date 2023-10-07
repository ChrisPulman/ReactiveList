// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
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
    private readonly ObservableCollection<T> _itemsAddedoc = new();
    private readonly ObservableCollection<T> _itemsChangedoc = new();
    private readonly ObservableCollection<T> _itemsRemovedoc = new();
    private readonly CompositeDisposable _cleanUp = new();
    private readonly SourceList<T> _sourceList = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveList{T}"/> class.
    /// </summary>
    public ReactiveList()
    {
        _items = new(new());
        ItemsAdded = new(_itemsAddedoc);
        ItemsRemoved = new(_itemsRemovedoc);
        ItemsChanged = new(_itemsChangedoc);
        var srcList = _sourceList.Connect();
        _cleanUp = new()
        {
            _sourceList,
            _added,
            _removed,
            _changed,
            _currentItems,
            srcList
                .ObserveOnUIWhenNotTesting()
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
                .ObserveOnUIWhenNotTesting()
                .Subscribe(v =>
                {
                    _itemsAddedoc.Clear();
                    _itemsAddedoc.Add(v);
                    _itemsRemovedoc.Clear();
                }),

            srcList
                .WhereReasonsAre(ListChangeReason.AddRange)
                .SelectMany(t => t.Select(v => v.Range))
                .Do(_added.OnNext)
                .ObserveOnUIWhenNotTesting()
                .Subscribe(v =>
                {
                    _itemsAddedoc.Clear();
                    _itemsAddedoc.Add(v);
                    _itemsRemovedoc.Clear();
                }),

            srcList
                .WhereReasonsAre(ListChangeReason.Remove)
                .Select(t => t.Select(v => v.Item.Current))
                .Do(_removed.OnNext)
                .ObserveOnUIWhenNotTesting()
                .Subscribe(v =>
                {
                    _itemsRemovedoc.Clear();
                    _itemsRemovedoc.Add(v);
                    _itemsAddedoc.Clear();
                }),

            srcList
                .WhereReasonsAre(ListChangeReason.RemoveRange)
                .SelectMany(t => t.Select(v => v.Range))
                .Do(_removed.OnNext)
                .ObserveOnUIWhenNotTesting()
                .Subscribe(v =>
                {
                    _itemsRemovedoc.Add(v);
                    _itemsAddedoc.Clear();
                }),

            srcList
                .WhereReasonsAre(ListChangeReason.Add, ListChangeReason.Remove, ListChangeReason.Replace)
                .Select(t => t.Select(v => v.Item.Current))
                .Do(_changed.OnNext)
                .ObserveOnUIWhenNotTesting()
                .Subscribe(v =>
                {
                    _itemsChangedoc.Clear();
                    _itemsChangedoc.Add(v);
                }),

            srcList
                .WhereReasonsAre(ListChangeReason.RemoveRange, ListChangeReason.AddRange)
                .SelectMany(t => t.Select(v => v.Range))
                .Do(_changed.OnNext)
                .ObserveOnUIWhenNotTesting()
                .Subscribe(v =>
                {
                    _itemsChangedoc.Clear();
                    _itemsChangedoc.Add(v);
                }),

            srcList
                .WhereReasonsAre(ListChangeReason.Clear)
                .SelectMany(t => t.Select(v => v.Range))
                .ObserveOnUIWhenNotTesting()
                .Subscribe(v =>
                {
                    _itemsAddedoc.Clear();
                    _itemsChangedoc.Clear();
                    _itemsChangedoc.Add(v);
                    _itemsRemovedoc.Clear();
                    _itemsRemovedoc.Add(v);
                })
        };
    }

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
    /// Adds the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Add(T item) => _sourceList.Edit(l => l.Add(item));

    /// <summary>
    /// Adds the specified items.
    /// </summary>
    /// <param name="items">The items.</param>
    public void AddRange(IEnumerable<T> items) => _sourceList.Edit(l => l.AddRange(items));

    /// <summary>
    /// Clears this instance.
    /// </summary>
    public void Clear()
    {
        ClearHistoryIfCountIsZero();
        _sourceList.Edit(l => l.Clear());
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
    /// Removes the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Remove(T item) => _sourceList.Edit(l => l.Remove(item));

    /// <summary>
    /// Removes the specified items.
    /// </summary>
    /// <param name="items">The items.</param>
    public void Remove(IEnumerable<T> items) => _sourceList.Edit(l => l.Remove(items));

    /// <summary>
    /// Removes the range.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="count">The count.</param>
    public void RemoveRange(int index, int count) => _sourceList.Edit(l => l.RemoveRange(index, count));

    /// <summary>
    /// Updates the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="newValue">The new value.</param>
    public void Update(T item, T newValue) => _sourceList.Edit(l => l.Replace(item, newValue));

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
