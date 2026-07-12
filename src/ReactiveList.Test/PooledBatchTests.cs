// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using CP.Primitives.Core;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for PooledBatch.</summary>
public class PooledBatchTests
{
    /// <summary>The pair count.</summary>
    private const int PairCount = 2;

    /// <summary>The triple count.</summary>
    private const int TripleCount = 3;

    /// <summary>The default batch count.</summary>
    private const int DefaultBatchCount = 5;

    /// <summary>The updated batch count.</summary>
    private const int UpdatedBatchCount = 8;

    /// <summary>The array capacity.</summary>
    private const int ArrayCapacity = 10;

    /// <summary>The arbitrary batch count.</summary>
    private const int ArbitraryBatchCount = 42;

    /// <summary>The large array capacity.</summary>
    private const int LargeArrayCapacity = 100;

    /// <summary>Constructor should initialize Items and Count.</summary>
    [Test]
    public void Constructor_ShouldInitializeProperties()
    {
        var array = ArrayPool<int>.Shared.Rent(ArrayCapacity);
        array[0] = 1;
        array[1] = PairCount;
        array[PairCount] = TripleCount;

        var batch = new PooledBatch<int>(array, TripleCount);

        batch.Items.Should().BeSameAs(array);
        batch.Count.Should().Be(TripleCount);
    }

    /// <summary>Items should be accessible before dispose.</summary>
    [Test]
    public void Items_BeforeDispose_ShouldBeAccessible()
    {
        var array = ArrayPool<string>.Shared.Rent(DefaultBatchCount);
        array[0] = "hello";
        array[1] = "world";

        var batch = new PooledBatch<string>(array, PairCount);

        batch.Items[0].Should().Be("hello");
        batch.Items[1].Should().Be("world");
    }

    /// <summary>Count should reflect actual item count.</summary>
    [Test]
    public void Count_ShouldReflectActualItemCount()
    {
        var array = ArrayPool<double>.Shared.Rent(LargeArrayCapacity);

        var batch = new PooledBatch<double>(array, ArbitraryBatchCount);

        batch.Count.Should().Be(ArbitraryBatchCount);
    }

    /// <summary>Dispose should return array to pool.</summary>
    [Test]
    public void Dispose_ShouldReturnArrayToPool()
    {
        var array = ArrayPool<int>.Shared.Rent(ArrayCapacity);
        var batch = new PooledBatch<int>(array, DefaultBatchCount);

        var act = batch.Dispose;

        act.Should().NotThrow();
    }

    /// <summary>Multiple dispose calls should be safe.</summary>
    [Test]
    public void Dispose_MultipleCalls_ShouldBeSafe()
    {
        var array = ArrayPool<int>.Shared.Rent(ArrayCapacity);
        var batch = new PooledBatch<int>(array, DefaultBatchCount);

        batch.Dispose();
        var act = batch.Dispose;

        act.Should().NotThrow();
    }

    /// <summary>Record equality should work correctly.</summary>
    [Test]
    public void RecordEquality_ShouldWorkCorrectly()
    {
        var array = ArrayPool<int>.Shared.Rent(ArrayCapacity);
        var batch1 = new PooledBatch<int>(array, DefaultBatchCount);
        var batch2 = new PooledBatch<int>(array, DefaultBatchCount);

        batch1.Should().Be(batch2);
        (batch1 == batch2).Should().BeTrue();

        // Clean up
        batch1.Dispose();
    }

    /// <summary>Record inequality should work for different counts.</summary>
    [Test]
    public void RecordInequality_DifferentCounts_ShouldNotBeEqual()
    {
        var array = ArrayPool<int>.Shared.Rent(ArrayCapacity);
        var batch1 = new PooledBatch<int>(array, DefaultBatchCount);
        var batch2 = new PooledBatch<int>(array, ArrayCapacity);

        batch1.Should().NotBe(batch2);
        (batch1 != batch2).Should().BeTrue();

        // Clean up - only one dispose needed since same array
        batch1.Dispose();
    }

    /// <summary>PooledBatch should work with reference types.</summary>
    [Test]
    public void PooledBatch_WithReferenceTypes_ShouldWork()
    {
        var array = ArrayPool<string>.Shared.Rent(DefaultBatchCount);
        array[0] = "test";
        array[1] = "data";

        using var batch = new PooledBatch<string>(array, PairCount);

        batch.Items[0].Should().Be("test");
        batch.Items[1].Should().Be("data");
        batch.Count.Should().Be(PairCount);
    }

    /// <summary>PooledBatch with zero count should be valid.</summary>
    [Test]
    public void PooledBatch_WithZeroCount_ShouldBeValid()
    {
        var array = ArrayPool<int>.Shared.Rent(ArrayCapacity);

        using var batch = new PooledBatch<int>(array, 0);

        batch.Count.Should().Be(0);
        batch.Items.Should().NotBeNull();
    }

    /// <summary>PooledBatch should support with expression.</summary>
    [Test]
    public void PooledBatch_WithExpression_ShouldWork()
    {
        var array = ArrayPool<int>.Shared.Rent(ArrayCapacity);
        var batch1 = new PooledBatch<int>(array, DefaultBatchCount);

        var batch2 = batch1 with { Count = UpdatedBatchCount };

        batch2.Items.Should().BeSameAs(array);
        batch2.Count.Should().Be(UpdatedBatchCount);

        // Clean up
        batch1.Dispose();
    }

    /// <summary>GetHashCode should be consistent.</summary>
    [Test]
    public void GetHashCode_ShouldBeConsistent()
    {
        var array = ArrayPool<int>.Shared.Rent(ArrayCapacity);
        var batch = new PooledBatch<int>(array, DefaultBatchCount);

        var hash1 = batch.GetHashCode();
        var hash2 = batch.GetHashCode();

        hash1.Should().Be(hash2);

        batch.Dispose();
    }

    /// <summary>ToString should return meaningful representation.</summary>
    [Test]
    public void ToString_ShouldReturnMeaningfulRepresentation()
    {
        var array = ArrayPool<int>.Shared.Rent(ArrayCapacity);
        using var batch = new PooledBatch<int>(array, DefaultBatchCount);

        var result = batch.ToString();

        result.Should().Contain("PooledBatch");
        result.Should().Contain("5");
    }
}
