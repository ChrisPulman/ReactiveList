// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER || NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Core;
#else
using CP.Primitives.Core;
#endif
using TUnit.Assertions;
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
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Rent_ReturnsNewWrapperWhenPoolEmpty()
    {
        // Arrange
        EditableListWrapperPool<int>.Clear();
        var list = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };

        // Act
        using var wrapper = EditableListWrapperPool.Rent(list);

        // Assert
        await Assert.That(wrapper).IsNotNull();
        await Assert.That(wrapper.Count).IsEqualTo(ThirdFixtureValue);
    }

    /// <summary>Tests that Return adds wrapper to pool.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Return_AddsWrapperToPool()
    {
        // Arrange
        EditableListWrapperPool<int>.Clear();
        var list = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };
        var wrapper = EditableListWrapperPool.Rent(list);

        // Act
        wrapper.Dispose();

        // Assert
        await Assert.That(EditableListWrapperPool<int>.CurrentPoolSize).IsEqualTo(1);
    }

    /// <summary>Tests that Rent reuses wrapper from pool.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Rent_ReusesWrapperFromPool()
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
        await Assert.That(wrapper2).IsSameReferenceAs(wrapper1);
        await Assert.That(wrapper2.Count).IsEqualTo(SecondFixtureValue);
        await Assert.That(EditableListWrapperPool<int>.CurrentPoolSize).IsEqualTo(0);

        wrapper2.Dispose();
    }

    /// <summary>Tests that wrapper operations work correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PooledWrapper_OperationsWork()
    {
        // Arrange
        var list = new List<int>();
        using var wrapper = EditableListWrapperPool.Rent(list);

        // Act & Assert
        wrapper.Add(1);
        await Assert.That(wrapper.Count).IsEqualTo(1);

        wrapper.AddRange([SecondFixtureValue, ThirdFixtureValue, FourthFixtureValue]);
        await Assert.That(wrapper.Count).IsEqualTo(FourthFixtureValue);

        wrapper.Insert(0, 0);
        await Assert.That(wrapper[0]).IsEqualTo(0);

        _ = wrapper.Remove(SecondFixtureValue);
        await Assert.That(wrapper.Contains(SecondFixtureValue)).IsFalse();

        wrapper.RemoveAt(0);
        await Assert.That(wrapper.Count).IsEqualTo(ThirdFixtureValue);

        wrapper.Clear();
        await Assert.That(wrapper.Count).IsEqualTo(0);
    }

    /// <summary>Tests that wrapper syncs with observable collection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PooledWrapper_SyncsWithObservableCollection()
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
        await Assert.That(observable).IsEquivalentTo([1, SecondFixtureValue, ThirdFixtureValue]);
    }

    /// <summary>Tests that disposed wrapper throws when used.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PooledWrapper_ThrowsAfterDispose()
    {
        // Arrange
        var list = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };
        var wrapper = EditableListWrapperPool.Rent(list);
        wrapper.Dispose();

        // Act & Assert
        var action = () => wrapper.Add(FourthFixtureValue);
        await Assert.That(action).Throws<ObjectDisposedException>();
    }

    /// <summary>Tests that MaxPoolSize limits pool growth.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MaxPoolSize_LimitsPoolGrowth()
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
            await Assert.That(EditableListWrapperPool<int>.CurrentPoolSize).IsGreaterThanOrEqualTo(0);
            await Assert.That(EditableListWrapperPool<int>.CurrentPoolSize).IsLessThanOrEqualTo(SecondFixtureValue);
        }
        finally
        {
            EditableListWrapperPool<int>.MaxPoolSize = originalMax;
            EditableListWrapperPool<int>.Clear();
        }
    }

    /// <summary>Tests that IResettable.Reset clears wrapper state.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IResettable_Reset_ClearsState()
    {
        // Arrange
        var list = new List<int> { 1, SecondFixtureValue, ThirdFixtureValue };
        var wrapper = EditableListWrapperPool.Rent(list);

        // Act
        ((IResettable)wrapper).Reset();

        // Assert
        await Assert.That(wrapper.Count).IsEqualTo(0);
    }
}
#endif
