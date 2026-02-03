// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER
using System;
using System.Buffers;
using CP.Reactive.Quaternary;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Tests for CacheNotify record.
/// </summary>
public class CacheNotifyTests
{
    /// <summary>
    /// Constructor should initialize Action and Item.
    /// </summary>
    [Fact]
    public void Constructor_WithActionAndItem_ShouldInitialize()
    {
        var notify = new CacheNotify<string>(CacheAction.Added, "test");

        notify.Action.Should().Be(CacheAction.Added);
        notify.Item.Should().Be("test");
        notify.Batch.Should().BeNull();
    }

    /// <summary>
    /// Constructor should initialize with batch.
    /// </summary>
    [Fact]
    public void Constructor_WithBatch_ShouldInitialize()
    {
        var array = ArrayPool<int>.Shared.Rent(10);
        array[0] = 1;
        array[1] = 2;
        var batch = new PooledBatch<int>(array, 2);

        var notify = new CacheNotify<int>(CacheAction.BatchOperation, default, batch);

        notify.Action.Should().Be(CacheAction.BatchOperation);
        notify.Item.Should().Be(default(int));
        notify.Batch.Should().BeSameAs(batch);

        batch.Dispose();
    }

    /// <summary>
    /// Constructor with null item should be valid.
    /// </summary>
    [Fact]
    public void Constructor_WithNullItem_ShouldBeValid()
    {
        var notify = new CacheNotify<string>(CacheAction.Cleared, null);

        notify.Action.Should().Be(CacheAction.Cleared);
        notify.Item.Should().BeNull();
    }

    /// <summary>
    /// Record equality should work correctly.
    /// </summary>
    [Fact]
    public void RecordEquality_ShouldWorkCorrectly()
    {
        var notify1 = new CacheNotify<string>(CacheAction.Added, "test");
        var notify2 = new CacheNotify<string>(CacheAction.Added, "test");

        notify1.Should().Be(notify2);
        (notify1 == notify2).Should().BeTrue();
    }

    /// <summary>
    /// Record inequality for different actions.
    /// </summary>
    [Fact]
    public void RecordInequality_DifferentActions_ShouldNotBeEqual()
    {
        var notify1 = new CacheNotify<string>(CacheAction.Added, "test");
        var notify2 = new CacheNotify<string>(CacheAction.Removed, "test");

        notify1.Should().NotBe(notify2);
        (notify1 != notify2).Should().BeTrue();
    }

    /// <summary>
    /// Record inequality for different items.
    /// </summary>
    [Fact]
    public void RecordInequality_DifferentItems_ShouldNotBeEqual()
    {
        var notify1 = new CacheNotify<string>(CacheAction.Added, "test1");
        var notify2 = new CacheNotify<string>(CacheAction.Added, "test2");

        notify1.Should().NotBe(notify2);
    }

    /// <summary>
    /// CacheNotify for Added action.
    /// </summary>
    [Fact]
    public void CacheNotify_AddedAction_ShouldHaveCorrectState()
    {
        var notify = new CacheNotify<int>(CacheAction.Added, 42);

        notify.Action.Should().Be(CacheAction.Added);
        notify.Item.Should().Be(42);
    }

    /// <summary>
    /// CacheNotify for Removed action.
    /// </summary>
    [Fact]
    public void CacheNotify_RemovedAction_ShouldHaveCorrectState()
    {
        var notify = new CacheNotify<int>(CacheAction.Removed, 42);

        notify.Action.Should().Be(CacheAction.Removed);
        notify.Item.Should().Be(42);
    }

    /// <summary>
    /// CacheNotify for Updated action.
    /// </summary>
    [Fact]
    public void CacheNotify_UpdatedAction_ShouldHaveCorrectState()
    {
        var notify = new CacheNotify<string>(CacheAction.Updated, "updated");

        notify.Action.Should().Be(CacheAction.Updated);
        notify.Item.Should().Be("updated");
    }

    /// <summary>
    /// CacheNotify for Cleared action.
    /// </summary>
    [Fact]
    public void CacheNotify_ClearedAction_ShouldHaveCorrectState()
    {
        var notify = new CacheNotify<string>(CacheAction.Cleared, null);

        notify.Action.Should().Be(CacheAction.Cleared);
        notify.Item.Should().BeNull();
    }

    /// <summary>
    /// CacheNotify for BatchOperation action.
    /// </summary>
    [Fact]
    public void CacheNotify_BatchOperationAction_ShouldHaveCorrectState()
    {
        var array = ArrayPool<string>.Shared.Rent(10);
        array[0] = "item1";
        array[1] = "item2";
        var batch = new PooledBatch<string>(array, 2);

        var notify = new CacheNotify<string>(CacheAction.BatchOperation, null, batch);

        notify.Action.Should().Be(CacheAction.BatchOperation);
        notify.Batch.Should().NotBeNull();
        notify.Batch!.Count.Should().Be(2);

        batch.Dispose();
    }

    /// <summary>
    /// CacheNotify with expression should work.
    /// </summary>
    [Fact]
    public void CacheNotify_WithExpression_ShouldWork()
    {
        var notify1 = new CacheNotify<int>(CacheAction.Added, 10);

        var notify2 = notify1 with { Action = CacheAction.Removed };

        notify2.Action.Should().Be(CacheAction.Removed);
        notify2.Item.Should().Be(10);
    }

    /// <summary>
    /// GetHashCode should be consistent.
    /// </summary>
    [Fact]
    public void GetHashCode_ShouldBeConsistent()
    {
        var notify = new CacheNotify<string>(CacheAction.Added, "test");

        var hash1 = notify.GetHashCode();
        var hash2 = notify.GetHashCode();

        hash1.Should().Be(hash2);
    }

    /// <summary>
    /// ToString should return meaningful representation.
    /// </summary>
    [Fact]
    public void ToString_ShouldReturnMeaningfulRepresentation()
    {
        var notify = new CacheNotify<string>(CacheAction.Added, "test");

        var result = notify.ToString();

        result.Should().Contain("CacheNotify");
        result.Should().Contain("Added");
        result.Should().Contain("test");
    }

    /// <summary>
    /// CacheNotify with value types.
    /// </summary>
    [Fact]
    public void CacheNotify_WithValueTypes_ShouldWork()
    {
        var notify = new CacheNotify<DateTime>(CacheAction.Added, DateTime.Today);

        notify.Item.Should().Be(DateTime.Today);
    }

    /// <summary>
    /// CacheNotify with complex types.
    /// </summary>
    [Fact]
    public void CacheNotify_WithComplexTypes_ShouldWork()
    {
        var person = new { Id = 1, Name = "John" };
        var notify = new CacheNotify<object>(CacheAction.Added, person);

        notify.Item.Should().Be(person);
    }
}
#endif
