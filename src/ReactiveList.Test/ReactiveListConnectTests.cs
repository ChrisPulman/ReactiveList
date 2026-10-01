// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
#if REACTIVELIST_REACTIVE
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
#else
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
#endif
using TUnit.Assertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for the ReactiveList Connect() method and unified ChangeSet.</summary>
public class ReactiveListConnectTests
{
    /// <summary>Connect returns observable stream.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_ReturnsObservableStream()
    {
        // Arrange
        using var list = new ReactiveList<int>();

        // Act
        var observable = list.Connect();

        // Assert
        await Assert.That(observable).IsNotNull();
    }

    /// <summary>Connect emits the current snapshot for preloaded sources.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_EmitsInitialSnapshot_WhenSourceHasItems()
    {
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var receivedChanges = new List<ChangeSet<int>>();

        using var subscription = list.Connect().Subscribe(receivedChanges.Add);

        await Assert.That(receivedChanges).HasSingleItem();
        await Assert.That(receivedChanges[0].Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(receivedChanges[0].Adds).IsEqualTo(TestData.TestValueThree);
        var currentItems = new List<int>(receivedChanges[0].Count);
        foreach (var change in receivedChanges[0])
        {
            currentItems.Add(change.Current);
        }

        await Assert.That(currentItems).IsEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree], CollectionOrdering.Matching);
    }

    /// <summary>Connect emits add changes when items are added.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_EmitsAddChanges_WhenItemsAdded()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(receivedChanges.Add);

        // Act
        list.Add(TestData.TestValueFortyTwo);

        // Assert
        await Assert.That(receivedChanges).Count().IsEqualTo(1);
        await Assert.That(receivedChanges[0].Count).IsEqualTo(1);
        await Assert.That(receivedChanges[0].Adds).IsEqualTo(1);
        await Assert.That(receivedChanges[0][0].Reason).IsEqualTo(ChangeReason.Add);
        await Assert.That(receivedChanges[0][0].Current).IsEqualTo(TestData.TestValueFortyTwo);
    }

    /// <summary>Connect emits batch add changes when AddRange is called.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_EmitsBatchAddChanges_WhenAddRangeCalled()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(receivedChanges.Add);

        // Act
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);

        // Assert
        await Assert.That(receivedChanges).Count().IsEqualTo(1);
        await Assert.That(receivedChanges[0].Count).IsEqualTo(TestData.TestValueFive);
        await Assert.That(receivedChanges[0].Adds).IsEqualTo(TestData.TestValueFive);
    }

    /// <summary>Connect emits remove changes when items are removed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_EmitsRemoveChanges_WhenItemsRemoved()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(receivedChanges.Add);
        receivedChanges.Clear();

        // Act
        _ = list.Remove(TestData.TestValueTwo);

        // Assert
        await Assert.That(receivedChanges).Count().IsEqualTo(1);
        await Assert.That(receivedChanges[0].Count).IsEqualTo(1);
        await Assert.That(receivedChanges[0].Removes).IsEqualTo(1);
        await Assert.That(receivedChanges[0][0].Reason).IsEqualTo(ChangeReason.Remove);
        await Assert.That(receivedChanges[0][0].Current).IsEqualTo(TestData.TestValueTwo);
    }

    /// <summary>
    /// Connect emits clear changes when collection is cleared.
    /// Clear emits individual Remove changes for each cleared item (consistent with DynamicData behavior).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_EmitsClearChanges_WhenCleared()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(receivedChanges.Add);
        receivedChanges.Clear();

        // Act
        list.Clear();

        // Assert - Clear emits Remove changes for each item (DynamicData compatible behavior)
        await Assert.That(receivedChanges).Count().IsEqualTo(1);
        await Assert.That(receivedChanges[0].Count).IsEqualTo(TestData.TestValueThree); // One Remove change per cleared item
        await Assert.That(receivedChanges[0].Removes).IsEqualTo(TestData.TestValueThree);
        await Assert.That(receivedChanges[0][0].Reason).IsEqualTo(ChangeReason.Remove);
        await Assert.That(receivedChanges[0][1].Reason).IsEqualTo(ChangeReason.Remove);
        await Assert.That(receivedChanges[0][TestData.TestValueTwo].Reason).IsEqualTo(ChangeReason.Remove);
    }

    /// <summary>Connect emits move changes when item is moved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_EmitsMoveChanges_WhenItemMoved()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(receivedChanges.Add);
        receivedChanges.Clear();

        // Act
        list.Move(0, TestData.TestValueFour);

        // Assert
        await Assert.That(receivedChanges).Count().IsEqualTo(1);
        await Assert.That(receivedChanges[0].Count).IsEqualTo(1);
        await Assert.That(receivedChanges[0].Moves).IsEqualTo(1);
        await Assert.That(receivedChanges[0][0].Reason).IsEqualTo(ChangeReason.Move);
        await Assert.That(receivedChanges[0][0].Current).IsEqualTo(1);
        await Assert.That(receivedChanges[0][0].CurrentIndex).IsEqualTo(TestData.TestValueFour);
        await Assert.That(receivedChanges[0][0].PreviousIndex).IsEqualTo(0);
    }

    /// <summary>Connect emits update changes when item is updated.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_EmitsUpdateChanges_WhenItemUpdated()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = list.Connect().Subscribe(receivedChanges.Add);
        receivedChanges.Clear();

        // Act
        list.Update(TestData.TestValueTwo, TestData.TestValueTwenty);

        // Assert
        await Assert.That(receivedChanges).Count().IsEqualTo(1);
        await Assert.That(receivedChanges[0].Count).IsEqualTo(1);
        await Assert.That(receivedChanges[0].Updates).IsEqualTo(1);
        await Assert.That(receivedChanges[0][0].Reason).IsEqualTo(ChangeReason.Update);
        await Assert.That(receivedChanges[0][0].Current).IsEqualTo(TestData.TestValueTwenty);
    }

    /// <summary>ChangeSet correctly counts different change types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChangeSet_CorrectlyCounts_DifferentChangeTypes()
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
        await Assert.That(changeSet.Count).IsEqualTo(TestData.TestValueFive);
        await Assert.That(changeSet.Adds).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(changeSet.Removes).IsEqualTo(1);
        await Assert.That(changeSet.Updates).IsEqualTo(1);
        await Assert.That(changeSet.Moves).IsEqualTo(1);
    }

    /// <summary>ChangeSet can be enumerated.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChangeSet_CanBeEnumerated()
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
        await Assert.That(items).Count().IsEqualTo(TestData.TestValueThree);
        await Assert.That(items[0].Current).IsEqualTo(1);
        await Assert.That(items[1].Current).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(items[TestData.TestValueTwo].Current).IsEqualTo(TestData.TestValueThree);
    }

    /// <summary>ChangeSet indexer returns correct change.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChangeSet_Indexer_ReturnsCorrectChange()
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
        await Assert.That(changeSet[0].Current).IsEqualTo(TestData.TestValueTen);
        await Assert.That(changeSet[1].Current).IsEqualTo(TestData.TestValueTwenty);
        await Assert.That(changeSet[TestData.TestValueTwo].Current).IsEqualTo(TestData.TestValueThirty);
    }

    /// <summary>ChangeSet indexer throws on out of range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChangeSet_Indexer_ThrowsOnOutOfRange()
    {
        // Arrange
        var changeSet = new ChangeSet<int>([Change<int>.CreateAdd(1, 0)]);

        // Act & Assert
        Action readOutOfRange = () => _ = changeSet[TestData.TestValueFive];
        await Assert.That(readOutOfRange).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Change factory methods create correct change types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Change_FactoryMethods_CreateCorrectChangeTypes()
    {
        // Act
        var add = Change<int>.CreateAdd(1, 0);
        var remove = Change<int>.CreateRemove(TestData.TestValueTwo, 1);
        var update = Change<int>.CreateUpdate(TestData.TestValueThree, TestData.TestValueTwo, 1);
        var move = Change<int>.CreateMove(TestData.TestValueFour, TestData.TestValueTwo, 0);
        var refresh = Change<int>.CreateRefresh(TestData.TestValueFive, TestData.TestValueTwo);

        // Assert
        await Assert.That(add.Reason).IsEqualTo(ChangeReason.Add);
        await Assert.That(add.Current).IsEqualTo(1);
        await Assert.That(add.CurrentIndex).IsEqualTo(0);

        await Assert.That(remove.Reason).IsEqualTo(ChangeReason.Remove);
        await Assert.That(remove.Current).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(remove.PreviousIndex).IsEqualTo(1);

        await Assert.That(update.Reason).IsEqualTo(ChangeReason.Update);
        await Assert.That(update.Current).IsEqualTo(TestData.TestValueThree);
        await Assert.That(update.Previous).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(update.CurrentIndex).IsEqualTo(1);

        await Assert.That(move.Reason).IsEqualTo(ChangeReason.Move);
        await Assert.That(move.Current).IsEqualTo(TestData.TestValueFour);
        await Assert.That(move.CurrentIndex).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(move.PreviousIndex).IsEqualTo(0);

        await Assert.That(refresh.Reason).IsEqualTo(ChangeReason.Refresh);
        await Assert.That(refresh.Current).IsEqualTo(TestData.TestValueFive);
        await Assert.That(refresh.CurrentIndex).IsEqualTo(TestData.TestValueTwo);
    }

#if NET6_0_OR_GREATER || NETFRAMEWORK
    /// <summary>ToArray returns snapshot of current items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ToArray_ReturnsSnapshot()
    {
        // Arrange
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);

        // Act
        var snapshot = list.ToArray();

        // Assert
        await Assert.That(snapshot).IsEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
    }

    /// <summary>ToArray returns empty array for empty list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ToArray_ReturnsEmptyArray_ForEmptyList()
    {
        // Arrange
        using var list = new ReactiveList<int>();

        // Act
        var snapshot = list.ToArray();

        // Assert
        await Assert.That(snapshot).IsEmpty();
    }
#endif
}
