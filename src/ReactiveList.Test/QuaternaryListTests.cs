// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using CP.Reactive.Quaternary;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Contains unit tests for the QuaternaryList class, verifying its core behaviors and supported operations.
/// </summary>
/// <remarks>These tests cover scenarios such as adding and removing items, index-based access, batch operations,
/// index management, and validation of unsupported operations. The tests ensure that QuaternaryList behaves as expected
/// under various conditions and that its public API contracts are enforced.</remarks>
public class QuaternaryListTests
{
    /// <summary>
    /// Verifies that adding an item to a QuaternaryList increases the count and that the item is present in the list.
    /// </summary>
    [Fact]
    public void Add_ShouldIncreaseCountAndContainItem()
    {
        using var list = new QuaternaryList<int>();

        list.Add(42);

        Assert.Single(list);
        Assert.Contains(42, list);
    }

    /// <summary>
    /// Verifies that the AddRange method emits a batch notification and correctly copies the added items to the
    /// underlying collection.
    /// </summary>
    [Fact]
    public void AddRange_ShouldEmitBatchAndCopyItems()
    {
        using var list = new QuaternaryList<int>();
        CacheNotify<int>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            notification = evt;
            reset.Set();
        });

        list.AddRange([0, 1, 2, 3, 4]);

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.NotNull(notification);
        Assert.Equal(CacheAction.BatchAdded, notification!.Action);
        Assert.NotNull(notification.Batch);
        Assert.Equal(5, notification.Batch!.Count);
        notification.Batch.Dispose();

        Assert.Equal(5, list.Count);
        var buffer = new int[5];
        list.CopyTo(buffer, 0);
        Assert.Contains(0, buffer);
        Assert.Contains(1, buffer);
        Assert.Contains(2, buffer);
        Assert.Contains(3, buffer);
        Assert.Contains(4, buffer);
    }

    /// <summary>
    /// Verifies that the indexer of the QuaternaryList returns the correct items across multiple shards.
    /// </summary>
    [Fact]
    public void Indexer_ShouldReturnItemsAcrossShards()
    {
        using var list = new QuaternaryList<int>();

        list.Add(0);
        list.Add(4);
        list.Add(8);

        Assert.Equal(0, list[0]);
        Assert.Equal(4, list[1]);
        Assert.Equal(8, list[2]);
    }

    /// <summary>
    /// Verifies that setting an item via the indexer throws NotSupportedException.
    /// </summary>
    [Fact]
    public void IndexerSetter_ShouldThrowNotSupportedException()
    {
        using var list = new QuaternaryList<int> { 1, 2, 3 };

        Assert.Throws<NotSupportedException>(() => list[0] = 5);
    }

    /// <summary>
    /// Verifies that adding an index to a QuaternaryList and querying by that index correctly tracks and updates
    /// results as items are added and removed.
    /// </summary>
    [Fact]
    public void AddIndexAndQuery_ShouldTrackAndUpdate()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex("ByCity", p => p.City);

        var ny = new TestPerson("A", "New York");
        var la = new TestPerson("B", "Los Angeles");
        var ny2 = new TestPerson("C", "New York");

        list.AddRange([ny, la, ny2]);

        var nyResults = list.GetItemsBySecondaryIndex("ByCity", "New York").ToList();
        Assert.Equal(2, nyResults.Count);
        Assert.Contains(ny, nyResults);
        Assert.Contains(ny2, nyResults);

        var laResults = list.GetItemsBySecondaryIndex("ByCity", "Los Angeles").ToList();
        Assert.Single(laResults);
        Assert.Equal(la, laResults[0]);

        list.Remove(ny);

        var nyResultsAfterRemove = list.GetItemsBySecondaryIndex("ByCity", "New York").ToList();
        Assert.Single(nyResultsAfterRemove);
        Assert.Equal(ny2, nyResultsAfterRemove[0]);
    }

    /// <summary>
    /// Verifies that calling Clear on a QuaternaryList resets the item collection and all associated indices.
    /// </summary>
    [Fact]
    public void Clear_ShouldResetItemsAndIndices()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex("ByCity", p => p.City);
        list.AddRange(
        [
            new TestPerson("A", "New York"),
            new TestPerson("B", "Chicago")
        ]);

        list.Clear();

        Assert.Empty(list);
        Assert.Empty(list.GetItemsBySecondaryIndex("ByCity", "New York"));
    }

    /// <summary>
    /// Verifies that the RemoveRange method removes the specified items from the list and emits a batch removed notification.
    /// </summary>
    [Fact]
    public void RemoveRange_ShouldRemoveItemsAndEmitBatchRemoved()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3, 4]);

        CacheNotify<int>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            if (evt.Action == CacheAction.BatchRemoved)
            {
                notification = evt;
                reset.Set();
            }
        });

        list.RemoveRange([2, 4]);

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.NotNull(notification);
        Assert.Equal(CacheAction.BatchRemoved, notification!.Action);
        Assert.NotNull(notification.Batch);
        Assert.Equal(2, notification.Batch!.Count);
        notification.Batch.Dispose();

        Assert.Equal(2, list.Count);
        Assert.DoesNotContain(2, list);
        Assert.DoesNotContain(4, list);
        Assert.Contains(1, list);
        Assert.Contains(3, list);
    }

    /// <summary>
    /// Verifies that RemoveMany with a predicate removes matching items and emits a batch removed notification.
    /// </summary>
    [Fact]
    public void RemoveMany_WithPredicate_ShouldRemoveMatchingItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);

        CacheNotify<int>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            if (evt.Action == CacheAction.BatchRemoved)
            {
                notification = evt;
                reset.Set();
            }
        });

        var removedCount = list.RemoveMany(x => x % 2 == 0);

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.Equal(5, removedCount);
        Assert.Equal(5, list.Count);
        Assert.DoesNotContain(2, list);
        Assert.DoesNotContain(4, list);
        Assert.Contains(1, list);
        Assert.Contains(3, list);
    }

    /// <summary>
    /// Verifies that the Edit method allows batch modifications with a single notification.
    /// </summary>
    [Fact]
    public void Edit_ShouldPerformBatchModificationsWithSingleNotification()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3]);

        var notifications = new List<CacheAction>();
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            notifications.Add(evt.Action);
            if (evt.Action == CacheAction.BatchOperation)
            {
                reset.Set();
            }
        });

        list.Edit(innerList =>
        {
            innerList.Clear();
            innerList.Add(10);
            innerList.Add(20);
            innerList.Add(30);
        });

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.Single(notifications);
        Assert.Equal(CacheAction.BatchOperation, notifications[0]);
        Assert.Equal(3, list.Count);
        Assert.Contains(10, list);
        Assert.Contains(20, list);
        Assert.Contains(30, list);
        Assert.DoesNotContain(1, list);
    }

    /// <summary>
    /// Verifies that Edit updates indices correctly.
    /// </summary>
    [Fact]
    public void Edit_ShouldUpdateIndicesCorrectly()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex("ByCity", p => p.City);

        list.AddRange([
            new TestPerson("Alice", "NYC"),
            new TestPerson("Bob", "LA")
        ]);

        list.Edit(innerList =>
        {
            innerList.Clear();
            innerList.Add(new TestPerson("Charlie", "NYC"));
            innerList.Add(new TestPerson("Diana", "Chicago"));
        });

        var nycResults = list.GetItemsBySecondaryIndex("ByCity", "NYC").ToList();
        Assert.Single(nycResults);
        Assert.Equal("Charlie", nycResults[0].Name);
        Assert.Empty(list.GetItemsBySecondaryIndex("ByCity", "LA"));
        var chicagoResults = list.GetItemsBySecondaryIndex("ByCity", "Chicago").ToList();
        Assert.Single(chicagoResults);
        Assert.Equal("Diana", chicagoResults[0].Name);
    }

    /// <summary>
    /// Verifies that Remove returns true when item exists and false when it doesn't.
    /// </summary>
    [Fact]
    public void Remove_ShouldReturnCorrectResult()
    {
        using var list = new QuaternaryList<int>();
        list.Add(42);

        Assert.True(list.Remove(42));
        Assert.False(list.Remove(42));
        Assert.Empty(list);
    }

    /// <summary>
    /// Verifies that Contains returns correct results.
    /// </summary>
    [Fact]
    public void Contains_ShouldReturnCorrectResult()
    {
        using var list = new QuaternaryList<int>();
        list.Add(42);

        Assert.Contains(42, list);
        Assert.DoesNotContain(99, list);
    }

    /// <summary>
    /// Verifies that CopyTo copies all items to the target array.
    /// </summary>
    [Fact]
    public void CopyTo_ShouldCopyAllItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3, 4, 5]);

        var buffer = new int[5];
        list.CopyTo(buffer, 0);

        Assert.Equal(5, buffer.Length);
        Assert.Contains(1, buffer);
        Assert.Contains(2, buffer);
        Assert.Contains(3, buffer);
        Assert.Contains(4, buffer);
        Assert.Contains(5, buffer);
    }

    /// <summary>
    /// Verifies that GetEnumerator iterates over all items.
    /// </summary>
    [Fact]
    public void GetEnumerator_ShouldIterateAllItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3, 4, 5]);

        var items = list.ToList();

        Assert.Equal(5, items.Count);
        Assert.Contains(1, items);
        Assert.Contains(2, items);
        Assert.Contains(3, items);
        Assert.Contains(4, items);
        Assert.Contains(5, items);
    }

    /// <summary>
    /// Verifies that IsReadOnly returns false.
    /// </summary>
    [Fact]
    public void IsReadOnly_ShouldReturnFalse()
    {
        using var list = new QuaternaryList<int>();
        Assert.False(list.IsReadOnly);
    }

    /// <summary>
    /// Verifies that ItemMatchesSecondaryIndex returns correct results.
    /// </summary>
    [Fact]
    public void ItemMatchesSecondaryIndex_ShouldReturnCorrectResult()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex("ByCity", p => p.City);

        var ny = new TestPerson("A", "New York");
        var la = new TestPerson("B", "Los Angeles");
        list.AddRange([ny, la]);

        Assert.True(list.ItemMatchesSecondaryIndex("ByCity", ny, "New York"));
        Assert.False(list.ItemMatchesSecondaryIndex("ByCity", ny, "Los Angeles"));
        Assert.True(list.ItemMatchesSecondaryIndex("ByCity", la, "Los Angeles"));
        Assert.False(list.ItemMatchesSecondaryIndex("NonExistent", ny, "New York"));
    }

    /// <summary>
    /// Verifies that GetItemsBySecondaryIndex returns empty when index doesn't exist.
    /// </summary>
    [Fact]
    public void GetItemsBySecondaryIndex_WithNonExistentIndex_ShouldReturnEmpty()
    {
        using var list = new QuaternaryList<TestPerson>();
        var result = list.GetItemsBySecondaryIndex("NonExistent", "SomeKey");
        Assert.Empty(result);
    }

    /// <summary>
    /// Verifies that Stream emits Added notification for single item add.
    /// </summary>
    [Fact]
    public void Stream_ShouldEmitAddedNotification()
    {
        using var list = new QuaternaryList<int>();
        CacheNotify<int>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            notification = evt;
            reset.Set();
        });

        list.Add(42);

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.NotNull(notification);
        Assert.Equal(CacheAction.Added, notification!.Action);
        Assert.Equal(42, notification.Item);
    }

    /// <summary>
    /// Verifies that Stream emits Removed notification for single item remove.
    /// </summary>
    [Fact]
    public void Stream_ShouldEmitRemovedNotification()
    {
        using var list = new QuaternaryList<int>();
        list.Add(42);

        CacheNotify<int>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            if (evt.Action == CacheAction.Removed)
            {
                notification = evt;
                reset.Set();
            }
        });

        list.Remove(42);

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.NotNull(notification);
        Assert.Equal(CacheAction.Removed, notification!.Action);
        Assert.Equal(42, notification.Item);
    }

    /// <summary>
    /// Verifies that Stream emits Cleared notification.
    /// </summary>
    [Fact]
    public void Stream_ShouldEmitClearedNotification()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3]);

        CacheNotify<int>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            if (evt.Action == CacheAction.Cleared)
            {
                notification = evt;
                reset.Set();
            }
        });

        list.Clear();

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.NotNull(notification);
        Assert.Equal(CacheAction.Cleared, notification!.Action);
    }

    /// <summary>
    /// Verifies that ReplaceAll replaces all items atomically.
    /// </summary>
    [Fact]
    public void ReplaceAll_ShouldReplaceAllItemsAtomically()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3, 4, 5]);

        list.ReplaceAll([10, 20, 30]);

        Assert.Equal(3, list.Count);
        Assert.Contains(10, list);
        Assert.Contains(20, list);
        Assert.Contains(30, list);
        Assert.DoesNotContain(1, list);
        Assert.DoesNotContain(5, list);
    }

    /// <summary>
    /// Verifies that ReplaceAll emits a single batch notification.
    /// </summary>
    [Fact]
    public void ReplaceAll_ShouldEmitSingleBatchNotification()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3]);
        var notificationCount = 0;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(_ =>
        {
            notificationCount++;
            reset.Set();
        });

        list.ReplaceAll([10, 20]);

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.Equal(1, notificationCount); // Should be exactly one notification
    }

    /// <summary>
    /// Verifies that ReplaceAll with empty collection clears the list.
    /// </summary>
    [Fact]
    public void ReplaceAll_WithEmptyCollection_ShouldClearList()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3, 4, 5]);

        list.ReplaceAll([]);

        Assert.Empty(list);
    }

    /// <summary>
    /// Verifies that ReplaceAll updates secondary indices correctly.
    /// </summary>
    [Fact]
    public void ReplaceAll_ShouldUpdateSecondaryIndices()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", x => x % 2);
        list.AddRange([1, 2, 3, 4, 5]);

        // Verify initial state
        Assert.Equal(2, list.GetItemsBySecondaryIndex("Mod2", 0).Count()); // 2, 4

        list.ReplaceAll([10, 20, 30]);

        // After replace, new even numbers
        var evenItems = list.GetItemsBySecondaryIndex("Mod2", 0).ToList();
        Assert.Equal(3, evenItems.Count); // 10, 20, 30 are all even
    }

    /// <summary>
    /// Verifies that ReplaceAll throws ArgumentNullException when items is null.
    /// </summary>
    [Fact]
    public void ReplaceAll_WithNull_ShouldThrowArgumentNullException()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3]);

        Assert.Throws<ArgumentNullException>(() => list.ReplaceAll(null!));
    }

    private record TestPerson(string Name, string City);
}
#endif
