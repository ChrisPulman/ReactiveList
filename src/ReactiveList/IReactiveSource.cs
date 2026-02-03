// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Specialized;
using System.Reactive.Disposables;

namespace CP.Reactive;

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
    /// Connects to the change stream. Similar to DynamicData's Connect().
    /// </summary>
    /// <returns>An observable stream of change sets.</returns>
    IObservable<ChangeSet<T>> Connect();

#if NET6_0_OR_GREATER
    /// <summary>
    /// Creates a snapshot of current items as an array.
    /// </summary>
    /// <returns>An array containing all current items.</returns>
    T[] ToArray();
#endif
}
