// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Specialized;
using System.Reactive.Disposables;
using CP.Reactive.Core;

namespace CP.Reactive.Collections;

/// <summary>
/// Represents a quaternary collection that provides an observable stream of cache notifications
/// and can be enumerated to get a snapshot of items.
/// </summary>
/// <remarks>
/// The <see cref="Stream"/> property is the primary observable for receiving change notifications.
/// It provides fine-grained notifications including single item and batch operations through
/// the <see cref="CacheNotify{T}"/> type. The collection also implements <see cref="INotifyCollectionChanged"/>
/// for compatibility with WPF and other UI frameworks.
/// </remarks>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public interface IQuaternarySource<T> : IEnumerable<T>, ICancelable, INotifyCollectionChanged
    where T : notnull
{
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
    /// </remarks>
    IObservable<CacheNotify<T>> Stream { get; }

    /// <summary>
    /// Gets the number of elements contained in the collection.
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
}
