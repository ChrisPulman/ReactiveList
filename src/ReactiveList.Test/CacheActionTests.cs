// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER || NETFRAMEWORK
using System;
using CP.Primitives.Core;
using FluentAssertions;
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
    [Test]
    public void CacheAction_ShouldHaveCorrectValues()
    {
        _ = ((int)CacheAction.Added).Should().Be(0);
        _ = ((int)CacheAction.Removed).Should().Be(1);
        _ = ((int)CacheAction.Updated).Should().Be(UpdatedActionValue);
        _ = ((int)CacheAction.Moved).Should().Be(MovedActionValue);
        _ = ((int)CacheAction.Refreshed).Should().Be(RefreshedActionValue);
        _ = ((int)CacheAction.Cleared).Should().Be(ClearedActionValue);
        _ = ((int)CacheAction.BatchOperation).Should().Be(BatchOperationActionValue);
        _ = ((int)CacheAction.BatchAdded).Should().Be(BatchAddedActionValue);
        _ = ((int)CacheAction.BatchRemoved).Should().Be(BatchRemovedActionValue);
    }

    /// <summary>All CacheAction values should be defined.</summary>
    [Test]
    public void CacheAction_AllValuesShouldBeDefined()
    {
        CacheAction[] values =
#if NET6_0_OR_GREATER
            Enum.GetValues<CacheAction>();
#else
            CreateCacheActionValues();
#endif

        _ = values.Should().HaveCount(DefinedActionCount);
        _ = values.Should().Contain(CacheAction.Added);
        _ = values.Should().Contain(CacheAction.Removed);
        _ = values.Should().Contain(CacheAction.Updated);
        _ = values.Should().Contain(CacheAction.Moved);
        _ = values.Should().Contain(CacheAction.Refreshed);
        _ = values.Should().Contain(CacheAction.Cleared);
        _ = values.Should().Contain(CacheAction.BatchOperation);
        _ = values.Should().Contain(CacheAction.BatchAdded);
        _ = values.Should().Contain(CacheAction.BatchRemoved);
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
