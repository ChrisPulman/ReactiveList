// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace CP.Reactive.Quaternary;

/// <summary>
/// Provides a reactive view over a <see cref="QuaternaryDictionary{TKey, TValue}"/> filtered by a secondary index key.
/// The view automatically updates when the source dictionary changes.
/// </summary>
/// <typeparam name="TKey">The type of primary keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
/// <typeparam name="TIndexKey">The type of the secondary index key.</typeparam>
public sealed class SecondaryIndexReactiveView<TKey, TValue, TIndexKey> : IReadOnlyList<TValue>, INotifyCollectionChanged, INotifyPropertyChanged, IDisposable
    where TKey : notnull
    where TIndexKey : notnull
{
    private readonly QuaternaryDictionary<TKey, TValue> _source;
    private readonly string _indexName;
    private readonly TIndexKey _indexKey;
    private readonly ObservableCollection<TValue> _filteredItems;
    private readonly CompositeDisposable _disposables = [];
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
        IScheduler scheduler,
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
                        // Check if the updated item's index key changed
                        var wasInView = _filteredItems.Contains(notification.Item.Value);
                        var shouldBeInView = _source.ValueMatchesSecondaryIndex(_indexName, notification.Item.Value, _indexKey);

                        if (wasInView && !shouldBeInView)
                        {
                            _filteredItems.Remove(notification.Item.Value);
                        }
                        else if (!wasInView && shouldBeInView)
                        {
                            _filteredItems.Add(notification.Item.Value);
                        }
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
