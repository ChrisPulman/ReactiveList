// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace CP.Reactive;

/// <summary>
/// Represents a group of items with a key for use in grouped views.
/// </summary>
/// <typeparam name="TKey">The type of the grouping key.</typeparam>
/// <typeparam name="T">The type of elements in the group.</typeparam>
public sealed class ReactiveGroup<TKey, T> : IGrouping<TKey, T>, INotifyCollectionChanged
    where TKey : notnull
{
    private readonly ObservableCollection<T> _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveGroup{TKey, T}"/> class.
    /// </summary>
    /// <param name="key">The group key.</param>
    /// <param name="items">The items in the group.</param>
    public ReactiveGroup(TKey key, ObservableCollection<T> items)
    {
        Key = key;
        _items = items;
        Items = new ReadOnlyObservableCollection<T>(_items);
        _items.CollectionChanged += (s, e) => CollectionChanged?.Invoke(this, e);
    }

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>
    /// Gets the group key.
    /// </summary>
    public TKey Key { get; }

    /// <summary>
    /// Gets the number of items in the group.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets the items in the group for UI binding.
    /// </summary>
    public ReadOnlyObservableCollection<T> Items { get; }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
#endif
