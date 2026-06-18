// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Collections;
#else
namespace CP.Primitives.Collections;
#endif
/// <summary>Defines a contract for a high-performance shard container used within quaternary collections.</summary>
/// <remarks>
/// IQuad represents an internal shard that stores elements. It is optimized for fast add/remove
/// operations and is not intended to emit its own change notifications. Change tracking is handled
/// by the parent QuaternaryBase collection through its Stream pipeline.
/// </remarks>
/// <typeparam name="T">The type of each element in the shard.</typeparam>
public interface IQuad<T> : IEnumerable<T>, IDisposable
{
    /// <summary>Gets the number of elements contained in the shard.</summary>
    int Count { get; }

    /// <summary>Removes all items from the shard.</summary>
    /// <remarks>After calling this method, the shard will be empty. This method does not modify the
    /// capacity of the shard, if applicable.</remarks>
    void Clear();
}
