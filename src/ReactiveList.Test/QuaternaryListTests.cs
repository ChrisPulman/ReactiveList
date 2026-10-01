// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Threading;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Collections;
using CP.Reactive.Core;
#else
using CP.Primitives.Collections;
using CP.Primitives.Core;
#endif
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Contains unit tests for the QuaternaryList class, verifying its core behaviors and supported operations.</summary>
/// <remarks>These tests cover scenarios such as adding and removing items, index-based access, batch operations,
/// index management, and validation of unsupported operations. The tests ensure that QuaternaryList behaves as expected
/// under various conditions and that its public API contracts are enforced.</remarks>
public class QuaternaryListTests
{
    /// <summary>The second collection value used by test data.</summary>
    private const int SecondCollectionValue = 2;

    /// <summary>The third collection value used by test data.</summary>
    private const int ThirdCollectionValue = 3;

    /// <summary>The fourth collection value used by test data.</summary>
    private const int FourthCollectionValue = 4;

    /// <summary>The fifth collection value used by test data.</summary>
    private const int FifthCollectionValue = 5;

    /// <summary>The sixth collection value used by test data.</summary>
    private const int SixthCollectionValue = 6;

    /// <summary>The seventh collection value used by test data.</summary>
    private const int SeventhCollectionValue = 7;

    /// <summary>The eighth collection value used by test data.</summary>
    private const int EighthCollectionValue = 8;

    /// <summary>The ninth collection value used by test data.</summary>
    private const int NinthCollectionValue = 9;

    /// <summary>The first replacement value used by test data.</summary>
    private const int FirstReplacementValue = 10;

    /// <summary>The second replacement value used by test data.</summary>
    private const int SecondReplacementValue = 20;

    /// <summary>The third replacement value used by test data.</summary>
    private const int ThirdReplacementValue = 30;

    /// <summary>The collection value tracked by notification tests.</summary>
    private const int TrackedCollectionValue = 42;

    /// <summary>A value deliberately absent from test collections.</summary>
    private const int MissingCollectionValue = 99;

    /// <summary>The name of the city secondary index.</summary>
    private const string CityIndexName = "ByCity";

    /// <summary>The New York test value.</summary>
    private const string NewYorkCity = "New York";

    /// <summary>The Los Angeles test value.</summary>
    private const string LosAngelesCity = "Los Angeles";

    /// <summary>The Chicago test value.</summary>
    private const string ChicagoCity = "Chicago";

    /// <summary>Verifies that adding an item to a QuaternaryList increases the count and that the item is present in the list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Add_ShouldIncreaseCountAndContainItem()
    {
        using var list = new QuaternaryList<int> { TrackedCollectionValue };

        await Assert.That(list).HasSingleItem();
        await Assert.That(list).Contains(TrackedCollectionValue);
    }

    /// <summary>
    /// Verifies that the AddRange method emits a batch notification and correctly copies the added items to the
    /// underlying collection.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddRange_ShouldEmitBatchAndCopyItems()
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

        await Assert.That(reset.Wait(TimeSpan.FromSeconds(1))).IsTrue();
        await Assert.That(notification).IsNotNull();
        await Assert.That(notification!.Action).IsEqualTo(CacheAction.BatchAdded);
        await Assert.That(notification.Batch).IsNotNull();
        await Assert.That(notification.Batch!.Count).IsEqualTo(FifthCollectionValue);
        notification.Batch.Dispose();

        await Assert.That(list.Count).IsEqualTo(FifthCollectionValue);
        var buffer = new int[5];
        list.CopyTo(buffer, 0);
        await Assert.That(buffer).Contains(0);
        await Assert.That(buffer).Contains(1);
        await Assert.That(buffer).Contains(SecondCollectionValue);
        await Assert.That(buffer).Contains(ThirdCollectionValue);
        await Assert.That(buffer).Contains(FourthCollectionValue);
    }

    /// <summary>Verifies that the indexer of the QuaternaryList returns the correct items across multiple shards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Indexer_ShouldReturnItemsAcrossShards()
    {
        using var list = new QuaternaryList<int> { 0, FourthCollectionValue, EighthCollectionValue };

        await Assert.That(list[0]).IsEqualTo(0);
        await Assert.That(list[1]).IsEqualTo(FourthCollectionValue);
        await Assert.That(list[SecondCollectionValue]).IsEqualTo(EighthCollectionValue);
    }

    /// <summary>Verifies that setting an item via the indexer throws NotSupportedException.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IndexerSetter_ShouldThrowNotSupportedException()
    {
        using var list = new QuaternaryList<int> { 1, SecondCollectionValue, ThirdCollectionValue };

        await Assert.That(() => list[0] = FifthCollectionValue).Throws<NotSupportedException>();
    }

    /// <summary>
    /// Verifies that adding an index to a QuaternaryList and querying by that index correctly tracks and updates
    /// results as items are added and removed.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddIndexAndQuery_ShouldTrackAndUpdate()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex(CityIndexName, static p => p.City);

        var newYorkPerson = new TestPerson("A", NewYorkCity);
        var losAngelesPerson = new TestPerson("B", LosAngelesCity);
        var secondNewYorkPerson = new TestPerson("C", NewYorkCity);

        list.AddRange([newYorkPerson, losAngelesPerson, secondNewYorkPerson]);

        var newYorkResults = new List<TestPerson>(list.GetItemsBySecondaryIndex(CityIndexName, NewYorkCity));
        await Assert.That(newYorkResults.Count).IsEqualTo(SecondCollectionValue);
        await Assert.That(newYorkResults).Contains(newYorkPerson);
        await Assert.That(newYorkResults).Contains(secondNewYorkPerson);

        var losAngelesResults = new List<TestPerson>(list.GetItemsBySecondaryIndex(CityIndexName, LosAngelesCity));
        await Assert.That(losAngelesResults).HasSingleItem();
        await Assert.That(losAngelesResults[0]).IsEqualTo(losAngelesPerson);

        _ = list.Remove(newYorkPerson);

        var newYorkResultsAfterRemove = new List<TestPerson>(list.GetItemsBySecondaryIndex(CityIndexName, NewYorkCity));
        await Assert.That(newYorkResultsAfterRemove).HasSingleItem();
        await Assert.That(newYorkResultsAfterRemove[0]).IsEqualTo(secondNewYorkPerson);
    }

    /// <summary>Verifies that calling Clear on a QuaternaryList resets the item collection and all associated indices.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Clear_ShouldResetItemsAndIndices()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex(CityIndexName, static p => p.City);
        list.AddRange(
        [
            new TestPerson("A", NewYorkCity),
            new TestPerson("B", ChicagoCity)
        ]);

        list.Clear();

        await Assert.That(list).IsEmpty();
        await Assert.That(list.GetItemsBySecondaryIndex(CityIndexName, NewYorkCity)).IsEmpty();
    }

    /// <summary>Verifies that the RemoveRange method removes the specified items from the list and emits a batch removed notification.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveRange_ShouldRemoveItemsAndEmitBatchRemoved()
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

        await Assert.That(reset.Wait(TimeSpan.FromSeconds(1))).IsTrue();
        await Assert.That(notification).IsNotNull();
        await Assert.That(notification!.Action).IsEqualTo(CacheAction.BatchRemoved);
        await Assert.That(notification.Batch).IsNotNull();
        await Assert.That(notification.Batch!.Count).IsEqualTo(SecondCollectionValue);
        notification.Batch.Dispose();

        await Assert.That(list.Count).IsEqualTo(SecondCollectionValue);
        await Assert.That(list).DoesNotContain(SecondCollectionValue);
        await Assert.That(list).DoesNotContain(FourthCollectionValue);
        await Assert.That(list).Contains(1);
        await Assert.That(list).Contains(ThirdCollectionValue);
    }

    /// <summary>Verifies that RemoveMany with a predicate removes matching items and emits a batch removed notification.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveMany_WithPredicate_ShouldRemoveMatchingItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(
        [
            1,
            SecondCollectionValue,
            ThirdCollectionValue,
            FourthCollectionValue,
            FifthCollectionValue,
            SixthCollectionValue,
            SeventhCollectionValue,
            EighthCollectionValue,
            NinthCollectionValue,
            FirstReplacementValue
        ]);

        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            if (evt.Action != CacheAction.BatchRemoved)
            {
                return;
            }

            reset.Set();
        });

        var removedCount = list.RemoveMany(static x => x % SecondCollectionValue == 0);

        await Assert.That(reset.Wait(TimeSpan.FromSeconds(1))).IsTrue();
        await Assert.That(removedCount).IsEqualTo(FifthCollectionValue);
        await Assert.That(list.Count).IsEqualTo(FifthCollectionValue);
        await Assert.That(list).DoesNotContain(SecondCollectionValue);
        await Assert.That(list).DoesNotContain(FourthCollectionValue);
        await Assert.That(list).Contains(1);
        await Assert.That(list).Contains(ThirdCollectionValue);
    }

    /// <summary>Snapshot should release earlier shard locks when reentrant acquisition fails.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Snapshot_ReentrantFailure_ShouldReleaseAcquiredShardLocks()
    {
        using var list = new QuaternaryList<int> { 1 };

        await Assert.That(() => list.RemoveMany(item =>
        {
            if (item != 1)
            {
                return false;
            }

            _ = list.Snapshot();
            return false;
        })).Throws<LockRecursionException>();

        list.Add(FourthCollectionValue);

        await Assert.That(list).Contains(FourthCollectionValue);
    }

    /// <summary>Verifies that the Edit method allows batch modifications with a single notification.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldPerformBatchModificationsWithSingleNotification()
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

        list.Edit(static innerList =>
        {
            innerList.Clear();
            innerList.Add(FirstReplacementValue);
            innerList.Add(SecondReplacementValue);
            innerList.Add(ThirdReplacementValue);
        });

        await Assert.That(reset.Wait(TimeSpan.FromSeconds(1))).IsTrue();
        await Assert.That(notifications).HasSingleItem();
        await Assert.That(notifications[0]).IsEqualTo(CacheAction.BatchOperation);
        await Assert.That(list.Count).IsEqualTo(ThirdCollectionValue);
        await Assert.That(list).Contains(FirstReplacementValue);
        await Assert.That(list).Contains(SecondReplacementValue);
        await Assert.That(list).Contains(ThirdReplacementValue);
        await Assert.That(list).DoesNotContain(1);
    }

    /// <summary>Verifies that Edit updates indices correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldUpdateIndicesCorrectly()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex(CityIndexName, static p => p.City);

        list.AddRange([
            new TestPerson("Alice", "NYC"),
            new TestPerson("Bob", "LA")
        ]);

        list.Edit(static innerList =>
        {
            innerList.Clear();
            innerList.Add(new("Charlie", "NYC"));
            innerList.Add(new("Diana", ChicagoCity));
        });

        var nycResults = new List<TestPerson>(list.GetItemsBySecondaryIndex(CityIndexName, "NYC"));
        await Assert.That(nycResults).HasSingleItem();
        await Assert.That(nycResults[0].Name).IsEqualTo("Charlie");
        await Assert.That(list.GetItemsBySecondaryIndex(CityIndexName, "LA")).IsEmpty();
        var chicagoResults = new List<TestPerson>(list.GetItemsBySecondaryIndex(CityIndexName, ChicagoCity));
        await Assert.That(chicagoResults).HasSingleItem();
        await Assert.That(chicagoResults[0].Name).IsEqualTo("Diana");
    }

    /// <summary>Verifies that Remove returns true when item exists and false when it doesn't.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_ShouldReturnCorrectResult()
    {
        using var list = new QuaternaryList<int> { TrackedCollectionValue };

        await Assert.That(list.Remove(TrackedCollectionValue)).IsTrue();
        await Assert.That(list.Remove(TrackedCollectionValue)).IsFalse();
        await Assert.That(list).IsEmpty();
    }

    /// <summary>Verifies that Contains returns correct results.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Contains_ShouldReturnCorrectResult()
    {
        using var list = new QuaternaryList<int> { TrackedCollectionValue };

        await Assert.That(list).Contains(TrackedCollectionValue);
        await Assert.That(list).DoesNotContain(MissingCollectionValue);
    }

    /// <summary>Verifies that CopyTo copies all items to the target array.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CopyTo_ShouldCopyAllItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue]);

        var buffer = new int[5];
        list.CopyTo(buffer, 0);

        await Assert.That(buffer.Length).IsEqualTo(FifthCollectionValue);
        await Assert.That(buffer).Contains(1);
        await Assert.That(buffer).Contains(SecondCollectionValue);
        await Assert.That(buffer).Contains(ThirdCollectionValue);
        await Assert.That(buffer).Contains(FourthCollectionValue);
        await Assert.That(buffer).Contains(FifthCollectionValue);
    }

    /// <summary>Verifies that GetEnumerator iterates over all items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetEnumerator_ShouldIterateAllItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue]);

        var items = new List<int>(list);

        await Assert.That(items.Count).IsEqualTo(FifthCollectionValue);
        await Assert.That(items).Contains(1);
        await Assert.That(items).Contains(SecondCollectionValue);
        await Assert.That(items).Contains(ThirdCollectionValue);
        await Assert.That(items).Contains(FourthCollectionValue);
        await Assert.That(items).Contains(FifthCollectionValue);
    }

    /// <summary>Verifies that IsReadOnly returns false.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IsReadOnly_ShouldReturnFalse()
    {
        using var list = new QuaternaryList<int>();
        await Assert.That(list.IsReadOnly).IsFalse();
    }

    /// <summary>Verifies that ItemMatchesSecondaryIndex returns correct results.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ItemMatchesSecondaryIndex_ShouldReturnCorrectResult()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex(CityIndexName, static p => p.City);

        var newYorkPerson = new TestPerson("A", NewYorkCity);
        var losAngelesPerson = new TestPerson("B", LosAngelesCity);
        list.AddRange([newYorkPerson, losAngelesPerson]);

        await Assert.That(list.ItemMatchesSecondaryIndex(CityIndexName, newYorkPerson, NewYorkCity)).IsTrue();
        await Assert.That(list.ItemMatchesSecondaryIndex(CityIndexName, newYorkPerson, LosAngelesCity)).IsFalse();
        await Assert.That(list.ItemMatchesSecondaryIndex(CityIndexName, losAngelesPerson, LosAngelesCity)).IsTrue();
        await Assert.That(list.ItemMatchesSecondaryIndex("NonExistent", newYorkPerson, NewYorkCity)).IsFalse();
    }

    /// <summary>Verifies that GetItemsBySecondaryIndex returns empty when index doesn't exist.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetItemsBySecondaryIndex_WithNonExistentIndex_ShouldReturnEmpty()
    {
        using var list = new QuaternaryList<TestPerson>();
        var result = list.GetItemsBySecondaryIndex("NonExistent", "SomeKey");
        await Assert.That(result).IsEmpty();
    }

    /// <summary>Verifies that Stream emits Added notification for single item add.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Stream_ShouldEmitAddedNotification()
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

        await Assert.That(reset.Wait(TimeSpan.FromSeconds(1))).IsTrue();
        await Assert.That(notification).IsNotNull();
        await Assert.That(notification!.Action).IsEqualTo(CacheAction.Added);
        await Assert.That(notification.Item).IsEqualTo(TrackedCollectionValue);
    }

    /// <summary>Verifies that Stream emits Removed notification for single item remove.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Stream_ShouldEmitRemovedNotification()
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

        _ = list.Remove(TrackedCollectionValue);

        await Assert.That(reset.Wait(TimeSpan.FromSeconds(1))).IsTrue();
        await Assert.That(notification).IsNotNull();
        await Assert.That(notification!.Action).IsEqualTo(CacheAction.Removed);
        await Assert.That(notification.Item).IsEqualTo(TrackedCollectionValue);
    }

    /// <summary>Verifies that Stream emits Cleared notification.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Stream_ShouldEmitClearedNotification()
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

        await Assert.That(reset.Wait(TimeSpan.FromSeconds(1))).IsTrue();
        await Assert.That(notification).IsNotNull();
        await Assert.That(notification!.Action).IsEqualTo(CacheAction.Cleared);
    }

    /// <summary>Verifies that ReplaceAll replaces all items atomically.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReplaceAll_ShouldReplaceAllItemsAtomically()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue]);

        list.ReplaceAll([FirstReplacementValue, SecondReplacementValue, ThirdReplacementValue]);

        await Assert.That(list.Count).IsEqualTo(ThirdCollectionValue);
        await Assert.That(list).Contains(FirstReplacementValue);
        await Assert.That(list).Contains(SecondReplacementValue);
        await Assert.That(list).Contains(ThirdReplacementValue);
        await Assert.That(list).DoesNotContain(1);
        await Assert.That(list).DoesNotContain(FifthCollectionValue);
    }

    /// <summary>Verifies that ReplaceAll emits a single batch notification.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReplaceAll_ShouldEmitSingleBatchNotification()
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

        await Assert.That(reset.Wait(TimeSpan.FromSeconds(1))).IsTrue();
        await Assert.That(notificationCount).IsEqualTo(1); // Should be exactly one notification
    }

    /// <summary>Verifies that ReplaceAll with empty collection clears the list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReplaceAll_WithEmptyCollection_ShouldClearList()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue]);

        list.ReplaceAll([]);

        await Assert.That(list).IsEmpty();
    }

    /// <summary>Verifies that ReplaceAll updates secondary indices correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReplaceAll_ShouldUpdateSecondaryIndices()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", static x => x % SecondCollectionValue);
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue]);

        // Verify initial state
        var initialEvenItems = new List<int>(list.GetItemsBySecondaryIndex("Mod2", 0));
        await Assert.That(initialEvenItems.Count).IsEqualTo(SecondCollectionValue); // 2, 4

        list.ReplaceAll([FirstReplacementValue, SecondReplacementValue, ThirdReplacementValue]);

        // After replace, new even numbers
        var evenItems = new List<int>(list.GetItemsBySecondaryIndex("Mod2", 0));
        await Assert.That(evenItems.Count).IsEqualTo(ThirdCollectionValue); // 10, 20, 30 are all even
    }

    /// <summary>Verifies that ReplaceAll throws ArgumentNullException when items is null.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReplaceAll_WithNull_ShouldThrowArgumentNullException()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue]);

        await Assert.That(() => list.ReplaceAll(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Provides TestPerson.</summary>
    /// <param name="Name">The Name value.</param>
    /// <param name="City">The City value.</param>
    private sealed record TestPerson(string Name, string City);
}
#endif
