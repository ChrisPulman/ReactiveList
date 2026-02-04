// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Tests for the ReactiveList Connect() method and unified ChangeSet.
/// </summary>
public class ReactiveListConnectTests
{
    /// <summary>
    /// Connect returns observable stream.
    /// </summary>
    [Fact]
    public void Connect_ReturnsObservableStream()
    {
        // Arrange
        using var list = new ReactiveList<int>();

        // Act
        var observable = list.Connect();

        // Assert
        observable.Should().NotBeNull();
    }

    /// <summary>
    /// Connect emits add changes when items are added.
    /// </summary>
    [Fact]
    public void Connect_EmitsAddChanges_WhenItemsAdded()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(cs => receivedChanges.Add(cs));

        // Act
        list.Add(42);

        // Assert
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Count.Should().Be(1);
        receivedChanges[0].Adds.Should().Be(1);
        receivedChanges[0][0].Reason.Should().Be(ChangeReason.Add);
        receivedChanges[0][0].Current.Should().Be(42);
    }

    /// <summary>
    /// Connect emits batch add changes when AddRange is called.
    /// </summary>
    [Fact]
    public void Connect_EmitsBatchAddChanges_WhenAddRangeCalled()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(cs => receivedChanges.Add(cs));

        // Act
        list.AddRange([1, 2, 3, 4, 5]);

        // Assert
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Count.Should().Be(5);
        receivedChanges[0].Adds.Should().Be(5);
    }

    /// <summary>
    /// Connect emits remove changes when items are removed.
    /// </summary>
    [Fact]
    public void Connect_EmitsRemoveChanges_WhenItemsRemoved()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, 2, 3]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(cs => receivedChanges.Add(cs));

        // Act
        list.Remove(2);

        // Assert
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Count.Should().Be(1);
        receivedChanges[0].Removes.Should().Be(1);
        receivedChanges[0][0].Reason.Should().Be(ChangeReason.Remove);
        receivedChanges[0][0].Current.Should().Be(2);
    }

    /// <summary>
    /// Connect emits clear changes when collection is cleared.
    /// </summary>
    [Fact]
    public void Connect_EmitsClearChanges_WhenCleared()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, 2, 3]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(cs => receivedChanges.Add(cs));

        // Act
        list.Clear();

        // Assert
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Count.Should().Be(1);
        receivedChanges[0][0].Reason.Should().Be(ChangeReason.Clear);
    }

    /// <summary>
    /// Connect emits move changes when item is moved.
    /// </summary>
    [Fact]
    public void Connect_EmitsMoveChanges_WhenItemMoved()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, 2, 3, 4, 5]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(cs => receivedChanges.Add(cs));

        // Act
        list.Move(0, 4);

        // Assert
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Count.Should().Be(1);
        receivedChanges[0].Moves.Should().Be(1);
        receivedChanges[0][0].Reason.Should().Be(ChangeReason.Move);
        receivedChanges[0][0].Current.Should().Be(1);
        receivedChanges[0][0].CurrentIndex.Should().Be(4);
        receivedChanges[0][0].PreviousIndex.Should().Be(0);
    }

    /// <summary>
    /// Connect emits update changes when item is updated.
    /// </summary>
    [Fact]
    public void Connect_EmitsUpdateChanges_WhenItemUpdated()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, 2, 3]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(cs => receivedChanges.Add(cs));

        // Act
        list.Update(2, 20);

        // Assert
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Count.Should().Be(1);
        receivedChanges[0].Updates.Should().Be(1);
        receivedChanges[0][0].Reason.Should().Be(ChangeReason.Update);
        receivedChanges[0][0].Current.Should().Be(20);
    }

    /// <summary>
    /// ChangeSet correctly counts different change types.
    /// </summary>
    [Fact]
    public void ChangeSet_CorrectlyCounts_DifferentChangeTypes()
    {
        // Arrange
        var changes = new Change<int>[]
        {
            Change<int>.CreateAdd(1, 0),
            Change<int>.CreateAdd(2, 1),
            Change<int>.CreateRemove(1, 0),
            Change<int>.CreateUpdate(3, 2, 1),
            Change<int>.CreateMove(2, 2, 1)
        };

        // Act
        var changeSet = new ChangeSet<int>(changes);

        // Assert
        changeSet.Count.Should().Be(5);
        changeSet.Adds.Should().Be(2);
        changeSet.Removes.Should().Be(1);
        changeSet.Updates.Should().Be(1);
        changeSet.Moves.Should().Be(1);
    }

    /// <summary>
    /// ChangeSet can be enumerated.
    /// </summary>
    [Fact]
    public void ChangeSet_CanBeEnumerated()
    {
        // Arrange
        var changes = new Change<int>[]
        {
            Change<int>.CreateAdd(1, 0),
            Change<int>.CreateAdd(2, 1),
            Change<int>.CreateAdd(3, 2)
        };

        // Act
        var changeSet = new ChangeSet<int>(changes);
        var items = changeSet.ToList();

        // Assert
        items.Should().HaveCount(3);
        items[0].Current.Should().Be(1);
        items[1].Current.Should().Be(2);
        items[2].Current.Should().Be(3);
    }

    /// <summary>
    /// ChangeSet indexer returns correct change.
    /// </summary>
    [Fact]
    public void ChangeSet_Indexer_ReturnsCorrectChange()
    {
        // Arrange
        var changes = new Change<int>[]
        {
            Change<int>.CreateAdd(10, 0),
            Change<int>.CreateAdd(20, 1),
            Change<int>.CreateAdd(30, 2)
        };
        var changeSet = new ChangeSet<int>(changes);

        // Act & Assert
        changeSet[0].Current.Should().Be(10);
        changeSet[1].Current.Should().Be(20);
        changeSet[2].Current.Should().Be(30);
    }

    /// <summary>
    /// ChangeSet indexer throws on out of range.
    /// </summary>
    [Fact]
    public void ChangeSet_Indexer_ThrowsOnOutOfRange()
    {
        // Arrange
        var changeSet = new ChangeSet<int>(new Change<int>[] { Change<int>.CreateAdd(1, 0) });

        // Act & Assert
        FluentActions.Invoking(() => changeSet[5])
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Change factory methods create correct change types.
    /// </summary>
    [Fact]
    public void Change_FactoryMethods_CreateCorrectChangeTypes()
    {
        // Act
        var add = Change<int>.CreateAdd(1, 0);
        var remove = Change<int>.CreateRemove(2, 1);
        var update = Change<int>.CreateUpdate(3, 2, 1);
        var move = Change<int>.CreateMove(4, 2, 0);
        var refresh = Change<int>.CreateRefresh(5, 2);

        // Assert
        add.Reason.Should().Be(ChangeReason.Add);
        add.Current.Should().Be(1);
        add.CurrentIndex.Should().Be(0);

        remove.Reason.Should().Be(ChangeReason.Remove);
        remove.Current.Should().Be(2);
        remove.PreviousIndex.Should().Be(1);

        update.Reason.Should().Be(ChangeReason.Update);
        update.Current.Should().Be(3);
        update.Previous.Should().Be(2);
        update.CurrentIndex.Should().Be(1);

        move.Reason.Should().Be(ChangeReason.Move);
        move.Current.Should().Be(4);
        move.CurrentIndex.Should().Be(2);
        move.PreviousIndex.Should().Be(0);

        refresh.Reason.Should().Be(ChangeReason.Refresh);
        refresh.Current.Should().Be(5);
        refresh.CurrentIndex.Should().Be(2);
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// ToArray returns snapshot of current items.
    /// </summary>
    [Fact]
    public void ToArray_ReturnsSnapshot()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, 2, 3, 4, 5]);

        // Act
        var snapshot = list.ToArray();

        // Assert
        snapshot.Should().BeEquivalentTo([1, 2, 3, 4, 5]);
    }

    /// <summary>
    /// ToArray returns empty array for empty list.
    /// </summary>
    [Fact]
    public void ToArray_ReturnsEmptyArray_ForEmptyList()
    {
        // Arrange
        using var list = new ReactiveList<int>();

        // Act
        var snapshot = list.ToArray();

        // Assert
        snapshot.Should().BeEmpty();
    }
#endif
}
