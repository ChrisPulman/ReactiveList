// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER
using System;
using CP.Reactive.Core;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Tests for CacheAction enum.
/// </summary>
public class CacheActionTests
{
    /// <summary>
    /// CacheAction should have correct values.
    /// </summary>
    [Fact]
    public void CacheAction_ShouldHaveCorrectValues()
    {
        ((int)CacheAction.Added).Should().Be(0);
        ((int)CacheAction.Removed).Should().Be(1);
        ((int)CacheAction.Updated).Should().Be(2);
        ((int)CacheAction.Moved).Should().Be(3);
        ((int)CacheAction.Refreshed).Should().Be(4);
        ((int)CacheAction.Cleared).Should().Be(5);
        ((int)CacheAction.BatchOperation).Should().Be(6);
        ((int)CacheAction.BatchAdded).Should().Be(7);
        ((int)CacheAction.BatchRemoved).Should().Be(8);
    }

    /// <summary>
    /// All CacheAction values should be defined.
    /// </summary>
    [Fact]
    public void CacheAction_AllValuesShouldBeDefined()
    {
        var values = Enum.GetValues<CacheAction>();

        values.Should().HaveCount(9);
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
