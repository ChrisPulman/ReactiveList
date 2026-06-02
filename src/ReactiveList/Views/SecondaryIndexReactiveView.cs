// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Internal;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;

namespace CP.Reactive.Views;

/// <summary>
/// Provides a reactive view over a <see cref="QuaternaryDictionary{TKey, TValue}"/> filtered by a secondary index key.
/// The view automatically updates when the source dictionary changes.
/// </summary>
/// <typeparam name="TKey">The type of primary keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
/// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
public sealed class SecondaryIndexReactiveView<TKey, TValue, TIndexKey> : IReadOnlyList<TValue>, INotifyCollectionChanged, INotifyPropertyChanged, IReactiveView<SecondaryIndexReactiveView<TKey, TValue, TIndexKey>, TValue>, IDisposable
where TKey : notnull
where TIndexKey : notnull
{
    private readonly QuaternaryDictionary<TKey, TValue> _source;
    private readonly string _indexName;
    private readonly TIndexKey _indexKey;
    private readonly ObservableCollection<TValue> _filteredItems;
    private readonly MultipleDisposable _disposables = new();
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SecondaryIndexReactiveView{TKey, TValue, TIndexKey}"/> class.
    /// </summary>
    /// <param name="source">The source dictionary to filter.</param>
    /// <param name="indexName">The name of the secondary index.</param>
    /// <param name="indexKey">The key value to filter on.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    public SecondaryIndexReactiveView(
        QuaternaryDictionary<TKey, TValue> source,
        string indexName,
        TIndexKey indexKey,
        ISequencer scheduler,
        TimeSpan throttle)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        _indexKey = indexKey;

        _filteredItems = [];
        Items = new ReadOnlyObservableCollection<TValue>(_filteredItems);

        // Initialize with current matching items
        RebuildView();

        // Subscribe to changes
        var subscription = _source.Stream
            .Throttle(throttle)
            .ObserveOn(scheduler)
            .Subscribe(OnSourceChanged);

        _disposables.Add(subscription);

        // Forward collection changed events
        _filteredItems.CollectionChanged += (s, e) => CollectionChanged?.Invoke(this, e);
    }

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the number of items in the filtered view.
    /// </summary>
    public int Count => _filteredItems.Count;

    /// <summary>
    /// Gets the underlying read-only observable collection for UI binding.
    /// </summary>
    public ReadOnlyObservableCollection<TValue> Items { get; }

    /// <summary>
    /// Gets the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to get.</param>
    /// <returns>The item at the specified index.</returns>
    public TValue this[int index] => _filteredItems[index];

    /// <inheritdoc/>
    public IEnumerator<TValue> GetEnumerator() => _filteredItems.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Forces a rebuild of the filtered view from the source.
    /// </summary>
    public void Refresh()
    {
        lock (_lock)
        {
            RebuildView();
        }
    }

    /// <summary>
    /// Assigns the current collection of items to a property using the specified setter action.
    /// </summary>
    /// <remarks>This method is typically used to bind the internal collection to an external property, such
    /// as a view model property, in a reactive UI pattern.</remarks>
    /// <param name="propertySetter">An action that sets a property to the current read-only observable collection of items. Cannot be null.</param>
    /// <returns>The current instance of <see cref="SecondaryIndexReactiveView{TKey, TValue, TIndexKey}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public SecondaryIndexReactiveView<TKey, TValue, TIndexKey> ToProperty(Action<ReadOnlyObservableCollection<TValue>> propertySetter)
    {
        CP.Reactive.Internal.ThrowHelper.ThrowIfNull(propertySetter);
        propertySetter(Items);
        return this;
    }

    /// <summary>
    /// Returns the current instance and provides a read-only observable collection of items contained in the view.
    /// </summary>
    /// <param name="collection">When this method returns, contains a read-only observable collection of items managed by this view.</param>
    /// <returns>The current <see cref="SecondaryIndexReactiveView{TKey, TValue, TIndexKey}"/> instance.</returns>
    public SecondaryIndexReactiveView<TKey, TValue, TIndexKey> ToProperty(out ReadOnlyObservableCollection<TValue> collection)
    {
        collection = Items;
        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnSourceChanged(CacheNotify<KeyValuePair<TKey, TValue>> notification)
    {
        lock (_lock)
        {
            switch (notification.Action)
            {
                case CacheAction.Added:
                    if (notification.Item.Value != null && _source.ValueMatchesSecondaryIndex(_indexName, notification.Item.Value, _indexKey))
                    {
                        _filteredItems.Add(notification.Item.Value);
                    }

                    break;

                case CacheAction.Removed:
                    if (notification.Item.Value != null)
                    {
                        _filteredItems.Remove(notification.Item.Value);
                    }

                    break;

                case CacheAction.Updated:
                    if (notification.Item.Value != null)
                    {
                        RebuildView();
                    }

                    break;

                case CacheAction.Cleared:
                    _filteredItems.Clear();
                    break;

                case CacheAction.BatchOperation:
                case CacheAction.Refreshed:
                    RebuildView();
                    break;
            }
        }

        OnPropertyChanged(nameof(Count));
    }

    private void RebuildView()
    {
        _filteredItems.Clear();
        foreach (var value in _source.GetValuesBySecondaryIndex(_indexName, _indexKey))
        {
            _filteredItems.Add(value);
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
#endif
