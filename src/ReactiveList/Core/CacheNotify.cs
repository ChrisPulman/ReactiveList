// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CP.Reactive.Core;

/// <summary>
/// Represents a notification describing a cache action and the associated item or batch of items.
/// </summary>
/// <param name="Action"> Gets the cache action that triggered the notification. </param>
/// <param name="Item"> Gets the item associated with the cache action, or default if the action applies to a batch. </param>
/// <param name="Batch"> Gets an optional batch of items associated with the cache action. </param>
/// <param name="CurrentIndex"> Gets the current index of the item in the collection, or -1 if not applicable. </param>
/// <param name="PreviousIndex"> Gets the previous index of the item in the collection, or -1 if not applicable. </param>
/// <param name="Previous"> Gets the previous item value (for update operations), or default if not applicable. </param>
/// <remarks>Use this type to convey information about cache changes, such as additions, removals, or updates,
/// along with the relevant item or group of items. Only one of <see cref="Item"/> or <see cref="Batch"/> is
/// typically populated for a given notification.</remarks>
/// <typeparam name="T">The type of the items contained in the cache notification.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="CacheNotify{T}"/> class.
/// </remarks>
public sealed record CacheNotify<T>(
    CacheAction Action,
    T? Item,
    PooledBatch<T>? Batch = null,
    int CurrentIndex = -1,
    int PreviousIndex = -1,
    T? Previous = default);
