// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
        observable.Should().NotBeNull();
    }

    /// <summary>Connect emits the current snapshot for preloaded sources.</summary>
    [Test]
    public void Connect_EmitsInitialSnapshot_WhenSourceHasItems()
    {
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var receivedChanges = new List<ChangeSet<int>>();

        using var subscription = list.Connect().Subscribe(receivedChanges.Add);

        receivedChanges.Should().ContainSingle();
        receivedChanges[0].Count.Should().Be(TestData.TestValueThree);
        receivedChanges[0].Adds.Should().Be(TestData.TestValueThree);
        receivedChanges[0].Select(change => change.Current).Should().Equal(1, TestData.TestValueTwo, TestData.TestValueThree);
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
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Count.Should().Be(1);
        receivedChanges[0].Adds.Should().Be(1);
        receivedChanges[0][0].Reason.Should().Be(ChangeReason.Add);
        receivedChanges[0][0].Current.Should().Be(TestData.TestValueFortyTwo);
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
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Count.Should().Be(TestData.TestValueFive);
        receivedChanges[0].Adds.Should().Be(TestData.TestValueFive);
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
        list.Remove(TestData.TestValueTwo);

        // Assert
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Count.Should().Be(1);
        receivedChanges[0].Removes.Should().Be(1);
        receivedChanges[0][0].Reason.Should().Be(ChangeReason.Remove);
        receivedChanges[0][0].Current.Should().Be(TestData.TestValueTwo);
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
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Count.Should().Be(TestData.TestValueThree); // One Remove change per cleared item
        receivedChanges[0].Removes.Should().Be(TestData.TestValueThree);
        receivedChanges[0][0].Reason.Should().Be(ChangeReason.Remove);
        receivedChanges[0][1].Reason.Should().Be(ChangeReason.Remove);
        receivedChanges[0][TestData.TestValueTwo].Reason.Should().Be(ChangeReason.Remove);
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
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Count.Should().Be(1);
        receivedChanges[0].Moves.Should().Be(1);
        receivedChanges[0][0].Reason.Should().Be(ChangeReason.Move);
        receivedChanges[0][0].Current.Should().Be(1);
        receivedChanges[0][0].CurrentIndex.Should().Be(TestData.TestValueFour);
        receivedChanges[0][0].PreviousIndex.Should().Be(0);
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
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Count.Should().Be(1);
        receivedChanges[0].Updates.Should().Be(1);
        receivedChanges[0][0].Reason.Should().Be(ChangeReason.Update);
        receivedChanges[0][0].Current.Should().Be(TestData.TestValueTwenty);
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
        changeSet.Count.Should().Be(TestData.TestValueFive);
        changeSet.Adds.Should().Be(TestData.TestValueTwo);
        changeSet.Removes.Should().Be(1);
        changeSet.Updates.Should().Be(1);
        changeSet.Moves.Should().Be(1);
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
        var items = changeSet.ToList();

        // Assert
        items.Should().HaveCount(TestData.TestValueThree);
        items[0].Current.Should().Be(1);
        items[1].Current.Should().Be(TestData.TestValueTwo);
        items[TestData.TestValueTwo].Current.Should().Be(TestData.TestValueThree);
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
        changeSet[0].Current.Should().Be(TestData.TestValueTen);
        changeSet[1].Current.Should().Be(TestData.TestValueTwenty);
        changeSet[TestData.TestValueTwo].Current.Should().Be(TestData.TestValueThirty);
    }

    /// <summary>ChangeSet indexer throws on out of range.</summary>
    [Test]
    public void ChangeSet_Indexer_ThrowsOnOutOfRange()
    {
        // Arrange
        var changeSet = new ChangeSet<int>([Change<int>.CreateAdd(1, 0)]);

        // Act & Assert
        Action readOutOfRange = () => _ = changeSet[TestData.TestValueFive];
        readOutOfRange.Should().Throw<ArgumentOutOfRangeException>();
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
        add.Reason.Should().Be(ChangeReason.Add);
        add.Current.Should().Be(1);
        add.CurrentIndex.Should().Be(0);

        remove.Reason.Should().Be(ChangeReason.Remove);
        remove.Current.Should().Be(TestData.TestValueTwo);
        remove.PreviousIndex.Should().Be(1);

        update.Reason.Should().Be(ChangeReason.Update);
        update.Current.Should().Be(TestData.TestValueThree);
        update.Previous.Should().Be(TestData.TestValueTwo);
        update.CurrentIndex.Should().Be(1);

        move.Reason.Should().Be(ChangeReason.Move);
        move.Current.Should().Be(TestData.TestValueFour);
        move.CurrentIndex.Should().Be(TestData.TestValueTwo);
        move.PreviousIndex.Should().Be(0);

        refresh.Reason.Should().Be(ChangeReason.Refresh);
        refresh.Current.Should().Be(TestData.TestValueFive);
        refresh.CurrentIndex.Should().Be(TestData.TestValueTwo);
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
        snapshot.Should().BeEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
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
        snapshot.Should().BeEmpty();
    }
#endif
}
