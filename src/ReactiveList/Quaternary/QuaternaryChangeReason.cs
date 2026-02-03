// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

namespace CP.Reactive.Quaternary;

/// <summary>
/// Specifies the reason for a change in a quaternary data structure or collection.
/// </summary>
/// <remarks>Use this enumeration to indicate the type of operation that caused a change, such as adding,
/// removing, updating, moving, refreshing, clearing, or batching items. This can be useful for event handlers or
/// observers that need to respond differently based on the nature of the change.</remarks>
public enum QuaternaryChangeReason
{
    /// <summary>
    /// Adds an item to the collection.
    /// </summary>
    Add,
    /// <summary>
    /// Removes the specified element from the collection.
    /// </summary>
    Remove,
    /// <summary>
    /// Represents an operation that updates the current state or data to reflect recent changes.
    /// </summary>
    Update,
    /// <summary>
    /// Represents a move in the context of a game or operation.
    /// </summary>
    Move,
    /// <summary>
    /// Refreshes the current state or data, updating it to reflect the latest information.
    /// </summary>
    Refresh,
    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    Clear,
    /// <summary>
    /// Represents a collection of items or operations that are processed together as a single unit.
    /// </summary>
    Batch
}
#endif
