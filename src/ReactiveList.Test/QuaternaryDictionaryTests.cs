// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER || NETFRAMEWORK

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using FluentAssertions;
using TUnit.Core;

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
    private const int SecondDictionaryKey = 2;

    private const int ThirdDictionaryKey = 3;

    private const int FourthDictionaryKey = 4;

    private const int FiveCharacterLength = 5;

    private const int NineCharacterLength = 9;

    private const int TenthDictionaryKey = 10;

    private const int ElevenCharacterLength = 11;

    private const int TwentiethDictionaryKey = 20;

    private const int MissingDictionaryKey = 99;

    private const string ThreeText = "three";

    private const string LengthIndexName = "ByLength";

    private const string ShortValue = "short";

    /// <summary>
    /// Verifies that the QuaternaryDictionary correctly stores values added with Add and allows updating values using
    /// the indexer.
    /// </summary>
    /// <remarks>This test ensures that adding a key-value pair stores the value, updating the value via the
    /// indexer replaces the existing value, and the dictionary maintains the correct count and key presence.</remarks>
    [Test]
    public void AddAndIndexer_ShouldStoreAndUpdateValues()
    {
        using var dict = new QuaternaryDictionary<int, string> { { 1, "one" } };

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
    [Test]
    public void TryAdd_ShouldPreventDuplicateKeys()
    {
        using var dict = new QuaternaryDictionary<int, string>();

        dict.TryAdd(SecondDictionaryKey, "two").Should().BeTrue();
        dict.TryAdd(SecondDictionaryKey, "dos").Should().BeFalse();

        dict[SecondDictionaryKey].Should().Be("two");
    }

    /// <summary>
    /// Verifies that the AddOrUpdate method emits the correct sequence of cache actions when adding and updating an
    /// entry in the dictionary.
    /// </summary>
    /// <remarks>This test ensures that the observable stream associated with the dictionary emits a
    /// CacheAction.Added event when a new entry is added and a CacheAction.Updated event when an existing entry is
    /// updated. It also verifies that the final value for the key reflects the most recent update.</remarks>
    [Test]
    public void AddOrUpdate_ShouldEmitCorrectActions()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        using var reset = new ManualResetEventSlim(false);
        var actions = new List<CacheAction>();
        using var subscription = dict.Stream.Subscribe(evt =>
        {
            actions.Add(evt.Action);
            if (actions.Count != 2)
            {
                return;
            }

            reset.Set();
        });

        dict.AddOrUpdate(ThirdDictionaryKey, "tres");
        dict.AddOrUpdate(ThirdDictionaryKey, ThreeText);

        reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        actions.Should().ContainInOrder(CacheAction.Added, CacheAction.Updated);
        dict[ThirdDictionaryKey].Should().Be(ThreeText);
    }

    /// <summary>
    /// Verifies that removing an existing key from the dictionary succeeds and that subsequent removal attempts for the
    /// same key return false.
    /// </summary>
    /// <remarks>This test ensures that the Remove method returns <see langword="true"/> when an existing key
    /// is removed and <see langword="false"/> when attempting to remove a key that is not present in the
    /// dictionary.</remarks>
    [Test]
    public void Remove_ShouldRemoveExistingAndReturnFalseForMissing()
    {
        using var dict = new QuaternaryDictionary<int, string> { { 1, "one" } };

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
    [Test]
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
            new KeyValuePair<int, string>(SecondDictionaryKey, "two"),
            new KeyValuePair<int, string>(ThirdDictionaryKey, ThreeText)
        };

        dict.AddRange(items);

        reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        notification.Should().NotBeNull();
        notification!.Action.Should().Be(CacheAction.BatchAdded);
        notification.Batch.Should().NotBeNull();
        notification.Batch!.Count.Should().Be(ThirdDictionaryKey);
        notification.Batch.Dispose();

        dict.Count.Should().Be(ThirdDictionaryKey);
        dict.Keys.Should().BeEquivalentTo([1, SecondDictionaryKey, ThirdDictionaryKey]);
        dict.Values.Should().BeEquivalentTo(["one", "two", ThreeText]);
    }

    /// <summary>
    /// Verifies that the CopyTo method copies all entries from the dictionary to the specified array starting at the
    /// given index.
    /// </summary>
    /// <remarks>This test ensures that the CopyTo method correctly transfers all key-value pairs to the
    /// target array without omitting or duplicating entries. It also checks that the entries are placed at the correct
    /// position in the array.</remarks>
    [Test]
    public void CopyTo_ShouldCopyAllEntries()
    {
        using var dict = new QuaternaryDictionary<int, string>
        {
            { 1, "one" },
            { SecondDictionaryKey, "two" }
        };

        var array = new KeyValuePair<int, string>[3];

        dict.CopyTo(array, 1);

        array.Skip(1).Should().BeEquivalentTo(dict.ToArray());
    }

    /// <summary>Verifies that the value index in a QuaternaryDictionary correctly tracks additions and removals of items.</summary>
    /// <remarks>This test ensures that when items are added to or removed from the dictionary, the associated
    /// value index reflects these changes as expected. It also verifies that clearing the dictionary updates the value
    /// index accordingly.</remarks>
    [Test]
    public void ValueIndex_ShouldTrackAddsAndRemovals()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex(LengthIndexName, v => v.Length);

        dict.AddRange([
            new KeyValuePair<int, string>(1, ShortValue),
            new KeyValuePair<int, string>(SecondDictionaryKey, "longvalue")
        ]);

        GetLookup(dict, LengthIndexName, FiveCharacterLength).Should().ContainSingle().Which.Should().Be(ShortValue);

        dict.Remove(1);

        GetLookup(dict, LengthIndexName, FiveCharacterLength).Should().BeEmpty();

        dict.Clear();

        GetLookup(dict, LengthIndexName, NineCharacterLength).Should().BeEmpty();
    }

    /// <summary>Verifies that the Lookup method returns the correct result for existing and non-existing keys.</summary>
    [Test]
    public void Lookup_ShouldReturnCorrectResult()
    {
        using var dict = new QuaternaryDictionary<int, string>
        {
            { 1, "one" },
            { SecondDictionaryKey, "two" }
        };

        var result1 = dict.Lookup(1);
        result1.HasValue.Should().BeTrue();
        result1.Value.Should().Be("one");

        var result2 = dict.Lookup(MissingDictionaryKey);
        result2.HasValue.Should().BeFalse();
        result2.Value.Should().BeNull();
    }

    /// <summary>Verifies that RemoveKeys removes multiple keys in a batch operation.</summary>
    [Test]
    public void RemoveKeys_ShouldRemoveMultipleKeysAndEmitBatch()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddRange([
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(SecondDictionaryKey, "two"),
            new KeyValuePair<int, string>(ThirdDictionaryKey, ThreeText),
            new KeyValuePair<int, string>(FourthDictionaryKey, "four")
        ]);

        CacheNotify<KeyValuePair<int, string>>? notification = null;
        using var reset = new ManualResetEventSlim(false);
        using var subscription = dict.Stream.Subscribe(evt =>
        {
            if (evt.Action != CacheAction.BatchOperation)
            {
                return;
            }

            notification = evt;
            reset.Set();
        });

        dict.RemoveKeys([SecondDictionaryKey, FourthDictionaryKey]);

        reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        notification.Should().NotBeNull();
        dict.Count.Should().Be(SecondDictionaryKey);
        dict.ContainsKey(SecondDictionaryKey).Should().BeFalse();
        dict.ContainsKey(FourthDictionaryKey).Should().BeFalse();
        dict.ContainsKey(1).Should().BeTrue();
        dict.ContainsKey(ThirdDictionaryKey).Should().BeTrue();
    }

    /// <summary>Verifies that RemoveMany with a predicate removes matching entries.</summary>
    [Test]
    public void RemoveMany_WithPredicate_ShouldRemoveMatchingEntries()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddRange([
            new KeyValuePair<int, string>(1, "tiny"),
            new KeyValuePair<int, string>(SecondDictionaryKey, "medium"),
            new KeyValuePair<int, string>(ThirdDictionaryKey, "verylongvalue")
        ]);

        var removedCount = dict.RemoveMany(kvp => kvp.Value.Length > 5);

        removedCount.Should().Be(SecondDictionaryKey);
        dict.Count.Should().Be(1);
        dict.ContainsKey(1).Should().BeTrue();
        dict.ContainsKey(SecondDictionaryKey).Should().BeFalse();
        dict.ContainsKey(ThirdDictionaryKey).Should().BeFalse();
    }

    /// <summary>Verifies that the Edit method allows batch modifications with a single notification.</summary>
    [Test]
    public void Edit_ShouldPerformBatchModificationsWithSingleNotification()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddRange([
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(SecondDictionaryKey, "two")
        ]);

        var notifications = new List<CacheAction>();
        using var reset = new ManualResetEventSlim(false);
        using var subscription = dict.Stream.Subscribe(evt =>
        {
            notifications.Add(evt.Action);
            if (evt.Action != CacheAction.BatchOperation)
            {
                return;
            }

            reset.Set();
        });

        dict.Edit(innerDict =>
        {
            innerDict.Clear();
            innerDict.Add(TenthDictionaryKey, "ten");
            innerDict.Add(TwentiethDictionaryKey, "twenty");
        });

        reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        notifications.Should().ContainSingle().Which.Should().Be(CacheAction.BatchOperation);
        dict.Count.Should().Be(SecondDictionaryKey);
        dict.ContainsKey(TenthDictionaryKey).Should().BeTrue();
        dict.ContainsKey(TwentiethDictionaryKey).Should().BeTrue();
        dict.ContainsKey(1).Should().BeFalse();
    }

    /// <summary>Verifies that Edit updates value indices correctly.</summary>
    [Test]
    public void Edit_ShouldUpdateValueIndicesCorrectly()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex(LengthIndexName, v => v.Length);

        dict.AddRange([
            new KeyValuePair<int, string>(1, ShortValue),
            new KeyValuePair<int, string>(SecondDictionaryKey, "longvalue")
        ]);

        dict.Edit(innerDict =>
        {
            innerDict.Clear();
            innerDict.Add(ThirdDictionaryKey, "tiny");
            innerDict.Add(FourthDictionaryKey, "biggervalue");
        });

        GetLookup(dict, LengthIndexName, FiveCharacterLength).Should().BeEmpty();
        GetLookup(dict, LengthIndexName, FourthDictionaryKey).Should().ContainSingle().Which.Should().Be("tiny");
        GetLookup(dict, LengthIndexName, ElevenCharacterLength).Should().ContainSingle().Which.Should().Be("biggervalue");
    }

    /// <summary>Verifies that GetValuesBySecondaryIndex returns matching values.</summary>
    [Test]
    public void GetValuesBySecondaryIndex_ShouldReturnMatchingValues()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex(LengthIndexName, v => v.Length);

        dict.AddRange([
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(SecondDictionaryKey, "two"),
            new KeyValuePair<int, string>(ThirdDictionaryKey, ThreeText),
            new KeyValuePair<int, string>(FourthDictionaryKey, "four")
        ]);

        var threeCharValues = dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey).ToList();
        threeCharValues.Should().HaveCount(SecondDictionaryKey);
        threeCharValues.Should().Contain("one");
        threeCharValues.Should().Contain("two");

        var fiveCharValues = dict.GetValuesBySecondaryIndex(LengthIndexName, FiveCharacterLength).ToList();
        fiveCharValues.Should().ContainSingle().Which.Should().Be(ThreeText);
    }

    /// <summary>Verifies that GetValuesBySecondaryIndex returns empty for non-existent index.</summary>
    [Test]
    public void GetValuesBySecondaryIndex_WithNonExistentIndex_ShouldReturnEmpty()
    {
        using var dict = new QuaternaryDictionary<int, string> { { 1, "one" } };

        var result = dict.GetValuesBySecondaryIndex("NonExistent", "key");
        result.Should().BeEmpty();
    }

    /// <summary>Verifies that ValueMatchesSecondaryIndex returns correct results.</summary>
    [Test]
    public void ValueMatchesSecondaryIndex_ShouldReturnCorrectResult()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex(LengthIndexName, v => v.Length);
        dict.Add(1, "test");

        dict.ValueMatchesSecondaryIndex(LengthIndexName, "test", FourthDictionaryKey).Should().BeTrue();
        dict.ValueMatchesSecondaryIndex(LengthIndexName, "test", FiveCharacterLength).Should().BeFalse();
        dict.ValueMatchesSecondaryIndex("NonExistent", "test", FourthDictionaryKey).Should().BeFalse();
    }

    /// <summary>Verifies that GetValuesBySecondaryIndex updates after additions and removals.</summary>
    [Test]
    public void GetValuesBySecondaryIndex_ShouldUpdateAfterAdditionsAndRemovals()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex(LengthIndexName, v => v.Length);

        dict.Add(1, "one");
        dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey).Should().ContainSingle().Which.Should().Be("one");

        dict.Add(SecondDictionaryKey, "two");
        dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey).Should().HaveCount(SecondDictionaryKey);

        dict.Remove(1);
        dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey).Should().ContainSingle().Which.Should().Be("two");

        dict.Clear();
        dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey).Should().BeEmpty();
    }

    /// <summary>Provides GetLookup.</summary>
    /// <typeparam name="TKey">The TKey type.</typeparam>
    /// <typeparam name="TValue">The TValue type.</typeparam>
    /// <param name="dictionary">The dictionary value.</param>
    /// <param name="indexName">The indexName value.</param>
    /// <param name="key">The key value.</param>
    /// <returns>The result.</returns>
    private static IEnumerable<TValue> GetLookup<TKey, TValue>(QuaternaryDictionary<TKey, TValue> dictionary, string indexName, object key)
        where TKey : notnull
    {
        // The Indices property is in the base class QuaternaryBase<TItem, TValue>
        var baseType = dictionary.GetType().BaseType ?? throw new InvalidOperationException("The dictionary has no base type.");
        var property = baseType.GetProperty("Indices", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            ?? throw new MissingMemberException(baseType.FullName, "Indices");
        var indices = (ConcurrentDictionary<string, ISecondaryIndex<TValue>>)(property.GetValue(dictionary)
            ?? throw new InvalidOperationException("The secondary-index dictionary was null."));
        indices.TryGetValue(indexName, out var index).Should().BeTrue();
        var concreteIndex = index ?? throw new InvalidOperationException($"The secondary index '{indexName}' was not found.");
        var lookupMethod = concreteIndex.GetType().GetMethod(nameof(QuaternaryDictionary<,>.Lookup), BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingMethodException(concreteIndex.GetType().FullName, nameof(QuaternaryDictionary<,>.Lookup));
        return (IEnumerable<TValue>)(lookupMethod.Invoke(concreteIndex, [key])
            ?? throw new InvalidOperationException("The secondary-index lookup returned null."));
    }
}
#endif
