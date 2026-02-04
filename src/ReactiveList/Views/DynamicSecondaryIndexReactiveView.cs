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
/// Provides a reactive view over a <see cref="QuaternaryList{T}"/> filtered by secondary index keys
/// that can change dynamically. The view rebuilds when the key observable emits new keys.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
/// <typeparam name="TKey">The type of the secondary index key.</typeparam>
public sealed class DynamicSecondaryIndexReactiveView<T, TKey> : IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged, IReactiveView<DynamicSecondaryIndexReactiveView<T, TKey>, T>, IDisposable
where T : notnull
where TKey : notnull
{
    private readonly QuaternaryList<T> _source;
    private readonly string _indexName;
    private readonly ObservableCollection<T> _filteredItems;
    private readonly CompositeDisposable _disposables = [];
    private readonly object _lock = new();
    private HashSet<TKey> _currentKeys = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicSecondaryIndexReactiveView{T, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source list to filter.</param>
    /// <param name="indexName">The name of the secondary index.</param>
    /// <param name="keysObservable">An observable of key arrays to filter by.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    public DynamicSecondaryIndexReactiveView(
        QuaternaryList<T> source,
        string indexName,
        IObservable<TKey[]> keysObservable,
        IScheduler scheduler,
        TimeSpan throttle)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        ArgumentNullException.ThrowIfNull(keysObservable);

        _filteredItems = [];
        Items = new ReadOnlyObservableCollection<T>(_filteredItems);

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
    public ReadOnlyObservableCollection<T> Items { get; }

    /// <summary>
    /// Gets the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to get.</param>
    /// <returns>The item at the specified index.</returns>
    public T this[int index] => _filteredItems[index];

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _filteredItems.GetEnumerator();

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
    /// <returns>The current instance of <see cref="DynamicSecondaryIndexReactiveView{T, TKey}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public DynamicSecondaryIndexReactiveView<T, TKey> ToProperty(Action<ReadOnlyObservableCollection<T>> propertySetter)
    {
        ArgumentNullException.ThrowIfNull(propertySetter);
        propertySetter(Items);
        return this;
    }

    /// <summary>
    /// Returns the current instance and provides a read-only observable collection of items contained in the view.
    /// </summary>
    /// <param name="collection">When this method returns, contains a read-only observable collection of items managed by this view.</param>
    /// <returns>The current <see cref="DynamicSecondaryIndexReactiveView{T, TKey}"/> instance.</returns>
    public DynamicSecondaryIndexReactiveView<T, TKey> ToProperty(out ReadOnlyObservableCollection<T> collection)
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
    private void OnSourceChanged(CacheNotify<T> notification)
    {
        lock (_lock)
        {
            switch (notification.Action)
            {
                case CacheAction.Added:
                    if (notification.Item != null && ItemMatchesCurrentKeys(notification.Item))
                    {
                        _filteredItems.Add(notification.Item);
                    }

                    break;

                case CacheAction.Removed:
                    if (notification.Item != null)
                    {
                        _filteredItems.Remove(notification.Item);
                    }

                    break;

                case CacheAction.Updated:
                    if (notification.Item != null)
                    {
                        var wasInView = _filteredItems.Contains(notification.Item);
                        var shouldBeInView = ItemMatchesCurrentKeys(notification.Item);

                        if (wasInView && !shouldBeInView)
                        {
                            _filteredItems.Remove(notification.Item);
                        }
                        else if (!wasInView && shouldBeInView)
                        {
                            _filteredItems.Add(notification.Item);
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

        // Get items from secondary index for all current keys
        // Explicitly specify TKey to ensure type inference is correct
        foreach (var key in _currentKeys)
        {
            var items = _source.GetItemsBySecondaryIndex<TKey>(_indexName, key);
            foreach (var item in items)
            {
                // Avoid duplicates if same item matches multiple keys
                if (!_filteredItems.Contains(item))
                {
                    _filteredItems.Add(item);
                }
            }
        }
    }

    private bool ItemMatchesCurrentKeys(T item) =>
        _currentKeys.Any(key => _source.ItemMatchesSecondaryIndex<TKey>(_indexName, item, key));

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
#endif
