// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using FluentAssertions;
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
        list.Add("original");
        list.Update("original", "updated");

        // Assert - Previous should contain the original value
        updates.Should().HaveCount(1);
        updates[0].Previous.Should().Be("original");
        updates[0].Current.Should().Be("updated");
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
        list.Add(2);
        list.Add(3);

        // Assert
        updateCount.Should().Be(0);
    }

    /// <summary>Tests that OnUpdate handles multiple sequential updates with previous values.</summary>
    [Test]
    public void OnUpdate_HandlesMultipleSequentialUpdates()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var updates = new List<(int? Previous, int Current)>();

        using var subscription = list.Connect()
            .OnUpdate()
            .Subscribe(update => updates.Add(update));

        // Act - use Update method (indexer does Remove+Add, not Update)
        list.Add(1);
        list.Update(1, 10);
        list.Update(10, 100);
        list.Update(100, 1000);

        // Assert - Previous should contain the actual previous value
        updates.Should().HaveCount(3);
        updates[0].Previous.Should().Be(1);
        updates[0].Current.Should().Be(10);
        updates[1].Previous.Should().Be(10);
        updates[1].Current.Should().Be(100);
        updates[2].Previous.Should().Be(100);
        updates[2].Current.Should().Be(1000);
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
        list.AddRange(new[] { "a", "b", "c", "d" });
        list.Move(0, 3); // Move "a" from index 0 to index 3

        // Assert
        moves.Should().HaveCount(1);
        moves[0].Item.Should().Be("a");
        moves[0].OldIndex.Should().Be(0);
        moves[0].NewIndex.Should().Be(3);
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
        list.Add(2);
        list.Remove(1);

        // Assert
        moveCount.Should().Be(0);
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
        list.AddRange(new[] { 1, 2, 3, 4, 5 });
        list.Move(0, 4); // Move 1 to end
        list.Move(3, 0); // Move 1 back to start (it's now at index 3)

        // Assert
        moves.Should().HaveCount(2);
    }

    /// <summary>Tests that FilterDynamic filters items based on dynamic predicate.</summary>
    [Test]
    public void FilterDynamic_FiltersBasedOnDynamicPredicate()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var filterSubject = new BehaviorSignal<Func<int, bool>>(_ => true);
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
        list.Add(2);
        list.Add(3);

        // Assert
        receivedItems.Should().BeEquivalentTo([1, 2, 3]);

        // Act - change filter to only even numbers
        receivedItems.Clear();
        filterSubject.OnNext(x => x % 2 == 0);
        list.Add(4);
        list.Add(5);

        // Assert - only even number should be received
        receivedItems.Should().BeEquivalentTo([4]);
    }

    /// <summary>Tests that FilterDynamic always passes removed items.</summary>
    [Test]
    public void FilterDynamic_AlwaysPassesRemovedItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var filterSubject = new BehaviorSignal<Func<int, bool>>(x => x > 5);
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
        list.Add(3); // filtered out on add
        list.Add(10); // passes filter
        list.Remove(3); // should still emit remove

        // Assert
        removedItems.Should().Contain(3);
    }

    /// <summary>Tests that FilterDynamic passes Cleared notifications.</summary>
    [Test]
    public void FilterDynamic_PassesClearedNotifications()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var filterSubject = new BehaviorSignal<Func<int, bool>>(x => x > 0);
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
        list.AddRange(new[] { 1, 2, 3 });
        list.Clear();

        // Assert
        clearReceived.Should().BeTrue();
    }

    /// <summary>Tests that CreateView without filter contains all items.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateView_WithoutFilter_ContainsAllItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5 });

        // Act
        using var view = list.CreateView(Sequencer.Immediate, 0);
        await Task.Delay(50);

        // Assert
        view.Count.Should().Be(5);
        view.Should().BeEquivalentTo([1, 2, 3, 4, 5]);
    }

    /// <summary>Tests that CreateView without filter updates when source changes.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateView_WithoutFilter_UpdatesOnSourceChange()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3 });

        using var view = list.CreateView(Sequencer.Immediate, 0);
        await Task.Delay(50);

        // Act
        list.Add(4);
        await Task.Delay(50);

        // Assert
        view.Should().BeEquivalentTo([1, 2, 3, 4]);
    }

#if NET8_0_OR_GREATER || NETFRAMEWORK

    /// <summary>Tests that CreateView with query observable filters based on query.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateView_WithQueryObservable_FiltersBasedOnQuery()
    {
        // Arrange
        using var list = new QuaternaryList<string>();
        list.AddRange(["apple", "banana", "apricot", "cherry", "avocado"]);

        var searchQuery = new BehaviorSignal<string>(string.Empty);

        // Act
        using var view = list.CreateView(
            searchQuery,
            (query, item) => string.IsNullOrEmpty(query) || item.StartsWith(query, StringComparison.OrdinalIgnoreCase),
            Sequencer.Immediate,
            0);

        await Task.Delay(50);

        // Initial - all items
        view.Items.Count.Should().Be(5);

        // Search for "a"
        searchQuery.OnNext("a");
        await Task.Delay(100);

        view.Items.Should().BeEquivalentTo(["apple", "apricot", "avocado"]);

        // Search for "ap"
        searchQuery.OnNext("ap");
        await Task.Delay(100);

        view.Items.Should().BeEquivalentTo(["apple", "apricot"]);
    }

    /// <summary>Tests that CreateView with query observable updates when source changes.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateView_WithQueryObservable_UpdatesWhenSourceChanges()
    {
        // Arrange
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3]);

        var thresholdQuery = new BehaviorSignal<int>(2);

        using var view = list.CreateView(
            thresholdQuery,
            (threshold, item) => item > threshold,
            Sequencer.Immediate,
            0);

        await Task.Delay(50);
        view.Items.Should().BeEquivalentTo([3]);

        // Act - add item that passes filter
        list.Add(5);
        await Task.Delay(100);

        // Assert
        view.Items.Should().BeEquivalentTo([3, 5]);

        // Act - change threshold
        thresholdQuery.OnNext(4);
        await Task.Delay(100);

        // Assert
        view.Items.Should().BeEquivalentTo([5]);
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
            .GroupByChanges(x => x % 2 == 0 ? "even" : "odd")
            .Subscribe(group =>
            {
                if (!groups.TryGetValue(group.Key!, out var value))
                {
                    value = [];
                    groups[group.Key!] = value;
                }

                group.Subscribe(item => value.Add(item));
            });

        // Act
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        // Assert
        groups.Should().ContainKey("odd");
        groups.Should().ContainKey("even");
        groups["odd"].Should().BeEquivalentTo([1, 3]);
        groups["even"].Should().BeEquivalentTo([2, 4]);
    }

    /// <summary>Tests that GroupByChanges handles string keys.</summary>
    [Test]
    public void GroupByChanges_HandlesStringKeys()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        var groups = new Dictionary<char, List<string>>();

        using var subscription = list.Connect()
            .GroupByChanges(s => s[0])
            .Subscribe(group =>
            {
                if (!groups.TryGetValue(group.Key, out var value))
                {
                    value = [];
                    groups[group.Key] = value;
                }

                group.Subscribe(item => value.Add(item));
            });

        // Act
        list.Add("apple");
        list.Add("banana");
        list.Add("apricot");
        list.Add("cherry");

        // Assert
        groups['a'].Should().BeEquivalentTo(["apple", "apricot"]);
        groups['b'].Should().BeEquivalentTo(["banana"]);
        groups['c'].Should().BeEquivalentTo(["cherry"]);
    }

    /// <summary>Tests that GroupingByChanges creates proper groupings.</summary>
    [Test]
    public void GroupingByChanges_CreatesProperGroupings()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var groupings = new List<System.Linq.IGrouping<string, Change<int>>>();

        using var subscription = list.Connect()
            .GroupingByChanges(x => x % 2 == 0 ? "even" : "odd")
            .Subscribe(groupings.Add);

        // Act
        list.AddRange(new[] { 1, 2, 3, 4 });

        // Assert - each add creates a separate changeset, which creates groupings
        groupings.Should().HaveCountGreaterThan(0);
    }

    /// <summary>Tests that GroupingByChanges handles batch operations.</summary>
    [Test]
    public void GroupingByChanges_HandlesBatchAdd()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var groupings = new List<System.Linq.IGrouping<int, Change<int>>>();

        using var subscription = list.Connect()
            .GroupingByChanges(x => x / 10) // Group by tens
            .Subscribe(groupings.Add);

        // Act - add items in different decades
        list.AddRange(new[] { 5, 15, 25, 7, 17 });

        // Assert
        groupings.Should().HaveCountGreaterThan(0);
        var keys = groupings.Select(g => g.Key).Distinct().ToList();
        keys.Should().Contain(0); // 5, 7
        keys.Should().Contain(1); // 15, 17
        keys.Should().Contain(2); // 25
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
        refreshCount.Should().Be(1);
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
        item.Value = 100; // Change different property

        // Assert
        refreshCount.Should().Be(0);
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
        item.Value = 2;

        // Assert - should get refresh for both property changes
        refreshCount.Should().Be(2);
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
        list.Add(2);
        list.Add(3);

        // Assert
        changeSets.Should().HaveCount(3);
        changeSets.SelectMany(cs => cs.Select(c => c.Current)).Should().BeEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests that Connect throws for null source.</summary>
    [Test]
    public void Connect_ThrowsForNullSource()
    {
        // Arrange
        IReactiveSource<int>? nullSource = null;

        // Act & Assert
        var act = () => nullSource!.Connect();
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>Tests that WhereItems filters notifications by predicate.</summary>
    [Test]
    public void WhereItems_FiltersNotificationsByPredicate()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var receivedItems = new List<int>();

        using var subscription = list.Stream
            .WhereItems(x => x > 5)
            .Subscribe(notification =>
            {
                if (notification.Action != CacheAction.Added)
                {
                    return;
                }

                receivedItems.Add(notification.Item);
            });

        // Act
        list.Add(3);
        list.Add(7);
        list.Add(2);
        list.Add(10);

        // Assert - only items > 5 should be received
        receivedItems.Should().BeEquivalentTo([7, 10]);
    }

    /// <summary>Tests that WhereItems passes Cleared notifications.</summary>
    [Test]
    public void WhereItems_PassesClearedNotifications()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        var clearedReceived = false;

        using var subscription = list.Stream
            .WhereItems(x => x.Length > 5)
            .Subscribe(notification =>
            {
                if (notification.Action != CacheAction.Cleared)
                {
                    return;
                }

                clearedReceived = true;
            });

        // Act
        list.AddRange(new[] { "short", "longertext", "x" });
        list.Clear();

        // Assert
        clearedReceived.Should().BeTrue();
    }

    /// <summary>Tests that WhereItems passes BatchOperation notifications.</summary>
    [Test]
    public void WhereItems_PassesBatchOperations()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        var batchReceived = false;

        using var subscription = list.Stream
            .WhereItems(x => x.Length > 5)
            .Subscribe(notification =>
            {
                if (notification.Action != CacheAction.BatchAdded &&
                    notification.Action != CacheAction.BatchOperation)
                {
                    return;
                }

                batchReceived = true;
            });

        // Act
        list.AddRange(new[] { "short", "medium", "verylongtext", "x" });

        // Assert
        batchReceived.Should().BeTrue();
    }

    /// <summary>Tests that WhereItems correctly filters value types including zero.</summary>
    [Test]
    public void WhereItems_HandlesValueTypesIncludingZero()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var receivedItems = new List<int>();

        using var subscription = list.Stream
            .WhereItems(x => x >= 0) // Filter: all non-negative numbers including 0
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
        list.Add(5); // Should be included
        list.Add(-5); // Should be filtered out
        list.Add(10); // Should be included

        // Assert - 0 should be correctly included
        receivedItems.Should().BeEquivalentTo([0, 5, 10]);
    }

    /// <summary>Tests that SortBy sorts change sets by key selector.</summary>
    [Test]
    public void SortBy_SortsChangeSetsByKeySelector()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var sortedItems = new List<int>();

        using var subscription = list.Connect()
            .SortBy(x => x)
            .Subscribe(cs =>
            {
                sortedItems.Clear();
                foreach (var change in cs)
                {
                    sortedItems.Add(change.Current);
                }
            });

        // Act
        list.AddRange([5, 1, 3, 2, 4]);

        // Assert
        sortedItems.Should().BeInAscendingOrder();
    }

    /// <summary>Tests that SortBy handles string sorting.</summary>
    [Test]
    public void SortBy_HandlesStringSorting()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        var sortedItems = new List<string>();

        using var subscription = list.Connect()
            .SortBy(s => s.Length)
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
        sortedItems.Select(s => s.Length).Should().BeInAscendingOrder();
    }

    /// <summary>Tests that SelectChanges transforms to different type maintaining change metadata.</summary>
    [Test]
    public void SelectChanges_TransformsToDifferentType()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var transformedSets = new List<ChangeSet<string>>();

        using var subscription = list.Connect()
            .SelectChanges((int x) => $"Value:{x}")
            .Subscribe(transformedSets.Add);

        // Act
        list.Add(1);
        list.Add(2);

        // Assert
        transformedSets.Should().HaveCount(2);
        transformedSets[0][0].Current.Should().Be("Value:1");
        transformedSets[1][0].Current.Should().Be("Value:2");
    }

    /// <summary>Tests that SelectChanges preserves change reason.</summary>
    [Test]
    public void SelectChanges_PreservesChangeReason()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var reasons = new List<ChangeReason>();

        using var subscription = list.Connect()
            .SelectChanges((int x) => x.ToString())
            .Subscribe(cs =>
            {
                foreach (var change in cs)
                {
                    reasons.Add(change.Reason);
                }
            });

        // Act - use Update method (indexer does Remove+Add, not Update)
        list.Add(1);
        list.Update(1, 2);
        list.Remove(2);

        // Assert
        reasons.Should().Contain(ChangeReason.Add);
        reasons.Should().Contain(ChangeReason.Update);
        reasons.Should().Contain(ChangeReason.Remove);
    }

    /// <summary>Test class that implements INotifyPropertyChanged.</summary>
    private sealed class NotifyingItem : INotifyPropertyChanged
    {
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
