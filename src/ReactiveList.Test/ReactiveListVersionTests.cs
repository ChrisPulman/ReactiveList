// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
using System;
using CP.Reactive;
using CP.Reactive.Collections;
#else
using CP.Primitives;
using CP.Primitives.Collections;
#endif
using TUnit.Assertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for ReactiveList Version tracking and ClearWithoutDeallocation.</summary>
public class ReactiveListVersionTests
{
    /// <summary>Tests that Version increments when adding an item.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Version_IncrementsOnAdd()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var initialVersion = list.Version;

        // Act
        list.Add(1);

        // Assert
        await Assert.That(list.Version).IsEqualTo(initialVersion + 1);
    }

    /// <summary>Tests that Version increments when adding a range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Version_IncrementsOnAddRange()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var initialVersion = list.Version;

        // Act
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree]);

        // Assert
        await Assert.That(list.Version).IsEqualTo(initialVersion + 1);
    }

    /// <summary>Tests that Version increments when removing an item.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Version_IncrementsOnRemove()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var initialVersion = list.Version;

        // Act
        _ = list.Remove(TestData.TestValueTwo);

        // Assert
        await Assert.That(list.Version).IsEqualTo(initialVersion + 1);
    }

    /// <summary>Tests that Version increments when clearing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Version_IncrementsOnClear()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var initialVersion = list.Version;

        // Act
        list.Clear();

        // Assert
        await Assert.That(list.Version).IsEqualTo(initialVersion + 1);
    }

    /// <summary>Tests that Version increments when updating.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Version_IncrementsOnUpdate()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var initialVersion = list.Version;

        // Act
        list.Update(TestData.TestValueTwo, TestData.TestValueTwenty);

        // Assert
        await Assert.That(list.Version).IsEqualTo(initialVersion + 1);
    }

    /// <summary>Tests that Version increments when moving.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Version_IncrementsOnMove()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var initialVersion = list.Version;

        // Act
        list.Move(0, TestData.TestValueTwo);

        // Assert
        await Assert.That(list.Version).IsEqualTo(initialVersion + 1);
    }

#if NET6_0_OR_GREATER || NETFRAMEWORK
    /// <summary>Tests that ClearWithoutDeallocation clears items but preserves capacity.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClearWithoutDeallocation_ClearsItemsPreservesCapacity()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var items = new int[TestData.TestValueOneHundred];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = i + 1;
        }

        list.AddRange(items);
        var countBefore = list.Count;

        // Act
        list.ClearWithoutDeallocation();

        // Assert
        await Assert.That(list.Count).IsEqualTo(0);
        await Assert.That(countBefore).IsEqualTo(TestData.TestValueOneHundred);
    }

    /// <summary>Tests that ClearWithoutDeallocation emits change notification.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClearWithoutDeallocation_EmitsChangeNotification()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var changeReceived = false;
        using var subscription = list.Connect().Subscribe(
            _ => changeReceived = true,
            static _ => { },
            static () => { });

        // Act
        list.ClearWithoutDeallocation();

        // Assert
        await Assert.That(changeReceived).IsTrue();
    }

    /// <summary>Tests that ClearWithoutDeallocation with notifyChange=false does not emit.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClearWithoutDeallocation_WithNotifyFalse_DoesNotEmit()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var changeCount = 0;
        using var subscription = list.Connect().Subscribe(
            _ => changeCount++,
            static _ => { },
            static () => { });
        var countBefore = changeCount;

        // Act
        list.ClearWithoutDeallocation(notifyChange: false);

        // Assert
        await Assert.That(list.Count).IsEqualTo(0);
        await Assert.That(changeCount).IsEqualTo(countBefore); // No additional changes
    }

    /// <summary>Tests that ClearWithoutDeallocation increments version.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClearWithoutDeallocation_IncrementsVersion()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var initialVersion = list.Version;

        // Act
        list.ClearWithoutDeallocation();

        // Assert
        await Assert.That(list.Version).IsEqualTo(initialVersion + 1);
    }
#endif
}
