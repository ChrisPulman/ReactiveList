// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.Reactive.Views;

/// <summary>
/// Provides a reactive view over a <see cref="QuaternaryDictionary{TKey, TValue}"/> filtered by a secondary index key.
/// The view automatically updates when the source dictionary changes.
/// </summary>
/// <typeparam name="TKey">The type of primary keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
public sealed class SecondaryIndexReactiveView<TKey, TValue> : IReadOnlyList<TValue>, INotifyCollectionChanged, INotifyPropertyChanged, IReactiveView<SecondaryIndexReactiveView<TKey, TValue>, TValue>, IDisposable
where TKey : notnull
{
    private readonly QuaternaryDictionary<TKey, TValue> _source;

    private readonly string _indexName;

    private readonly object _indexKey;

    private readonly Func<QuaternaryDictionary<TKey, TValue>, string, object, IEnumerable<TValue>> _getValuesByIndex;

    private readonly Func<QuaternaryDictionary<TKey, TValue>, string, TValue, object, bool> _valueMatchesIndex;

    private readonly ObservableCollection<TValue> _filteredItems;

    private readonly MultipleDisposable _disposables = [];

    private readonly Lock _lock = new();

    /// <summary>Initializes a new instance of the <see cref="SecondaryIndexReactiveView{TKey, TValue}"/> class.</summary>
    /// <param name="source">The source dictionary to filter.</param>
    /// <param name="indexName">The name of the secondary index.</param>
    /// <param name="indexKey">The secondary index key (boxed) to filter on.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    /// <param name="getValuesByIndex">The delegate used to retrieve values for a boxed secondary index key.</param>
    /// <param name="valueMatchesIndex">The delegate used to test whether a value matches a boxed secondary index key.</param>
    private SecondaryIndexReactiveView(
        QuaternaryDictionary<TKey, TValue> source,
        string indexName,
        object indexKey,
        ISequencer scheduler,
        TimeSpan throttle,
        Func<QuaternaryDictionary<TKey, TValue>, string, object, IEnumerable<TValue>> getValuesByIndex,
        Func<QuaternaryDictionary<TKey, TValue>, string, TValue, object, bool> valueMatchesIndex)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        _indexKey = indexKey;
        _getValuesByIndex = getValuesByIndex ?? throw new ArgumentNullException(nameof(getValuesByIndex));
        _valueMatchesIndex = valueMatchesIndex ?? throw new ArgumentNullException(nameof(valueMatchesIndex));

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

    /// <summary>Gets the number of items in the filtered view.</summary>
    public int Count => _filteredItems.Count;

    /// <summary>Gets the underlying read-only observable collection for UI binding.</summary>
    public ReadOnlyObservableCollection<TValue> Items { get; }

    /// <summary>Gets the item at the specified index.</summary>
    /// <param name="index">The zero-based index of the item to get.</param>
    /// <returns>The item at the specified index.</returns>
    public TValue this[int index] => _filteredItems[index];

    /// <summary>Creates a typed instance of <see cref="SecondaryIndexReactiveView{TKey, TValue}"/> for a secondary index key.</summary>
    /// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
    /// <param name="source">The source dictionary to filter.</param>
    /// <param name="indexName">The name of the secondary index.</param>
    /// <param name="indexKey">The key value to filter on.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    /// <returns>A new <see cref="SecondaryIndexReactiveView{TKey, TValue}"/> bound to the specified index key.</returns>
    public static SecondaryIndexReactiveView<TKey, TValue> Create<TIndexKey>(
        QuaternaryDictionary<TKey, TValue> source,
        string indexName,
        TIndexKey indexKey,
        ISequencer scheduler,
        TimeSpan throttle)
        where TIndexKey : notnull
    {
        return new SecondaryIndexReactiveView<TKey, TValue>(
            source,
            indexName,
            indexKey!,
            scheduler,
            throttle,
            static (dict, name, key) => dict.GetValuesBySecondaryIndex(name, (TIndexKey)key),
            static (dict, name, value, key) => dict.ValueMatchesSecondaryIndex(name, value, (TIndexKey)key));
    }

    /// <inheritdoc/>
    public IEnumerator<TValue> GetEnumerator() => _filteredItems.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Forces a rebuild of the filtered view from the source.</summary>
    public void Refresh()
    {
        lock (_lock)
        {
            RebuildView();
        }
    }

    /// <summary>Assigns the current collection of items to a property using the specified setter action.</summary>
    /// <remarks>This method is typically used to bind the internal collection to an external property, such
    /// as a view model property, in a reactive UI pattern.</remarks>
    /// <param name="propertySetter">An action that sets a property to the current read-only observable collection of items. Cannot be null.</param>
    /// <returns>The current instance of <see cref="SecondaryIndexReactiveView{TKey, TValue}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public SecondaryIndexReactiveView<TKey, TValue> ToProperty(Action<ReadOnlyObservableCollection<TValue>> propertySetter)
    {
        ThrowHelper.ThrowIfNull(propertySetter);
        propertySetter(Items);
        return this;
    }

    /// <summary>Returns the current instance and provides a read-only observable collection of items contained in the view.</summary>
    /// <param name="collection">When this method returns, contains a read-only observable collection of items managed by this view.</param>
    /// <returns>The current <see cref="SecondaryIndexReactiveView{TKey, TValue}"/> instance.</returns>
    public SecondaryIndexReactiveView<TKey, TValue> ToProperty(out ReadOnlyObservableCollection<TValue> collection)
    {
        collection = Items;
        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
    }

    /// <summary>Handles source change notifications.</summary>
    /// <param name="notification">The notification value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnSourceChanged(CacheNotify<KeyValuePair<TKey, TValue>> notification)
    {
        lock (_lock)
        {
            switch (notification.Action)
            {
                case CacheAction.Added:
                    {
                        if (notification.Item.Value is not null && _valueMatchesIndex(_source, _indexName, notification.Item.Value, _indexKey))
                        {
                            _filteredItems.Add(notification.Item.Value);
                        }

                        break;
                    }

                case CacheAction.Removed:
                    {
                        if (notification.Item.Value is not null)
                        {
                            _filteredItems.Remove(notification.Item.Value);
                        }

                        break;
                    }

                case CacheAction.Updated:
                    {
                        if (notification.Item.Value is not null)
                        {
                            RebuildView();
                        }

                        break;
                    }

                case CacheAction.Cleared:
                    {
                        _filteredItems.Clear();
                        break;
                    }

                case CacheAction.BatchOperation or CacheAction.Refreshed:
                    {
                        RebuildView();
                        break;
                    }
            }
        }

        OnPropertyChanged(nameof(Count));
    }

    /// <summary>Rebuilds the view from the current source state.</summary>
    private void RebuildView()
    {
        _filteredItems.Clear();
        foreach (var value in _getValuesByIndex(_source, _indexName, _indexKey))
        {
            _filteredItems.Add(value);
        }
    }

    /// <summary>Handles property change notifications.</summary>
    /// <param name="propertyName">The propertyName value.</param>
    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
