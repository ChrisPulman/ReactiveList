// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CP.Reactive;

/// <summary>
/// Defines a contract for objects that can be reset to their initial state for reuse.
/// </summary>
/// <remarks>
/// This interface is typically used for pooled objects that need to be cleaned
/// before being returned to a pool for reuse, reducing allocations.
/// </remarks>
public interface IResettable
{
    /// <summary>
    /// Resets the object to its initial state, clearing all data and references.
    /// </summary>
    void Reset();
}
