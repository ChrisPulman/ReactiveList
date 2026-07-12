// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Contains unit tests for the QuaternaryList class, verifying its core behaviors and supported operations.</summary>
/// <remarks>These tests cover scenarios such as adding and removing items, index-based access, batch operations,
/// index management, and validation of unsupported operations. The tests ensure that QuaternaryList behaves as expected
/// under various conditions and that its public API contracts are enforced.</remarks>
public class QuaternaryListTests
{
    private const int SecondCollectionValue = 2;

    private const int ThirdCollectionValue = 3;

    private const int FourthCollectionValue = 4;

    private const int FifthCollectionValue = 5;

    private const int SixthCollectionValue = 6;

    private const int SeventhCollectionValue = 7;

    private const int EighthCollectionValue = 8;

    private const int NinthCollectionValue = 9;

    private const int FirstReplacementValue = 10;

    private const int SecondReplacementValue = 20;

    private const int ThirdReplacementValue = 30;

    private const int TrackedCollectionValue = 42;

    private const int MissingCollectionValue = 99;

    private const string CityIndexName = "ByCity";

    private const string NewYorkCity = "New York";

    private const string LosAngelesCity = "Los Angeles";

    private const string ChicagoCity = "Chicago";

    /// <summary>Verifies that adding an item to a QuaternaryList increases the count and that the item is present in the list.</summary>
    [Test]
    public void Add_ShouldIncreaseCountAndContainItem()
    {
        using var list = new QuaternaryList<int> { TrackedCollectionValue };

        Assert.Single(list);
        Assert.Contains(TrackedCollectionValue, list);
    }

    /// <summary>
    /// Verifies that the AddRange method emits a batch notification and correctly copies the added items to the
    /// underlying collection.
    /// </summary>
    [Test]
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

        list.AddRange([0, 1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue]);

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.NotNull(notification);
        Assert.Equal(CacheAction.BatchAdded, notification!.Action);
        Assert.NotNull(notification.Batch);
        Assert.Equal(FifthCollectionValue, notification.Batch!.Count);
        notification.Batch.Dispose();

        Assert.Equal(FifthCollectionValue, list.Count);
        var buffer = new int[5];
        list.CopyTo(buffer, 0);
        Assert.Contains(0, buffer);
        Assert.Contains(1, buffer);
        Assert.Contains(SecondCollectionValue, buffer);
        Assert.Contains(ThirdCollectionValue, buffer);
        Assert.Contains(FourthCollectionValue, buffer);
    }

    /// <summary>Verifies that the indexer of the QuaternaryList returns the correct items across multiple shards.</summary>
    [Test]
    public void Indexer_ShouldReturnItemsAcrossShards()
    {
        using var list = new QuaternaryList<int> { 0, FourthCollectionValue, EighthCollectionValue };

        Assert.Equal(0, list[0]);
        Assert.Equal(FourthCollectionValue, list[1]);
        Assert.Equal(EighthCollectionValue, list[SecondCollectionValue]);
    }

    /// <summary>Verifies that setting an item via the indexer throws NotSupportedException.</summary>
    [Test]
    public void IndexerSetter_ShouldThrowNotSupportedException()
    {
        using var list = new QuaternaryList<int> { 1, SecondCollectionValue, ThirdCollectionValue };

        Assert.Throws<NotSupportedException>(() => list[0] = FifthCollectionValue);
    }

    /// <summary>
    /// Verifies that adding an index to a QuaternaryList and querying by that index correctly tracks and updates
    /// results as items are added and removed.
    /// </summary>
    [Test]
    public void AddIndexAndQuery_ShouldTrackAndUpdate()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex(CityIndexName, p => p.City);

        var newYorkPerson = new TestPerson("A", NewYorkCity);
        var losAngelesPerson = new TestPerson("B", LosAngelesCity);
        var secondNewYorkPerson = new TestPerson("C", NewYorkCity);

        list.AddRange([newYorkPerson, losAngelesPerson, secondNewYorkPerson]);

        var newYorkResults = list.GetItemsBySecondaryIndex(CityIndexName, NewYorkCity).ToList();
        Assert.Equal(SecondCollectionValue, newYorkResults.Count);
        Assert.Contains(newYorkPerson, newYorkResults);
        Assert.Contains(secondNewYorkPerson, newYorkResults);

        var losAngelesResults = list.GetItemsBySecondaryIndex(CityIndexName, LosAngelesCity).ToList();
        Assert.Single(losAngelesResults);
        Assert.Equal(losAngelesPerson, losAngelesResults[0]);

        list.Remove(newYorkPerson);

        var newYorkResultsAfterRemove = list.GetItemsBySecondaryIndex(CityIndexName, NewYorkCity).ToList();
        Assert.Single(newYorkResultsAfterRemove);
        Assert.Equal(secondNewYorkPerson, newYorkResultsAfterRemove[0]);
    }

    /// <summary>Verifies that calling Clear on a QuaternaryList resets the item collection and all associated indices.</summary>
    [Test]
    public void Clear_ShouldResetItemsAndIndices()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex(CityIndexName, p => p.City);
        list.AddRange(
        [
            new TestPerson("A", NewYorkCity),
            new TestPerson("B", ChicagoCity)
        ]);

        list.Clear();

        Assert.Empty(list);
        Assert.Empty(list.GetItemsBySecondaryIndex(CityIndexName, NewYorkCity));
    }

    /// <summary>Verifies that the RemoveRange method removes the specified items from the list and emits a batch removed notification.</summary>
    [Test]
    public void RemoveRange_ShouldRemoveItemsAndEmitBatchRemoved()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue]);

        CacheNotify<int>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            if (evt.Action != CacheAction.BatchRemoved)
            {
                return;
            }

            notification = evt;
            reset.Set();
        });

        list.RemoveRange([SecondCollectionValue, FourthCollectionValue]);

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.NotNull(notification);
        Assert.Equal(CacheAction.BatchRemoved, notification!.Action);
        Assert.NotNull(notification.Batch);
        Assert.Equal(SecondCollectionValue, notification.Batch!.Count);
        notification.Batch.Dispose();

        Assert.Equal(SecondCollectionValue, list.Count);
        Assert.DoesNotContain(SecondCollectionValue, list);
        Assert.DoesNotContain(FourthCollectionValue, list);
        Assert.Contains(1, list);
        Assert.Contains(ThirdCollectionValue, list);
    }

    /// <summary>Verifies that RemoveMany with a predicate removes matching items and emits a batch removed notification.</summary>
    [Test]
    public void RemoveMany_WithPredicate_ShouldRemoveMatchingItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue, SixthCollectionValue, SeventhCollectionValue, EighthCollectionValue, NinthCollectionValue, FirstReplacementValue]);

        CacheNotify<int>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            if (evt.Action != CacheAction.BatchRemoved)
            {
                return;
            }

            notification = evt;
            reset.Set();
        });

        var removedCount = list.RemoveMany(x => x % SecondCollectionValue == 0);

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.Equal(FifthCollectionValue, removedCount);
        Assert.Equal(FifthCollectionValue, list.Count);
        Assert.DoesNotContain(SecondCollectionValue, list);
        Assert.DoesNotContain(FourthCollectionValue, list);
        Assert.Contains(1, list);
        Assert.Contains(ThirdCollectionValue, list);
    }

    /// <summary>Snapshot should release earlier shard locks when reentrant acquisition fails.</summary>
    [Test]
    public void Snapshot_ReentrantFailure_ShouldReleaseAcquiredShardLocks()
    {
        using var list = new QuaternaryList<int> { 1 };

        Assert.Throws<LockRecursionException>(() => list.RemoveMany(item =>
        {
            if (item != 1)
            {
                return false;
            }

            _ = list.Snapshot();
            return false;
        }));

        list.Add(FourthCollectionValue);

        Assert.Contains(FourthCollectionValue, list);
    }

    /// <summary>Verifies that the Edit method allows batch modifications with a single notification.</summary>
    [Test]
    public void Edit_ShouldPerformBatchModificationsWithSingleNotification()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue]);

        var notifications = new List<CacheAction>();
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            notifications.Add(evt.Action);
            if (evt.Action != CacheAction.BatchOperation)
            {
                return;
            }

            reset.Set();
        });

        list.Edit(innerList =>
        {
            innerList.Clear();
            innerList.Add(FirstReplacementValue);
            innerList.Add(SecondReplacementValue);
            innerList.Add(ThirdReplacementValue);
        });

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.Single(notifications);
        Assert.Equal(CacheAction.BatchOperation, notifications[0]);
        Assert.Equal(ThirdCollectionValue, list.Count);
        Assert.Contains(FirstReplacementValue, list);
        Assert.Contains(SecondReplacementValue, list);
        Assert.Contains(ThirdReplacementValue, list);
        Assert.DoesNotContain(1, list);
    }

    /// <summary>Verifies that Edit updates indices correctly.</summary>
    [Test]
    public void Edit_ShouldUpdateIndicesCorrectly()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex(CityIndexName, p => p.City);

        list.AddRange([
            new TestPerson("Alice", "NYC"),
            new TestPerson("Bob", "LA")
        ]);

        list.Edit(innerList =>
        {
            innerList.Clear();
            innerList.Add(new TestPerson("Charlie", "NYC"));
            innerList.Add(new TestPerson("Diana", ChicagoCity));
        });

        var nycResults = list.GetItemsBySecondaryIndex(CityIndexName, "NYC").ToList();
        Assert.Single(nycResults);
        Assert.Equal("Charlie", nycResults[0].Name);
        Assert.Empty(list.GetItemsBySecondaryIndex(CityIndexName, "LA"));
        var chicagoResults = list.GetItemsBySecondaryIndex(CityIndexName, ChicagoCity).ToList();
        Assert.Single(chicagoResults);
        Assert.Equal("Diana", chicagoResults[0].Name);
    }

    /// <summary>Verifies that Remove returns true when item exists and false when it doesn't.</summary>
    [Test]
    public void Remove_ShouldReturnCorrectResult()
    {
        using var list = new QuaternaryList<int> { TrackedCollectionValue };

        Assert.True(list.Remove(TrackedCollectionValue));
        Assert.False(list.Remove(TrackedCollectionValue));
        Assert.Empty(list);
    }

    /// <summary>Verifies that Contains returns correct results.</summary>
    [Test]
    public void Contains_ShouldReturnCorrectResult()
    {
        using var list = new QuaternaryList<int> { TrackedCollectionValue };

        Assert.Contains(TrackedCollectionValue, list);
        Assert.DoesNotContain(MissingCollectionValue, list);
    }

    /// <summary>Verifies that CopyTo copies all items to the target array.</summary>
    [Test]
    public void CopyTo_ShouldCopyAllItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue]);

        var buffer = new int[5];
        list.CopyTo(buffer, 0);

        Assert.Equal(FifthCollectionValue, buffer.Length);
        Assert.Contains(1, buffer);
        Assert.Contains(SecondCollectionValue, buffer);
        Assert.Contains(ThirdCollectionValue, buffer);
        Assert.Contains(FourthCollectionValue, buffer);
        Assert.Contains(FifthCollectionValue, buffer);
    }

    /// <summary>Verifies that GetEnumerator iterates over all items.</summary>
    [Test]
    public void GetEnumerator_ShouldIterateAllItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue]);

        var items = list.ToList();

        Assert.Equal(FifthCollectionValue, items.Count);
        Assert.Contains(1, items);
        Assert.Contains(SecondCollectionValue, items);
        Assert.Contains(ThirdCollectionValue, items);
        Assert.Contains(FourthCollectionValue, items);
        Assert.Contains(FifthCollectionValue, items);
    }

    /// <summary>Verifies that IsReadOnly returns false.</summary>
    [Test]
    public void IsReadOnly_ShouldReturnFalse()
    {
        using var list = new QuaternaryList<int>();
        Assert.False(list.IsReadOnly);
    }

    /// <summary>Verifies that ItemMatchesSecondaryIndex returns correct results.</summary>
    [Test]
    public void ItemMatchesSecondaryIndex_ShouldReturnCorrectResult()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex(CityIndexName, p => p.City);

        var newYorkPerson = new TestPerson("A", NewYorkCity);
        var losAngelesPerson = new TestPerson("B", LosAngelesCity);
        list.AddRange([newYorkPerson, losAngelesPerson]);

        Assert.True(list.ItemMatchesSecondaryIndex(CityIndexName, newYorkPerson, NewYorkCity));
        Assert.False(list.ItemMatchesSecondaryIndex(CityIndexName, newYorkPerson, LosAngelesCity));
        Assert.True(list.ItemMatchesSecondaryIndex(CityIndexName, losAngelesPerson, LosAngelesCity));
        Assert.False(list.ItemMatchesSecondaryIndex("NonExistent", newYorkPerson, NewYorkCity));
    }

    /// <summary>Verifies that GetItemsBySecondaryIndex returns empty when index doesn't exist.</summary>
    [Test]
    public void GetItemsBySecondaryIndex_WithNonExistentIndex_ShouldReturnEmpty()
    {
        using var list = new QuaternaryList<TestPerson>();
        var result = list.GetItemsBySecondaryIndex("NonExistent", "SomeKey");
        Assert.Empty(result);
    }

    /// <summary>Verifies that Stream emits Added notification for single item add.</summary>
    [Test]
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

        list.Add(TrackedCollectionValue);

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.NotNull(notification);
        Assert.Equal(CacheAction.Added, notification!.Action);
        Assert.Equal(TrackedCollectionValue, notification.Item);
    }

    /// <summary>Verifies that Stream emits Removed notification for single item remove.</summary>
    [Test]
    public void Stream_ShouldEmitRemovedNotification()
    {
        using var list = new QuaternaryList<int> { TrackedCollectionValue };

        CacheNotify<int>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            if (evt.Action != CacheAction.Removed)
            {
                return;
            }

            notification = evt;
            reset.Set();
        });

        list.Remove(TrackedCollectionValue);

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.NotNull(notification);
        Assert.Equal(CacheAction.Removed, notification!.Action);
        Assert.Equal(TrackedCollectionValue, notification.Item);
    }

    /// <summary>Verifies that Stream emits Cleared notification.</summary>
    [Test]
    public void Stream_ShouldEmitClearedNotification()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue]);

        CacheNotify<int>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            if (evt.Action != CacheAction.Cleared)
            {
                return;
            }

            notification = evt;
            reset.Set();
        });

        list.Clear();

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.NotNull(notification);
        Assert.Equal(CacheAction.Cleared, notification!.Action);
    }

    /// <summary>Verifies that ReplaceAll replaces all items atomically.</summary>
    [Test]
    public void ReplaceAll_ShouldReplaceAllItemsAtomically()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue]);

        list.ReplaceAll([FirstReplacementValue, SecondReplacementValue, ThirdReplacementValue]);

        Assert.Equal(ThirdCollectionValue, list.Count);
        Assert.Contains(FirstReplacementValue, list);
        Assert.Contains(SecondReplacementValue, list);
        Assert.Contains(ThirdReplacementValue, list);
        Assert.DoesNotContain(1, list);
        Assert.DoesNotContain(FifthCollectionValue, list);
    }

    /// <summary>Verifies that ReplaceAll emits a single batch notification.</summary>
    [Test]
    public void ReplaceAll_ShouldEmitSingleBatchNotification()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue]);
        var notificationCount = 0;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(_ =>
        {
            notificationCount++;
            reset.Set();
        });

        list.ReplaceAll([FirstReplacementValue, SecondReplacementValue]);

        Assert.True(reset.Wait(TimeSpan.FromSeconds(1)));
        Assert.Equal(1, notificationCount); // Should be exactly one notification
    }

    /// <summary>Verifies that ReplaceAll with empty collection clears the list.</summary>
    [Test]
    public void ReplaceAll_WithEmptyCollection_ShouldClearList()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue]);

        list.ReplaceAll([]);

        Assert.Empty(list);
    }

    /// <summary>Verifies that ReplaceAll updates secondary indices correctly.</summary>
    [Test]
    public void ReplaceAll_ShouldUpdateSecondaryIndices()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", x => x % SecondCollectionValue);
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue]);

        // Verify initial state
        Assert.Equal(SecondCollectionValue, list.GetItemsBySecondaryIndex("Mod2", 0).Count()); // 2, 4

        list.ReplaceAll([FirstReplacementValue, SecondReplacementValue, ThirdReplacementValue]);

        // After replace, new even numbers
        var evenItems = list.GetItemsBySecondaryIndex("Mod2", 0).ToList();
        Assert.Equal(ThirdCollectionValue, evenItems.Count); // 10, 20, 30 are all even
    }

    /// <summary>Verifies that ReplaceAll throws ArgumentNullException when items is null.</summary>
    [Test]
    public void ReplaceAll_WithNull_ShouldThrowArgumentNullException()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue]);

        Assert.Throws<ArgumentNullException>(() => list.ReplaceAll(null!));
    }

    /// <summary>Provides TestPerson.</summary>
    /// <param name="Name">The Name value.</param>
    /// <param name="City">The City value.</param>
    private sealed record TestPerson(string Name, string City);
}
#endif
