// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Core;
#else
using CP.Primitives.Core;
#endif
using TUnit.Assertions;
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
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_WithActionAndItem_ShouldInitialize()
    {
        var notify = new CacheNotify<string>(CacheAction.Added, "test");

        await Assert.That(notify.Action).IsEqualTo(CacheAction.Added);
        await Assert.That(notify.Item).IsEqualTo("test");
        await Assert.That(notify.Batch).IsNull();
    }

    /// <summary>Constructor should initialize with batch.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_WithBatch_ShouldInitialize()
    {
        var array = ArrayPool<int>.Shared.Rent(BatchCapacity);
        array[0] = 1;
        array[1] = BatchItemCount;
        var batch = new PooledBatch<int>(array, BatchItemCount);

        var notify = new CacheNotify<int>(CacheAction.BatchOperation, default, batch);

        await Assert.That(notify.Action).IsEqualTo(CacheAction.BatchOperation);
        await Assert.That(notify.Item).IsEqualTo(default(int));
        await Assert.That(notify.Batch).IsSameReferenceAs(batch);

        batch.Dispose();
    }

    /// <summary>Constructor with null item should be valid.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_WithNullItem_ShouldBeValid()
    {
        var notify = new CacheNotify<string>(CacheAction.Cleared, null);

        await Assert.That(notify.Action).IsEqualTo(CacheAction.Cleared);
        await Assert.That(notify.Item).IsNull();
    }

    /// <summary>Record equality should work correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RecordEquality_ShouldWorkCorrectly()
    {
        var notify1 = new CacheNotify<string>(CacheAction.Added, "test");
        var notify2 = new CacheNotify<string>(CacheAction.Added, "test");

        await Assert.That(notify1).IsEqualTo(notify2);
        await Assert.That(notify1 == notify2).IsTrue();
    }

    /// <summary>Record inequality for different actions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RecordInequality_DifferentActions_ShouldNotBeEqual()
    {
        var notify1 = new CacheNotify<string>(CacheAction.Added, "test");
        var notify2 = new CacheNotify<string>(CacheAction.Removed, "test");

        await Assert.That(notify1).IsNotEqualTo(notify2);
        await Assert.That(notify1 != notify2).IsTrue();
    }

    /// <summary>Record inequality for different items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RecordInequality_DifferentItems_ShouldNotBeEqual()
    {
        var notify1 = new CacheNotify<string>(CacheAction.Added, "test1");
        var notify2 = new CacheNotify<string>(CacheAction.Added, "test2");

        await Assert.That(notify1).IsNotEqualTo(notify2);
    }

    /// <summary>CacheNotify for Added action.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CacheNotify_AddedAction_ShouldHaveCorrectState()
    {
        var notify = new CacheNotify<int>(CacheAction.Added, NotificationItemValue);

        await Assert.That(notify.Action).IsEqualTo(CacheAction.Added);
        await Assert.That(notify.Item).IsEqualTo(NotificationItemValue);
    }

    /// <summary>CacheNotify for Removed action.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CacheNotify_RemovedAction_ShouldHaveCorrectState()
    {
        var notify = new CacheNotify<int>(CacheAction.Removed, NotificationItemValue);

        await Assert.That(notify.Action).IsEqualTo(CacheAction.Removed);
        await Assert.That(notify.Item).IsEqualTo(NotificationItemValue);
    }

    /// <summary>CacheNotify for Updated action.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CacheNotify_UpdatedAction_ShouldHaveCorrectState()
    {
        var notify = new CacheNotify<string>(CacheAction.Updated, "updated");

        await Assert.That(notify.Action).IsEqualTo(CacheAction.Updated);
        await Assert.That(notify.Item).IsEqualTo("updated");
    }

    /// <summary>CacheNotify for Cleared action.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CacheNotify_ClearedAction_ShouldHaveCorrectState()
    {
        var notify = new CacheNotify<string>(CacheAction.Cleared, null!);
        await Assert.That(notify.Action).IsEqualTo(CacheAction.Cleared);
        await Assert.That(notify.Item).IsNull();
    }

    /// <summary>CacheNotify for BatchOperation action.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CacheNotify_BatchOperationAction_ShouldHaveCorrectState()
    {
        var array = ArrayPool<string>.Shared.Rent(BatchCapacity);
        array[0] = "item1";
        array[1] = "item2";
        var batch = new PooledBatch<string>(array, BatchItemCount);

        var notify = new CacheNotify<string>(CacheAction.BatchOperation, null, batch);

        await Assert.That(notify.Action).IsEqualTo(CacheAction.BatchOperation);
        await Assert.That(notify.Batch).IsNotNull();
        await Assert.That(notify.Batch!.Count).IsEqualTo(BatchItemCount);

        batch.Dispose();
    }

    /// <summary>CacheNotify with expression should work.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CacheNotify_WithExpression_ShouldWork()
    {
        var notify1 = new CacheNotify<int>(CacheAction.Added, BatchCapacity);

        var notify2 = notify1 with { Action = CacheAction.Removed };

        await Assert.That(notify2.Action).IsEqualTo(CacheAction.Removed);
        await Assert.That(notify2.Item).IsEqualTo(BatchCapacity);
    }

    /// <summary>GetHashCode should be consistent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetHashCode_ShouldBeConsistent()
    {
        var notify = new CacheNotify<string>(CacheAction.Added, "test");

        var hash1 = notify.GetHashCode();
        var hash2 = notify.GetHashCode();

        await Assert.That(hash1).IsEqualTo(hash2);
    }

    /// <summary>ToString should return meaningful representation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ToString_ShouldReturnMeaningfulRepresentation()
    {
        var notify = new CacheNotify<string>(CacheAction.Added, "test");

        var result = notify.ToString();

        await Assert.That(result).Contains("CacheNotify");
        await Assert.That(result).Contains("Added");
        await Assert.That(result).Contains("test");
    }

    /// <summary>CacheNotify with value types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CacheNotify_WithValueTypes_ShouldWork()
    {
        var notify = new CacheNotify<DateTime>(CacheAction.Added, DateTime.Today);

        await Assert.That(notify.Item).IsEqualTo(DateTime.Today);
    }

    /// <summary>CacheNotify with complex types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CacheNotify_WithComplexTypes_ShouldWork()
    {
        var person = (Id: 1, Name: "John");
        var notify = new CacheNotify<object>(CacheAction.Added, person);

        await Assert.That(notify.Item).IsEqualTo(person);
    }
}
