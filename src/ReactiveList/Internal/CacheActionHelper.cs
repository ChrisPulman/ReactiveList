// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;
#else
namespace CP.Primitives.Internal;
#endif

/// <summary>Identifies cache actions that require rebuilding secondary-index projections.</summary>
internal static class CacheActionHelper
{
    /// <summary>Determines whether an action invalidates a secondary-index projection.</summary>
    /// <param name="action">The cache action to inspect.</param>
    /// <returns>Whether the projection must be rebuilt from its source.</returns>
    internal static bool RequiresIndexRebuild(CacheAction action) =>
        action is CacheAction.Moved or CacheAction.Refreshed or CacheAction.BatchOperation or CacheAction.BatchAdded or CacheAction.BatchRemoved;
}
