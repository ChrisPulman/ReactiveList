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
public sealed class DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue> : IReadOnlyList<KeyValuePair<TKey, TValue>>, INotifyCollectionChanged, INotifyPropertyChanged, IReactiveView<DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue>, KeyValuePair<TKey, TValue>>, IDisposable
where TKey : notnull
{
    private readonly QuaternaryDictionary<TKey, TValue> _source;

    private readonly string _indexName;

    private readonly Func<QuaternaryDictionary<TKey, TValue>, string, object, IEnumerable<TValue>> _getValuesByIndex;

    private readonly Func<QuaternaryDictionary<TKey, TValue>, string, TValue, object, bool> _valueMatchesIndex;

    private readonly ObservableCollection<KeyValuePair<TKey, TValue>> _filteredItems;

    private readonly MultipleDisposable _disposables = new();

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private HashSet<object> _currentKeys = [];

    /// <summary>Initializes a new instance of the <see cref="DynamicSecondaryIndexDictionaryReactiveView{TKey, TValue}"/> class.</summary>
    /// <param name="source">The source dictionary to filter.</param>
    /// <param name="indexName">The name of the secondary index.</param>
    /// <param name="keysObservable">An observable of key arrays to filter by.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    /// <param name="getValuesByIndex">The delegate used to retrieve values for a boxed secondary index key.</param>
    /// <param name="valueMatchesIndex">The delegate used to test whether a value matches a boxed secondary index key.</param>
    private DynamicSecondaryIndexDictionaryReactiveView(
        QuaternaryDictionary<TKey, TValue> source,
        string indexName,
        IObservable<object[]> keysObservable,
        ISequencer scheduler,
        TimeSpan throttle,
        Func<QuaternaryDictionary<TKey, TValue>, string, object, IEnumerable<TValue>> getValuesByIndex,
        Func<QuaternaryDictionary<TKey, TValue>, string, TValue, object, bool> valueMatchesIndex)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        ThrowHelper.ThrowIfNull(keysObservable);
        _getValuesByIndex = getValuesByIndex ?? throw new ArgumentNullException(nameof(getValuesByIndex));
        _valueMatchesIndex = valueMatchesIndex ?? throw new ArgumentNullException(nameof(valueMatchesIndex));

        _filteredItems = [];
        Items = new ReadOnlyObservableCollection<KeyValuePair<TKey, TValue>>(_filteredItems);

        var hasInitialKeys = TryGetLatest(keysObservable, out var initialKeys);
        _currentKeys = initialKeys?.ToHashSet() ?? [];
        RebuildView();

        // Subscribe to key changes (skip the first since we already processed it)
        var keyChanges = hasInitialKeys ? keysObservable.Skip(1) : keysObservable;
        keyChanges
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
        _source.Stream
            .Throttle(throttle)
            .ObserveOn(scheduler)
            .Subscribe(OnSourceChanged)
            .DisposeWith(_disposables);

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
    /// <param name="throttle">The throttle duration for updates.</param>
    /// <returns>A <see cref="DynamicSecondaryIndexDictionaryReactiveView{TKey, TValue}"/> instance.</returns>
    public static DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue> Create<TIndexKey>(
        QuaternaryDictionary<TKey, TValue> source,
        string indexName,
        IObservable<TIndexKey[]> keysObservable,
        ISequencer scheduler,
        TimeSpan throttle)
        where TIndexKey : notnull
    {
        var typedKeys = keysObservable.Select(static keys => keys.Select(static key => (object)key).ToArray());
        return new DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue>(
            source,
            indexName,
            typedKeys,
            scheduler,
            throttle,
            static (dict, name, key) => dict.GetValuesBySecondaryIndex<TIndexKey>(name, (TIndexKey)key),
            static (dict, name, value, key) => dict.ValueMatchesSecondaryIndex(name, value, (TIndexKey)key));
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
    public void Dispose()
    {
        _disposables.Dispose();
    }

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
            _ => { });

        value = current;
        return hasValue;
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
                        if (notification.Item.Value is not null && ValueMatchesCurrentKeys(notification.Item.Value))
                        {
                            _filteredItems.Add(notification.Item);
                        }

                        break;
                    }

                case CacheAction.Removed:
                    {
                        if (notification.Item.Key is not null)
                        {
                            // Find and remove the item with matching key
                            for (var i = _filteredItems.Count - 1; i >= 0; i--)
                            {
                                if (EqualityComparer<TKey>.Default.Equals(_filteredItems[i].Key, notification.Item.Key))
                                {
                                    _filteredItems.RemoveAt(i);
                                    break;
                                }
                            }
                        }

                        break;
                    }

                case CacheAction.Updated:
                    {
                        if (notification.Item.Value is not null)
                        {
                            var existingIndex = -1;
                            for (var i = 0; i < _filteredItems.Count; i++)
                            {
                                if (EqualityComparer<TKey>.Default.Equals(_filteredItems[i].Key, notification.Item.Key))
                                {
                                    existingIndex = i;
                                    break;
                                }
                            }

                            var wasInView = existingIndex >= 0;
                            var shouldBeInView = ValueMatchesCurrentKeys(notification.Item.Value);

                            if (wasInView && !shouldBeInView)
                            {
                                _filteredItems.RemoveAt(existingIndex);
                            }
                            else if (!wasInView && shouldBeInView)
                            {
                                _filteredItems.Add(notification.Item);
                            }
                            else if (wasInView && shouldBeInView)
                            {
                                // Update the existing item
                                _filteredItems[existingIndex] = notification.Item;
                            }
                        }

                        break;
                    }

                case CacheAction.Cleared:
                    {
                        _filteredItems.Clear();
                        break;
                    }

                case CacheAction.BatchOperation or CacheAction.BatchAdded or CacheAction.BatchRemoved or CacheAction.Refreshed:
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

        // Get values from secondary index for all current keys, then find their dictionary entries
        var addedKeys = new HashSet<TKey>();
        foreach (var indexKey in _currentKeys)
        {
            foreach (var value in _getValuesByIndex(_source, _indexName, indexKey))
            {
                // Find the key for this value in the source dictionary
                foreach (var kvp in _source)
                {
                    if (EqualityComparer<TValue>.Default.Equals(kvp.Value, value) && addedKeys.Add(kvp.Key))
                    {
                        _filteredItems.Add(kvp);
                    }
                }
            }
        }
    }

    /// <summary>Performs the ValueMatchesCurrentKeys operation.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> when the value matches any current key; otherwise, <see langword="false"/>.</returns>
    private bool ValueMatchesCurrentKeys(TValue value) =>
        _currentKeys.Any(key => _valueMatchesIndex(_source, _indexName, value, key));

    /// <summary>Handles property change notifications.</summary>
    /// <param name="propertyName">The propertyName value.</param>
    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
