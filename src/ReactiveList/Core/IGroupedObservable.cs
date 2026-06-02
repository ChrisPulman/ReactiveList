// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CP.Reactive.Core;

/// <summary>
/// Represents an observable sequence that has an associated grouping key.
/// </summary>
/// <typeparam name="TKey">The type of the grouping key.</typeparam>
/// <typeparam name="TElement">The type of elements emitted by the group.</typeparam>
public interface IGroupedObservable<out TKey, out TElement> : IObservable<TElement>
{
    /// <summary>
    /// Gets the key shared by the elements in this group.
    /// </summary>
    TKey Key { get; }
}
