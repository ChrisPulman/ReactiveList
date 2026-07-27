// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER || NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CP.Primitives.Core;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for EditableListWrapperPool and PooledEditableListWrapper.</summary>
public class EditableListWrapperPoolTests
{
    /// <summary>The second fixture value.</summary>
    private const int SecondFixtureValue = 2;

    /// <summary>The third fixture value.</summary>
    private const int ThirdFixtureValue = 3;

    /// <summary>The fourth fixture value.</summary>
    private const int FourthFixtureValue = 4;

    /// <summary>The fifth fixture value.</summary>
    private const int FifthFixtureValue = 5;

    /// <summary>Tests that Rent returns a new wrapper when pool is empty.</summary>
    [Test]
    public void Rent_ReturnsNewWrapperWhenPoolEmpty()
    {
        // Arrange
        EditableListWrapperPool<int>.Clear();
        var list = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };

        // Act
        using var wrapper = EditableListWrapperPool.Rent(list);

        // Assert
        _ = wrapper.Should().NotBeNull();
        _ = wrapper.Count.Should().Be(ThirdFixtureValue);
    }

    /// <summary>Tests that Return adds wrapper to pool.</summary>
    [Test]
    public void Return_AddsWrapperToPool()
    {
        // Arrange
        EditableListWrapperPool<int>.Clear();
        var list = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };
        var wrapper = EditableListWrapperPool.Rent(list);

        // Act
        wrapper.Dispose();

        // Assert
        _ = EditableListWrapperPool<int>.CurrentPoolSize.Should().Be(1);
    }

    /// <summary>Tests that Rent reuses wrapper from pool.</summary>
    [Test]
    public void Rent_ReusesWrapperFromPool()
    {
        // Arrange
        EditableListWrapperPool<int>.Clear();
        var list1 = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };
        var list2 = new List<int> { FourthFixtureValue, FifthFixtureValue };

        var wrapper1 = EditableListWrapperPool.Rent(list1);
        wrapper1.Dispose();

        // Act
        var wrapper2 = EditableListWrapperPool.Rent(list2);

        // Assert
        _ = wrapper2.Should().BeSameAs(wrapper1);
        _ = wrapper2.Count.Should().Be(SecondFixtureValue);
        _ = EditableListWrapperPool<int>.CurrentPoolSize.Should().Be(0);

        wrapper2.Dispose();
    }

    /// <summary>Tests that wrapper operations work correctly.</summary>
    [Test]
    public void PooledWrapper_OperationsWork()
    {
        // Arrange
        var list = new List<int>();
        using var wrapper = EditableListWrapperPool.Rent(list);

        // Act & Assert
        wrapper.Add(1);
        _ = wrapper.Count.Should().Be(1);

        wrapper.AddRange([SecondFixtureValue, ThirdFixtureValue, FourthFixtureValue]);
        _ = wrapper.Count.Should().Be(FourthFixtureValue);

        wrapper.Insert(0, 0);
        _ = wrapper[0].Should().Be(0);

        _ = wrapper.Remove(SecondFixtureValue);
        _ = wrapper.Contains(SecondFixtureValue).Should().BeFalse();

        wrapper.RemoveAt(0);
        _ = wrapper.Count.Should().Be(ThirdFixtureValue);

        wrapper.Clear();
        _ = wrapper.Count.Should().Be(0);
    }

    /// <summary>Tests that wrapper syncs with observable collection.</summary>
    [Test]
    public void PooledWrapper_SyncsWithObservableCollection()
    {
        // Arrange
        var list = new List<int>();
        var observable = new ObservableCollection<int>();
        using var wrapper = EditableListWrapperPool.Rent(list, observable);

        // Act
        wrapper.Add(1);
        wrapper.Add(SecondFixtureValue);
        wrapper.Add(ThirdFixtureValue);

        // Assert
        _ = observable.Should().BeEquivalentTo([1, SecondFixtureValue, ThirdFixtureValue]);
    }

    /// <summary>Tests that disposed wrapper throws when used.</summary>
    [Test]
    public void PooledWrapper_ThrowsAfterDispose()
    {
        // Arrange
        var list = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };
        var wrapper = EditableListWrapperPool.Rent(list);
        wrapper.Dispose();

        // Act & Assert
        var action = () => wrapper.Add(FourthFixtureValue);
        _ = action.Should().Throw<ObjectDisposedException>();
    }

    /// <summary>Tests that MaxPoolSize limits pool growth.</summary>
    [Test]
    public void MaxPoolSize_LimitsPoolGrowth()
    {
        // Arrange
        EditableListWrapperPool<int>.Clear();
        var originalMax = EditableListWrapperPool<int>.MaxPoolSize;
        EditableListWrapperPool<int>.MaxPoolSize = SecondFixtureValue;

        try
        {
            var list = new List<int>();

            // Act - create and return 3 wrappers
            var w1 = EditableListWrapperPool.Rent(list);
            var w2 = EditableListWrapperPool.Rent(list);
            var w3 = EditableListWrapperPool.Rent(list);

            w1.Dispose();
            w2.Dispose();
            w3.Dispose();

            // Assert - only 2 should be pooled
            _ = EditableListWrapperPool<int>.CurrentPoolSize.Should().BeGreaterThanOrEqualTo(0);
            _ = EditableListWrapperPool<int>.CurrentPoolSize.Should().BeLessThanOrEqualTo(SecondFixtureValue);
        }
        finally
        {
            EditableListWrapperPool<int>.MaxPoolSize = originalMax;
            EditableListWrapperPool<int>.Clear();
        }
    }

    /// <summary>Tests that IResettable.Reset clears wrapper state.</summary>
    [Test]
    public void IResettable_Reset_ClearsState()
    {
        // Arrange
        var list = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };
        var wrapper = EditableListWrapperPool.Rent(list);

        // Act
        ((IResettable)wrapper).Reset();

        // Assert
        _ = wrapper.Count.Should().Be(0);
    }
}
#endif
