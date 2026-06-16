// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.Reactive.Core;

/// <summary>Defines a contract for objects that can be reset to their initial state for reuse.</summary>
/// <remarks>
/// This interface is typically used for pooled objects that need to be cleaned
/// before being returned to a pool for reuse, reducing allocations.
/// </remarks>
public interface IResettable
{
    /// <summary>Resets the object to its initial state, clearing all data and references.</summary>
    void Reset();
}
