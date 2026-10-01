// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER || NETFRAMEWORK

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
using TUnit.Assertions;
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
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddAndIndexer_ShouldStoreAndUpdateValues()
    {
        using var dict = new QuaternaryDictionary<int, string> { { 1, "one" } };

        await Assert.That(dict[1]).IsEqualTo("one");

        dict[1] = "uno";

        await Assert.That(dict[1]).IsEqualTo("uno");
        await Assert.That(dict.Count).IsEqualTo(1);
        await Assert.That(dict.ContainsKey(1)).IsTrue();
    }

    /// <summary>
    /// Verifies that the TryAdd method of QuaternaryDictionary prevents adding duplicate keys and retains the original
    /// value for an existing key.
    /// </summary>
    /// <remarks>This test ensures that when an attempt is made to add a key that already exists in the
    /// dictionary, TryAdd returns <see langword="false"/> and does not overwrite the existing value.</remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryAdd_ShouldPreventDuplicateKeys()
    {
        using var dict = new QuaternaryDictionary<int, string>();

        await Assert.That(dict.TryAdd(SecondDictionaryKey, "two")).IsTrue();
        await Assert.That(dict.TryAdd(SecondDictionaryKey, "dos")).IsFalse();

        await Assert.That(dict[SecondDictionaryKey]).IsEqualTo("two");
    }

    /// <summary>
    /// Verifies that the AddOrUpdate method emits the correct sequence of cache actions when adding and updating an
    /// entry in the dictionary.
    /// </summary>
    /// <remarks>This test ensures that the observable stream associated with the dictionary emits a
    /// CacheAction.Added event when a new entry is added and a CacheAction.Updated event when an existing entry is
    /// updated. It also verifies that the final value for the key reflects the most recent update.</remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddOrUpdate_ShouldEmitCorrectActions()
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

        await Assert.That(reset.Wait(TimeSpan.FromSeconds(1))).IsTrue();
        await Assert.That(TestSequences.ContainsInOrder(actions, [CacheAction.Added, CacheAction.Updated])).IsTrue();
        await Assert.That(dict[ThirdDictionaryKey]).IsEqualTo(ThreeText);
    }

    /// <summary>
    /// Verifies that removing an existing key from the dictionary succeeds and that subsequent removal attempts for the
    /// same key return false.
    /// </summary>
    /// <remarks>This test ensures that the Remove method returns <see langword="true"/> when an existing key
    /// is removed and <see langword="false"/> when attempting to remove a key that is not present in the
    /// dictionary.</remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_ShouldRemoveExistingAndReturnFalseForMissing()
    {
        using var dict = new QuaternaryDictionary<int, string> { { 1, "one" } };

        await Assert.That(dict.Remove(1)).IsTrue();
        await Assert.That(dict.ContainsKey(1)).IsFalse();
        await Assert.That(dict.Remove(1)).IsFalse();
    }

    /// <summary>
    /// Verifies that adding a range of items to a QuaternaryDictionary emits a batch added notification and correctly exposes
    /// the keys and values of the added items.
    /// </summary>
    /// <remarks>This test ensures that the AddRange method triggers a batch added event on the Stream,
    /// and that the dictionary's Keys and Values properties reflect the newly added items. It also checks that the
    /// batch notification contains all added items and that the dictionary's count is updated accordingly.</remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddRange_ShouldEmitBatchAndExposeKeysAndValues()
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

        await Assert.That(reset.Wait(TimeSpan.FromSeconds(1))).IsTrue();
        await Assert.That(notification).IsNotNull();
        await Assert.That(notification!.Action).IsEqualTo(CacheAction.BatchAdded);
        await Assert.That(notification.Batch).IsNotNull();
        await Assert.That(notification.Batch!.Count).IsEqualTo(ThirdDictionaryKey);
        notification.Batch.Dispose();

        await Assert.That(dict.Count).IsEqualTo(ThirdDictionaryKey);
        await Assert.That(dict.Keys).IsEquivalentTo([1, SecondDictionaryKey, ThirdDictionaryKey]);
        await Assert.That(dict.Values).IsEquivalentTo(["one", "two", ThreeText]);
    }

    /// <summary>
    /// Verifies that the CopyTo method copies all entries from the dictionary to the specified array starting at the
    /// given index.
    /// </summary>
    /// <remarks>This test ensures that the CopyTo method correctly transfers all key-value pairs to the
    /// target array without omitting or duplicating entries. It also checks that the entries are placed at the correct
    /// position in the array.</remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CopyTo_ShouldCopyAllEntries()
    {
        using var dict = new QuaternaryDictionary<int, string> { { 1, "one" }, { SecondDictionaryKey, "two" } };

        var array = new KeyValuePair<int, string>[3];

        dict.CopyTo(array, 1);

        var copiedEntries = new List<KeyValuePair<int, string>>(dict.Count);
        for (var index = 1; index < array.Length; index++)
        {
            copiedEntries.Add(array[index]);
        }

        await Assert.That(copiedEntries).IsEquivalentTo(dict);
    }

    /// <summary>Verifies that the value index in a QuaternaryDictionary correctly tracks additions and removals of items.</summary>
    /// <remarks>This test ensures that when items are added to or removed from the dictionary, the associated
    /// value index reflects these changes as expected. It also verifies that clearing the dictionary updates the value
    /// index accordingly.</remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ValueIndex_ShouldTrackAddsAndRemovals()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex(LengthIndexName, static v => v.Length);

        dict.AddRange([
            new KeyValuePair<int, string>(1, ShortValue),
            new KeyValuePair<int, string>(SecondDictionaryKey, "longvalue")
        ]);

        await Assert.That(await Assert.That(GetLookup(dict, LengthIndexName, FiveCharacterLength)).HasSingleItem()).IsEqualTo(ShortValue);

        _ = dict.Remove(1);

        await Assert.That(GetLookup(dict, LengthIndexName, FiveCharacterLength)).IsEmpty();

        dict.Clear();

        await Assert.That(GetLookup(dict, LengthIndexName, NineCharacterLength)).IsEmpty();
    }

    /// <summary>Verifies that the Lookup method returns the correct result for existing and non-existing keys.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Lookup_ShouldReturnCorrectResult()
    {
        using var dict = new QuaternaryDictionary<int, string> { { 1, "one" }, { SecondDictionaryKey, "two" } };

        var result1 = dict.Lookup(1);
        await Assert.That(result1.HasValue).IsTrue();
        await Assert.That(result1.Value).IsEqualTo("one");

        var result2 = dict.Lookup(MissingDictionaryKey);
        await Assert.That(result2.HasValue).IsFalse();
        await Assert.That(result2.Value).IsNull();
    }

    /// <summary>Verifies that RemoveKeys removes multiple keys in a batch operation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveKeys_ShouldRemoveMultipleKeysAndEmitBatch()
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

        await Assert.That(reset.Wait(TimeSpan.FromSeconds(1))).IsTrue();
        await Assert.That(notification).IsNotNull();
        await Assert.That(dict.Count).IsEqualTo(SecondDictionaryKey);
        await Assert.That(dict.ContainsKey(SecondDictionaryKey)).IsFalse();
        await Assert.That(dict.ContainsKey(FourthDictionaryKey)).IsFalse();
        await Assert.That(dict.ContainsKey(1)).IsTrue();
        await Assert.That(dict.ContainsKey(ThirdDictionaryKey)).IsTrue();
    }

    /// <summary>Verifies that RemoveMany with a predicate removes matching entries.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveMany_WithPredicate_ShouldRemoveMatchingEntries()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddRange([
            new KeyValuePair<int, string>(1, "tiny"),
            new KeyValuePair<int, string>(SecondDictionaryKey, "medium"),
            new KeyValuePair<int, string>(ThirdDictionaryKey, "verylongvalue")
        ]);

        var removedCount = dict.RemoveMany(static kvp => kvp.Value.Length > 5);

        await Assert.That(removedCount).IsEqualTo(SecondDictionaryKey);
        await Assert.That(dict.Count).IsEqualTo(1);
        await Assert.That(dict.ContainsKey(1)).IsTrue();
        await Assert.That(dict.ContainsKey(SecondDictionaryKey)).IsFalse();
        await Assert.That(dict.ContainsKey(ThirdDictionaryKey)).IsFalse();
    }

    /// <summary>Verifies that the Edit method allows batch modifications with a single notification.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldPerformBatchModificationsWithSingleNotification()
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

        await Assert.That(reset.Wait(TimeSpan.FromSeconds(1))).IsTrue();
        await Assert.That(await Assert.That(notifications).HasSingleItem()).IsEqualTo(CacheAction.BatchOperation);
        await Assert.That(dict.Count).IsEqualTo(SecondDictionaryKey);
        await Assert.That(dict.ContainsKey(TenthDictionaryKey)).IsTrue();
        await Assert.That(dict.ContainsKey(TwentiethDictionaryKey)).IsTrue();
        await Assert.That(dict.ContainsKey(1)).IsFalse();
    }

    /// <summary>Verifies that Edit updates value indices correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldUpdateValueIndicesCorrectly()
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

        await Assert.That(GetLookup(dict, LengthIndexName, FiveCharacterLength)).IsEmpty();
        await Assert.That(await Assert.That(GetLookup(dict, LengthIndexName, FourthDictionaryKey)).HasSingleItem()).IsEqualTo("tiny");
        await Assert.That(await Assert.That(GetLookup(dict, LengthIndexName, ElevenCharacterLength)).HasSingleItem()).IsEqualTo("biggervalue");
    }

    /// <summary>Verifies that GetValuesBySecondaryIndex returns matching values.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetValuesBySecondaryIndex_ShouldReturnMatchingValues()
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
        await Assert.That(threeCharValues).Count().IsEqualTo(SecondDictionaryKey);
        await Assert.That(threeCharValues).Contains("one");
        await Assert.That(threeCharValues).Contains("two");

        var fiveCharValues = new List<string>(dict.GetValuesBySecondaryIndex(LengthIndexName, FiveCharacterLength));
        await Assert.That(await Assert.That(fiveCharValues).HasSingleItem()).IsEqualTo(ThreeText);
    }

    /// <summary>Verifies that GetValuesBySecondaryIndex returns empty for non-existent index.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetValuesBySecondaryIndex_WithNonExistentIndex_ShouldReturnEmpty()
    {
        using var dict = new QuaternaryDictionary<int, string> { { 1, "one" } };

        var result = dict.GetValuesBySecondaryIndex("NonExistent", "key");
        await Assert.That(result).IsEmpty();
    }

    /// <summary>Verifies that ValueMatchesSecondaryIndex returns correct results.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ValueMatchesSecondaryIndex_ShouldReturnCorrectResult()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex(LengthIndexName, static v => v.Length);
        dict.Add(1, "test");

        await Assert.That(dict.ValueMatchesSecondaryIndex(LengthIndexName, "test", FourthDictionaryKey)).IsTrue();
        await Assert.That(dict.ValueMatchesSecondaryIndex(LengthIndexName, "test", FiveCharacterLength)).IsFalse();
        await Assert.That(dict.ValueMatchesSecondaryIndex("NonExistent", "test", FourthDictionaryKey)).IsFalse();
    }

    /// <summary>Verifies that GetValuesBySecondaryIndex updates after additions and removals.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetValuesBySecondaryIndex_ShouldUpdateAfterAdditionsAndRemovals()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddValueIndex(LengthIndexName, static v => v.Length);

        dict.Add(1, "one");
        await Assert.That(await Assert.That(dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey)).HasSingleItem()).IsEqualTo("one");

        dict.Add(SecondDictionaryKey, "two");
        await Assert.That(dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey)).Count().IsEqualTo(SecondDictionaryKey);

        _ = dict.Remove(1);
        await Assert.That(await Assert.That(dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey)).HasSingleItem()).IsEqualTo("two");

        dict.Clear();
        await Assert.That(dict.GetValuesBySecondaryIndex(LengthIndexName, ThirdDictionaryKey)).IsEmpty();
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
