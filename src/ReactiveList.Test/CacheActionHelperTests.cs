// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Core;
using CP.Reactive.Internal;
#else
using CP.Primitives.Core;
using CP.Primitives.Internal;
#endif

namespace ReactiveList.Test;

/// <summary>Verifies the actions that invalidate secondary-index projections.</summary>
public class CacheActionHelperTests
{
    /// <summary>Verifies that rebuild decisions distinguish structural batches from individual edits.</summary>
    /// <param name="action">The action reported by the source.</param>
    /// <param name="expected">Whether the index projection must be rebuilt.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(CacheAction.Added, false)]
    [Arguments(CacheAction.Removed, false)]
    [Arguments(CacheAction.Updated, false)]
    [Arguments(CacheAction.Cleared, false)]
    [Arguments(CacheAction.Moved, true)]
    [Arguments(CacheAction.Refreshed, true)]
    [Arguments(CacheAction.BatchOperation, true)]
    [Arguments(CacheAction.BatchAdded, true)]
    [Arguments(CacheAction.BatchRemoved, true)]
    [Arguments((CacheAction)(-1), false)]
    [Arguments((CacheAction)99, false)]
    public async Task RequiresIndexRebuild_Action_ReturnsExpectedDecision(CacheAction action, bool expected) =>
        await TUnit.Assertions.Assert.That(CacheActionHelper.RequiresIndexRebuild(action)).IsEqualTo(expected);
}
