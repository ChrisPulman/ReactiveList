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
/// <summary>Provides a sorted, read-only view over a <see cref="IReactiveList{T}"/> that automatically updates when the source list changes.</summary>
/// <typeparam name="T">The type of elements in the view.</typeparam>
public sealed class SortedReactiveView<T> : IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged, IReactiveView<SortedReactiveView<T>, T>
where T : notnull
{
    /// <summary>Synchronizes collection-changed subscriptions to this facade.</summary>
    private readonly Lock _collectionChangedGate = new();

    /// <summary>Synchronizes property-changed subscriptions to this facade.</summary>
    private readonly Lock _propertyChangedGate = new();

    /// <summary>The completed state object that owns sorting and source subscriptions.</summary>
    private readonly State _state;

    /// <summary>Relays collection notifications with this facade as the sender.</summary>
    private NotificationRelay<NotifyCollectionChangedEventArgs>? _collectionChangedRelay;

    /// <summary>Relays property notifications with this facade as the sender.</summary>
    private NotificationRelay<PropertyChangedEventArgs>? _propertyChangedRelay;

    /// <summary>Initializes a new instance of the <see cref="SortedReactiveView{T}"/> class.</summary>
    /// <param name="source">The source reactive list to sort.</param>
    /// <param name="comparer">The comparer for sorting items.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    public SortedReactiveView(
        IReactiveList<T> source,
        IComparer<T> comparer,
        ISequencer scheduler,
        TimeSpan throttle)
    {
        _state = new(source, comparer, scheduler, throttle);
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

    /// <summary>Gets the number of items in the sorted view.</summary>
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

    /// <summary>Forces a rebuild of the sorted view from the source.</summary>
    public void Refresh() => _state.Refresh();

    /// <summary>Assigns the current collection of items to a property using the specified setter action.</summary>
    /// <remarks>This method is typically used to bind the internal collection to an external property, such
    /// as a view model property, in a reactive UI pattern.</remarks>
    /// <param name="propertySetter">An action that sets a property to the current read-only observable collection of items. Cannot be null.</param>
    /// <returns>The current instance of <see cref="SortedReactiveView{T}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public SortedReactiveView<T> ToProperty(Action<ReadOnlyObservableCollection<T>> propertySetter)
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
    /// <returns>The current <see cref="SortedReactiveView{T}"/> instance.</returns>
    public SortedReactiveView<T> ToProperty(out ReadOnlyObservableCollection<T> collection)
    {
        collection = Items;
        return this;
    }

    /// <inheritdoc/>
    public void Dispose() => _state.Dispose();

    /// <summary>Owns the mutable sorted view after construction has completed.</summary>
    private sealed class State
    {
        /// <summary>The reactive list that supplies items to this view.</summary>
        private readonly IReactiveList<T> _source;

        /// <summary>The comparer used to order items in this view.</summary>
        private readonly IComparer<T> _comparer;

        /// <summary>The mutable collection that backs the read-only sorted items collection.</summary>
        private readonly ObservableCollection<T> _sortedItems = [];

        /// <summary>The scheduler used to dispatch source notifications.</summary>
        private readonly ISequencer _scheduler;

        /// <summary>The throttle applied to source notifications.</summary>
        private readonly TimeSpan _throttle;

        /// <summary>The subscriptions owned by this state.</summary>
        private readonly MultipleDisposable _disposables = [];

        /// <summary>Synchronizes access to the sorted items collection.</summary>
        private readonly Lock _lock = new();

        /// <summary>Initializes a new instance of the <see cref="State"/> class without publishing callbacks.</summary>
        /// <param name="source">The source reactive list to sort.</param>
        /// <param name="comparer">The comparer for sorting items.</param>
        /// <param name="scheduler">The scheduler for dispatching updates.</param>
        /// <param name="throttle">The throttle duration for updates.</param>
        internal State(
            IReactiveList<T> source,
            IComparer<T> comparer,
            ISequencer scheduler,
            TimeSpan throttle)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            _scheduler = scheduler;
            _throttle = throttle;
            Items = new(_sortedItems);
            RebuildView();
        }

        /// <summary>Raised when the sorted collection changes.</summary>
        internal event EventHandler<NotifyCollectionChangedEventArgs>? CollectionChanged;

        /// <summary>Raised when a state property changes.</summary>
        internal event EventHandler<PropertyChangedEventArgs>? PropertyChanged;

        /// <summary>Gets the number of sorted items.</summary>
        internal int Count => _sortedItems.Count;

        /// <summary>Gets the read-only observable sorted items.</summary>
        internal ReadOnlyObservableCollection<T> Items { get; }

        /// <summary>Gets the item at the specified index.</summary>
        /// <param name="index">The zero-based item index.</param>
        /// <returns>The sorted item.</returns>
        internal T GetItem(int index) => _sortedItems[index];

        /// <summary>Starts collection forwarding and source observation after state construction.</summary>
        internal void Start()
        {
            _sortedItems.CollectionChanged += OnCollectionChanged;
            var subscription = _source.Stream
                .ToChangeSets()
                .Throttle(_throttle)
                .ObserveOn(_scheduler)
                .Subscribe(OnSourceChanged);

            _disposables.Add(subscription);
        }

        /// <summary>Returns an enumerator over the sorted items.</summary>
        /// <returns>An enumerator over the sorted items.</returns>
        internal IEnumerator<T> GetEnumerator() => _sortedItems.GetEnumerator();

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
                var needsRebuild = false;

                for (var i = 0; i < changes.Count; i++)
                {
                    var change = changes[i];
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                            {
                                InsertSorted(change.Current);
                                break;
                            }

                        case ChangeReason.Remove:
                            {
                                _ = _sortedItems.Remove(change.Current);
                                break;
                            }

                        case ChangeReason.Update:
                            {
                                if (change.Previous is not null)
                                {
                                    _ = _sortedItems.Remove(change.Previous);
                                }

                                InsertSorted(change.Current);
                                break;
                            }

                        case ChangeReason.Clear:
                            {
                                _sortedItems.Clear();
                                break;
                            }

                        case ChangeReason.Move or ChangeReason.Refresh:
                            {
                                needsRebuild = true;
                                break;
                            }

                        default:
                            {
                                break;
                            }
                    }
                }

                if (needsRebuild)
                {
                    RebuildView();
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }

        /// <summary>Inserts data for the InsertSorted operation.</summary>
        /// <param name="item">The item value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InsertSorted(T item)
        {
            var index = BinarySearch(item);
            if (index < 0)
            {
                index = ~index;
            }

            _sortedItems.Insert(index, item);
        }

        /// <summary>Searches for the sorted insertion index.</summary>
        /// <param name="item">The item value.</param>
        /// <returns>The matching item index or the bitwise complement of the insertion index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int BinarySearch(T item)
        {
            var lo = 0;
            var hi = _sortedItems.Count - 1;

            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var comparison = _comparer.Compare(_sortedItems[mid], item);

                if (comparison == 0)
                {
                    return mid;
                }

                if (comparison < 0)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return ~lo;
        }

        /// <summary>Rebuilds the view from the current source state.</summary>
        private void RebuildView()
        {
            _sortedItems.Clear();
            var sourceItems = _source.Items;
            var count = sourceItems.Count;
            var sorted = new List<T>(count);
            for (var i = 0; i < count; i++)
            {
                sorted.Add(sourceItems[i]);
            }

            sorted.Sort(_comparer);
            for (var i = 0; i < sorted.Count; i++)
            {
                _sortedItems.Add(sorted[i]);
            }
        }

        /// <summary>Forwards collection changes from the mutable collection.</summary>
        /// <param name="sender">The originating collection.</param>
        /// <param name="eventArgs">The collection change event data.</param>
        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs) =>
            CollectionChanged?.Invoke(sender, eventArgs);
    }
}
