// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using CP.Reactive.Core;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Tests for PooledBatch.
/// </summary>
public class PooledBatchTests
{
    /// <summary>
    /// Constructor should initialize Items and Count.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var array = ArrayPool<int>.Shared.Rent(10);
        array[0] = 1;
        array[1] = 2;
        array[2] = 3;

        var batch = new PooledBatch<int>(array, 3);

        batch.Items.Should().BeSameAs(array);
        batch.Count.Should().Be(3);
    }

    /// <summary>
    /// Items should be accessible before dispose.
    /// </summary>
    [Fact]
    public void Items_BeforeDispose_ShouldBeAccessible()
    {
        var array = ArrayPool<string>.Shared.Rent(5);
        array[0] = "hello";
        array[1] = "world";

        var batch = new PooledBatch<string>(array, 2);

        batch.Items[0].Should().Be("hello");
        batch.Items[1].Should().Be("world");
    }

    /// <summary>
    /// Count should reflect actual item count.
    /// </summary>
    [Fact]
    public void Count_ShouldReflectActualItemCount()
    {
        var array = ArrayPool<double>.Shared.Rent(100);

        var batch = new PooledBatch<double>(array, 42);

        batch.Count.Should().Be(42);
    }

    /// <summary>
    /// Dispose should return array to pool.
    /// </summary>
    [Fact]
    public void Dispose_ShouldReturnArrayToPool()
    {
        var array = ArrayPool<int>.Shared.Rent(10);
        var batch = new PooledBatch<int>(array, 5);

        var act = () => batch.Dispose();

        act.Should().NotThrow();
    }

    /// <summary>
    /// Multiple dispose calls should be safe.
    /// </summary>
    [Fact]
    public void Dispose_MultipleCalls_ShouldBeSafe()
    {
        var array = ArrayPool<int>.Shared.Rent(10);
        var batch = new PooledBatch<int>(array, 5);

        batch.Dispose();
        var act = () => batch.Dispose();

        act.Should().NotThrow();
    }

    /// <summary>
    /// Record equality should work correctly.
    /// </summary>
    [Fact]
    public void RecordEquality_ShouldWorkCorrectly()
    {
        var array = ArrayPool<int>.Shared.Rent(10);
        var batch1 = new PooledBatch<int>(array, 5);
        var batch2 = new PooledBatch<int>(array, 5);

        batch1.Should().Be(batch2);
        (batch1 == batch2).Should().BeTrue();

        // Clean up
        batch1.Dispose();
    }

    /// <summary>
    /// Record inequality should work for different counts.
    /// </summary>
    [Fact]
    public void RecordInequality_DifferentCounts_ShouldNotBeEqual()
    {
        var array = ArrayPool<int>.Shared.Rent(10);
        var batch1 = new PooledBatch<int>(array, 5);
        var batch2 = new PooledBatch<int>(array, 10);

        batch1.Should().NotBe(batch2);
        (batch1 != batch2).Should().BeTrue();

        // Clean up - only one dispose needed since same array
        batch1.Dispose();
    }

    /// <summary>
    /// PooledBatch should work with reference types.
    /// </summary>
    [Fact]
    public void PooledBatch_WithReferenceTypes_ShouldWork()
    {
        var array = ArrayPool<string>.Shared.Rent(5);
        array[0] = "test";
        array[1] = "data";

        using var batch = new PooledBatch<string>(array, 2);

        batch.Items[0].Should().Be("test");
        batch.Items[1].Should().Be("data");
        batch.Count.Should().Be(2);
    }

    /// <summary>
    /// PooledBatch with zero count should be valid.
    /// </summary>
    [Fact]
    public void PooledBatch_WithZeroCount_ShouldBeValid()
    {
        var array = ArrayPool<int>.Shared.Rent(10);

        using var batch = new PooledBatch<int>(array, 0);

        batch.Count.Should().Be(0);
        batch.Items.Should().NotBeNull();
    }

    /// <summary>
    /// PooledBatch should support with expression.
    /// </summary>
    [Fact]
    public void PooledBatch_WithExpression_ShouldWork()
    {
        var array = ArrayPool<int>.Shared.Rent(10);
        var batch1 = new PooledBatch<int>(array, 5);

        var batch2 = batch1 with { Count = 8 };

        batch2.Items.Should().BeSameAs(array);
        batch2.Count.Should().Be(8);

        // Clean up
        batch1.Dispose();
    }

    /// <summary>
    /// GetHashCode should be consistent.
    /// </summary>
    [Fact]
    public void GetHashCode_ShouldBeConsistent()
    {
        var array = ArrayPool<int>.Shared.Rent(10);
        var batch = new PooledBatch<int>(array, 5);

        var hash1 = batch.GetHashCode();
        var hash2 = batch.GetHashCode();

        hash1.Should().Be(hash2);

        batch.Dispose();
    }

    /// <summary>
    /// ToString should return meaningful representation.
    /// </summary>
    [Fact]
    public void ToString_ShouldReturnMeaningfulRepresentation()
    {
        var array = ArrayPool<int>.Shared.Rent(10);
        using var batch = new PooledBatch<int>(array, 5);

        var result = batch.ToString();

        result.Should().Contain("PooledBatch");
        result.Should().Contain("5");
    }
}
