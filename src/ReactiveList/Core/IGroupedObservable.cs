// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Core;
#else
namespace CP.Primitives.Core;
#endif
/// <summary>Represents an observable sequence that has an associated grouping key.</summary>
/// <typeparam name="TKey">The type of the grouping key.</typeparam>
/// <typeparam name="TElement">The type of elements emitted by the group.</typeparam>
public interface IGroupedObservable<out TKey, out TElement> : IObservable<TElement>
{
    /// <summary>Gets the key shared by the elements in this group.</summary>
    TKey Key { get; }
}
