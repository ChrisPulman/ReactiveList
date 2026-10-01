// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER || NETFRAMEWORK
using System;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Core;
#else
using CP.Primitives.Core;
#endif
using TUnit.Assertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for CacheAction enum.</summary>
public class CacheActionTests
{
    /// <summary>The updated action value.</summary>
    private const int UpdatedActionValue = 2;

    /// <summary>The moved action value.</summary>
    private const int MovedActionValue = 3;

    /// <summary>The refreshed action value.</summary>
    private const int RefreshedActionValue = 4;

    /// <summary>The cleared action value.</summary>
    private const int ClearedActionValue = 5;

    /// <summary>The batch operation action value.</summary>
    private const int BatchOperationActionValue = 6;

    /// <summary>The batch added action value.</summary>
    private const int BatchAddedActionValue = 7;

    /// <summary>The batch removed action value.</summary>
    private const int BatchRemovedActionValue = 8;

    /// <summary>The defined action count.</summary>
    private const int DefinedActionCount = 9;

    /// <summary>CacheAction should have correct values.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CacheAction_ShouldHaveCorrectValues()
    {
        CacheAction[] actions =
#if NET6_0_OR_GREATER
            Enum.GetValues<CacheAction>();
#else
            CreateCacheActionValues();
#endif
        var values = Array.ConvertAll(actions, static action => (int)action);
        await Assert.That(values).IsEquivalentTo(
            [0, 1, UpdatedActionValue, MovedActionValue, RefreshedActionValue, ClearedActionValue, BatchOperationActionValue, BatchAddedActionValue, BatchRemovedActionValue],
            CollectionOrdering.Matching);
    }

    /// <summary>All CacheAction values should be defined.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CacheAction_AllValuesShouldBeDefined()
    {
        CacheAction[] values =
#if NET6_0_OR_GREATER
            Enum.GetValues<CacheAction>();
#else
            CreateCacheActionValues();
#endif

        await Assert.That(values).Count().IsEqualTo(DefinedActionCount);
        await Assert.That(values).Contains(CacheAction.Added);
        await Assert.That(values).Contains(CacheAction.Removed);
        await Assert.That(values).Contains(CacheAction.Updated);
        await Assert.That(values).Contains(CacheAction.Moved);
        await Assert.That(values).Contains(CacheAction.Refreshed);
        await Assert.That(values).Contains(CacheAction.Cleared);
        await Assert.That(values).Contains(CacheAction.BatchOperation);
        await Assert.That(values).Contains(CacheAction.BatchAdded);
        await Assert.That(values).Contains(CacheAction.BatchRemoved);
    }

#if NETFRAMEWORK
    /// <summary>Gets the cache-action values on .NET Framework.</summary>
    /// <returns>The defined cache-action values.</returns>
    private static CacheAction[] CreateCacheActionValues()
    {
        var rawValues = Enum.GetValues(typeof(CacheAction));
        var values = new CacheAction[rawValues.Length];
        for (var index = 0; index < rawValues.Length; index++)
        {
            values[index] = (CacheAction)rawValues.GetValue(index)!;
        }

        return values;
    }
#endif
}
#endif
