// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Views;
#else
namespace CP.Primitives.Views;
#endif
/// <summary>
/// Provides a reactive view over a <see cref="QuaternaryDictionary{TKey, TValue}"/> filtered by secondary index keys
/// that can change dynamically. The view rebuilds when the key observable emits new keys.
/// Returns KeyValuePairs to support dictionary iteration patterns.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
public sealed class DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue> :
    IReadOnlyList<KeyValuePair<TKey, TValue>>,
    INotifyCollectionChanged,
    INotifyPropertyChanged,
    IReactiveView<DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue>, KeyValuePair<TKey, TValue>>
where TKey : notnull
{
    /// <summary>The indexed dictionary whose changes feed this view.</summary>
    private readonly QuaternaryDictionary<TKey, TValue> _source;

    /// <summary>The name of the secondary index queried by this view.</summary>
    private readonly string _indexName;

    /// <summary>Retrieves values for a boxed secondary-index key.</summary>
    private readonly Func<QuaternaryDictionary<TKey, TValue>, string, object, IEnumerable<TValue>> _getValuesByIndex;

    /// <summary>Validates current membership when an indexed value is mutable.</summary>
    private readonly Func<QuaternaryDictionary<TKey, TValue>, string, TValue, object, bool> _valueMatchesIndex;

    /// <summary>The mutable collection backing the public read-only view.</summary>
    private readonly ObservableCollection<KeyValuePair<TKey, TValue>> _filteredItems;

    /// <summary>The subscriptions owned by this view.</summary>
    private readonly MultipleDisposable _disposables = [];

    /// <summary>Serializes key changes and collection updates.</summary>
    private readonly Lock _lock = new();

    /// <summary>The secondary-index keys currently included in the view.</summary>
    private HashSet<object> _currentKeys = [];

    /// <summary>Indicates that reconciliation is delivering collection notifications.</summary>
    private bool _rebuilding;

    /// <summary>Defers reentrant requests until the current reconciliation completes.</summary>
    private bool _rebuildRequested;

    /// <summary>Initializes a new instance of the <see cref="DynamicSecondaryIndexDictionaryReactiveView{TKey, TValue}"/> class.</summary>
    /// <param name="source">The source dictionary to filter.</param>
    /// <param name="indexName">The name of the secondary index.</param>
    /// <param name="getValuesByIndex">The delegate used to retrieve values for a boxed secondary index key.</param>
    /// <param name="valueMatchesIndex">The delegate used to validate current secondary-index membership.</param>
    private DynamicSecondaryIndexDictionaryReactiveView(
        QuaternaryDictionary<TKey, TValue> source,
        string indexName,
        Func<QuaternaryDictionary<TKey, TValue>, string, object, IEnumerable<TValue>> getValuesByIndex,
        Func<QuaternaryDictionary<TKey, TValue>, string, TValue, object, bool> valueMatchesIndex)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        _getValuesByIndex = getValuesByIndex ?? throw new ArgumentNullException(nameof(getValuesByIndex));
        _valueMatchesIndex = valueMatchesIndex ?? throw new ArgumentNullException(nameof(valueMatchesIndex));

        _filteredItems = [];
        Items = new(_filteredItems);
    }

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the number of items in the filtered view.</summary>
    public int Count => _filteredItems.Count;

    /// <summary>Gets the underlying read-only observable collection for UI binding.</summary>
    public ReadOnlyObservableCollection<KeyValuePair<TKey, TValue>> Items { get; }

    /// <summary>Gets the item at the specified index.</summary>
    /// <param name="index">The zero-based index of the item to get.</param>
    /// <returns>The item at the specified index.</returns>
    public KeyValuePair<TKey, TValue> this[int index] => _filteredItems[index];

    /// <summary>Creates a typed instance of <see cref="DynamicSecondaryIndexDictionaryReactiveView{TKey, TValue}"/> for dynamic secondary index keys.</summary>
    /// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
    /// <param name="source">The source dictionary to filter.</param>
    /// <param name="indexName">The name of the secondary index.</param>
    /// <param name="keysObservable">An observable of key arrays to filter by.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The quiet period before reconciling indexed entries; zero dispatches without debounce.</param>
    /// <returns>A <see cref="DynamicSecondaryIndexDictionaryReactiveView{TKey, TValue}"/> instance.</returns>
    public static DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue> Create<TIndexKey>(
        QuaternaryDictionary<TKey, TValue> source,
        string indexName,
        IObservable<TIndexKey[]> keysObservable,
        ISequencer scheduler,
        TimeSpan throttle)
        where TIndexKey : notnull
    {
        var typedKeys = keysObservable.Select(static keys =>
        {
            var boxedKeys = new object[keys.Length];
            for (var i = 0; i < keys.Length; i++)
            {
                boxedKeys[i] = keys[i];
            }

            return boxedKeys;
        });
        var view = new DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue>(
            source,
            indexName,
            static (dict, name, key) => dict.GetValuesBySecondaryIndex(name, (TIndexKey)key),
            static (dict, name, value, key) => dict.ValueMatchesSecondaryIndex(name, value, (TIndexKey)key));
        view.Start(typedKeys, scheduler, throttle);
        return view;
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _filteredItems.GetEnumerator();

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
    /// <returns>The current instance of <see cref="DynamicSecondaryIndexDictionaryReactiveView{TKey, TValue}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue> ToProperty(Action<ReadOnlyObservableCollection<KeyValuePair<TKey, TValue>>> propertySetter)
    {
        ThrowHelper.ThrowIfNull(propertySetter);
        propertySetter(Items);
        return this;
    }

    /// <summary>Returns the current instance and provides a read-only observable collection of items contained in the view.</summary>
    /// <param name="collection">When this method returns, contains a read-only observable collection of items managed by this view.</param>
    /// <returns>The current <see cref="DynamicSecondaryIndexDictionaryReactiveView{TKey, TValue}"/> instance.</returns>
    public DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue> ToProperty(out ReadOnlyObservableCollection<KeyValuePair<TKey, TValue>> collection)
    {
        collection = Items;
        return this;
    }

    /// <inheritdoc/>
    public void Dispose() => _disposables.Dispose();

    /// <summary>Attempts to get the latest value.</summary>
    /// <param name="source">The source value.</param>
    /// <param name="value">The latest value.</param>
    /// <returns><see langword="true"/> when a value was read; otherwise, <see langword="false"/>.</returns>
    private static bool TryGetLatest(IObservable<object[]> source, out object[]? value)
    {
        var hasValue = false;
        object[]? current = null;
        using var subscription = source.Subscribe(
            next =>
            {
                if (hasValue)
                {
                    return;
                }

                current = next;
                hasValue = true;
            },
            static _ => { });

        value = current;
        return hasValue;
    }

    /// <summary>Initializes the view contents and activates its subscriptions.</summary>
    /// <param name="keysObservable">An observable of key arrays to filter by.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    private void Start(IObservable<object[]> keysObservable, ISequencer scheduler, TimeSpan throttle)
    {
        ThrowHelper.ThrowIfNull(keysObservable);

        var hasInitialKeys = TryGetLatest(keysObservable, out var initialKeys);
        _currentKeys = initialKeys?.ToHashSet() ?? [];
        RebuildView();

        // Subscribe to key changes (skip the first since we already processed it)
        var keyChanges = hasInitialKeys ? keysObservable.Skip(1) : keysObservable;
        _ = keyChanges
            .Subscribe(keys =>
            {
                lock (_lock)
                {
                    _currentKeys = keys is null ? [] : new HashSet<object>(keys);
                    RebuildView();
                }

                OnPropertyChanged(nameof(Count));
            })
            .DisposeWith(_disposables);

        // Subscribe to source changes
        var updates = _source.Stream.Map(static notification => notification.Action);
        if (throttle > TimeSpan.Zero)
        {
            updates = updates.Throttle(throttle);
        }

        _ = updates
            .ObserveOn(scheduler)
            .Subscribe(_ => OnSourceChanged())
            .DisposeWith(_disposables);

        // Forward collection changed events
        _filteredItems.CollectionChanged += (_, e) => CollectionChanged?.Invoke(this, e);
    }

    /// <summary>Handles source change notifications.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnSourceChanged()
    {
        lock (_lock)
        {
            RebuildView();
        }

        OnPropertyChanged(nameof(Count));
    }

    /// <summary>Rebuilds the view from the current source state.</summary>
    private void RebuildView()
    {
        if (_rebuilding)
        {
            _rebuildRequested = true;
            return;
        }

        _rebuilding = true;
        try
        {
            do
            {
                _rebuildRequested = false;
                ReconcileSnapshot();
            }
            while (_rebuildRequested);
        }
        finally
        {
            _rebuilding = false;
        }
    }

    /// <summary>Reconciles current indexed entries, preserving primary-key uniqueness and value identity.</summary>
    private void ReconcileSnapshot()
    {
        var snapshot = CreateSnapshot();
        for (var index = 0; index < snapshot.Count; index++)
        {
            var item = snapshot[index];
            if (index == _filteredItems.Count)
            {
                _filteredItems.Add(item);
            }
            else if (!EqualityComparer<TKey>.Default.Equals(_filteredItems[index].Key, item.Key)
                || (typeof(TValue).IsValueType
                    ? !EqualityComparer<TValue>.Default.Equals(_filteredItems[index].Value, item.Value)
                    : !ReferenceEquals(_filteredItems[index].Value, item.Value)))
            {
                _filteredItems[index] = item;
            }
        }

        while (_filteredItems.Count > snapshot.Count)
        {
            _filteredItems.RemoveAt(_filteredItems.Count - 1);
        }
    }

    /// <summary>Snapshots all matching indexed entries once per primary key.</summary>
    /// <returns>The current matching dictionary entries.</returns>
    private List<KeyValuePair<TKey, TValue>> CreateSnapshot()
    {
        List<KeyValuePair<TKey, TValue>> snapshot = [];
        HashSet<TKey> addedKeys = [];
        foreach (var indexKey in _currentKeys)
        {
            foreach (var value in _getValuesByIndex(_source, _indexName, indexKey))
            {
                if (value is null || !_valueMatchesIndex(_source, _indexName, value, indexKey))
                {
                    continue;
                }

                AppendMatchingEntries(value, addedKeys, snapshot);
            }
        }

        return snapshot;
    }

    /// <summary>Adds dictionary entries associated with one indexed value.</summary>
    /// <param name="value">The indexed value.</param>
    /// <param name="addedKeys">The primary keys already included.</param>
    /// <param name="snapshot">The matching entries.</param>
    private void AppendMatchingEntries(TValue value, HashSet<TKey> addedKeys, List<KeyValuePair<TKey, TValue>> snapshot)
    {
        foreach (var item in _source)
        {
            if (EqualityComparer<TValue>.Default.Equals(item.Value, value) && addedKeys.Add(item.Key))
            {
                snapshot.Add(item);
            }
        }
    }

    /// <summary>Handles property change notifications.</summary>
    /// <param name="propertyName">The propertyName value.</param>
    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
