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
/// <summary>Provides a grouped view over a <see cref="IReactiveList{T}"/> that automatically updates when the source list changes.</summary>
/// <typeparam name="T">The type of elements in the view.</typeparam>
/// <typeparam name="TKey">The type of the grouping key.</typeparam>
public sealed class GroupedReactiveView<T, TKey> :
    IReadOnlyDictionary<TKey, IReadOnlyList<T>>,
    INotifyCollectionChanged,
    INotifyPropertyChanged,
    IReactiveView<GroupedReactiveView<T, TKey>, ReactiveGroup<TKey, T>>
where T : notnull
where TKey : notnull
{
    /// <summary>Synchronizes collection-changed subscriptions to this facade.</summary>
    private readonly Lock _collectionChangedGate = new();

    /// <summary>Synchronizes property-changed subscriptions to this facade.</summary>
    private readonly Lock _propertyChangedGate = new();

    /// <summary>The completed state object that owns grouping and source subscriptions.</summary>
    private readonly State _state;

    /// <summary>Relays collection notifications with this facade as the sender.</summary>
    private NotificationRelay<NotifyCollectionChangedEventArgs>? _collectionChangedRelay;

    /// <summary>Relays property notifications with this facade as the sender.</summary>
    private NotificationRelay<PropertyChangedEventArgs>? _propertyChangedRelay;

    /// <summary>Initializes a new instance of the <see cref="GroupedReactiveView{T, TKey}"/> class.</summary>
    /// <param name="source">The source reactive list to group.</param>
    /// <param name="keySelector">A function to extract the grouping key.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    public GroupedReactiveView(
        IReactiveList<T> source,
        Func<T, TKey> keySelector,
        ISequencer scheduler,
        TimeSpan throttle)
    {
        _state = new(source, keySelector, scheduler, throttle);
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
                _collectionChangedRelay ??= new(this);
                if (_collectionChangedRelay.Add(value.Invoke))
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
                if (_collectionChangedRelay?.Remove(value.Invoke) is true)
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
                _propertyChangedRelay ??= new(this);
                if (_propertyChangedRelay.Add(value.Invoke))
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
                if (_propertyChangedRelay?.Remove(value.Invoke) is true)
                {
                    _state.PropertyChanged -= _propertyChangedRelay.OnEvent;
                }
            }
        }
    }

    /// <summary>Gets the number of groups.</summary>
    public int Count => _state.Count;

    /// <summary>Gets the collection of groups for UI binding.</summary>
    public ReadOnlyObservableCollection<ReactiveGroup<TKey, T>> Groups => _state.Groups;

    /// <summary>Gets the collection of groups for UI binding. This is an alias for <see cref="Groups"/>.</summary>
    /// <remarks>This property exists to satisfy the <see cref="IReactiveView{TView, TItem}"/> interface.</remarks>
    public ReadOnlyObservableCollection<ReactiveGroup<TKey, T>> Items => Groups;

    /// <summary>Gets the keys of all groups.</summary>
    public IEnumerable<TKey> Keys => _state.Keys;

    /// <summary>Gets the values (item lists) of all groups.</summary>
    public IEnumerable<IReadOnlyList<T>> Values => _state.Values;

    /// <summary>Gets the items in the specified group.</summary>
    /// <param name="key">The group key.</param>
    /// <returns>The items in the group.</returns>
    public IReadOnlyList<T> this[TKey key] => _state.GetItems(key);

    /// <summary>Determines whether the view contains a group with the specified key.</summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>true if the view contains a group with the key; otherwise, false.</returns>
    public bool ContainsKey(TKey key) => _state.ContainsKey(key);

    /// <summary>Gets the items in the specified group, if it exists.</summary>
    /// <param name="key">The group key.</param>
    /// <param name="value">When this method returns, contains the items, if the key is found; otherwise, null.</param>
    /// <returns>true if the view contains a group with the specified key; otherwise, false.</returns>
    public bool TryGetValue(TKey key, out IReadOnlyList<T> value) => _state.TryGetValue(key, out value);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, IReadOnlyList<T>>> GetEnumerator() => _state.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Forces a rebuild of the grouped view from the source.</summary>
    public void Refresh() => _state.Refresh();

    /// <summary>Assigns the current collection of groups to a property using the specified setter action.</summary>
    /// <remarks>This method is typically used to bind the internal collection to an external property, such
    /// as a view model property, in a reactive UI pattern.</remarks>
    /// <param name="propertySetter">An action that sets a property to the current read-only observable collection of groups. Cannot be null.</param>
    /// <returns>The current instance of <see cref="GroupedReactiveView{T, TKey}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public GroupedReactiveView<T, TKey> ToProperty(Action<ReadOnlyObservableCollection<ReactiveGroup<TKey, T>>> propertySetter)
    {
#if NET8_0_OR_GREATER
        ThrowHelper.ThrowIfNull(propertySetter);
#else
        if (propertySetter is null)
        {
            throw new ArgumentNullException(nameof(propertySetter));
        }
#endif
        propertySetter(Groups);
        return this;
    }

    /// <summary>Returns the current instance and provides a read-only observable collection of groups contained in the view.</summary>
    /// <param name="collection">When this method returns, contains a read-only observable collection of groups managed by this view.</param>
    /// <returns>The current <see cref="GroupedReactiveView{T, TKey}"/> instance.</returns>
    public GroupedReactiveView<T, TKey> ToProperty(out ReadOnlyObservableCollection<ReactiveGroup<TKey, T>> collection)
    {
        collection = Groups;
        return this;
    }

    /// <inheritdoc/>
    public void Dispose() => _state.Dispose();

    /// <summary>Owns the mutable grouped view after construction has completed.</summary>
    private sealed class State
    {
        /// <summary>The list whose changes feed this grouped view.</summary>
        private readonly IReactiveList<T> _source;

        /// <summary>Extracts the grouping key for each source item.</summary>
        private readonly Func<T, TKey> _keySelector;

        /// <summary>The mutable item collection for each group key.</summary>
        private readonly Dictionary<TKey, ObservableCollection<T>> _groups = [];

        /// <summary>The observable collection backing the public group view.</summary>
        private readonly ObservableCollection<ReactiveGroup<TKey, T>> _groupCollection = [];

        /// <summary>The scheduler used to dispatch source notifications.</summary>
        private readonly ISequencer _scheduler;

        /// <summary>The throttle applied to source notifications.</summary>
        private readonly TimeSpan _throttle;

        /// <summary>The subscriptions owned by this state.</summary>
        private readonly MultipleDisposable _disposables = [];

        /// <summary>Serializes source notifications and group updates.</summary>
        private readonly Lock _lock = new();

        /// <summary>Initializes a new instance of the <see cref="State"/> class without publishing callbacks.</summary>
        /// <param name="source">The source reactive list to group.</param>
        /// <param name="keySelector">A function to extract the grouping key.</param>
        /// <param name="scheduler">The scheduler for dispatching updates.</param>
        /// <param name="throttle">The throttle duration for updates.</param>
        internal State(
            IReactiveList<T> source,
            Func<T, TKey> keySelector,
            ISequencer scheduler,
            TimeSpan throttle)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            _scheduler = scheduler;
            _throttle = throttle;
            Groups = new(_groupCollection);
            RebuildView();
        }

        /// <summary>Raised when the group collection changes.</summary>
        internal event EventHandler<NotifyCollectionChangedEventArgs>? CollectionChanged;

        /// <summary>Raised when a state property changes.</summary>
        internal event EventHandler<PropertyChangedEventArgs>? PropertyChanged;

        /// <summary>Gets the number of groups.</summary>
        internal int Count => _groups.Count;

        /// <summary>Gets the read-only observable collection of groups.</summary>
        internal ReadOnlyObservableCollection<ReactiveGroup<TKey, T>> Groups { get; }

        /// <summary>Gets all group keys.</summary>
        internal IEnumerable<TKey> Keys => _groups.Keys;

        /// <summary>Gets all group item collections.</summary>
        internal IEnumerable<IReadOnlyList<T>> Values => EnumerateValues();

        /// <summary>Gets the items in the specified group.</summary>
        /// <param name="key">The group key.</param>
        /// <returns>The items in the group.</returns>
        internal ObservableCollection<T> GetItems(TKey key) => _groups[key];

        /// <summary>Starts collection forwarding and source observation after state construction.</summary>
        internal void Start()
        {
            _groupCollection.CollectionChanged += OnCollectionChanged;
            var subscription = _source.Stream
                .ToChangeSets()
                .Throttle(_throttle)
                .ObserveOn(_scheduler)
                .Subscribe(OnSourceChanged);

            _disposables.Add(subscription);
        }

        /// <summary>Determines whether a group exists.</summary>
        /// <param name="key">The group key.</param>
        /// <returns><see langword="true"/> when the group exists; otherwise, <see langword="false"/>.</returns>
        internal bool ContainsKey(TKey key) => _groups.ContainsKey(key);

        /// <summary>Gets a group when it exists.</summary>
        /// <param name="key">The group key.</param>
        /// <param name="value">The located item collection or an empty collection.</param>
        /// <returns><see langword="true"/> when the group exists; otherwise, <see langword="false"/>.</returns>
        internal bool TryGetValue(TKey key, out IReadOnlyList<T> value)
        {
            if (_groups.TryGetValue(key, out var list))
            {
                value = list;
                return true;
            }

            value = [];
            return false;
        }

        /// <summary>Returns an enumerator over the groups.</summary>
        /// <returns>An enumerator over the groups.</returns>
        internal IEnumerator<KeyValuePair<TKey, IReadOnlyList<T>>> GetEnumerator() => EnumerateGroups().GetEnumerator();

        /// <summary>Rebuilds the view from the current source state.</summary>
        internal void Refresh()
        {
            lock (_lock)
            {
                RebuildView();
            }
        }

        /// <summary>Disposes the source subscription.</summary>
        internal void Dispose() => _disposables.Dispose();

        /// <summary>Handles source change notifications.</summary>
        /// <param name="changes">The changes value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnSourceChanged(ChangeSet<T> changes)
        {
            lock (_lock)
            {
                for (var i = 0; i < changes.Count; i++)
                {
                    ProcessChange(changes[i]);
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
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
                        AddToGroup(change.Current);
                        break;
                    }

                case ChangeReason.Remove:
                    {
                        RemoveFromGroup(change.Current);
                        break;
                    }

                case ChangeReason.Update:
                    {
                        UpdateGroup(change);
                        break;
                    }

                case ChangeReason.Clear:
                    {
                        _groups.Clear();
                        _groupCollection.Clear();
                        break;
                    }

                case ChangeReason.Move or ChangeReason.Refresh:
                    {
                        RebuildView();
                        break;
                    }

                default:
                    {
                        break;
                    }
            }
        }

        /// <summary>Updates an item in its existing group or moves it to its new group.</summary>
        /// <param name="change">The update change to apply.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateGroup(Change<T> change)
        {
            var previous = change.Previous;
            if (previous is null)
            {
                AddToGroup(change.Current);
                return;
            }

            var oldKey = _keySelector(previous);
            var newKey = _keySelector(change.Current);
            if (!EqualityComparer<TKey>.Default.Equals(oldKey, newKey))
            {
                RemoveFromGroup(previous);
                AddToGroup(change.Current);
                return;
            }

            if (!_groups.TryGetValue(oldKey, out var group))
            {
                return;
            }

            var index = group.IndexOf(previous);
            if (index < 0)
            {
                return;
            }

            group[index] = change.Current;
        }

        /// <summary>Adds data for the AddToGroup operation.</summary>
        /// <param name="item">The item value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddToGroup(T item)
        {
            var key = _keySelector(item);
            if (!_groups.TryGetValue(key, out var group))
            {
                group = [];
                _groups[key] = group;
                _groupCollection.Add(new(key, group));
            }

            group.Add(item);
        }

        /// <summary>Removes data for the RemoveFromGroup operation.</summary>
        /// <param name="item">The item value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveFromGroup(T item)
        {
            var key = _keySelector(item);
            if (!_groups.TryGetValue(key, out var group))
            {
                return;
            }

            _ = group.Remove(item);
            if (group.Count != 0)
            {
                return;
            }

            _ = _groups.Remove(key);
            ReactiveGroup<TKey, T>? reactiveGroup = null;
            var comparer = EqualityComparer<TKey>.Default;
            for (var i = 0; i < _groupCollection.Count; i++)
            {
                var candidate = _groupCollection[i];
                if (comparer.Equals(candidate.Key, key))
                {
                    reactiveGroup = candidate;
                    break;
                }
            }

            if (reactiveGroup is null)
            {
                return;
            }

            _ = _groupCollection.Remove(reactiveGroup);
        }

        /// <summary>Rebuilds the view from the current source state.</summary>
        private void RebuildView()
        {
            _groups.Clear();
            _groupCollection.Clear();

            foreach (var item in _source)
            {
                AddToGroup(item);
            }
        }

        /// <summary>Enumerates the group item collections without LINQ allocation.</summary>
        /// <returns>The group item collections.</returns>
        private IEnumerable<IReadOnlyList<T>> EnumerateValues()
        {
            foreach (var value in _groups.Values)
            {
                yield return value;
            }
        }

        /// <summary>Enumerates the groups using the public read-only value type.</summary>
        /// <returns>The key and read-only item collection for each group.</returns>
        private IEnumerable<KeyValuePair<TKey, IReadOnlyList<T>>> EnumerateGroups()
        {
            foreach (var group in _groups)
            {
                yield return new(group.Key, group.Value);
            }
        }

        /// <summary>Forwards collection changes from the mutable collection.</summary>
        /// <param name="sender">The originating collection.</param>
        /// <param name="eventArgs">The collection change event data.</param>
        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs) =>
            CollectionChanged?.Invoke(sender, eventArgs);
    }
}
