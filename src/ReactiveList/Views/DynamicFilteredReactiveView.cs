// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Views;
#else
namespace CP.Primitives.Views;
#endif
/// <summary>
/// Provides a dynamically filtered view over a <see cref="IReactiveList{T}"/> that automatically
/// updates when the source list changes or the filter predicate changes.
/// </summary>
/// <typeparam name="T">The type of elements in the view.</typeparam>
public sealed class DynamicFilteredReactiveView<T> : IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged, IReactiveView<DynamicFilteredReactiveView<T>, T>
where T : notnull
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

    /// <summary>Initializes a new instance of the <see cref="DynamicFilteredReactiveView{T}"/> class.</summary>
    /// <param name="source">The source reactive list to filter.</param>
    /// <param name="filterObservable">An observable that emits filter predicates.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    public DynamicFilteredReactiveView(
        IReactiveList<T> source,
        IObservable<Func<T, bool>> filterObservable,
        ISequencer scheduler,
        TimeSpan throttle)
    {
        _state = new(source);
#if NET8_0_OR_GREATER
        ThrowHelper.ThrowIfNull(filterObservable);
#else
        if (filterObservable is null)
        {
            throw new ArgumentNullException(nameof(filterObservable));
        }
#endif

        _state.Start(filterObservable, scheduler, throttle);
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

    /// <summary>Forces a rebuild of the filtered view from the source using the current filter.</summary>
    public void Refresh() => _state.Refresh();

    /// <summary>Assigns the current collection of items to a property using the specified setter action.</summary>
    /// <remarks>This method is typically used to bind the internal collection to an external property, such
    /// as a view model property, in a reactive UI pattern.</remarks>
    /// <param name="propertySetter">An action that sets a property to the current read-only observable collection of items. Cannot be null.</param>
    /// <returns>The current instance of <see cref="DynamicFilteredReactiveView{T}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public DynamicFilteredReactiveView<T> ToProperty(Action<ReadOnlyObservableCollection<T>> propertySetter)
    {
#if NET8_0_OR_GREATER
        ThrowHelper.ThrowIfNull(propertySetter);
#else
        if (propertySetter is null)
        {
            throw new ArgumentNullException(nameof(propertySetter));
        }
#endif
        propertySetter(Items);
        return this;
    }

    /// <summary>Returns the current instance and provides a read-only observable collection of items contained in the view.</summary>
    /// <param name="collection">When this method returns, contains a read-only observable collection of items managed by this view.</param>
    /// <returns>The current <see cref="DynamicFilteredReactiveView{T}"/> instance.</returns>
    public DynamicFilteredReactiveView<T> ToProperty(out ReadOnlyObservableCollection<T> collection)
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
        /// <summary>The source list whose changes feed this view.</summary>
        private readonly IReactiveList<T> _source;

        /// <summary>The mutable collection backing the public read-only view.</summary>
        private readonly ObservableCollection<T> _filteredItems;

        /// <summary>The subscriptions owned by this view.</summary>
        private readonly MultipleDisposable _disposables = [];

        /// <summary>Serializes filter changes and collection updates.</summary>
        private readonly Lock _lock = new();

        /// <summary>The predicate currently applied to source items.</summary>
        private Func<T, bool> _currentFilter = static _ => true;

        /// <summary>The property notification subscribers attached to this state.</summary>
        private PropertyChangedEventHandler? _propertyChanged;

        /// <summary>Initializes a new instance of the <see cref="State"/> class.</summary>
        /// <param name="source">The source reactive list to filter.</param>
        public State(IReactiveList<T> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
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

        /// <summary>Starts dynamic-filter and source subscriptions after this state is fully constructed.</summary>
        /// <param name="filterObservable">An observable that emits filter predicates.</param>
        /// <param name="scheduler">The scheduler for dispatching updates.</param>
        /// <param name="throttle">The throttle duration for updates.</param>
        public void Start(
            IObservable<Func<T, bool>> filterObservable,
            ISequencer scheduler,
            TimeSpan throttle)
        {
            // Initialize with current items (no filter initially).
            RebuildView();

            // Subscribe to filter changes before source changes.
            var filterSubscription = filterObservable
                .Throttle(throttle)
                .ObserveOn(scheduler)
                .Subscribe(OnFilterChanged);
            _disposables.Add(filterSubscription);

            var sourceSubscription = _source.Stream
                .ToChangeSets()
                .Throttle(throttle)
                .ObserveOn(scheduler)
                .Subscribe(OnSourceChanged);
            _disposables.Add(sourceSubscription);
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

        /// <summary>Rebuilds the filtered view using its current predicate.</summary>
        public void Refresh()
        {
            lock (_lock)
            {
                RebuildView();
            }

            OnPropertyChanged(nameof(Count));
        }

        /// <summary>Stops the dynamic-filter and source subscriptions.</summary>
        public void Stop() => _disposables.Dispose();

        /// <summary>Handles filter change notifications.</summary>
        /// <param name="newFilter">The new filter value.</param>
        private void OnFilterChanged(Func<T, bool> newFilter)
        {
            lock (_lock)
            {
                _currentFilter = newFilter ?? (static _ => true);
                RebuildView();
            }

            OnPropertyChanged(nameof(Count));
        }

        /// <summary>Handles source change notifications.</summary>
        /// <param name="changes">The changes value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnSourceChanged(ChangeSet<T> changes)
        {
            lock (_lock)
            {
                for (var i = 0; i < changes.Count; i++)
                {
                    var change = changes[i];
                    ProcessChange(change);
                }
            }

            OnPropertyChanged(nameof(Count));
        }

        /// <summary>Processes a source collection change.</summary>
        /// <param name="change">The change value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessChange(Change<T> change)
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    {
                        if (_currentFilter(change.Current))
                        {
                            _filteredItems.Add(change.Current);
                        }

                        break;
                    }

                case ChangeReason.Remove:
                    {
                        _ = _filteredItems.Remove(change.Current);
                        break;
                    }

                case ChangeReason.Update:
                    {
                        UpdateItem(change);
                        break;
                    }

                case ChangeReason.Clear:
                    {
                        _filteredItems.Clear();
                        break;
                    }

                case ChangeReason.Move or ChangeReason.Refresh:
                    {
                        // For move and refresh, rebuild the view to maintain correct order.
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

        /// <summary>Updates an item while preserving its filtered position when possible.</summary>
        /// <param name="change">The update change to apply.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateItem(Change<T> change)
        {
            var previous = change.Previous;
            var existingIndex = previous is null ? -1 : _filteredItems.IndexOf(previous);
            var shouldInclude = _currentFilter(change.Current);

            if (existingIndex < 0)
            {
                if (!shouldInclude)
                {
                    return;
                }

                _filteredItems.Add(change.Current);
                return;
            }

            if (!shouldInclude)
            {
                _filteredItems.RemoveAt(existingIndex);
                return;
            }

            _filteredItems[existingIndex] = change.Current;
        }

        /// <summary>Rebuilds the view from the current source state.</summary>
        private void RebuildView()
        {
            _filteredItems.Clear();
            foreach (var item in _source)
            {
                if (_currentFilter(item))
                {
                    _filteredItems.Add(item);
                }
            }
        }

        /// <summary>Raises a property change notification.</summary>
        /// <param name="propertyName">The name of the changed property.</param>
        private void OnPropertyChanged(string propertyName) =>
            _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
