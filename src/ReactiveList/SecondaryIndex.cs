// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET6_0_OR_GREATER

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CP.Reactive;

/// <summary>
/// Provides a thread-safe secondary index for associating items with keys derived from a selector function.
/// </summary>
/// <typeparam name="T">The type of items to be indexed.</typeparam>
/// <typeparam name="TKey">The type of the key used for indexing. Must be non-nullable.</typeparam>
/// <param name="selector">A function that extracts the secondary key from an item.</param>
public class SecondaryIndex<T, TKey>(Func<T, TKey> selector) : ISecondaryIndex<T>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, HashSet<T>>[] _shards =
    [
        new ConcurrentDictionary<TKey, HashSet<T>>(),
        new ConcurrentDictionary<TKey, HashSet<T>>(),
        new ConcurrentDictionary<TKey, HashSet<T>>(),
        new ConcurrentDictionary<TKey, HashSet<T>>()
    ];

    /// <summary>
    /// Adds the specified item to the collection.
    /// </summary>
    /// <param name="item">The item to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnAdded(T item)
    {
        var key = selector(item);
        var s = GetShardIndex(key);

        _shards[s].AddOrUpdate(
            key,
            addValueFactory: static (_, newItem) => [newItem],
            updateValueFactory: static (_, set, newItem) =>
            {
                lock (set)
                {
                    set.Add(newItem);
                }

                return set;
            },
            factoryArgument: item);
    }

    /// <summary>
    /// Removes the specified item from the collection.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnRemoved(T item)
    {
        var key = selector(item);
        var s = GetShardIndex(key);

        if (_shards[s].TryGetValue(key, out var set))
        {
            lock (set)
            {
                set.Remove(item);
                if (set.Count == 0)
                {
                    _shards[s].TryRemove(key, out _);
                }
            }
        }
    }

    /// <summary>
    /// Handles the update of an item.
    /// </summary>
    /// <param name="oldItem">The old item to remove.</param>
    /// <param name="newItem">The new item to add.</param>
    public void OnUpdated(T oldItem, T newItem)
    {
        OnRemoved(oldItem);
        OnAdded(newItem);
    }

    /// <summary>
    /// Retrieves all values associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>An enumerable of items matching the key.</returns>
    public IEnumerable<T> Lookup(TKey key)
    {
        var s = GetShardIndex(key);
        if (_shards[s].TryGetValue(key, out var set))
        {
            lock (set)
            {
                return [.. set];
            }
        }

        return [];
    }

    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    public void Clear()
    {
        for (var i = 0; i < 4; i++)
        {
            _shards[i].Clear();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetShardIndex(TKey item) => (item.GetHashCode() & 0x7FFFFFFF) % 4;
}
#endif
