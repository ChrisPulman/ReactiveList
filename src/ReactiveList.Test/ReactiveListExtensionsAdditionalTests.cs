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
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using FluentAssertions;
using ReactiveList.Test;
using TUnit.Core;

namespace ReactiveList.Tests;

/// <summary>
/// Additional comprehensive tests for ReactiveListExtensions covering OnUpdate, OnMove,
/// FilterDynamic, GroupByChanges, GroupingByChanges, AutoRefresh, Connect, WhereItems, and SortBy.
/// </summary>
public class ReactiveListExtensionsAdditionalTests
{
    /// <summary>Tests that OnUpdate returns previous and current values when items are updated.</summary>
    [Test]
    public void OnUpdate_ReturnsPreviousAndCurrentValues()
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
        _ = updates.Should().HaveCount(1);
        _ = updates[0].Previous.Should().Be(TestData.OriginalText);
        _ = updates[0].Current.Should().Be("updated");
    }

    /// <summary>Tests that OnUpdate does not emit for add operations.</summary>
    [Test]
    public void OnUpdate_DoesNotEmitForAddOperations()
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
        _ = updateCount.Should().Be(0);
    }

    /// <summary>Tests that OnUpdate handles multiple sequential updates with previous values.</summary>
    [Test]
    public void OnUpdate_HandlesMultipleSequentialUpdates()
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
        _ = updates.Should().HaveCount(TestData.TestValueThree);
        _ = updates[0].Previous.Should().Be(1);
        _ = updates[0].Current.Should().Be(TestData.TestValueTen);
        _ = updates[1].Previous.Should().Be(TestData.TestValueTen);
        _ = updates[1].Current.Should().Be(TestData.TestValueOneHundred);
        _ = updates[TestData.TestValueTwo].Previous.Should().Be(TestData.TestValueOneHundred);
        _ = updates[TestData.TestValueTwo].Current.Should().Be(TestData.TestValueOneThousand);
    }

    /// <summary>Tests that OnMove returns item and indices when items are moved.</summary>
    [Test]
    public void OnMove_ReturnsItemAndIndices()
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
        _ = moves.Should().HaveCount(1);
        _ = moves[0].Item.Should().Be("a");
        _ = moves[0].OldIndex.Should().Be(0);
        _ = moves[0].NewIndex.Should().Be(TestData.TestValueThree);
    }

    /// <summary>Tests that OnMove does not emit for add or remove operations.</summary>
    [Test]
    public void OnMove_DoesNotEmitForAddRemove()
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
        _ = moveCount.Should().Be(0);
    }

    /// <summary>Tests that OnMove handles multiple move operations.</summary>
    [Test]
    public void OnMove_HandlesMultipleMoves()
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
        _ = moves.Should().HaveCount(TestData.TestValueTwo);
    }

    /// <summary>Tests that FilterDynamic filters items based on dynamic predicate.</summary>
    [Test]
    public void FilterDynamic_FiltersBasedOnDynamicPredicate()
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
        _ = receivedItems.Should().BeEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree]);

        // Act - change filter to only even numbers
        receivedItems.Clear();
        filterSubject.OnNext(static x => x % TestData.TestValueTwo == 0);
        list.Add(TestData.TestValueFour);
        list.Add(TestData.TestValueFive);

        // Assert - only even number should be received
        _ = receivedItems.Should().BeEquivalentTo([TestData.TestValueFour]);
    }

    /// <summary>Tests that FilterDynamic always passes removed items.</summary>
    [Test]
    public void FilterDynamic_AlwaysPassesRemovedItems()
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
        _ = removedItems.Should().Contain(TestData.TestValueThree);
    }

    /// <summary>Tests that FilterDynamic passes Cleared notifications.</summary>
    [Test]
    public void FilterDynamic_PassesClearedNotifications()
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
        _ = clearReceived.Should().BeTrue();
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
        _ = view.Count.Should().Be(TestData.TestValueFive);
        _ = view.Should().BeEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
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
        _ = view.Should().BeEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour]);
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
        _ = view.Items.Count.Should().Be(TestData.TestValueFive);

        // Search for "a"
        searchQuery.OnNext("a");
        await Task.Delay(TestData.TestValueOneHundred);

        _ = view.Items.Should().BeEquivalentTo([TestData.AppleText, TestData.ApricotText, "avocado"]);

        // Search for "ap"
        searchQuery.OnNext("ap");
        await Task.Delay(TestData.TestValueOneHundred);

        _ = view.Items.Should().BeEquivalentTo([TestData.AppleText, TestData.ApricotText]);
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
        _ = view.Items.Should().BeEquivalentTo([TestData.TestValueThree]);

        // Act - add item that passes filter
        list.Add(TestData.TestValueFive);
        await Task.Delay(TestData.TestValueOneHundred);

        // Assert
        _ = view.Items.Should().BeEquivalentTo([TestData.TestValueThree, TestData.TestValueFive]);

        // Act - change threshold
        thresholdQuery.OnNext(TestData.TestValueFour);
        await Task.Delay(TestData.TestValueOneHundred);

        // Assert
        _ = view.Items.Should().BeEquivalentTo([TestData.TestValueFive]);
    }
#endif

    /// <summary>Tests that GroupByChanges groups items by key selector.</summary>
    [Test]
    public void GroupByChanges_GroupsItemsByKeySelector()
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
        _ = groups.Should().ContainKey("odd");
        _ = groups.Should().ContainKey("even");
        _ = groups["odd"].Should().BeEquivalentTo([1, TestData.TestValueThree]);
        _ = groups["even"].Should().BeEquivalentTo([TestData.TestValueTwo, TestData.TestValueFour]);
    }

    /// <summary>Tests that GroupByChanges handles string keys.</summary>
    [Test]
    public void GroupByChanges_HandlesStringKeys()
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
        _ = groups['a'].Should().BeEquivalentTo([TestData.AppleText, TestData.ApricotText]);
        _ = groups['b'].Should().BeEquivalentTo([TestData.BananaText]);
        _ = groups['c'].Should().BeEquivalentTo([TestData.CherryText]);
    }

    /// <summary>Tests that GroupingByChanges creates proper groupings.</summary>
    [Test]
    public void GroupingByChanges_CreatesProperGroupings()
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
        _ = groupings.Should().HaveCountGreaterThan(0);
    }

    /// <summary>Tests that GroupingByChanges handles batch operations.</summary>
    [Test]
    public void GroupingByChanges_HandlesBatchAdd()
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
        _ = groupings.Should().HaveCountGreaterThan(0);
        var keys = GetDistinctKeys(groupings);
        _ = keys.Should().Contain(0); // 5, 7
        _ = keys.Should().Contain(1); // 15, 17
        _ = keys.Should().Contain(TestData.TestValueTwo); // 25
    }

    /// <summary>Tests that AutoRefresh emits refresh when property changes.</summary>
    [Test]
    public void AutoRefresh_EmitsRefreshWhenPropertyChanges()
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
        _ = refreshCount.Should().Be(1);
    }

    /// <summary>Tests that AutoRefresh does not emit for unrelated property changes.</summary>
    [Test]
    public void AutoRefresh_DoesNotEmitForUnrelatedPropertyChanges()
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
        _ = refreshCount.Should().Be(0);
    }

    /// <summary>Tests that AutoRefresh without property name watches all property changes.</summary>
    [Test]
    public void AutoRefresh_WithoutPropertyName_WatchesAllProperties()
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
        _ = refreshCount.Should().Be(TestData.TestValueTwo);
    }

    /// <summary>Tests that Connect returns observable of change sets.</summary>
    [Test]
    public void Connect_ReturnsObservableOfChangeSets()
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
        _ = changeSets.Should().HaveCount(TestData.TestValueThree);
        _ = GetCurrentItems(changeSets).Should().BeEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree]);
    }

    /// <summary>Tests that Connect throws for null source.</summary>
    [Test]
    public void Connect_ThrowsForNullSource()
    {
        // Arrange
        IReactiveSource<int>? nullSource = null;

        // Act & Assert
        var act = () => nullSource!.Connect();
        _ = act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>Tests that WhereItems filters notifications by predicate.</summary>
    [Test]
    public void WhereItems_FiltersNotificationsByPredicate()
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
        _ = receivedItems.Should().BeEquivalentTo([TestData.TestValueSeven, TestData.TestValueTen]);
    }

    /// <summary>Tests that WhereItems passes Cleared notifications.</summary>
    [Test]
    public void WhereItems_PassesClearedNotifications()
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
        _ = clearedReceived.Should().BeTrue();
    }

    /// <summary>Tests that WhereItems passes BatchOperation notifications.</summary>
    [Test]
    public void WhereItems_PassesBatchOperations()
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
        _ = batchReceived.Should().BeTrue();
    }

    /// <summary>Tests that WhereItems correctly filters value types including zero.</summary>
    [Test]
    public void WhereItems_HandlesValueTypesIncludingZero()
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
        _ = receivedItems.Should().BeEquivalentTo([0, TestData.TestValueFive, TestData.TestValueTen]);
    }

    /// <summary>Tests that SortBy sorts change sets by key selector.</summary>
    [Test]
    public void SortBy_SortsChangeSetsByKeySelector()
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
        _ = sortedItems.Should().BeInAscendingOrder();
    }

    /// <summary>Tests that SortBy handles string sorting.</summary>
    [Test]
    public void SortBy_HandlesStringSorting()
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
        _ = GetLengths(sortedItems).Should().BeInAscendingOrder();
    }

    /// <summary>Tests that SelectChanges transforms to different type maintaining change metadata.</summary>
    [Test]
    public void SelectChanges_TransformsToDifferentType()
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
        _ = transformedSets.Should().HaveCount(TestData.TestValueTwo);
        _ = transformedSets[0][0].Current.Should().Be("Value:1");
        _ = transformedSets[1][0].Current.Should().Be("Value:2");
    }

    /// <summary>Tests that SelectChanges preserves change reason.</summary>
    [Test]
    public void SelectChanges_PreservesChangeReason()
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
        _ = reasons.Should().Contain(ChangeReason.Add);
        _ = reasons.Should().Contain(ChangeReason.Update);
        _ = reasons.Should().Contain(ChangeReason.Remove);
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
