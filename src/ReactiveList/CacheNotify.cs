// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET6_0_OR_GREATER

namespace CP.Reactive;

/// <summary>
/// Represents a notification describing a cache action and the associated item or batch of items.
/// </summary>
/// <remarks>Use this record to convey information about cache changes, such as additions, removals, or updates,
/// along with the relevant item or group of items. Only one of <paramref name="Item"/> or <paramref name="Batch"/> is
/// typically populated for a given notification.</remarks>
/// <typeparam name="T">The type of the items contained in the cache notification.</typeparam>
/// <param name="Action">The cache action that triggered the notification.</param>
/// <param name="Item">The item associated with the cache action, or null if the action applies to a batch.</param>
/// <param name="Batch">An optional batch of items associated with the cache action. This value is null if the action pertains to a single
/// item.</param>
public record CacheNotify<T>(CacheAction Action, T? Item, PooledBatch<T>? Batch = null);
#endif
