// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Tests for ReactiveList Version tracking and ClearWithoutDeallocation.
/// </summary>
public class ReactiveListVersionTests
{
    /// <summary>
    /// Tests that Version increments when adding an item.
    /// </summary>
    [Fact]
    public void Version_IncrementsOnAdd()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var initialVersion = list.Version;

        // Act
        list.Add(1);

        // Assert
        list.Version.Should().Be(initialVersion + 1);
    }

    /// <summary>
    /// Tests that Version increments when adding a range.
    /// </summary>
    [Fact]
    public void Version_IncrementsOnAddRange()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var initialVersion = list.Version;

        // Act
        list.AddRange([1, 2, 3]);

        // Assert
        list.Version.Should().Be(initialVersion + 1);
    }

    /// <summary>
    /// Tests that Version increments when removing an item.
    /// </summary>
    [Fact]
    public void Version_IncrementsOnRemove()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, 2, 3]);
        var initialVersion = list.Version;

        // Act
        list.Remove(2);

        // Assert
        list.Version.Should().Be(initialVersion + 1);
    }

    /// <summary>
    /// Tests that Version increments when clearing.
    /// </summary>
    [Fact]
    public void Version_IncrementsOnClear()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, 2, 3]);
        var initialVersion = list.Version;

        // Act
        list.Clear();

        // Assert
        list.Version.Should().Be(initialVersion + 1);
    }

    /// <summary>
    /// Tests that Version increments when updating.
    /// </summary>
    [Fact]
    public void Version_IncrementsOnUpdate()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, 2, 3]);
        var initialVersion = list.Version;

        // Act
        list.Update(2, 20);

        // Assert
        list.Version.Should().Be(initialVersion + 1);
    }

    /// <summary>
    /// Tests that Version increments when moving.
    /// </summary>
    [Fact]
    public void Version_IncrementsOnMove()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, 2, 3]);
        var initialVersion = list.Version;

        // Act
        list.Move(0, 2);

        // Assert
        list.Version.Should().Be(initialVersion + 1);
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Tests that ClearWithoutDeallocation clears items but preserves capacity.
    /// </summary>
    [Fact]
    public void ClearWithoutDeallocation_ClearsItemsPreservesCapacity()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(Enumerable.Range(1, 100).ToArray());
        var countBefore = list.Count;

        // Act
        list.ClearWithoutDeallocation();

        // Assert
        list.Count.Should().Be(0);
        countBefore.Should().Be(100);
    }

    /// <summary>
    /// Tests that ClearWithoutDeallocation emits change notification.
    /// </summary>
    [Fact]
    public void ClearWithoutDeallocation_EmitsChangeNotification()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, 2, 3]);
        var changeReceived = false;
        using var subscription = list.Connect().Subscribe(new System.Reactive.AnonymousObserver<ChangeSet<int>>(
            onNext: _ => changeReceived = true,
            onError: _ => { },
            onCompleted: () => { }));

        // Act
        list.ClearWithoutDeallocation();

        // Assert
        changeReceived.Should().BeTrue();
    }

    /// <summary>
    /// Tests that ClearWithoutDeallocation with notifyChange=false does not emit.
    /// </summary>
    [Fact]
    public void ClearWithoutDeallocation_WithNotifyFalse_DoesNotEmit()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, 2, 3]);
        var changeCount = 0;
        using var subscription = list.Connect().Subscribe(new System.Reactive.AnonymousObserver<ChangeSet<int>>(
            onNext: _ => changeCount++,
            onError: _ => { },
            onCompleted: () => { }));
        var countBefore = changeCount;

        // Act
        list.ClearWithoutDeallocation(notifyChange: false);

        // Assert
        list.Count.Should().Be(0);
        changeCount.Should().Be(countBefore); // No additional changes
    }

    /// <summary>
    /// Tests that ClearWithoutDeallocation increments version.
    /// </summary>
    [Fact]
    public void ClearWithoutDeallocation_IncrementsVersion()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, 2, 3]);
        var initialVersion = list.Version;

        // Act
        list.ClearWithoutDeallocation();

        // Assert
        list.Version.Should().Be(initialVersion + 1);
    }
#endif
}
