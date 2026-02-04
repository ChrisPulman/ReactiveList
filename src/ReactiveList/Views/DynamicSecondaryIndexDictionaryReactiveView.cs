// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using CP.Reactive.Collections;
using CP.Reactive.Core;

namespace CP.Reactive.Views;

/// <summary>
/// Provides a reactive view over a <see cref="QuaternaryDictionary{TKey, TValue}"/> filtered by secondary index keys
/// that can change dynamically. The view rebuilds when the key observable emits new keys.
/// Returns KeyValuePairs to support dictionary iteration patterns.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
/// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
public sealed class DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue, TIndexKey> : IReadOnlyList<KeyValuePair<TKey, TValue>>, INotifyCollectionChanged, INotifyPropertyChanged, IReactiveView<DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue, TIndexKey>, KeyValuePair<TKey, TValue>>, IDisposable
where TKey : notnull
where TIndexKey : notnull
{
    private readonly QuaternaryDictionary<TKey, TValue> _source;
    private readonly string _indexName;
    private readonly ObservableCollection<KeyValuePair<TKey, TValue>> _filteredItems;
    private readonly CompositeDisposable _disposables = [];
    private readonly object _lock = new();
    private HashSet<TIndexKey> _currentKeys = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicSecondaryIndexDictionaryReactiveView{TKey, TValue, TIndexKey}"/> class.
    /// </summary>
    /// <param name="source">The source dictionary to filter.</param>
    /// <param name="indexName">The name of the secondary index.</param>
    /// <param name="keysObservable">An observable of key arrays to filter by.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    public DynamicSecondaryIndexDictionaryReactiveView(
        QuaternaryDictionary<TKey, TValue> source,
        string indexName,
        IObservable<TIndexKey[]> keysObservable,
        IScheduler scheduler,
        TimeSpan throttle)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        ArgumentNullException.ThrowIfNull(keysObservable);

        _filteredItems = [];
        Items = new ReadOnlyObservableCollection<KeyValuePair<TKey, TValue>>(_filteredItems);

        // Get initial keys synchronously for BehaviorSubject/ReplaySubject
        var initialKeys = keysObservable.FirstAsync().GetAwaiter().GetResult();
        _currentKeys = initialKeys?.ToHashSet() ?? [];
        RebuildView();

        // Subscribe to key changes (skip the first since we already processed it)
        keysObservable
            .Skip(1)
            .Subscribe(keys =>
            {
                lock (_lock)
                {
                    _currentKeys = keys?.ToHashSet() ?? [];
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

    /// <summary>
    /// Gets the number of items in the filtered view.
    /// </summary>
    public int Count => _filteredItems.Count;

    /// <summary>
    /// Gets the underlying read-only observable collection for UI binding.
    /// </summary>
    public ReadOnlyObservableCollection<KeyValuePair<TKey, TValue>> Items { get; }

    /// <summary>
    /// Gets the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to get.</param>
    /// <returns>The item at the specified index.</returns>
    public KeyValuePair<TKey, TValue> this[int index] => _filteredItems[index];

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _filteredItems.GetEnumerator();

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
    /// <returns>The current instance of <see cref="DynamicSecondaryIndexDictionaryReactiveView{TKey, TValue, TIndexKey}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue, TIndexKey> ToProperty(Action<ReadOnlyObservableCollection<KeyValuePair<TKey, TValue>>> propertySetter)
    {
        ArgumentNullException.ThrowIfNull(propertySetter);
        propertySetter(Items);
        return this;
    }

    /// <summary>
    /// Returns the current instance and provides a read-only observable collection of items contained in the view.
    /// </summary>
    /// <param name="collection">When this method returns, contains a read-only observable collection of items managed by this view.</param>
    /// <returns>The current <see cref="DynamicSecondaryIndexDictionaryReactiveView{TKey, TValue, TIndexKey}"/> instance.</returns>
    public DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue, TIndexKey> ToProperty(out ReadOnlyObservableCollection<KeyValuePair<TKey, TValue>> collection)
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
                    if (notification.Item.Value != null && ValueMatchesCurrentKeys(notification.Item.Value))
                    {
                        _filteredItems.Add(notification.Item);
                    }

                    break;

                case CacheAction.Removed:
                    if (notification.Item.Key != null)
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

                case CacheAction.Updated:
                    if (notification.Item.Value != null)
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

                case CacheAction.Cleared:
                    _filteredItems.Clear();
                    break;

                case CacheAction.BatchOperation:
                case CacheAction.BatchAdded:
                case CacheAction.BatchRemoved:
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

        // Get values from secondary index for all current keys, then find their dictionary entries
        var addedKeys = new HashSet<TKey>();
        foreach (var indexKey in _currentKeys)
        {
            foreach (var value in _source.GetValuesBySecondaryIndex<TIndexKey>(_indexName, indexKey))
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

    private bool ValueMatchesCurrentKeys(TValue value) =>
        _currentKeys.Any(key => _source.ValueMatchesSecondaryIndex(_indexName, value, key));

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
#endif
