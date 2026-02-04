// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CP.Reactive.Collections;

/// <summary>
/// Defines a contract for a high-performance shard container used within quaternary collections.
/// </summary>
/// <remarks>
/// IQuad represents an internal shard that stores elements. It is optimized for fast add/remove
/// operations and is not intended to emit its own change notifications. Change tracking is handled
/// by the parent QuaternaryBase collection through its Stream pipeline.
/// </remarks>
/// <typeparam name="T">The type of each element in the shard.</typeparam>
public interface IQuad<T>
{
    /// <summary>
    /// Gets the number of elements contained in the shard.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Removes all items from the shard.
    /// </summary>
    /// <remarks>After calling this method, the shard will be empty. This method does not modify the
    /// capacity of the shard, if applicable.</remarks>
    void Clear();
}
