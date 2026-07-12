// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using CP.Primitives.Core;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for CacheNotify record.</summary>
public class CacheNotifyTests
{
    /// <summary>The batch capacity.</summary>
    private const int BatchCapacity = 10;

    /// <summary>The batch item count.</summary>
    private const int BatchItemCount = 2;

    /// <summary>The notification item value.</summary>
    private const int NotificationItemValue = 42;

    /// <summary>Constructor should initialize Action and Item.</summary>
    [Test]
    public void Constructor_WithActionAndItem_ShouldInitialize()
    {
        var notify = new CacheNotify<string>(CacheAction.Added, "test");

        notify.Action.Should().Be(CacheAction.Added);
        notify.Item.Should().Be("test");
        notify.Batch.Should().BeNull();
    }

    /// <summary>Constructor should initialize with batch.</summary>
    [Test]
    public void Constructor_WithBatch_ShouldInitialize()
    {
        var array = ArrayPool<int>.Shared.Rent(BatchCapacity);
        array[0] = 1;
        array[1] = BatchItemCount;
        var batch = new PooledBatch<int>(array, BatchItemCount);

        var notify = new CacheNotify<int>(CacheAction.BatchOperation, default, batch);

        notify.Action.Should().Be(CacheAction.BatchOperation);
        notify.Item.Should().Be(default(int));
        notify.Batch.Should().BeSameAs(batch);

        batch.Dispose();
    }

    /// <summary>Constructor with null item should be valid.</summary>
    [Test]
    public void Constructor_WithNullItem_ShouldBeValid()
    {
        var notify = new CacheNotify<string>(CacheAction.Cleared, null);

        notify.Action.Should().Be(CacheAction.Cleared);
        notify.Item.Should().BeNull();
    }

    /// <summary>Record equality should work correctly.</summary>
    [Test]
    public void RecordEquality_ShouldWorkCorrectly()
    {
        var notify1 = new CacheNotify<string>(CacheAction.Added, "test");
        var notify2 = new CacheNotify<string>(CacheAction.Added, "test");

        notify1.Should().Be(notify2);
        (notify1 == notify2).Should().BeTrue();
    }

    /// <summary>Record inequality for different actions.</summary>
    [Test]
    public void RecordInequality_DifferentActions_ShouldNotBeEqual()
    {
        var notify1 = new CacheNotify<string>(CacheAction.Added, "test");
        var notify2 = new CacheNotify<string>(CacheAction.Removed, "test");

        notify1.Should().NotBe(notify2);
        (notify1 != notify2).Should().BeTrue();
    }

    /// <summary>Record inequality for different items.</summary>
    [Test]
    public void RecordInequality_DifferentItems_ShouldNotBeEqual()
    {
        var notify1 = new CacheNotify<string>(CacheAction.Added, "test1");
        var notify2 = new CacheNotify<string>(CacheAction.Added, "test2");

        notify1.Should().NotBe(notify2);
    }

    /// <summary>CacheNotify for Added action.</summary>
    [Test]
    public void CacheNotify_AddedAction_ShouldHaveCorrectState()
    {
        var notify = new CacheNotify<int>(CacheAction.Added, NotificationItemValue);

        notify.Action.Should().Be(CacheAction.Added);
        notify.Item.Should().Be(NotificationItemValue);
    }

    /// <summary>CacheNotify for Removed action.</summary>
    [Test]
    public void CacheNotify_RemovedAction_ShouldHaveCorrectState()
    {
        var notify = new CacheNotify<int>(CacheAction.Removed, NotificationItemValue);

        notify.Action.Should().Be(CacheAction.Removed);
        notify.Item.Should().Be(NotificationItemValue);
    }

    /// <summary>CacheNotify for Updated action.</summary>
    [Test]
    public void CacheNotify_UpdatedAction_ShouldHaveCorrectState()
    {
        var notify = new CacheNotify<string>(CacheAction.Updated, "updated");

        notify.Action.Should().Be(CacheAction.Updated);
        notify.Item.Should().Be("updated");
    }

    /// <summary>CacheNotify for Cleared action.</summary>
    [Test]
    public void CacheNotify_ClearedAction_ShouldHaveCorrectState()
    {
        var notify = new CacheNotify<string>(CacheAction.Cleared, null);

        notify.Action.Should().Be(CacheAction.Cleared);
        notify.Item.Should().BeNull();
    }

    /// <summary>CacheNotify for BatchOperation action.</summary>
    [Test]
    public void CacheNotify_BatchOperationAction_ShouldHaveCorrectState()
    {
        var array = ArrayPool<string>.Shared.Rent(BatchCapacity);
        array[0] = "item1";
        array[1] = "item2";
        var batch = new PooledBatch<string>(array, BatchItemCount);

        var notify = new CacheNotify<string>(CacheAction.BatchOperation, null, batch);

        notify.Action.Should().Be(CacheAction.BatchOperation);
        notify.Batch.Should().NotBeNull();
        notify.Batch!.Count.Should().Be(BatchItemCount);

        batch.Dispose();
    }

    /// <summary>CacheNotify with expression should work.</summary>
    [Test]
    public void CacheNotify_WithExpression_ShouldWork()
    {
        var notify1 = new CacheNotify<int>(CacheAction.Added, BatchCapacity);

        var notify2 = notify1 with { Action = CacheAction.Removed };

        notify2.Action.Should().Be(CacheAction.Removed);
        notify2.Item.Should().Be(BatchCapacity);
    }

    /// <summary>GetHashCode should be consistent.</summary>
    [Test]
    public void GetHashCode_ShouldBeConsistent()
    {
        var notify = new CacheNotify<string>(CacheAction.Added, "test");

        var hash1 = notify.GetHashCode();
        var hash2 = notify.GetHashCode();

        hash1.Should().Be(hash2);
    }

    /// <summary>ToString should return meaningful representation.</summary>
    [Test]
    public void ToString_ShouldReturnMeaningfulRepresentation()
    {
        var notify = new CacheNotify<string>(CacheAction.Added, "test");

        var result = notify.ToString();

        result.Should().Contain("CacheNotify");
        result.Should().Contain("Added");
        result.Should().Contain("test");
    }

    /// <summary>CacheNotify with value types.</summary>
    [Test]
    public void CacheNotify_WithValueTypes_ShouldWork()
    {
        var notify = new CacheNotify<DateTime>(CacheAction.Added, DateTime.Today);

        notify.Item.Should().Be(DateTime.Today);
    }

    /// <summary>CacheNotify with complex types.</summary>
    [Test]
    public void CacheNotify_WithComplexTypes_ShouldWork()
    {
        var person = new { Id = 1, Name = "John" };
        var notify = new CacheNotify<object>(CacheAction.Added, person);

        notify.Item.Should().Be(person);
    }
}
