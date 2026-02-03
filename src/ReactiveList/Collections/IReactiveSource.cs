// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Specialized;
using System.Reactive.Disposables;
using CP.Reactive.Core;

namespace CP.Reactive.Collections;

/// <summary>
/// Base interface for all reactive collections providing change observation.
/// </summary>
/// <remarks>
/// This interface provides a unified API for reactive collections, compatible with DynamicData patterns.
/// All implementations should support change tracking via IObservable streams.
/// </remarks>
/// <typeparam name="T">The type of elements in the collection. Must be non-nullable.</typeparam>
public interface IReactiveSource<T> : IEnumerable<T>, INotifyCollectionChanged, ICancelable
    where T : notnull
{
    /// <summary>
    /// Gets the count of items in the collection.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets a value indicating whether the collection is read-only.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Gets the current version number of the collection, which is incremented on each modification.
    /// </summary>
    /// <remarks>This property can be used for efficient change detection without subscribing to notifications.
    /// The version is incremented atomically.</remarks>
    long Version { get; }

    /// <summary>
    /// Gets an observable sequence that emits cache change notifications as they occur.
    /// </summary>
    /// <remarks>
    /// This is the primary observable for change notifications. Subscribe to receive:
    /// <list type="bullet">
    /// <item><description>Single item changes (Added, Removed, Updated, Moved, Refreshed)</description></item>
    /// <item><description>Batch operations (BatchAdded, BatchRemoved, BatchOperation)</description></item>
    /// <item><description>Clear operations (Cleared)</description></item>
    /// </list>
    /// The Stream uses a channel-based pipeline for efficient, low-allocation event delivery.
    /// </remarks>
    IObservable<CacheNotify<T>> Stream { get; }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Creates a snapshot of current items as an array.
    /// </summary>
    /// <returns>An array containing all current items.</returns>
    T[] ToArray();
#endif
}
