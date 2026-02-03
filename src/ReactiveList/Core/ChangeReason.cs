// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CP.Reactive.Core;

/// <summary>
/// Specifies the reason for a change in a reactive collection.
/// </summary>
/// <remarks>
/// This enumeration provides a unified change reason type compatible with DynamicData patterns.
/// Use this to identify the type of modification that occurred in a collection.
/// </remarks>
public enum ChangeReason
{
    /// <summary>
    /// An item was added to the collection.
    /// </summary>
    Add,

    /// <summary>
    /// An item was removed from the collection.
    /// </summary>
    Remove,

    /// <summary>
    /// An item was updated/replaced in the collection.
    /// </summary>
    Update,

    /// <summary>
    /// An item was moved to a different position in the collection.
    /// </summary>
    Move,

    /// <summary>
    /// An item was refreshed (re-evaluated without modification).
    /// </summary>
    Refresh,

    /// <summary>
    /// The collection was cleared of all items.
    /// </summary>
    Clear
}
