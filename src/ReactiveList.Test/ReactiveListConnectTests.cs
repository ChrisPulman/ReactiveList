// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for the ReactiveList Connect() method and unified ChangeSet.</summary>
public class ReactiveListConnectTests
{
    /// <summary>Connect returns observable stream.</summary>
    [Test]
    public void Connect_ReturnsObservableStream()
    {
        // Arrange
        using var list = new ReactiveList<int>();

        // Act
        var observable = list.Connect();

        // Assert
        _ = observable.Should().NotBeNull();
    }

    /// <summary>Connect emits the current snapshot for preloaded sources.</summary>
    [Test]
    public void Connect_EmitsInitialSnapshot_WhenSourceHasItems()
    {
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var receivedChanges = new List<ChangeSet<int>>();

        using var subscription = list.Connect().Subscribe(receivedChanges.Add);

        _ = receivedChanges.Should().ContainSingle();
        _ = receivedChanges[0].Count.Should().Be(TestData.TestValueThree);
        _ = receivedChanges[0].Adds.Should().Be(TestData.TestValueThree);
        var currentItems = new List<int>(receivedChanges[0].Count);
        foreach (var change in receivedChanges[0])
        {
            currentItems.Add(change.Current);
        }

        _ = currentItems.Should().Equal(1, TestData.TestValueTwo, TestData.TestValueThree);
    }

    /// <summary>Connect emits add changes when items are added.</summary>
    [Test]
    public void Connect_EmitsAddChanges_WhenItemsAdded()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(receivedChanges.Add);

        // Act
        list.Add(TestData.TestValueFortyTwo);

        // Assert
        _ = receivedChanges.Should().HaveCount(1);
        _ = receivedChanges[0].Count.Should().Be(1);
        _ = receivedChanges[0].Adds.Should().Be(1);
        _ = receivedChanges[0][0].Reason.Should().Be(ChangeReason.Add);
        _ = receivedChanges[0][0].Current.Should().Be(TestData.TestValueFortyTwo);
    }

    /// <summary>Connect emits batch add changes when AddRange is called.</summary>
    [Test]
    public void Connect_EmitsBatchAddChanges_WhenAddRangeCalled()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(receivedChanges.Add);

        // Act
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);

        // Assert
        _ = receivedChanges.Should().HaveCount(1);
        _ = receivedChanges[0].Count.Should().Be(TestData.TestValueFive);
        _ = receivedChanges[0].Adds.Should().Be(TestData.TestValueFive);
    }

    /// <summary>Connect emits remove changes when items are removed.</summary>
    [Test]
    public void Connect_EmitsRemoveChanges_WhenItemsRemoved()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(receivedChanges.Add);
        receivedChanges.Clear();

        // Act
        _ = list.Remove(TestData.TestValueTwo);

        // Assert
        _ = receivedChanges.Should().HaveCount(1);
        _ = receivedChanges[0].Count.Should().Be(1);
        _ = receivedChanges[0].Removes.Should().Be(1);
        _ = receivedChanges[0][0].Reason.Should().Be(ChangeReason.Remove);
        _ = receivedChanges[0][0].Current.Should().Be(TestData.TestValueTwo);
    }

    /// <summary>
    /// Connect emits clear changes when collection is cleared.
    /// Clear emits individual Remove changes for each cleared item (consistent with DynamicData behavior).
    /// </summary>
    [Test]
    public void Connect_EmitsClearChanges_WhenCleared()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(receivedChanges.Add);
        receivedChanges.Clear();

        // Act
        list.Clear();

        // Assert - Clear emits Remove changes for each item (DynamicData compatible behavior)
        _ = receivedChanges.Should().HaveCount(1);
        _ = receivedChanges[0].Count.Should().Be(TestData.TestValueThree); // One Remove change per cleared item
        _ = receivedChanges[0].Removes.Should().Be(TestData.TestValueThree);
        _ = receivedChanges[0][0].Reason.Should().Be(ChangeReason.Remove);
        _ = receivedChanges[0][1].Reason.Should().Be(ChangeReason.Remove);
        _ = receivedChanges[0][TestData.TestValueTwo].Reason.Should().Be(ChangeReason.Remove);
    }

    /// <summary>Connect emits move changes when item is moved.</summary>
    [Test]
    public void Connect_EmitsMoveChanges_WhenItemMoved()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(receivedChanges.Add);
        receivedChanges.Clear();

        // Act
        list.Move(0, TestData.TestValueFour);

        // Assert
        _ = receivedChanges.Should().HaveCount(1);
        _ = receivedChanges[0].Count.Should().Be(1);
        _ = receivedChanges[0].Moves.Should().Be(1);
        _ = receivedChanges[0][0].Reason.Should().Be(ChangeReason.Move);
        _ = receivedChanges[0][0].Current.Should().Be(1);
        _ = receivedChanges[0][0].CurrentIndex.Should().Be(TestData.TestValueFour);
        _ = receivedChanges[0][0].PreviousIndex.Should().Be(0);
    }

    /// <summary>Connect emits update changes when item is updated.</summary>
    [Test]
    public void Connect_EmitsUpdateChanges_WhenItemUpdated()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(receivedChanges.Add);
        receivedChanges.Clear();

        // Act
        list.Update(TestData.TestValueTwo, TestData.TestValueTwenty);

        // Assert
        _ = receivedChanges.Should().HaveCount(1);
        _ = receivedChanges[0].Count.Should().Be(1);
        _ = receivedChanges[0].Updates.Should().Be(1);
        _ = receivedChanges[0][0].Reason.Should().Be(ChangeReason.Update);
        _ = receivedChanges[0][0].Current.Should().Be(TestData.TestValueTwenty);
    }

    /// <summary>ChangeSet correctly counts different change types.</summary>
    [Test]
    public void ChangeSet_CorrectlyCounts_DifferentChangeTypes()
    {
        // Arrange
        var changes = new Change<int>[]
        {
            Change<int>.CreateAdd(1, 0),
            Change<int>.CreateAdd(TestData.TestValueTwo, 1),
            Change<int>.CreateRemove(1, 0),
            Change<int>.CreateUpdate(TestData.TestValueThree, TestData.TestValueTwo, 1),
            Change<int>.CreateMove(TestData.TestValueTwo, TestData.TestValueTwo, 1)
        };

        // Act
        var changeSet = new ChangeSet<int>(changes);

        // Assert
        _ = changeSet.Count.Should().Be(TestData.TestValueFive);
        _ = changeSet.Adds.Should().Be(TestData.TestValueTwo);
        _ = changeSet.Removes.Should().Be(1);
        _ = changeSet.Updates.Should().Be(1);
        _ = changeSet.Moves.Should().Be(1);
    }

    /// <summary>ChangeSet can be enumerated.</summary>
    [Test]
    public void ChangeSet_CanBeEnumerated()
    {
        // Arrange
        var changes = new Change<int>[]
        {
            Change<int>.CreateAdd(1, 0),
            Change<int>.CreateAdd(TestData.TestValueTwo, 1),
            Change<int>.CreateAdd(TestData.TestValueThree, TestData.TestValueTwo)
        };

        // Act
        var changeSet = new ChangeSet<int>(changes);
        var items = new List<Change<int>>(changeSet.Count);
        items.AddRange(changeSet);

        // Assert
        _ = items.Should().HaveCount(TestData.TestValueThree);
        _ = items[0].Current.Should().Be(1);
        _ = items[1].Current.Should().Be(TestData.TestValueTwo);
        _ = items[TestData.TestValueTwo].Current.Should().Be(TestData.TestValueThree);
    }

    /// <summary>ChangeSet indexer returns correct change.</summary>
    [Test]
    public void ChangeSet_Indexer_ReturnsCorrectChange()
    {
        // Arrange
        var changes = new Change<int>[]
        {
            Change<int>.CreateAdd(TestData.TestValueTen, 0),
            Change<int>.CreateAdd(TestData.TestValueTwenty, 1),
            Change<int>.CreateAdd(TestData.TestValueThirty, TestData.TestValueTwo)
        };
        var changeSet = new ChangeSet<int>(changes);

        // Act & Assert
        _ = changeSet[0].Current.Should().Be(TestData.TestValueTen);
        _ = changeSet[1].Current.Should().Be(TestData.TestValueTwenty);
        _ = changeSet[TestData.TestValueTwo].Current.Should().Be(TestData.TestValueThirty);
    }

    /// <summary>ChangeSet indexer throws on out of range.</summary>
    [Test]
    public void ChangeSet_Indexer_ThrowsOnOutOfRange()
    {
        // Arrange
        var changeSet = new ChangeSet<int>([Change<int>.CreateAdd(1, 0)]);

        // Act & Assert
        Action readOutOfRange = () => _ = changeSet[TestData.TestValueFive];
        _ = readOutOfRange.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>Change factory methods create correct change types.</summary>
    [Test]
    public void Change_FactoryMethods_CreateCorrectChangeTypes()
    {
        // Act
        var add = Change<int>.CreateAdd(1, 0);
        var remove = Change<int>.CreateRemove(TestData.TestValueTwo, 1);
        var update = Change<int>.CreateUpdate(TestData.TestValueThree, TestData.TestValueTwo, 1);
        var move = Change<int>.CreateMove(TestData.TestValueFour, TestData.TestValueTwo, 0);
        var refresh = Change<int>.CreateRefresh(TestData.TestValueFive, TestData.TestValueTwo);

        // Assert
        _ = add.Reason.Should().Be(ChangeReason.Add);
        _ = add.Current.Should().Be(1);
        _ = add.CurrentIndex.Should().Be(0);

        _ = remove.Reason.Should().Be(ChangeReason.Remove);
        _ = remove.Current.Should().Be(TestData.TestValueTwo);
        _ = remove.PreviousIndex.Should().Be(1);

        _ = update.Reason.Should().Be(ChangeReason.Update);
        _ = update.Current.Should().Be(TestData.TestValueThree);
        _ = update.Previous.Should().Be(TestData.TestValueTwo);
        _ = update.CurrentIndex.Should().Be(1);

        _ = move.Reason.Should().Be(ChangeReason.Move);
        _ = move.Current.Should().Be(TestData.TestValueFour);
        _ = move.CurrentIndex.Should().Be(TestData.TestValueTwo);
        _ = move.PreviousIndex.Should().Be(0);

        _ = refresh.Reason.Should().Be(ChangeReason.Refresh);
        _ = refresh.Current.Should().Be(TestData.TestValueFive);
        _ = refresh.CurrentIndex.Should().Be(TestData.TestValueTwo);
    }

#if NET6_0_OR_GREATER || NETFRAMEWORK
    /// <summary>ToArray returns snapshot of current items.</summary>
    [Test]
    public void ToArray_ReturnsSnapshot()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);

        // Act
        var snapshot = list.ToArray();

        // Assert
        _ = snapshot.Should().BeEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
    }

    /// <summary>ToArray returns empty array for empty list.</summary>
    [Test]
    public void ToArray_ReturnsEmptyArray_ForEmptyList()
    {
        // Arrange
        using var list = new ReactiveList<int>();

        // Act
        var snapshot = list.ToArray();

        // Assert
        _ = snapshot.Should().BeEmpty();
    }
#endif
}
