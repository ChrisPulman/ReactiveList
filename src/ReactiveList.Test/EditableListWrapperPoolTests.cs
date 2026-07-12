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
        EditableListWrapperPool.Clear<int>();
        var list = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };

        // Act
        using var wrapper = EditableListWrapperPool.Rent<int>(list);

        // Assert
        wrapper.Should().NotBeNull();
        wrapper.Count.Should().Be(ThirdFixtureValue);
    }

    /// <summary>Tests that Return adds wrapper to pool.</summary>
    [Test]
    public void Return_AddsWrapperToPool()
    {
        // Arrange
        EditableListWrapperPool.Clear<int>();
        var list = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };
        var wrapper = EditableListWrapperPool.Rent<int>(list);

        // Act
        wrapper.Dispose();

        // Assert
        EditableListWrapperPool.GetCurrentPoolSize<int>().Should().Be(1);
    }

    /// <summary>Tests that Rent reuses wrapper from pool.</summary>
    [Test]
    public void Rent_ReusesWrapperFromPool()
    {
        // Arrange
        EditableListWrapperPool.Clear<int>();
        var list1 = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };
        var list2 = new List<int> { FourthFixtureValue, FifthFixtureValue };

        var wrapper1 = EditableListWrapperPool.Rent<int>(list1);
        wrapper1.Dispose();

        // Act
        var wrapper2 = EditableListWrapperPool.Rent<int>(list2);

        // Assert
        wrapper2.Should().BeSameAs(wrapper1);
        wrapper2.Count.Should().Be(SecondFixtureValue);
        EditableListWrapperPool.GetCurrentPoolSize<int>().Should().Be(0);

        wrapper2.Dispose();
    }

    /// <summary>Tests that wrapper operations work correctly.</summary>
    [Test]
    public void PooledWrapper_OperationsWork()
    {
        // Arrange
        var list = new List<int>();
        using var wrapper = EditableListWrapperPool.Rent<int>(list);

        // Act & Assert
        wrapper.Add(1);
        wrapper.Count.Should().Be(1);

        wrapper.AddRange([SecondFixtureValue, ThirdFixtureValue, FourthFixtureValue]);
        wrapper.Count.Should().Be(FourthFixtureValue);

        wrapper.Insert(0, 0);
        wrapper[0].Should().Be(0);

        wrapper.Remove(SecondFixtureValue);
        wrapper.Contains(SecondFixtureValue).Should().BeFalse();

        wrapper.RemoveAt(0);
        wrapper.Count.Should().Be(ThirdFixtureValue);

        wrapper.Clear();
        wrapper.Count.Should().Be(0);
    }

    /// <summary>Tests that wrapper syncs with observable collection.</summary>
    [Test]
    public void PooledWrapper_SyncsWithObservableCollection()
    {
        // Arrange
        var list = new List<int>();
        var observable = new ObservableCollection<int>();
        using var wrapper = EditableListWrapperPool.Rent<int>(list, observable);

        // Act
        wrapper.Add(1);
        wrapper.Add(SecondFixtureValue);
        wrapper.Add(ThirdFixtureValue);

        // Assert
        observable.Should().BeEquivalentTo([1, SecondFixtureValue, ThirdFixtureValue]);
    }

    /// <summary>Tests that disposed wrapper throws when used.</summary>
    [Test]
    public void PooledWrapper_ThrowsAfterDispose()
    {
        // Arrange
        var list = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };
        var wrapper = EditableListWrapperPool.Rent<int>(list);
        wrapper.Dispose();

        // Act & Assert
        var action = () => wrapper.Add(FourthFixtureValue);
        action.Should().Throw<ObjectDisposedException>();
    }

    /// <summary>Tests that MaxPoolSize limits pool growth.</summary>
    [Test]
    public void MaxPoolSize_LimitsPoolGrowth()
    {
        // Arrange
        EditableListWrapperPool.Clear<int>();
        var originalMax = EditableListWrapperPool.GetMaxPoolSize<int>();
        EditableListWrapperPool.SetMaxPoolSize<int>(SecondFixtureValue);

        try
        {
            var list = new List<int>();

            // Act - create and return 3 wrappers
            var w1 = EditableListWrapperPool.Rent<int>(list);
            var w2 = EditableListWrapperPool.Rent<int>(list);
            var w3 = EditableListWrapperPool.Rent<int>(list);

            w1.Dispose();
            w2.Dispose();
            w3.Dispose();

            // Assert - only 2 should be pooled
            EditableListWrapperPool.GetCurrentPoolSize<int>().Should().BeGreaterThanOrEqualTo(0);
            EditableListWrapperPool.GetCurrentPoolSize<int>().Should().BeLessThanOrEqualTo(SecondFixtureValue);
        }
        finally
        {
            EditableListWrapperPool.SetMaxPoolSize<int>(originalMax);
            EditableListWrapperPool.Clear<int>();
        }
    }

    /// <summary>Tests that IResettable.Reset clears wrapper state.</summary>
    [Test]
    public void IResettable_Reset_ClearsState()
    {
        // Arrange
        var list = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };
        var wrapper = EditableListWrapperPool.Rent<int>(list);

        // Act
        ((IResettable)wrapper).Reset();

        // Assert
        wrapper.Count.Should().Be(0);
    }
}
#endif
