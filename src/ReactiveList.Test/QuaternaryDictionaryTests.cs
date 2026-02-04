// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Contains unit tests for the QuaternaryDictionary class, verifying its core behaviors such as adding, updating,
/// removing, and indexing values, as well as batch operations and value indexing functionality.
/// </summary>
/// <remarks>These tests ensure that QuaternaryDictionary methods and properties behave as expected under various
/// scenarios, including duplicate key handling, event notifications, and secondary value indexing. The tests are
/// intended to validate the public API and observable behaviors of QuaternaryDictionary.</remarks>
public class QuaternaryDictionaryTests
{
    /// <summary>
    /// Verifies that the QuaternaryDictionary correctly stores values added with Add and allows updating values using
    /// the indexer.
    /// </summary>
    /// <remarks>This test ensures that adding a key-value pair stores the value, updating the value via the
    /// indexer replaces the existing value, and the dictionary maintains the correct count and key presence.</remarks>
    [Fact]
    public void AddAndIndexer_ShouldStoreAndUpdateValues()
    {
        using var dict = new QuaternaryDictionary<int, string>();

        dict.Add(1, "one");

        dict[1].Should().Be("one");

        dict[1] = "uno";

        dict[1].Should().Be("uno");
        dict.Count.Should().Be(1);
        dict.ContainsKey(1).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the TryAdd method of QuaternaryDictionary prevents adding duplicate keys and retains the original
    /// value for an existing key.
    /// </summary>
    /// <remarks>This test ensures that when an attempt is made to add a key that already exists in the
    /// dictionary, TryAdd returns <see langword="false"/> and does not overwrite the existing value.</remarks>
    [Fact]
    public void TryAdd_ShouldPreventDuplicateKeys()
    {
        using var dict = new QuaternaryDictionary<int, string>();

        dict.TryAdd(2, "two").Should().BeTrue();
        dict.TryAdd(2, "dos").Should().BeFalse();

        dict[2].Should().Be("two");
    }

    /// <summary>
    /// Verifies that the AddOrUpdate method emits the correct sequence of cache actions when adding and updating an
    /// entry in the dictionary.
    /// </summary>
    /// <remarks>This test ensures that the observable stream associated with the dictionary emits a
    /// CacheAction.Added event when a new entry is added and a CacheAction.Updated event when an existing entry is
    /// updated. It also verifies that the final value for the key reflects the most recent update.</remarks>
    [Fact]
    public void AddOrUpdate_ShouldEmitCorrectActions()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        using var reset = new ManualResetEventSlim(false);
        var actions = new List<CacheAction>();
        using var subscription = dict.Stream.Subscribe(evt =>
        {
            actions.Add(evt.Action);
            if (actions.Count == 2)
            {
                reset.Set();
            }
        });

        dict.AddOrUpdate(3, "tres");
        dict.AddOrUpdate(3, "three");

        reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        actions.Should().ContainInOrder(CacheAction.Added, CacheAction.Updated);
        dict[3].Should().Be("three");
    }

    /// <summary>
    /// Verifies that removing an existing key from the dictionary succeeds and that subsequent removal attempts for the
    /// same key return false.
    /// </summary>
    /// <remarks>This test ensures that the Remove method returns <see langword="true"/> when an existing key
    /// is removed and <see langword="false"/> when attempting to remove a key that is not present in the
    /// dictionary.</remarks>
    [Fact]
    public void Remove_ShouldRemoveExistingAndReturnFalseForMissing()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.Add(1, "one");

        dict.Remove(1).Should().BeTrue();
        dict.ContainsKey(1).Should().BeFalse();
        dict.Remove(1).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that adding a range of items to a QuaternaryDictionary emits a batch added notification and correctly exposes
    /// the keys and values of the added items.
    /// </summary>
    /// <remarks>This test ensures that the AddRange method triggers a batch added event on the Stream,
    /// and that the dictionary's Keys and Values properties reflect the newly added items. It also checks that the
    /// batch notification contains all added items and that the dictionary's count is updated accordingly.</remarks>
    [Fact]
    public void AddRange_ShouldEmitBatchAndExposeKeysAndValues()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        CacheNotify<KeyValuePair<int, string>>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = dict.Stream.Subscribe(evt =>
        {
            notification = evt;
            reset.Set();
        });

        var items = new[]
        {
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three")
        };

        dict.AddRange(items);

        reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        notification.Should().NotBeNull();
        notification!.Action.Should().Be(CacheAction.BatchAdded);
        notification.Batch.Should().NotBeNull();
        notification.Batch!.Count.Should().Be(3);
        notification.Batch.Dispose();

        dict.Count.Should().Be(3);
        dict.Keys.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        dict.Values.Should().BeEquivalentTo(new[] { "one", "two", "three" });
    }

    /// <summary>
    /// Verifies that the CopyTo method copies all entries from the dictionary to the specified array starting at the
    /// given index.
    /// </summary>
    /// <remarks>This test ensures that the CopyTo method correctly transfers all key-value pairs to the
    /// target array without omitting or duplicating entries. It also checks that the entries are placed at the correct
    /// position in the array.</remarks>
    [Fact]
    public void CopyTo_ShouldCopyAllEntries()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.Add(1, "one");
        dict.Add(2, "two");

        var array = new KeyValuePair<int, string>[3];

        dict.CopyTo(array, 1);

        array[1..].Should().BeEquivalentTo(dict.ToArray());
    }

    /// <summary>
    /// Verifies that the value index in a QuaternaryDictionary correctly tracks additions and removals of items.
    /// </summary>
    /// <remarks>This test ensures that when items are added to or removed from the dictionary, the associated
    /// value index reflects these changes as expected. It also verifies that clearing the dictionary updates the value
    /// index accordingly.</remarks>
    [Fact]
    public void ValueIndex_ShouldTrackAddsAndRemovals()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex("ByLength", v => v.Length);

        dict.AddRange(new[]
        {
            new KeyValuePair<int, string>(1, "short"),
            new KeyValuePair<int, string>(2, "longvalue")
        });

        GetLookup(dict, "ByLength", 5).Should().ContainSingle().Which.Should().Be("short");

        dict.Remove(1);

        GetLookup(dict, "ByLength", 5).Should().BeEmpty();

        dict.Clear();

        GetLookup(dict, "ByLength", 9).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that the Lookup method returns the correct result for existing and non-existing keys.
    /// </summary>
    [Fact]
    public void Lookup_ShouldReturnCorrectResult()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.Add(1, "one");
        dict.Add(2, "two");

        var result1 = dict.Lookup(1);
        result1.HasValue.Should().BeTrue();
        result1.Value.Should().Be("one");

        var result2 = dict.Lookup(99);
        result2.HasValue.Should().BeFalse();
        result2.Value.Should().BeNull();
    }

    /// <summary>
    /// Verifies that RemoveKeys removes multiple keys in a batch operation.
    /// </summary>
    [Fact]
    public void RemoveKeys_ShouldRemoveMultipleKeysAndEmitBatch()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddRange([
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three"),
            new KeyValuePair<int, string>(4, "four")
        ]);

        CacheNotify<KeyValuePair<int, string>>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = dict.Stream.Subscribe(evt =>
        {
            if (evt.Action == CacheAction.BatchOperation)
            {
                notification = evt;
                reset.Set();
            }
        });

        dict.RemoveKeys([2, 4]);

        reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        notification.Should().NotBeNull();
        dict.Count.Should().Be(2);
        dict.ContainsKey(2).Should().BeFalse();
        dict.ContainsKey(4).Should().BeFalse();
        dict.ContainsKey(1).Should().BeTrue();
        dict.ContainsKey(3).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that RemoveMany with a predicate removes matching entries.
    /// </summary>
    [Fact]
    public void RemoveMany_WithPredicate_ShouldRemoveMatchingEntries()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddRange([
            new KeyValuePair<int, string>(1, "tiny"),
            new KeyValuePair<int, string>(2, "medium"),
            new KeyValuePair<int, string>(3, "verylongvalue")
        ]);

        var removedCount = dict.RemoveMany(kvp => kvp.Value.Length > 5);

        removedCount.Should().Be(2);
        dict.Count.Should().Be(1);
        dict.ContainsKey(1).Should().BeTrue();
        dict.ContainsKey(2).Should().BeFalse();
        dict.ContainsKey(3).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that the Edit method allows batch modifications with a single notification.
    /// </summary>
    [Fact]
    public void Edit_ShouldPerformBatchModificationsWithSingleNotification()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddRange([
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two")
        ]);

        var notifications = new List<CacheAction>();
        using var reset = new ManualResetEventSlim(false);
        using var subscription = dict.Stream.Subscribe(evt =>
        {
            notifications.Add(evt.Action);
            if (evt.Action == CacheAction.BatchOperation)
            {
                reset.Set();
            }
        });

        dict.Edit(innerDict =>
        {
            innerDict.Clear();
            innerDict.Add(10, "ten");
            innerDict.Add(20, "twenty");
        });

        reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        notifications.Should().ContainSingle().Which.Should().Be(CacheAction.BatchOperation);
        dict.Count.Should().Be(2);
        dict.ContainsKey(10).Should().BeTrue();
        dict.ContainsKey(20).Should().BeTrue();
        dict.ContainsKey(1).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that Edit updates value indices correctly.
    /// </summary>
    [Fact]
    public void Edit_ShouldUpdateValueIndicesCorrectly()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex("ByLength", v => v.Length);

        dict.AddRange([
            new KeyValuePair<int, string>(1, "short"),
            new KeyValuePair<int, string>(2, "longvalue")
        ]);

        dict.Edit(innerDict =>
        {
            innerDict.Clear();
            innerDict.Add(3, "tiny");
            innerDict.Add(4, "biggervalue");
        });

        GetLookup(dict, "ByLength", 5).Should().BeEmpty();
        GetLookup(dict, "ByLength", 4).Should().ContainSingle().Which.Should().Be("tiny");
        GetLookup(dict, "ByLength", 11).Should().ContainSingle().Which.Should().Be("biggervalue");
    }

    /// <summary>
    /// Verifies that GetValuesBySecondaryIndex returns matching values.
    /// </summary>
    [Fact]
    public void GetValuesBySecondaryIndex_ShouldReturnMatchingValues()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex("ByLength", v => v.Length);

        dict.AddRange([
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three"),
            new KeyValuePair<int, string>(4, "four")
        ]);

        var threeCharValues = dict.GetValuesBySecondaryIndex("ByLength", 3).ToList();
        threeCharValues.Should().HaveCount(2);
        threeCharValues.Should().Contain("one");
        threeCharValues.Should().Contain("two");

        var fiveCharValues = dict.GetValuesBySecondaryIndex("ByLength", 5).ToList();
        fiveCharValues.Should().ContainSingle().Which.Should().Be("three");
    }

    /// <summary>
    /// Verifies that GetValuesBySecondaryIndex returns empty for non-existent index.
    /// </summary>
    [Fact]
    public void GetValuesBySecondaryIndex_WithNonExistentIndex_ShouldReturnEmpty()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.Add(1, "one");

        var result = dict.GetValuesBySecondaryIndex("NonExistent", "key");
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that ValueMatchesSecondaryIndex returns correct results.
    /// </summary>
    [Fact]
    public void ValueMatchesSecondaryIndex_ShouldReturnCorrectResult()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex("ByLength", v => v.Length);
        dict.Add(1, "test");

        dict.ValueMatchesSecondaryIndex("ByLength", "test", 4).Should().BeTrue();
        dict.ValueMatchesSecondaryIndex("ByLength", "test", 5).Should().BeFalse();
        dict.ValueMatchesSecondaryIndex("NonExistent", "test", 4).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that GetValuesBySecondaryIndex updates after additions and removals.
    /// </summary>
    [Fact]
    public void GetValuesBySecondaryIndex_ShouldUpdateAfterAdditionsAndRemovals()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex("ByLength", v => v.Length);

        dict.Add(1, "one");
        dict.GetValuesBySecondaryIndex("ByLength", 3).Should().ContainSingle().Which.Should().Be("one");

        dict.Add(2, "two");
        dict.GetValuesBySecondaryIndex("ByLength", 3).Should().HaveCount(2);

        dict.Remove(1);
        dict.GetValuesBySecondaryIndex("ByLength", 3).Should().ContainSingle().Which.Should().Be("two");

        dict.Clear();
        dict.GetValuesBySecondaryIndex("ByLength", 3).Should().BeEmpty();
    }

    private static IEnumerable<TValue> GetLookup<TKey, TValue>(QuaternaryDictionary<TKey, TValue> dictionary, string indexName, object key)
        where TKey : notnull
    {
        // The Indices field is now in the base class QuaternaryBase<TItem, TQuad, TValue>
        var baseType = dictionary.GetType().BaseType!;
        var field = baseType.GetField("Indices", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!;
        var indices = (ConcurrentDictionary<string, ISecondaryIndex<TValue>>)field.GetValue(dictionary)!;
        indices.TryGetValue(indexName, out var index).Should().BeTrue();
        var lookupMethod = index!.GetType().GetMethod("Lookup", BindingFlags.Public | BindingFlags.Instance)!;
        return (IEnumerable<TValue>)lookupMethod.Invoke(index, new[] { key })!;
    }
}
#endif
