// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
#endif
using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
#else
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
#endif
using ReactiveList.Test;
using TUnit.Assertions;
using TUnit.Core;

namespace ReactiveList.Tests;

/// <summary>
/// Additional comprehensive tests for ReactiveListExtensions covering OnUpdate, OnMove,
/// FilterDynamic, GroupByChanges, GroupingByChanges, AutoRefresh, Connect, WhereItems, and SortBy.
/// </summary>
public class ReactiveListExtensionsAdditionalTests
{
    /// <summary>Tests that OnUpdate returns previous and current values when items are updated.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnUpdate_ReturnsPreviousAndCurrentValues()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        var updates = new List<(string? Previous, string Current)>();

        using var subscription = list.Connect()
            .OnUpdate()
            .Subscribe(updates.Add);

        // Act - use Update method (indexer does Remove+Add, not Update)
        list.Add(TestData.OriginalText);
        list.Update(TestData.OriginalText, "updated");

        // Assert - Previous should contain the original value
        await Assert.That(updates).Count().IsEqualTo(1);
        await Assert.That(updates[0].Previous).IsEqualTo(TestData.OriginalText);
        await Assert.That(updates[0].Current).IsEqualTo("updated");
    }

    /// <summary>Tests that OnUpdate does not emit for add operations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnUpdate_DoesNotEmitForAddOperations()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var updateCount = 0;

        using var subscription = list.Connect()
            .OnUpdate()
            .Subscribe(_ => updateCount++);

        // Act
        list.Add(1);
        list.Add(TestData.TestValueTwo);
        list.Add(TestData.TestValueThree);

        // Assert
        await Assert.That(updateCount).IsEqualTo(0);
    }

    /// <summary>Tests that OnUpdate handles multiple sequential updates with previous values.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnUpdate_HandlesMultipleSequentialUpdates()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var updates = new List<(int Previous, int Current)>();

        using var subscription = list.Connect()
            .OnUpdate()
            .Subscribe(updates.Add);

        // Act - use Update method (indexer does Remove+Add, not Update)
        list.Add(1);
        list.Update(1, TestData.TestValueTen);
        list.Update(TestData.TestValueTen, TestData.TestValueOneHundred);
        list.Update(TestData.TestValueOneHundred, TestData.TestValueOneThousand);

        // Assert - Previous should contain the actual previous value
        await Assert.That(updates).Count().IsEqualTo(TestData.TestValueThree);
        await Assert.That(updates[0].Previous).IsEqualTo(1);
        await Assert.That(updates[0].Current).IsEqualTo(TestData.TestValueTen);
        await Assert.That(updates[1].Previous).IsEqualTo(TestData.TestValueTen);
        await Assert.That(updates[1].Current).IsEqualTo(TestData.TestValueOneHundred);
        await Assert.That(updates[TestData.TestValueTwo].Previous).IsEqualTo(TestData.TestValueOneHundred);
        await Assert.That(updates[TestData.TestValueTwo].Current).IsEqualTo(TestData.TestValueOneThousand);
    }

    /// <summary>Tests that OnMove returns item and indices when items are moved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnMove_ReturnsItemAndIndices()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        var moves = new List<(string Item, int OldIndex, int NewIndex)>();

        using var subscription = list.Connect()
            .OnMove()
            .Subscribe(moves.Add);

        // Act
        list.AddRange(["a", "b", "c", "d"]);
        list.Move(0, TestData.TestValueThree); // Move "a" from index 0 to index 3

        // Assert
        await Assert.That(moves).Count().IsEqualTo(1);
        await Assert.That(moves[0].Item).IsEqualTo("a");
        await Assert.That(moves[0].OldIndex).IsEqualTo(0);
        await Assert.That(moves[0].NewIndex).IsEqualTo(TestData.TestValueThree);
    }

    /// <summary>Tests that OnMove does not emit for add or remove operations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnMove_DoesNotEmitForAddRemove()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var moveCount = 0;

        using var subscription = list.Connect()
            .OnMove()
            .Subscribe(_ => moveCount++);

        // Act
        list.Add(1);
        list.Add(TestData.TestValueTwo);
        _ = list.Remove(1);

        // Assert
        await Assert.That(moveCount).IsEqualTo(0);
    }

    /// <summary>Tests that OnMove handles multiple move operations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnMove_HandlesMultipleMoves()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var moves = new List<(int Item, int OldIndex, int NewIndex)>();

        using var subscription = list.Connect()
            .OnMove()
            .Subscribe(moves.Add);

        // Act
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
        list.Move(0, TestData.TestValueFour); // Move 1 to end
        list.Move(TestData.TestValueThree, 0); // Move 1 back to start (it's now at index 3)

        // Assert
        await Assert.That(moves).Count().IsEqualTo(TestData.TestValueTwo);
    }

    /// <summary>Tests that FilterDynamic filters items based on dynamic predicate.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FilterDynamic_FiltersBasedOnDynamicPredicate()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        using var filterSubject = new BehaviorSignal<Func<int, bool>>(static _ => true);
        var receivedItems = new List<int>();

        using var subscription = list.Stream
            .FilterDynamic(filterSubject)
            .Subscribe(notification =>
            {
                if (notification.Item == 0)
                {
                    return;
                }

                receivedItems.Add(notification.Item);
            });

        // Act - add items with all-pass filter
        list.Add(1);
        list.Add(TestData.TestValueTwo);
        list.Add(TestData.TestValueThree);

        // Assert
        await Assert.That(receivedItems).IsEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree]);

        // Act - change filter to only even numbers
        receivedItems.Clear();
        filterSubject.OnNext(static x => x % TestData.TestValueTwo == 0);
        list.Add(TestData.TestValueFour);
        list.Add(TestData.TestValueFive);

        // Assert - only even number should be received
        await Assert.That(receivedItems).IsEquivalentTo([TestData.TestValueFour]);
    }

    /// <summary>Tests that FilterDynamic always passes removed items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FilterDynamic_AlwaysPassesRemovedItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        using var filterSubject = new BehaviorSignal<Func<int, bool>>(static x => x > TestData.TestValueFive);
        var removedItems = new List<int>();

        using var subscription = list.Stream
            .FilterDynamic(filterSubject)
            .Subscribe(notification =>
            {
                if (notification.Action != CacheAction.Removed || notification.Item == 0)
                {
                    return;
                }

                removedItems.Add(notification.Item);
            });

        // Act - add items (only > 5 pass filter)
        list.Add(TestData.TestValueThree); // filtered out on add
        list.Add(TestData.TestValueTen); // passes filter
        _ = list.Remove(TestData.TestValueThree); // should still emit remove

        // Assert
        await Assert.That(removedItems).Contains(TestData.TestValueThree);
    }

    /// <summary>Tests that FilterDynamic passes Cleared notifications.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FilterDynamic_PassesClearedNotifications()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        using var filterSubject = new BehaviorSignal<Func<int, bool>>(static x => x > 0);
        var clearReceived = false;

        using var subscription = list.Stream
            .FilterDynamic(filterSubject)
            .Subscribe(notification =>
            {
                if (notification.Action != CacheAction.Cleared)
                {
                    return;
                }

                clearReceived = true;
            });

        // Act
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree]);
        list.Clear();

        // Assert
        await Assert.That(clearReceived).IsTrue();
    }

    /// <summary>Tests that CreateView without filter contains all items.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateView_WithoutFilter_ContainsAllItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);

        // Act
        using var view = list.CreateView(Sequencer.Immediate, 0);
        await Task.Delay(TestData.TestValueFifty);

        // Assert
        await Assert.That(view.Count).IsEqualTo(TestData.TestValueFive);
        await Assert.That(view).IsEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
    }

    /// <summary>Tests that CreateView without filter updates when source changes.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateView_WithoutFilter_UpdatesOnSourceChange()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree]);

        using var view = list.CreateView(Sequencer.Immediate, 0);
        await Task.Delay(TestData.TestValueFifty);

        // Act
        list.Add(TestData.TestValueFour);
        await Task.Delay(TestData.TestValueFifty);

        // Assert
        await Assert.That(view).IsEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour]);
    }

#if NET8_0_OR_GREATER || NETFRAMEWORK

    /// <summary>Tests that CreateView with query observable filters based on query.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateView_WithQueryObservable_FiltersBasedOnQuery()
    {
        // Arrange
        using var list = new QuaternaryList<string>();
        list.AddRange([TestData.AppleText, TestData.BananaText, TestData.ApricotText, TestData.CherryText, "avocado"]);

        using var searchQuery = new BehaviorSignal<string>(string.Empty);

        // Act
        using var view = list.CreateView(
            searchQuery,
            static (query, item) => string.IsNullOrEmpty(query) || item.StartsWith(query, StringComparison.OrdinalIgnoreCase),
            Sequencer.Immediate,
            0);

        await Task.Delay(TestData.TestValueFifty);

        // Initial - all items
        await Assert.That(view.Items.Count).IsEqualTo(TestData.TestValueFive);

        // Search for "a"
        searchQuery.OnNext("a");
        await Task.Delay(TestData.TestValueOneHundred);

        await Assert.That(view.Items).IsEquivalentTo([TestData.AppleText, TestData.ApricotText, "avocado"]);

        // Search for "ap"
        searchQuery.OnNext("ap");
        await Task.Delay(TestData.TestValueOneHundred);

        await Assert.That(view.Items).IsEquivalentTo([TestData.AppleText, TestData.ApricotText]);
    }

    /// <summary>Tests that CreateView with query observable updates when source changes.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateView_WithQueryObservable_UpdatesWhenSourceChanges()
    {
        // Arrange
        using var list = new QuaternaryList<int>();
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree]);

        using var thresholdQuery = new BehaviorSignal<int>(TestData.TestValueTwo);

        using var view = list.CreateView(
            thresholdQuery,
            static (threshold, item) => item > threshold,
            Sequencer.Immediate,
            0);

        await Task.Delay(TestData.TestValueFifty);
        await Assert.That(view.Items).IsEquivalentTo([TestData.TestValueThree]);

        // Act - add item that passes filter
        list.Add(TestData.TestValueFive);
        await Task.Delay(TestData.TestValueOneHundred);

        // Assert
        await Assert.That(view.Items).IsEquivalentTo([TestData.TestValueThree, TestData.TestValueFive]);

        // Act - change threshold
        thresholdQuery.OnNext(TestData.TestValueFour);
        await Task.Delay(TestData.TestValueOneHundred);

        // Assert
        await Assert.That(view.Items).IsEquivalentTo([TestData.TestValueFive]);
    }
#endif

    /// <summary>Tests that GroupByChanges groups items by key selector.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GroupByChanges_GroupsItemsByKeySelector()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var groups = new Dictionary<string, List<int>>();

        using var subscription = list.Connect()
            .GroupByChanges(static x => x % TestData.TestValueTwo == 0 ? "even" : "odd")
            .Subscribe(group =>
            {
#if NET8_0_OR_GREATER
                ref var value = ref CollectionsMarshal.GetValueRefOrAddDefault(groups, group.Key, out _);
                value ??= [];
                _ = group.Subscribe(value.Add);
#else
                if (!groups.TryGetValue(group.Key, out var value))
                {
                    value = [];
                    groups.Add(group.Key, value);
                }

                _ = group.Subscribe(value.Add);
#endif
            });

        // Act
        list.Add(1);
        list.Add(TestData.TestValueTwo);
        list.Add(TestData.TestValueThree);
        list.Add(TestData.TestValueFour);

        // Assert
        await Assert.That(groups).ContainsKey("odd");
        await Assert.That(groups).ContainsKey("even");
        await Assert.That(groups["odd"]).IsEquivalentTo([1, TestData.TestValueThree]);
        await Assert.That(groups["even"]).IsEquivalentTo([TestData.TestValueTwo, TestData.TestValueFour]);
    }

    /// <summary>Tests that GroupByChanges handles string keys.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GroupByChanges_HandlesStringKeys()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        var groups = new Dictionary<char, List<string>>();

        using var subscription = list.Connect()
            .GroupByChanges(static s => s[0])
            .Subscribe(group =>
            {
#if NET8_0_OR_GREATER
                ref var value = ref CollectionsMarshal.GetValueRefOrAddDefault(groups, group.Key, out _);
                value ??= [];
                _ = group.Subscribe(value.Add);
#else
                if (!groups.TryGetValue(group.Key, out var value))
                {
                    value = [];
                    groups.Add(group.Key, value);
                }

                _ = group.Subscribe(value.Add);
#endif
            });

        // Act
        list.Add(TestData.AppleText);
        list.Add(TestData.BananaText);
        list.Add(TestData.ApricotText);
        list.Add(TestData.CherryText);

        // Assert
        await Assert.That(groups['a']).IsEquivalentTo([TestData.AppleText, TestData.ApricotText]);
        await Assert.That(groups['b']).IsEquivalentTo([TestData.BananaText]);
        await Assert.That(groups['c']).IsEquivalentTo([TestData.CherryText]);
    }

    /// <summary>Tests that GroupingByChanges creates proper groupings.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GroupingByChanges_CreatesProperGroupings()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var groupings = new List<IGrouping<string, Change<int>>>();

        using var subscription = list.Connect()
            .GroupingByChanges(static x => x % TestData.TestValueTwo == 0 ? "even" : "odd")
            .Subscribe(groupings.Add);

        // Act
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour]);

        // Assert - each add creates a separate changeset, which creates groupings
        await Assert.That(groupings).Count(static count => count.IsGreaterThan(0));
    }

    /// <summary>Tests that GroupingByChanges handles batch operations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GroupingByChanges_HandlesBatchAdd()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var groupings = new List<IGrouping<int, Change<int>>>();

        using var subscription = list.Connect()
            .GroupingByChanges(static x => x / TestData.TestValueTen) // Group by tens
            .Subscribe(groupings.Add);

        // Act - add items in different decades
        list.AddRange([TestData.TestValueFive, TestData.TestValueFifteen, TestData.TestValueTwentyFive, TestData.TestValueSeven, TestData.TestValueSeventeen]);

        // Assert
        await Assert.That(groupings).Count(static count => count.IsGreaterThan(0));
        var keys = GetDistinctKeys(groupings);
        await Assert.That(keys).Contains(0); // 5, 7
        await Assert.That(keys).Contains(1); // 15, 17
        await Assert.That(keys).Contains(TestData.TestValueTwo); // 25
    }

    /// <summary>Tests that AutoRefresh emits refresh when property changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AutoRefresh_EmitsRefreshWhenPropertyChanges()
    {
        // Arrange
        using var list = new ReactiveList<NotifyingItem>();
        var refreshCount = 0;

        var item = new NotifyingItem { Name = "Original" };

        using var subscription = list.Connect()
            .AutoRefresh(nameof(NotifyingItem.Name))
            .WhereReason(ChangeReason.Refresh)
            .Subscribe(_ => refreshCount++);

        // Act
        list.Add(item);
        item.Name = "Updated";

        // Assert
        await Assert.That(refreshCount).IsEqualTo(1);
    }

    /// <summary>Tests that AutoRefresh does not emit for unrelated property changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AutoRefresh_DoesNotEmitForUnrelatedPropertyChanges()
    {
        // Arrange
        using var list = new ReactiveList<NotifyingItem>();
        var refreshCount = 0;

        var item = new NotifyingItem { Name = "Test", Value = 1 };

        using var subscription = list.Connect()
            .AutoRefresh(nameof(NotifyingItem.Name))
            .WhereReason(ChangeReason.Refresh)
            .Subscribe(_ => refreshCount++);

        // Act
        list.Add(item);
        item.Value = TestData.TestValueOneHundred; // Change different property

        // Assert
        await Assert.That(refreshCount).IsEqualTo(0);
    }

    /// <summary>Tests that AutoRefresh without property name watches all property changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AutoRefresh_WithoutPropertyName_WatchesAllProperties()
    {
        // Arrange
        using var list = new ReactiveList<NotifyingItem>();
        var refreshCount = 0;

        var item = new NotifyingItem { Name = "Test", Value = 1 };

        using var subscription = list.Connect()
            .AutoRefresh()
            .WhereReason(ChangeReason.Refresh)
            .Subscribe(_ => refreshCount++);

        // Act
        list.Add(item);
        item.Name = "Updated Name";
        item.Value = TestData.TestValueTwo;

        // Assert - should get refresh for both property changes
        await Assert.That(refreshCount).IsEqualTo(TestData.TestValueTwo);
    }

    /// <summary>Tests that Connect returns observable of change sets.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_ReturnsObservableOfChangeSets()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var changeSets = new List<ChangeSet<int>>();

        using var subscription = list.Connect()
            .Subscribe(changeSets.Add);

        // Act
        list.Add(1);
        list.Add(TestData.TestValueTwo);
        list.Add(TestData.TestValueThree);

        // Assert
        await Assert.That(changeSets).Count().IsEqualTo(TestData.TestValueThree);
        await Assert.That(GetCurrentItems(changeSets)).IsEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree]);
    }

    /// <summary>Tests that Connect throws for null source.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_ThrowsForNullSource()
    {
        // Arrange
        IReactiveSource<int>? nullSource = null;

        // Act & Assert
        var act = () => nullSource!.Connect();
        await Assert.That(act).Throws<ArgumentNullException>();
    }

    /// <summary>Tests that WhereItems filters notifications by predicate.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WhereItems_FiltersNotificationsByPredicate()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var receivedItems = new List<int>();

        using var subscription = list.Stream
            .WhereItems(static x => x > TestData.TestValueFive)
            .Subscribe(notification =>
            {
                if (notification.Action != CacheAction.Added)
                {
                    return;
                }

                receivedItems.Add(notification.Item);
            });

        // Act
        list.Add(TestData.TestValueThree);
        list.Add(TestData.TestValueSeven);
        list.Add(TestData.TestValueTwo);
        list.Add(TestData.TestValueTen);

        // Assert - only items > 5 should be received
        await Assert.That(receivedItems).IsEquivalentTo([TestData.TestValueSeven, TestData.TestValueTen]);
    }

    /// <summary>Tests that WhereItems passes Cleared notifications.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WhereItems_PassesClearedNotifications()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        var clearedReceived = false;

        using var subscription = list.Stream
            .WhereItems(static x => x.Length > 5)
            .Subscribe(notification =>
            {
                if (notification.Action != CacheAction.Cleared)
                {
                    return;
                }

                clearedReceived = true;
            });

        // Act
        list.AddRange(["short", "longertext", "x"]);
        list.Clear();

        // Assert
        await Assert.That(clearedReceived).IsTrue();
    }

    /// <summary>Tests that WhereItems passes BatchOperation notifications.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WhereItems_PassesBatchOperations()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        var batchReceived = false;

        using var subscription = list.Stream
            .WhereItems(static x => x.Length > 5)
            .Subscribe(notification =>
            {
                if (notification.Action != CacheAction.BatchAdded
                    && notification.Action != CacheAction.BatchOperation)
                {
                    return;
                }

                batchReceived = true;
            });

        // Act
        list.AddRange(["short", "medium", "verylongtext", "x"]);

        // Assert
        await Assert.That(batchReceived).IsTrue();
    }

    /// <summary>Tests that WhereItems correctly filters value types including zero.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WhereItems_HandlesValueTypesIncludingZero()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var receivedItems = new List<int>();

        using var subscription = list.Stream
            .WhereItems(static x => x >= 0) // Filter: all non-negative numbers including 0
            .Subscribe(notification =>
            {
                if (notification.Action != CacheAction.Added)
                {
                    return;
                }

                receivedItems.Add(notification.Item);
            });

        // Act
        list.Add(-1); // Should be filtered out
        list.Add(0); // Should be included (this was the bug - 0 would be treated as "no item")
        list.Add(TestData.TestValueFive); // Should be included
        list.Add(TestData.TestValueNegativeFive); // Should be filtered out
        list.Add(TestData.TestValueTen); // Should be included

        // Assert - 0 should be correctly included
        await Assert.That(receivedItems).IsEquivalentTo([0, TestData.TestValueFive, TestData.TestValueTen]);
    }

    /// <summary>Tests that SortBy sorts change sets by key selector.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SortBy_SortsChangeSetsByKeySelector()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var sortedItems = new List<int>();

        using var subscription = list.Connect()
            .SortBy(static x => x)
            .Subscribe(cs =>
            {
                sortedItems.Clear();
                foreach (var change in cs)
                {
                    sortedItems.Add(change.Current);
                }
            });

        // Act
        list.AddRange([TestData.TestValueFive, 1, TestData.TestValueThree, TestData.TestValueTwo, TestData.TestValueFour]);

        // Assert
        await Assert.That(sortedItems).IsInOrder();
    }

    /// <summary>Tests that SortBy handles string sorting.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SortBy_HandlesStringSorting()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        var sortedItems = new List<string>();

        using var subscription = list.Connect()
            .SortBy(static s => s.Length)
            .Subscribe(cs =>
            {
                sortedItems.Clear();
                foreach (var change in cs)
                {
                    sortedItems.Add(change.Current);
                }
            });

        // Act
        list.AddRange(["elephant", "cat", "dog", "bird"]);

        // Assert
        await Assert.That(GetLengths(sortedItems)).IsInOrder();
    }

    /// <summary>Tests that SelectChanges transforms to different type maintaining change metadata.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SelectChanges_TransformsToDifferentType()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var transformedSets = new List<ChangeSet<string>>();

        using var subscription = list.Connect()
            .SelectChanges(static (int x) => $"Value:{x}")
            .Subscribe(transformedSets.Add);

        // Act
        list.Add(1);
        list.Add(TestData.TestValueTwo);

        // Assert
        await Assert.That(transformedSets).Count().IsEqualTo(TestData.TestValueTwo);
        await Assert.That(transformedSets[0][0].Current).IsEqualTo("Value:1");
        await Assert.That(transformedSets[1][0].Current).IsEqualTo("Value:2");
    }

    /// <summary>Tests that SelectChanges preserves change reason.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SelectChanges_PreservesChangeReason()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var reasons = new List<ChangeReason>();

        using var subscription = list.Connect()
            .SelectChanges(static (int x) => x.ToString())
            .Subscribe(cs =>
            {
                foreach (var change in cs)
                {
                    reasons.Add(change.Reason);
                }
            });

        // Act - use Update method (indexer does Remove+Add, not Update)
        list.Add(1);
        list.Update(1, TestData.TestValueTwo);
        _ = list.Remove(TestData.TestValueTwo);

        // Assert
        await Assert.That(reasons).Contains(ChangeReason.Add);
        await Assert.That(reasons).Contains(ChangeReason.Update);
        await Assert.That(reasons).Contains(ChangeReason.Remove);
    }

    /// <summary>Collects distinct grouping keys without allocating a LINQ pipeline.</summary>
    /// <typeparam name="TKey">The grouping key type.</typeparam>
    /// <typeparam name="TElement">The grouping element type.</typeparam>
    /// <param name="groupings">The groupings.</param>
    /// <returns>The distinct keys.</returns>
    private static List<TKey> GetDistinctKeys<TKey, TElement>(IEnumerable<IGrouping<TKey, TElement>> groupings)
    {
        var keys = new List<TKey>();
        foreach (var grouping in groupings)
        {
            if (!keys.Contains(grouping.Key))
            {
                keys.Add(grouping.Key);
            }
        }

        return keys;
    }

    /// <summary>Collects the current items from change sets.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="changeSets">The change sets.</param>
    /// <returns>The current items.</returns>
    private static List<T> GetCurrentItems<T>(IEnumerable<ChangeSet<T>> changeSets)
    {
        var items = new List<T>();
        foreach (var changeSet in changeSets)
        {
            foreach (var change in changeSet)
            {
                items.Add(change.Current);
            }
        }

        return items;
    }

    /// <summary>Gets the lengths of the supplied strings.</summary>
    /// <param name="items">The strings.</param>
    /// <returns>The string lengths.</returns>
    private static List<int> GetLengths(IEnumerable<string> items)
    {
        var lengths = new List<int>();
        foreach (var item in items)
        {
            lengths.Add(item.Length);
        }

        return lengths;
    }

    /// <summary>Test class that implements INotifyPropertyChanged.</summary>
    private sealed class NotifyingItem : INotifyPropertyChanged
    {
        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Gets or sets Value.</summary>
        public string Name
        {
            get;
            set
            {
                if (field == value)
                {
                    return;
                }

                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        } = string.Empty;

        /// <summary>Gets or sets Value.</summary>
        public int Value
        {
            get;
            set
            {
                if (field == value)
                {
                    return;
                }

                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }
}
