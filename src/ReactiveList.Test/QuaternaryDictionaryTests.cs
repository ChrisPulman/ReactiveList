// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER || NETFRAMEWORK

using System;
using System.Collections.Generic;
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
    /// <summary>The second key used by dictionary test data.</summary>
    private const int SecondDictionaryKey = 2;

    /// <summary>The third key used by dictionary test data.</summary>
    private const int ThirdDictionaryKey = 3;

    /// <summary>The fourth key used by dictionary test data.</summary>
    private const int FourthDictionaryKey = 4;

    /// <summary>The expected length of five-character test values.</summary>
    private const int FiveCharacterLength = 5;

    /// <summary>The expected length of nine-character test values.</summary>
    private const int NineCharacterLength = 9;

    /// <summary>The tenth key used by dictionary test data.</summary>
    private const int TenthDictionaryKey = 10;

    /// <summary>The expected length of eleven-character test values.</summary>
    private const int ElevenCharacterLength = 11;

    /// <summary>The twentieth key used by dictionary test data.</summary>
    private const int TwentiethDictionaryKey = 20;

    /// <summary>A key that is intentionally absent from test dictionaries.</summary>
    private const int MissingDictionaryKey = 99;

    /// <summary>The textual value associated with the third key.</summary>
    private const string ThreeText = "three";

    /// <summary>The name of the secondary index that groups values by length.</summary>
    private const string LengthIndexName = "ByLength";

    /// <summary>A five-character value used by length-index tests.</summary>
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

        _ = dict[1].Should().Be("one");

        dict[1] = "uno";

        _ = dict[1].Should().Be("uno");
        _ = dict.Count.Should().Be(1);
        _ = dict.ContainsKey(1).Should().BeTrue();
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

        _ = dict.TryAdd(SecondDictionaryKey, "two").Should().BeTrue();
        _ = dict.TryAdd(SecondDictionaryKey, "dos").Should().BeFalse();

        _ = dict[SecondDictionaryKey].Should().Be("two");
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

        _ = reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        _ = actions.Should().ContainInOrder(CacheAction.Added, CacheAction.Updated);
        _ = dict[ThirdDictionaryKey].Should().Be(ThreeText);
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

        _ = dict.Remove(1).Should().BeTrue();
        _ = dict.ContainsKey(1).Should().BeFalse();
        _ = dict.Remove(1).Should().BeFalse();
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

        _ = reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        _ = notification.Should().NotBeNull();
        _ = notification!.Action.Should().Be(CacheAction.BatchAdded);
        _ = notification.Batch.Should().NotBeNull();
        _ = notification.Batch!.Count.Should().Be(ThirdDictionaryKey);
        notification.Batch.Dispose();

        _ = dict.Count.Should().Be(ThirdDictionaryKey);
        _ = dict.Keys.Should().BeEquivalentTo([1, SecondDictionaryKey, ThirdDictionaryKey]);
        _ = dict.Values.Should().BeEquivalentTo(["one", "two", ThreeText]);
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
        using var dict = new QuaternaryDictionary<int, string> { { 1, "one" }, { SecondDictionaryKey, "two" } };

        var array = new KeyValuePair<int, string>[3];

        dict.CopyTo(array, 1);

        var copiedEntries = new List<KeyValuePair<int, string>>(dict.Count);
        for (var index = 1; index < array.Length; index++)
        {
            copiedEntries.Add(array[index]);
        }

        _ = copiedEntries.Should().BeEquivalentTo(dict);
    }

    /// <summary>Verifies that the value index in a QuaternaryDictionary correctly tracks additions and removals of items.</summary>
    /// <remarks>This test ensures that when items are added to or removed from the dictionary, the associated
    /// value index reflects these changes as expected. It also verifies that clearing the dictionary updates the value
    /// index accordingly.</remarks>
    [Test]
    public void ValueIndex_ShouldTrackAddsAndRemovals()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex(LengthIndexName, static v => v.Length);

        dict.AddRange([
            new KeyValuePair<int, string>(1, ShortValue),
            new KeyValuePair<int, string>(SecondDictionaryKey, "longvalue")
        ]);

        _ = GetLookup(dict, LengthIndexName, FiveCharacterLength).Should().ContainSingle().Which.Should().Be(ShortValue);

        _ = dict.Remove(1);

        _ = GetLookup(dict, LengthIndexName, FiveCharacterLength).Should().BeEmpty();

        dict.Clear();

        _ = GetLookup(dict, LengthIndexName, NineCharacterLength).Should().BeEmpty();
    }

    /// <summary>Verifies that the Lookup method returns the correct result for existing and non-existing keys.</summary>
    [Test]
    public void Lookup_ShouldReturnCorrectResult()
    {
        using var dict = new QuaternaryDictionary<int, string> { { 1, "one" }, { SecondDictionaryKey, "two" } };

        var result1 = dict.Lookup(1);
        _ = result1.HasValue.Should().BeTrue();
        _ = result1.Value.Should().Be("one");

        var result2 = dict.Lookup(MissingDictionaryKey);
        _ = result2.HasValue.Should().BeFalse();
        _ = result2.Value.Should().BeNull();
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

        _ = reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        _ = notification.Should().NotBeNull();
        _ = dict.Count.Should().Be(SecondDictionaryKey);
        _ = dict.ContainsKey(SecondDictionaryKey).Should().BeFalse();
        _ = dict.ContainsKey(FourthDictionaryKey).Should().BeFalse();
        _ = dict.ContainsKey(1).Should().BeTrue();
        _ = dict.ContainsKey(ThirdDictionaryKey).Should().BeTrue();
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

        var removedCount = dict.RemoveMany(static kvp => kvp.Value.Length > 5);

        _ = removedCount.Should().Be(SecondDictionaryKey);
        _ = dict.Count.Should().Be(1);
        _ = dict.ContainsKey(1).Should().BeTrue();
        _ = dict.ContainsKey(SecondDictionaryKey).Should().BeFalse();
        _ = dict.ContainsKey(ThirdDictionaryKey).Should().BeFalse();
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

        dict.Edit(static innerDict =>
        {
            innerDict.Clear();
            innerDict.Add(TenthDictionaryKey, "ten");
            innerDict.Add(TwentiethDictionaryKey, "twenty");
        });

        _ = reset.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        _ = notifications.Should().ContainSingle().Which.Should().Be(CacheAction.BatchOperation);
        _ = dict.Count.Should().Be(SecondDictionaryKey);
        _ = dict.ContainsKey(TenthDictionaryKey).Should().BeTrue();
        _ = dict.ContainsKey(TwentiethDictionaryKey).Should().BeTrue();
        _ = dict.ContainsKey(1).Should().BeFalse();
    }

    /// <summary>Verifies that Edit updates value indices correctly.</summary>
    [Test]
    public void Edit_ShouldUpdateValueIndicesCorrectly()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex(LengthIndexName, static v => v.Length);

        dict.AddRange([
            new KeyValuePair<int, string>(1, ShortValue),
            new KeyValuePair<int, string>(SecondDictionaryKey, "longvalue")
        ]);

        dict.Edit(static innerDict =>
        {
            innerDict.Clear();
            innerDict.Add(ThirdDictionaryKey, "tiny");
            innerDict.Add(FourthDictionaryKey, "biggervalue");
        });

        _ = GetLookup(dict, LengthIndexName, FiveCharacterLength).Should().BeEmpty();
        _ = GetLookup(dict, LengthIndexName, FourthDictionaryKey).Should().ContainSingle().Which.Should().Be("tiny");
        _ = GetLookup(dict, LengthIndexName, ElevenCharacterLength).Should().ContainSingle().Which.Should().Be("biggervalue");
    }

    /// <summary>Verifies that GetValuesBySecondaryIndex returns matching values.</summary>
    [Test]
    public void GetValuesBySecondaryIndex_ShouldReturnMatchingValues()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex(LengthIndexName, static v => v.Length);

        dict.AddRange([
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(SecondDictionaryKey, "two"),
            new KeyValuePair<int, string>(ThirdDictionaryKey, ThreeText),
            new KeyValuePair<int, string>(FourthDictionaryKey, "four")
        ]);

        var threeCharValues = new List<string>(dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey));
        _ = threeCharValues.Should().HaveCount(SecondDictionaryKey);
        _ = threeCharValues.Should().Contain("one");
        _ = threeCharValues.Should().Contain("two");

        var fiveCharValues = new List<string>(dict.GetValuesBySecondaryIndex(LengthIndexName, FiveCharacterLength));
        _ = fiveCharValues.Should().ContainSingle().Which.Should().Be(ThreeText);
    }

    /// <summary>Verifies that GetValuesBySecondaryIndex returns empty for non-existent index.</summary>
    [Test]
    public void GetValuesBySecondaryIndex_WithNonExistentIndex_ShouldReturnEmpty()
    {
        using var dict = new QuaternaryDictionary<int, string> { { 1, "one" } };

        var result = dict.GetValuesBySecondaryIndex("NonExistent", "key");
        _ = result.Should().BeEmpty();
    }

    /// <summary>Verifies that ValueMatchesSecondaryIndex returns correct results.</summary>
    [Test]
    public void ValueMatchesSecondaryIndex_ShouldReturnCorrectResult()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex(LengthIndexName, static v => v.Length);
        dict.Add(1, "test");

        _ = dict.ValueMatchesSecondaryIndex(LengthIndexName, "test", FourthDictionaryKey).Should().BeTrue();
        _ = dict.ValueMatchesSecondaryIndex(LengthIndexName, "test", FiveCharacterLength).Should().BeFalse();
        _ = dict.ValueMatchesSecondaryIndex("NonExistent", "test", FourthDictionaryKey).Should().BeFalse();
    }

    /// <summary>Verifies that GetValuesBySecondaryIndex updates after additions and removals.</summary>
    [Test]
    public void GetValuesBySecondaryIndex_ShouldUpdateAfterAdditionsAndRemovals()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex(LengthIndexName, static v => v.Length);

        dict.Add(1, "one");
        _ = dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey).Should().ContainSingle().Which.Should().Be("one");

        dict.Add(SecondDictionaryKey, "two");
        _ = dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey).Should().HaveCount(SecondDictionaryKey);

        _ = dict.Remove(1);
        _ = dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey).Should().ContainSingle().Which.Should().Be("two");

        dict.Clear();
        _ = dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey).Should().BeEmpty();
    }

    /// <summary>Provides GetLookup.</summary>
    /// <typeparam name="TKey">The TKey type.</typeparam>
    /// <typeparam name="TValue">The TValue type.</typeparam>
    /// <typeparam name="TIndexKey">The secondary-index key type.</typeparam>
    /// <param name="dictionary">The dictionary value.</param>
    /// <param name="indexName">The indexName value.</param>
    /// <param name="key">The key value.</param>
    /// <returns>The result.</returns>
    private static IEnumerable<TValue> GetLookup<TKey, TValue, TIndexKey>(
        QuaternaryDictionary<TKey, TValue> dictionary,
        string indexName,
        TIndexKey key)
        where TKey : notnull
        where TIndexKey : notnull =>
        dictionary.GetValuesBySecondaryIndex(indexName, key);
}
#endif
