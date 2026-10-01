// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
using CP.Reactive.Internal;

namespace CP.Reactive.Views;
#else
using CP.Primitives.Internal;

namespace CP.Primitives.Views;
#endif
/// <summary>Provides a filtered, read-only view over a <see cref="IReactiveList{T}"/> that automatically updates when the source list changes.</summary>
/// <typeparam name="T">The type of elements in the view.</typeparam>
public sealed class FilteredReactiveView<T> : IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged, IReactiveView<FilteredReactiveView<T>, T>
where T : notnull
{
    /// <summary>Synchronizes collection-changed subscriptions to this facade.</summary>
    private readonly Lock _collectionChangedGate = new();

    /// <summary>Synchronizes property-changed subscriptions to this facade.</summary>
    private readonly Lock _propertyChangedGate = new();

    /// <summary>The completed state object that owns filtering and source subscriptions.</summary>
    private readonly State _state;

    /// <summary>Relays collection notifications with this facade as the sender.</summary>
    private TypedNotificationRelay<NotifyCollectionChangedEventArgs, NotifyCollectionChangedEventHandler>? _collectionChangedRelay;

    /// <summary>Relays property notifications with this facade as the sender.</summary>
    private TypedNotificationRelay<PropertyChangedEventArgs, PropertyChangedEventHandler>? _propertyChangedRelay;

    /// <summary>Initializes a new instance of the <see cref="FilteredReactiveView{T}"/> class.</summary>
    /// <param name="source">The source reactive list to filter.</param>
    /// <param name="filter">The filter predicate.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The quiet period before consuming retained source changes; zero dispatches without debounce.</param>
    public FilteredReactiveView(
        IReactiveList<T> source,
        Func<T, bool> filter,
        ISequencer scheduler,
        TimeSpan throttle)
    {
        _state = new(source, filter, scheduler, throttle);
        _state.Start();
    }

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add
        {
            if (value is null)
            {
                return;
            }

            lock (_collectionChangedGate)
            {
                _collectionChangedRelay ??= new(this, static (handler, sender, eventArgs) => handler(sender, eventArgs));
                if (_collectionChangedRelay.Add(value))
                {
                    _state.CollectionChanged += _collectionChangedRelay.OnEvent;
                }
            }
        }

        remove
        {
            if (value is null)
            {
                return;
            }

            lock (_collectionChangedGate)
            {
                if (_collectionChangedRelay?.Remove(value) is true)
                {
                    _state.CollectionChanged -= _collectionChangedRelay.OnEvent;
                }
            }
        }
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            if (value is null)
            {
                return;
            }

            lock (_propertyChangedGate)
            {
                _propertyChangedRelay ??= new(this, static (handler, sender, eventArgs) => handler(sender, eventArgs));
                if (_propertyChangedRelay.Add(value))
                {
                    _state.PropertyChanged += _propertyChangedRelay.OnEvent;
                }
            }
        }

        remove
        {
            if (value is null)
            {
                return;
            }

            lock (_propertyChangedGate)
            {
                if (_propertyChangedRelay?.Remove(value) is true)
                {
                    _state.PropertyChanged -= _propertyChangedRelay.OnEvent;
                }
            }
        }
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
    /// <returns>The current instance of <see cref="FilteredReactiveView{T}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public FilteredReactiveView<T> ToProperty(Action<ReadOnlyObservableCollection<T>> propertySetter)
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
    /// <returns>The current <see cref="FilteredReactiveView{T}"/> instance.</returns>
    public FilteredReactiveView<T> ToProperty(out ReadOnlyObservableCollection<T> collection)
    {
        collection = Items;
        return this;
    }

    /// <inheritdoc/>
    public void Dispose() => _state.Dispose();

    /// <summary>Owns the mutable filtered view after construction has completed.</summary>
    private sealed class State
    {
        /// <summary>The reactive list that supplies items to this view.</summary>
        private readonly IReactiveList<T> _source;

        /// <summary>The predicate that determines whether an item is included.</summary>
        private readonly Func<T, bool> _filter;

        /// <summary>The mutable collection that backs the read-only filtered items collection.</summary>
        private readonly ObservableCollection<T> _filteredItems = [];

        /// <summary>The scheduler used to dispatch source notifications.</summary>
        private readonly ISequencer _scheduler;

        /// <summary>The throttle applied to source notifications.</summary>
        private readonly TimeSpan _throttle;

        /// <summary>The subscriptions owned by this state.</summary>
        private readonly MultipleDisposable _disposables = [];

        /// <summary>Synchronizes access to the filtered items collection.</summary>
        private readonly Lock _lock = new();

        /// <summary>Retains every structural delta before scheduler dispatch.</summary>
        private readonly ViewChangeBuffer<T> _changes;

        /// <summary>Indicates that collection reconciliation is delivering notifications.</summary>
        private bool _rebuilding;

        /// <summary>Defers a refresh requested by a collection notification handler.</summary>
        private bool _rebuildRequested;

        /// <summary>Defers explicit live-source snapshots until notification delivery finishes.</summary>
        private bool _refreshFromSource;

        /// <summary>Initializes a new instance of the <see cref="State"/> class without publishing callbacks.</summary>
        /// <param name="source">The source reactive list to filter.</param>
        /// <param name="filter">The filter predicate.</param>
        /// <param name="scheduler">The scheduler for dispatching updates.</param>
        /// <param name="throttle">The throttle duration for updates.</param>
        internal State(
            IReactiveList<T> source,
            Func<T, bool> filter,
            ISequencer scheduler,
            TimeSpan throttle)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _scheduler = scheduler;
            _throttle = throttle;
            _changes = new(source);
            Items = new(_filteredItems);
            RebuildView();
        }

        /// <summary>Raised when the filtered collection changes.</summary>
        internal event EventHandler<NotifyCollectionChangedEventArgs>? CollectionChanged;

        /// <summary>Raised when a state property changes.</summary>
        internal event EventHandler<PropertyChangedEventArgs>? PropertyChanged;

        /// <summary>Gets the number of filtered items.</summary>
        internal int Count => _filteredItems.Count;

        /// <summary>Gets the read-only observable filtered items.</summary>
        internal ReadOnlyObservableCollection<T> Items { get; }

        /// <summary>Gets the item at the specified index.</summary>
        /// <param name="index">The zero-based item index.</param>
        /// <returns>The filtered item.</returns>
        internal T GetItem(int index) => _filteredItems[index];

        /// <summary>Starts collection forwarding and source observation after state construction.</summary>
        internal void Start()
        {
            _filteredItems.CollectionChanged += OnCollectionChanged;
            var updates = _source.Stream.ToChangeSets().Map(changes => _changes.Capture(changes, _source.Version));
            if (_throttle > TimeSpan.Zero)
            {
                updates = updates.Throttle(_throttle);
            }

            var subscription = updates
                .ObserveOn(_scheduler)
                .Subscribe(_ => OnSourceChanged());

            _disposables.Add(subscription);
        }

        /// <summary>Returns an enumerator over the filtered items.</summary>
        /// <returns>An enumerator over the filtered items.</returns>
        internal IEnumerator<T> GetEnumerator() => _filteredItems.GetEnumerator();

        /// <summary>Rebuilds the view from the current source state.</summary>
        internal void Refresh()
        {
            lock (_lock)
            {
                _refreshFromSource = true;
                RebuildView();
            }
        }

        /// <summary>Disposes the source subscription.</summary>
        internal void Dispose() => _disposables.Dispose();

        /// <summary>Handles source change notifications.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnSourceChanged()
        {
            lock (_lock)
            {
                ApplyUpdates();
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }

        /// <summary>Reconciles the current source snapshot without replaying stale structural changes.</summary>
        private void RebuildView()
        {
            _rebuildRequested = true;
            ApplyUpdates();
        }

        /// <summary>Consumes retained deltas and defers reentrant refreshes.</summary>
        private void ApplyUpdates()
        {
            if (_rebuilding)
            {
                return;
            }

            _rebuilding = true;
            try
            {
                do
                {
                    if (_refreshFromSource)
                    {
                        _refreshFromSource = false;
                        _changes.Reset(_source);
                    }

                    while (_changes.TryTake(out var changes))
                    {
                        for (var i = 0; i < changes.Count; i++)
                        {
                            var change = changes[i];
                            _changes.Apply(change);
                            ProcessChange(change);
                        }
                    }

                    if (_rebuildRequested)
                    {
                        _rebuildRequested = false;
                        ReconcileSnapshot();
                    }
                }
                while (_rebuildRequested || _refreshFromSource || _changes.HasPending);
            }
            finally
            {
                _rebuilding = false;
            }
        }

        /// <summary>Preserves incremental membership positions between explicit rebuilds.</summary>
        /// <param name="change">The consumed source change.</param>
        private void ProcessChange(Change<T> change)
        {
            if (change.Reason is ChangeReason.Add)
            {
                if (_filter(change.Current))
                {
                    _filteredItems.Add(change.Current);
                }
            }
            else if (change.Reason is ChangeReason.Remove)
            {
                var index = ViewChangeBuffer<T>.FindIndex(_filteredItems, change.Current);
                if (index >= 0)
                {
                    _filteredItems.RemoveAt(index);
                }
            }
            else if (change.Reason is ChangeReason.Update)
            {
                UpdateItem(change);
            }
            else if (change.Reason is ChangeReason.Clear)
            {
                _filteredItems.Clear();
            }
            else if (change.Reason is ChangeReason.Move or ChangeReason.Refresh)
            {
                ReconcileSnapshot();
            }
        }

        /// <summary>Updates an item while preserving its existing filtered position.</summary>
        /// <param name="change">The consumed update.</param>
        private void UpdateItem(Change<T> change)
        {
            var index = change.Previous is null ? -1 : ViewChangeBuffer<T>.FindIndex(_filteredItems, change.Previous);
            var included = _filter(change.Current);
            if (index < 0)
            {
                if (included)
                {
                    _filteredItems.Add(change.Current);
                }

                return;
            }

            if (!included)
            {
                _filteredItems.RemoveAt(index);
                return;
            }

            _filteredItems[index] = change.Current;
        }

        /// <summary>Preserves source order, duplicate occurrences and reference identity.</summary>
        private void ReconcileSnapshot()
        {
            var index = 0;
            foreach (var item in _changes.Items)
            {
                if (!_filter(item))
                {
                    continue;
                }

                if (index == _filteredItems.Count)
                {
                    _filteredItems.Add(item);
                }
                else if (typeof(T).IsValueType
                    ? !EqualityComparer<T>.Default.Equals(_filteredItems[index], item)
                    : !ReferenceEquals(_filteredItems[index], item))
                {
                    _filteredItems[index] = item;
                }

                index++;
            }

            while (_filteredItems.Count > index)
            {
                _filteredItems.RemoveAt(_filteredItems.Count - 1);
            }
        }

        /// <summary>Forwards collection changes from the mutable collection.</summary>
        /// <param name="sender">The originating collection.</param>
        /// <param name="eventArgs">The collection change event data.</param>
        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs) =>
            CollectionChanged?.Invoke(sender, eventArgs);
    }
}
