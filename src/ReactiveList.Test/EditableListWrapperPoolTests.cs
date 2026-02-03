// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CP.Reactive;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Tests for EditableListWrapperPool and PooledEditableListWrapper.
/// </summary>
public class EditableListWrapperPoolTests
{
    /// <summary>
    /// Tests that Rent returns a new wrapper when pool is empty.
    /// </summary>
    [Fact]
    public void Rent_ReturnsNewWrapperWhenPoolEmpty()
    {
        // Arrange
        EditableListWrapperPool<int>.Clear();
        var list = new List<int> { 1, 2, 3 };

        // Act
        using var wrapper = EditableListWrapperPool<int>.Rent(list);

        // Assert
        wrapper.Should().NotBeNull();
        wrapper.Count.Should().Be(3);
    }

    /// <summary>
    /// Tests that Return adds wrapper to pool.
    /// </summary>
    [Fact]
    public void Return_AddsWrapperToPool()
    {
        // Arrange
        EditableListWrapperPool<int>.Clear();
        var list = new List<int> { 1, 2, 3 };
        var wrapper = EditableListWrapperPool<int>.Rent(list);

        // Act
        wrapper.Dispose();

        // Assert
        EditableListWrapperPool<int>.CurrentPoolSize.Should().Be(1);
    }

    /// <summary>
    /// Tests that Rent reuses wrapper from pool.
    /// </summary>
    [Fact]
    public void Rent_ReusesWrapperFromPool()
    {
        // Arrange
        EditableListWrapperPool<int>.Clear();
        var list1 = new List<int> { 1, 2, 3 };
        var list2 = new List<int> { 4, 5 };

        var wrapper1 = EditableListWrapperPool<int>.Rent(list1);
        wrapper1.Dispose();

        // Act
        var wrapper2 = EditableListWrapperPool<int>.Rent(list2);

        // Assert
        wrapper2.Should().BeSameAs(wrapper1);
        wrapper2.Count.Should().Be(2);
        EditableListWrapperPool<int>.CurrentPoolSize.Should().Be(0);

        wrapper2.Dispose();
    }

    /// <summary>
    /// Tests that wrapper operations work correctly.
    /// </summary>
    [Fact]
    public void PooledWrapper_OperationsWork()
    {
        // Arrange
        var list = new List<int>();
        using var wrapper = EditableListWrapperPool<int>.Rent(list);

        // Act & Assert
        wrapper.Add(1);
        wrapper.Count.Should().Be(1);

        wrapper.AddRange([2, 3, 4]);
        wrapper.Count.Should().Be(4);

        wrapper.Insert(0, 0);
        wrapper[0].Should().Be(0);

        wrapper.Remove(2);
        wrapper.Contains(2).Should().BeFalse();

        wrapper.RemoveAt(0);
        wrapper.Count.Should().Be(3);

        wrapper.Clear();
        wrapper.Count.Should().Be(0);
    }

    /// <summary>
    /// Tests that wrapper syncs with observable collection.
    /// </summary>
    [Fact]
    public void PooledWrapper_SyncsWithObservableCollection()
    {
        // Arrange
        var list = new List<int>();
        var observable = new ObservableCollection<int>();
        using var wrapper = EditableListWrapperPool<int>.Rent(list, observable);

        // Act
        wrapper.Add(1);
        wrapper.Add(2);
        wrapper.Add(3);

        // Assert
        observable.Should().BeEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests that disposed wrapper throws when used.
    /// </summary>
    [Fact]
    public void PooledWrapper_ThrowsAfterDispose()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };
        var wrapper = EditableListWrapperPool<int>.Rent(list);
        wrapper.Dispose();

        // Act & Assert
        var action = () => wrapper.Add(4);
        action.Should().Throw<ObjectDisposedException>();
    }

    /// <summary>
    /// Tests that MaxPoolSize limits pool growth.
    /// </summary>
    [Fact]
    public void MaxPoolSize_LimitsPoolGrowth()
    {
        // Arrange
        EditableListWrapperPool<int>.Clear();
        var originalMax = EditableListWrapperPool<int>.MaxPoolSize;
        EditableListWrapperPool<int>.MaxPoolSize = 2;

        try
        {
            var list = new List<int>();

            // Act - create and return 3 wrappers
            var w1 = EditableListWrapperPool<int>.Rent(list);
            var w2 = EditableListWrapperPool<int>.Rent(list);
            var w3 = EditableListWrapperPool<int>.Rent(list);

            w1.Dispose();
            w2.Dispose();
            w3.Dispose();

            // Assert - only 2 should be pooled
            EditableListWrapperPool<int>.CurrentPoolSize.Should().BeGreaterThanOrEqualTo(0);
            EditableListWrapperPool<int>.CurrentPoolSize.Should().BeLessThanOrEqualTo(2);
        }
        finally
        {
            EditableListWrapperPool<int>.MaxPoolSize = originalMax;
            EditableListWrapperPool<int>.Clear();
        }
    }

    /// <summary>
    /// Tests that IResettable.Reset clears wrapper state.
    /// </summary>
    [Fact]
    public void IResettable_Reset_ClearsState()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };
        var wrapper = EditableListWrapperPool<int>.Rent(list);

        // Act
        ((IResettable)wrapper).Reset();

        // Assert
        wrapper.Count.Should().Be(0);
    }
}
#endif
