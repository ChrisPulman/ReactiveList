// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

namespace CP.Reactive;

/// <summary>
/// Specifies the type of action performed on a cache entry.
/// </summary>
/// <remarks>Use this enumeration to indicate the nature of a cache operation, such as when an entry is added,
/// removed, updated, cleared, evicted, or when a batch operation is performed. This can be useful for event handling,
/// logging, or auditing cache changes.</remarks>
public enum CacheAction
{
    /// <summary>
    /// Gets or sets the date and time when the entity was added.
    /// </summary>
    Added,
    /// <summary>
    /// Gets or sets a value indicating whether the item has been removed.
    /// </summary>
    Removed,
    /// <summary>
    /// Gets or sets the date and time when the entity was last updated.
    /// </summary>
    Updated,
    /// <summary>
    /// Indicates that the item has been cleared and is no longer in use.
    /// </summary>
    Cleared,
    /// <summary>
    /// Represents an operation that can be executed as part of a batch process.
    /// </summary>
    /// <remarks>Use this type to group multiple operations together for collective execution, which can
    /// improve performance and ensure consistency. The specific behavior and requirements of a batch operation depend
    /// on the implementation.</remarks>
    BatchOperation
}
#endif
