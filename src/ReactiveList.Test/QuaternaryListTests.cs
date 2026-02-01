// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Threading;
using CP.Reactive;
using FluentAssertions;
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
    /// <remarks>This unit test ensures that the Add method correctly updates the Count property and that the
    /// added item can be found using the Contains method.</remarks>
    [Fact]
    public void Add_ShouldIncreaseCountAndContainItem()
    {
        using var list = new QuaternaryList<int>();

        list.Add(42);

        list.Count.Should().Be(1);
        list.Contains(42).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the AddRange method emits a batch notification and correctly copies the added items to the
    /// underlying collection.
    /// </summary>
    /// <remarks>This test ensures that when multiple items are added to the QuaternaryList using AddRange, a
    /// batch CacheNotify event is emitted, the batch contains all added items, and the items are correctly stored and
    /// retrievable via CopyTo.</remarks>
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

        reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        notification.Should().NotBeNull();
        notification!.Action.Should().Be(CacheAction.BatchOperation);
        notification.Batch.Should().NotBeNull();
        notification.Batch!.Count.Should().Be(5);
        notification.Batch.Dispose();

        list.Count.Should().Be(5);
        var buffer = new int[5];
        list.CopyTo(buffer, 0);
        buffer.Should().BeEquivalentTo([0, 1, 2, 3, 4]);
    }

    /// <summary>
    /// Verifies that the indexer of the QuaternaryList of T returns the correct items across multiple shards.
    /// </summary>
    /// <remarks>This test ensures that items added to a QuaternaryList of int can be accessed by their index,
    /// even when the underlying storage spans multiple shards. It validates that the indexer retrieves the expected
    /// values for each position.</remarks>
    [Fact]
    public void Indexer_ShouldReturnItemsAcrossShards()
    {
        using var list = new QuaternaryList<int>();

        list.Add(0);
        list.Add(4);
        list.Add(8);

        list[0].Should().Be(0);
        list[1].Should().Be(4);
        list[2].Should().Be(8);
    }

    /// <summary>
    /// Verifies that unsupported modification operations on a QuaternaryList throw a NotSupportedException.
    /// </summary>
    /// <remarks>This test ensures that attempting to set an item via the indexer, call IndexOf, insert an
    /// item, or remove an item by index on a QuaternaryList results in a NotSupportedException, indicating that these
    /// operations are not supported by the collection.</remarks>
    [Fact]
    public void UnsupportedOperations_ShouldThrow()
    {
        using var list = new QuaternaryList<int> { 1, 2, 3 };

        Action indexerSetter = () => list[0] = 5;
        Action indexOf = () => list.IndexOf(1);
        Action insert = () => list.Insert(0, 1);
        Action removeAt = () => list.RemoveAt(0);

        indexerSetter.Should().Throw<NotSupportedException>();
        indexOf.Should().Throw<NotSupportedException>();
        insert.Should().Throw<NotSupportedException>();
        removeAt.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// Verifies that adding an index to a QuaternaryList and querying by that index correctly tracks and updates
    /// results as items are added and removed.
    /// </summary>
    /// <remarks>This test ensures that the index remains consistent with the underlying collection, returning
    /// accurate query results after modifications such as additions and removals.</remarks>
    [Fact]
    public void AddIndexAndQuery_ShouldTrackAndUpdate()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex("ByCity", p => p.City);

        var ny = new TestPerson("A", "New York");
        var la = new TestPerson("B", "Los Angeles");
        var ny2 = new TestPerson("C", "New York");

        list.AddRange([ny, la, ny2]);

        list.GetItemsBySecondaryIndex("ByCity", "New York").Should().BeEquivalentTo([ny, ny2]);
        list.GetItemsBySecondaryIndex("ByCity", "Los Angeles").Should().ContainSingle().Which.Should().Be(la);

        list.Remove(ny);

        list.GetItemsBySecondaryIndex("ByCity", "New York").Should().ContainSingle().Which.Should().Be(ny2);
    }

    /// <summary>
    /// Verifies that calling Clear on a QuaternaryList resets the item collection and all associated indices.
    /// </summary>
    /// <remarks>This test ensures that after invoking Clear, the list contains no items and all defined
    /// indices return empty results. It validates that both the primary data and any secondary indices are properly
    /// cleared.</remarks>
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

        list.Count.Should().Be(0);
        list.GetItemsBySecondaryIndex("ByCity", "New York").Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that the RemoveRange method removes the specified items from the list and emits a batch notification
    /// event.
    /// </summary>
    /// <remarks>This test ensures that when multiple items are removed using RemoveRange, the list updates
    /// its contents accordingly and a batch operation notification is published to subscribers of the Stream. The test
    /// also checks that the removed items are no longer present in the list and that the notification is received
    /// within a reasonable time frame.</remarks>
    [Fact]
    public void RemoveRange_ShouldRemoveItemsAndEmitBatch()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3, 4]);

        CacheNotify<int>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = list.Stream.Subscribe(evt =>
        {
            if (evt.Action == CacheAction.BatchOperation)
            {
                notification = evt;
                reset.Set();
            }
        });

        list.RemoveRange([2, 4]);

        reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        notification.Should().NotBeNull();
        list.Count.Should().Be(2);
        list.Contains(2).Should().BeFalse();
        list.Contains(4).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that RemoveMany with a predicate removes matching items and emits a batch notification.
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
            if (evt.Action == CacheAction.BatchOperation)
            {
                notification = evt;
                reset.Set();
            }
        });

        var removedCount = list.RemoveMany(x => x % 2 == 0);

        reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        removedCount.Should().Be(5);
        list.Count.Should().Be(5);
        list.Contains(2).Should().BeFalse();
        list.Contains(4).Should().BeFalse();
        list.Contains(1).Should().BeTrue();
        list.Contains(3).Should().BeTrue();
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

        reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        notifications.Should().ContainSingle().Which.Should().Be(CacheAction.BatchOperation);
        list.Count.Should().Be(3);
        list.Contains(10).Should().BeTrue();
        list.Contains(20).Should().BeTrue();
        list.Contains(30).Should().BeTrue();
        list.Contains(1).Should().BeFalse();
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

        list.GetItemsBySecondaryIndex("ByCity", "NYC").Should().ContainSingle().Which.Name.Should().Be("Charlie");
        list.GetItemsBySecondaryIndex("ByCity", "LA").Should().BeEmpty();
        list.GetItemsBySecondaryIndex("ByCity", "Chicago").Should().ContainSingle().Which.Name.Should().Be("Diana");
    }

    private record TestPerson(string Name, string City);
}
#endif
