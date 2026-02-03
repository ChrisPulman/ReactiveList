// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

namespace CP.Reactive.Quaternary;

/// <summary>
/// Represents a change to an item in a quaternary collection, including the reason for the change, the affected
/// item, and its index information.
/// </summary>
/// <typeparam name="T">The type of the item affected by the change.</typeparam>
/// <param name="Reason">The reason for the change, indicating the type of operation performed on the collection.</param>
/// <param name="Item">The item that was added, removed, moved, or otherwise affected by the change.</param>
/// <param name="Index">The index in the collection where the change occurred. The value is -1 if the index is not applicable.</param>
/// <param name="OldIndex">The previous index of the item before the change occurred. The value is -1 if the old index is not applicable.</param>
public readonly record struct QuaternaryChange<T>(
QuaternaryChangeReason Reason,
T Item,
int Index = -1,
int OldIndex = -1);
#endif
