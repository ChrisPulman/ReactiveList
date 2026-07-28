// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Views;
#else
namespace CP.Primitives.Views;
#endif
/// <summary>
/// Provides a reactive view over a <see cref="QuaternaryList{T}"/> filtered by secondary index keys
/// that can change dynamically. The view rebuilds when the key observable emits new keys.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
/// <typeparam name="TKey">The type of the secondary index key.</typeparam>
public sealed class DynamicSecondaryIndexReactiveView<T, TKey> :
    IReadOnlyList<T>,
    INotifyCollectionChanged,
    INotifyPropertyChanged,
    IReactiveView<DynamicSecondaryIndexReactiveView<T, TKey>, T>
where T : notnull
where TKey : notnull
{
    /// <summary>Serializes changes to the facade's event subscriptions.</summary>
    private readonly Lock _eventLock = new();

    /// <summary>Contains the mutable state and subscriptions owned by this view.</summary>
    private readonly State _state;

    /// <summary>Relays collection notifications with this facade as their sender.</summary>
    private NotificationRelay<NotifyCollectionChangedEventArgs>? _collectionChangedRelay;

    /// <summary>Relays property notifications with this facade as their sender.</summary>
    private NotificationRelay<PropertyChangedEventArgs>? _propertyChangedRelay;

    /// <summary>The collection handlers currently registered with this facade.</summary>
    private NotifyCollectionChangedEventHandler? _collectionChangedHandlers;

    /// <summary>The property handlers currently registered with this facade.</summary>
    private PropertyChangedEventHandler? _propertyChangedHandlers;

    /// <summary>Initializes a new instance of the <see cref="DynamicSecondaryIndexReactiveView{T, TKey}"/> class.</summary>
    /// <param name="source">The source list to filter.</param>
    /// <param name="indexName">The name of the secondary index.</param>
    /// <param name="keysObservable">An observable of key arrays to filter by.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    public DynamicSecondaryIndexReactiveView(
        QuaternaryList<T> source,
        string indexName,
        IObservable<TKey[]> keysObservable,
        ISequencer scheduler,
        TimeSpan throttle)
    {
        _state = new(source, indexName);
        ThrowHelper.ThrowIfNull(keysObservable);
        _state.Start(keysObservable, scheduler, throttle);
    }

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add => AddCollectionChanged(value);

        remove => RemoveCollectionChanged(value);
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => AddPropertyChanged(value);

        remove => RemovePropertyChanged(value);
    }

    /// <summary>Gets the number of items in the filtered view.</summary>
    public int Count => _state.Count;

    /// <summary>Gets the underlying read-only observable collection for UI binding.</summary>
    public ReadOnlyObservableCollection<T> Items => _state.Items;

    /// <summary>Gets the item at the specified index.</summary>
    /// <param name="index">The zero-based index of the item to get.</param>
    /// <returns>The item at the specified index.</returns>
    public T this[int index] => _state.GetItem(index);

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _state.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Forces a rebuild of the filtered view from the source.</summary>
    public void Refresh() => _state.Refresh();

    /// <summary>Assigns the current collection of items to a property using the specified setter action.</summary>
    /// <remarks>This method is typically used to bind the internal collection to an external property, such
    /// as a view model property, in a reactive UI pattern.</remarks>
    /// <param name="propertySetter">An action that sets a property to the current read-only observable collection of items. Cannot be null.</param>
    /// <returns>The current instance of <see cref="DynamicSecondaryIndexReactiveView{T, TKey}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public DynamicSecondaryIndexReactiveView<T, TKey> ToProperty(Action<ReadOnlyObservableCollection<T>> propertySetter)
    {
        ThrowHelper.ThrowIfNull(propertySetter);
        propertySetter(Items);
        return this;
    }

    /// <summary>Returns the current instance and provides a read-only observable collection of items contained in the view.</summary>
    /// <param name="collection">When this method returns, contains a read-only observable collection of items managed by this view.</param>
    /// <returns>The current <see cref="DynamicSecondaryIndexReactiveView{T, TKey}"/> instance.</returns>
    public DynamicSecondaryIndexReactiveView<T, TKey> ToProperty(out ReadOnlyObservableCollection<T> collection)
    {
        collection = Items;
        return this;
    }

    /// <inheritdoc/>
    public void Dispose() => _state.Stop();

    /// <summary>Adds a collection handler and hooks the state when it is the first handler.</summary>
    /// <param name="handler">The handler to add.</param>
    private void AddCollectionChanged(NotifyCollectionChangedEventHandler? handler)
    {
        if (handler is null)
        {
            return;
        }

        lock (_eventLock)
        {
            if (_collectionChangedHandlers is not null)
            {
                _collectionChangedHandlers += handler;
                return;
            }

            _collectionChangedHandlers = handler;
            var relay = new NotificationRelay<NotifyCollectionChangedEventArgs>(this);
            _collectionChangedRelay = relay;
            _ = relay.Add(DispatchCollectionChanged);
            _state.HookCollectionChanged(relay);
        }
    }

    /// <summary>Removes a collection handler and unhooks the state after the last handler is removed.</summary>
    /// <param name="handler">The handler to remove.</param>
    private void RemoveCollectionChanged(NotifyCollectionChangedEventHandler? handler)
    {
        if (handler is null)
        {
            return;
        }

        lock (_eventLock)
        {
            _collectionChangedHandlers -= handler;
            if (_collectionChangedHandlers is not null || _collectionChangedRelay is not { } relay)
            {
                return;
            }

            _state.UnhookCollectionChanged(relay);
            _ = relay.Remove(DispatchCollectionChanged);
            _collectionChangedRelay = null;
        }
    }

    /// <summary>Adds a property handler and hooks the state when it is the first handler.</summary>
    /// <param name="handler">The handler to add.</param>
    private void AddPropertyChanged(PropertyChangedEventHandler? handler)
    {
        if (handler is null)
        {
            return;
        }

        lock (_eventLock)
        {
            if (_propertyChangedHandlers is not null)
            {
                _propertyChangedHandlers += handler;
                return;
            }

            _propertyChangedHandlers = handler;
            var relay = new NotificationRelay<PropertyChangedEventArgs>(this);
            _propertyChangedRelay = relay;
            _ = relay.Add(DispatchPropertyChanged);
            _state.HookPropertyChanged(relay);
        }
    }

    /// <summary>Removes a property handler and unhooks the state after the last handler is removed.</summary>
    /// <param name="handler">The handler to remove.</param>
    private void RemovePropertyChanged(PropertyChangedEventHandler? handler)
    {
        if (handler is null)
        {
            return;
        }

        lock (_eventLock)
        {
            _propertyChangedHandlers -= handler;
            if (_propertyChangedHandlers is not null || _propertyChangedRelay is not { } relay)
            {
                return;
            }

            _state.UnhookPropertyChanged(relay);
            _ = relay.Remove(DispatchPropertyChanged);
            _propertyChangedRelay = null;
        }
    }

    /// <summary>Delivers a relayed collection notification to this facade's subscribers.</summary>
    /// <param name="sender">The facade reported as the notification sender.</param>
    /// <param name="eventArgs">The collection notification data.</param>
    private void DispatchCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        NotifyCollectionChangedEventHandler? handlers;
        lock (_eventLock)
        {
            handlers = _collectionChangedHandlers;
        }

        handlers?.Invoke(sender, eventArgs);
    }

    /// <summary>Delivers a relayed property notification to this facade's subscribers.</summary>
    /// <param name="sender">The facade reported as the notification sender.</param>
    /// <param name="eventArgs">The property notification data.</param>
    private void DispatchPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        PropertyChangedEventHandler? handlers;
        lock (_eventLock)
        {
            handlers = _propertyChangedHandlers;
        }

        handlers?.Invoke(sender, eventArgs);
    }

    /// <summary>Owns the mutable data and subscriptions behind the public facade.</summary>
    private sealed class State
    {
        /// <summary>The indexed list whose changes feed this view.</summary>
        private readonly QuaternaryList<T> _source;

        /// <summary>The name of the secondary index queried by this view.</summary>
        private readonly string _indexName;

        /// <summary>The mutable collection backing the public read-only view.</summary>
        private readonly ObservableCollection<T> _filteredItems;

        /// <summary>The subscriptions owned by this view.</summary>
        private readonly MultipleDisposable _disposables = [];

        /// <summary>Serializes key changes and collection updates.</summary>
        private readonly Lock _lock = new();

        /// <summary>The secondary-index keys currently included in the view.</summary>
        private HashSet<TKey> _currentKeys = [];

        /// <summary>The property notification subscribers attached to this state.</summary>
        private PropertyChangedEventHandler? _propertyChanged;

        /// <summary>Initializes a new instance of the <see cref="State"/> class.</summary>
        /// <param name="source">The source list to filter.</param>
        /// <param name="indexName">The name of the secondary index.</param>
        public State(QuaternaryList<T> source, string indexName)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
            _filteredItems = [];
            Items = new(_filteredItems);
        }

        /// <summary>Gets the number of items currently in the filtered view.</summary>
        public int Count => _filteredItems.Count;

        /// <summary>Gets the read-only collection exposed by the facade.</summary>
        public ReadOnlyObservableCollection<T> Items { get; }

        /// <summary>Gets the item at the specified index.</summary>
        /// <param name="index">The zero-based index of the item to get.</param>
        /// <returns>The item at the specified index.</returns>
        public T GetItem(int index) => _filteredItems[index];

        /// <summary>Starts key and source subscriptions after this state is fully constructed.</summary>
        /// <param name="keysObservable">An observable of key arrays to filter by.</param>
        /// <param name="scheduler">The scheduler for dispatching source updates.</param>
        /// <param name="throttle">The throttle duration for source updates.</param>
        public void Start(
            IObservable<TKey[]> keysObservable,
            ISequencer scheduler,
            TimeSpan throttle)
        {
            var hasInitialKeys = false;
            TKey[]? initialKeys = null;
            var initialSubscription = keysObservable.Subscribe(
                next =>
                {
                    if (hasInitialKeys)
                    {
                        return;
                    }

                    initialKeys = next;
                    hasInitialKeys = true;
                },
                static _ => { });
            initialSubscription.Dispose();

            _currentKeys = initialKeys?.ToHashSet() ?? [];
            RebuildView();

            // Subscribe to key changes before source changes, skipping the value already read.
            var keyChanges = hasInitialKeys ? keysObservable.Skip(1) : keysObservable;
            _ = keyChanges
                .Subscribe(OnKeysChanged)
                .DisposeWith(_disposables);

            _ = _source.Stream
                .Throttle(throttle)
                .ObserveOn(scheduler)
                .Subscribe(OnSourceChanged)
                .DisposeWith(_disposables);
        }

        /// <summary>Adds the collection relay used while the facade has subscribers.</summary>
        /// <param name="relay">The relay to attach.</param>
        public void HookCollectionChanged(NotificationRelay<NotifyCollectionChangedEventArgs> relay) =>
            _filteredItems.CollectionChanged += relay.OnEvent;

        /// <summary>Removes the collection relay after the facade loses its final subscriber.</summary>
        /// <param name="relay">The relay to detach.</param>
        public void UnhookCollectionChanged(NotificationRelay<NotifyCollectionChangedEventArgs> relay) =>
            _filteredItems.CollectionChanged -= relay.OnEvent;

        /// <summary>Adds the property relay used while the facade has subscribers.</summary>
        /// <param name="relay">The relay to attach.</param>
        public void HookPropertyChanged(NotificationRelay<PropertyChangedEventArgs> relay) =>
            _propertyChanged += relay.OnEvent;

        /// <summary>Removes the property relay after the facade loses its final subscriber.</summary>
        /// <param name="relay">The relay to detach.</param>
        public void UnhookPropertyChanged(NotificationRelay<PropertyChangedEventArgs> relay) =>
            _propertyChanged -= relay.OnEvent;

        /// <summary>Gets an enumerator over the filtered items.</summary>
        /// <returns>An enumerator over the filtered items.</returns>
        public IEnumerator<T> GetEnumerator() => _filteredItems.GetEnumerator();

        /// <summary>Rebuilds the view from the current source state.</summary>
        public void Refresh()
        {
            lock (_lock)
            {
                RebuildView();
            }
        }

        /// <summary>Stops the key and source subscriptions.</summary>
        public void Stop() => _disposables.Dispose();

        /// <summary>Handles a dynamic key change.</summary>
        /// <param name="keys">The keys now included in the view.</param>
        private void OnKeysChanged(TKey[] keys)
        {
            lock (_lock)
            {
                _currentKeys = keys?.ToHashSet() ?? [];
                RebuildView();
            }

            OnPropertyChanged(nameof(Count));
        }

        /// <summary>Handles source change notifications.</summary>
        /// <param name="notification">The notification value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnSourceChanged(CacheNotify<T> notification)
        {
            lock (_lock)
            {
                switch (notification.Action)
                {
                    case CacheAction.Added:
                        {
                            if (notification.Item is not null && ItemMatchesCurrentKeys(notification.Item))
                            {
                                _filteredItems.Add(notification.Item);
                            }

                            break;
                        }

                    case CacheAction.Removed:
                        {
                            if (notification.Item is not null)
                            {
                                _ = _filteredItems.Remove(notification.Item);
                            }

                            break;
                        }

                    case CacheAction.Updated:
                        {
                            UpdateItem(notification);
                            break;
                        }

                    case CacheAction.Cleared:
                        {
                            _filteredItems.Clear();
                            break;
                        }

                    case CacheAction.Moved or
                         CacheAction.Refreshed or
                         CacheAction.BatchOperation or
                         CacheAction.BatchAdded or
                         CacheAction.BatchRemoved:
                        {
                            RebuildView();
                            break;
                        }

                    default:
                        {
                            // Ignore invalid enum values to preserve the view's current state.
                            break;
                        }
                }
            }

            OnPropertyChanged(nameof(Count));
        }

        /// <summary>Updates an item and its membership in the current secondary-index view.</summary>
        /// <param name="notification">The update notification to apply.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateItem(CacheNotify<T> notification)
        {
            var current = notification.Item;
            if (current is null)
            {
                return;
            }

            var previous = notification.Previous;
            var existingIndex = previous is null
                ? _filteredItems.IndexOf(current)
                : _filteredItems.IndexOf(previous);
            var shouldBeInView = ItemMatchesCurrentKeys(current);

            if (existingIndex < 0)
            {
                if (!shouldBeInView)
                {
                    return;
                }

                _filteredItems.Add(current);
                return;
            }

            if (!shouldBeInView)
            {
                _filteredItems.RemoveAt(existingIndex);
                return;
            }

            _filteredItems[existingIndex] = current;
        }

        /// <summary>Rebuilds the view from the current source state.</summary>
        private void RebuildView()
        {
            _filteredItems.Clear();

            foreach (var key in _currentKeys)
            {
                foreach (var item in _source.GetItemsBySecondaryIndex(_indexName, key))
                {
                    // Avoid duplicates if the same item matches multiple keys.
                    if (!_filteredItems.Contains(item))
                    {
                        _filteredItems.Add(item);
                    }
                }
            }
        }

        /// <summary>Determines whether an item matches any current secondary-index key.</summary>
        /// <param name="item">The item to test.</param>
        /// <returns><see langword="true"/> when the item matches a current key; otherwise, <see langword="false"/>.</returns>
        private bool ItemMatchesCurrentKeys(T item)
        {
            foreach (var key in _currentKeys)
            {
                if (_source.ItemMatchesSecondaryIndex(_indexName, item, key))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Raises a property change notification.</summary>
        /// <param name="propertyName">The name of the changed property.</param>
        private void OnPropertyChanged(string propertyName) =>
            _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
