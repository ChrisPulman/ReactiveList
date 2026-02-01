// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Collections.Specialized;
using System.Reactive.Disposables;

namespace CP.Reactive;

/// <summary>
/// Represents a quaternary collection that provides an observable stream of cache notifications
/// and can be enumerated to get a snapshot of items.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public interface IQuaternarySource<T> : IEnumerable<T>, ICancelable, INotifyCollectionChanged
    where T : notnull
{
    /// <summary>
    /// Gets an observable sequence that emits cache change notifications as they occur.
    /// </summary>
    IObservable<CacheNotify<T>> Stream { get; }

    /// <summary>
    /// Gets the number of elements contained in the collection.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets a value indicating whether the collection is read-only.
    /// </summary>
    bool IsReadOnly { get; }
}
#endif
