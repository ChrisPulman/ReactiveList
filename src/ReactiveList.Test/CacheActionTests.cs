// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER || NETFRAMEWORK
using System;
#if NETFRAMEWORK
using System.Linq;
#endif
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
        ((int)CacheAction.Added).Should().Be(0);
        ((int)CacheAction.Removed).Should().Be(1);
        ((int)CacheAction.Updated).Should().Be(UpdatedActionValue);
        ((int)CacheAction.Moved).Should().Be(MovedActionValue);
        ((int)CacheAction.Refreshed).Should().Be(RefreshedActionValue);
        ((int)CacheAction.Cleared).Should().Be(ClearedActionValue);
        ((int)CacheAction.BatchOperation).Should().Be(BatchOperationActionValue);
        ((int)CacheAction.BatchAdded).Should().Be(BatchAddedActionValue);
        ((int)CacheAction.BatchRemoved).Should().Be(BatchRemovedActionValue);
    }

    /// <summary>All CacheAction values should be defined.</summary>
    [Test]
    public void CacheAction_AllValuesShouldBeDefined()
    {
        CacheAction[] values =
#if NET6_0_OR_GREATER
            Enum.GetValues<CacheAction>();
#else
            Enum.GetValues(typeof(CacheAction)).Cast<CacheAction>().ToArray();
#endif

        values.Should().HaveCount(DefinedActionCount);
        values.Should().Contain(CacheAction.Added);
        values.Should().Contain(CacheAction.Removed);
        values.Should().Contain(CacheAction.Updated);
        values.Should().Contain(CacheAction.Moved);
        values.Should().Contain(CacheAction.Refreshed);
        values.Should().Contain(CacheAction.Cleared);
        values.Should().Contain(CacheAction.BatchOperation);
        values.Should().Contain(CacheAction.BatchAdded);
        values.Should().Contain(CacheAction.BatchRemoved);
    }
}
#endif
