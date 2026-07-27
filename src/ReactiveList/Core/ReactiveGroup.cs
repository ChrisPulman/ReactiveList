// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
using CP.Reactive.Internal;

namespace CP.Reactive.Core;
#else
using CP.Primitives.Internal;

namespace CP.Primitives.Core;
#endif
/// <summary>Represents a group of items with a key for use in grouped views.</summary>
/// <typeparam name="TKey">The type of the grouping key.</typeparam>
/// <typeparam name="T">The type of elements in the group.</typeparam>
public sealed class ReactiveGroup<TKey, T> : IGrouping<TKey, T>, INotifyCollectionChanged, INotifyPropertyChanged
    where TKey : notnull
{
    /// <summary>Synchronizes collection-changed subscriptions to the facade.</summary>
    private readonly Lock _collectionChangedGate = new();

    /// <summary>Synchronizes property-changed subscriptions to the facade.</summary>
    private readonly Lock _propertyChangedGate = new();

    /// <summary>Owns the observable collection and its source subscription.</summary>
    private readonly State _state;

    /// <summary>Relays collection notifications through this facade when it has subscribers.</summary>
    private NotificationRelay<NotifyCollectionChangedEventArgs>? _collectionChangedRelay;

    /// <summary>Relays property notifications through this facade when it has subscribers.</summary>
    private NotificationRelay<PropertyChangedEventArgs>? _propertyChangedRelay;

    /// <summary>Initializes a new instance of the <see cref="ReactiveGroup{TKey, T}"/> class.</summary>
    /// <param name="key">The group key.</param>
    /// <param name="items">The items in the group.</param>
    public ReactiveGroup(TKey key, ObservableCollection<T> items)
    {
        Key = key;
        _state = new(items);
        Items = _state.ReadOnlyItems;
        _state.Activate();
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

    /// <summary>Gets the group key.</summary>
    public TKey Key { get; }

    /// <summary>Gets the number of items in the group.</summary>
    public int Count => _state.Items.Count;

    /// <summary>Gets the items in the group for UI binding.</summary>
    public ReadOnlyObservableCollection<T> Items { get; }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _state.Items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Owns source notifications independently of the public facade.</summary>
    private sealed class State
    {
        /// <summary>Initializes a new instance of the <see cref="State"/> class.</summary>
        /// <param name="items">The items in the group.</param>
        internal State(ObservableCollection<T> items)
        {
            Items = items;
            ReadOnlyItems = new(items);
        }

        /// <summary>Raised when the source collection changes.</summary>
        internal event EventHandler<NotifyCollectionChangedEventArgs>? CollectionChanged;

        /// <summary>Raised when a facade property changes.</summary>
        internal event EventHandler<PropertyChangedEventArgs>? PropertyChanged;

        /// <summary>Gets the mutable source items.</summary>
        internal ObservableCollection<T> Items { get; }

        /// <summary>Gets the read-only source projection.</summary>
        internal ReadOnlyObservableCollection<T> ReadOnlyItems { get; }

        /// <summary>Subscribes this fully constructed state to its source.</summary>
        internal void Activate() => Items.CollectionChanged += OnCollectionChanged;

        /// <summary>Forwards a source collection notification to the active facade relays.</summary>
        /// <param name="sender">The source collection.</param>
        /// <param name="eventArgs">The collection change details.</param>
        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
        {
            PropertyChanged?.Invoke(sender, new(nameof(Count)));
            PropertyChanged?.Invoke(sender, new("Item[]"));
            CollectionChanged?.Invoke(sender, eventArgs);
        }
    }
}
