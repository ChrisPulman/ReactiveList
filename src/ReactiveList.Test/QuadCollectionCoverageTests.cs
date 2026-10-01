// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Collections;
#else
using CP.Primitives.Collections;
#endif
using TUnit.Assertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Covers low-level quad collection and pooled helper paths that are not reached by the public collection tests.</summary>
public class QuadCollectionCoverageTests
{
    /// <summary>The quad list item count.</summary>
    private const int QuadListItemCount = 40;

    /// <summary>The quad list mutation index.</summary>
    private const int QuadListMutationIndex = 3;

    /// <summary>The quad list replacement value.</summary>
    private const int QuadListReplacementValue = 300;

    /// <summary>The missing lookup value.</summary>
    private const int MissingLookupValue = 999;

    /// <summary>The copy padding.</summary>
    private const int CopyPadding = 2;

    /// <summary>The highest remaining value.</summary>
    private const int HighestRemainingValue = 38;

    /// <summary>The removed tail value.</summary>
    private const int RemovedTailValue = 39;

    /// <summary>The second dictionary value.</summary>
    private const int SecondDictionaryValue = 2;

    /// <summary>The initial dictionary count.</summary>
    private const int InitialDictionaryCount = 3;

    /// <summary>The duplicate dictionary value.</summary>
    private const int DuplicateDictionaryValue = 22;

    /// <summary>The fourth dictionary value.</summary>
    private const int FourthDictionaryValue = 4;

    /// <summary>The fifth dictionary value.</summary>
    private const int FifthDictionaryValue = 5;

    /// <summary>The updated dictionary value.</summary>
    private const int UpdatedDictionaryValue = 10;

    /// <summary>The initial dictionary capacity.</summary>
    private const int InitialDictionaryCapacity = 8;

    /// <summary>The expanded dictionary capacity.</summary>
    private const int ExpandedDictionaryCapacity = 128;

    /// <summary>The dictionary population count.</summary>
    private const int DictionaryPopulationCount = 120;

    /// <summary>The existing dictionary key.</summary>
    private const int ExistingDictionaryKey = 42;

    /// <summary>The auto resize item count.</summary>
    private const int AutoResizeItemCount = 20;

    /// <summary>The auto resize capacity.</summary>
    private const int AutoResizeCapacity = 64;

    /// <summary>The auto resize last key.</summary>
    private const int AutoResizeLastKey = 19;

    /// <summary>The added tracker item count.</summary>
    private const int AddedTrackerItemCount = 24;

    /// <summary>The removed tracker item count.</summary>
    private const int RemovedTrackerItemCount = 20;

    /// <summary>The initial token version.</summary>
    private const int InitialTokenVersion = 7;

    /// <summary>The tracked item count.</summary>
    private const int TrackedItemCount = 3;

    /// <summary>The next token version.</summary>
    private const int NextTokenVersion = 8;

    /// <summary>The second buffered value.</summary>
    private const int SecondBufferedValue = 2;

    /// <summary>The third buffered value.</summary>
    private const int ThirdBufferedValue = 3;

    /// <summary>The value buffer final count.</summary>
    private const int ValueBufferFinalCount = 40;

    /// <summary>The four way shard count.</summary>
    private const int FourWayShardCount = 4;

    /// <summary>The four way maximum index.</summary>
    private const int FourWayMaximumIndex = 3;

    /// <summary>The eight way shard count.</summary>
    private const int EightWayShardCount = 8;

    /// <summary>The eight way maximum index.</summary>
    private const int EightWayMaximumIndex = 7;

    /// <summary>The sixteen way shard count.</summary>
    private const int SixteenWayShardCount = 16;

    /// <summary>The sixteen way maximum index.</summary>
    private const int SixteenWayMaximumIndex = 15;

    /// <summary>Verifies QuadList indexing, resizing, removal, copy, and enumerator wrapper behavior.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task QuadList_ShouldSupportMutationAndEnumerationPaths()
    {
        var list = new QuadList<int>();

        list.AddRange(ReadOnlySpan<int>.Empty);
        for (var value = 0; value < QuadListItemCount; value++)
        {
            list.Add(value);
        }

        await Assert.That(list.Count).IsEqualTo(QuadListItemCount);
        await Assert.That(list[QuadListMutationIndex]).IsEqualTo(QuadListMutationIndex);

        list[QuadListMutationIndex] = QuadListReplacementValue;
        await Assert.That(list[QuadListMutationIndex]).IsEqualTo(QuadListReplacementValue);
        await Assert.That(list.Contains(QuadListReplacementValue)).IsTrue();
        await Assert.That(list.IndexOf(QuadListReplacementValue)).IsEqualTo(QuadListMutationIndex);

        await Assert.That(list.Remove(QuadListReplacementValue)).IsTrue();
        await Assert.That(list.Remove(MissingLookupValue)).IsFalse();
        list.RemoveAt(list.Count - 1);

        var copied = new int[list.Count + CopyPadding];
        list.CopyTo(copied, 1);
        await Assert.That(copied[1]).IsEqualTo(0);

        var structEnumerator = list.GetEnumerator();
        var matchingStructEnumerator = list.GetEnumerator();
        await Assert.That(structEnumerator == matchingStructEnumerator).IsTrue();
        await Assert.That(structEnumerator != matchingStructEnumerator).IsFalse();
        await Assert.That(structEnumerator.Equals((object)matchingStructEnumerator)).IsTrue();
        await Assert.That(structEnumerator.Equals(new object())).IsFalse();
        await Assert.That(structEnumerator.GetHashCode()).IsNotEqualTo(0);
        await Assert.That(structEnumerator.MoveNext()).IsTrue();
        await Assert.That(structEnumerator != matchingStructEnumerator).IsTrue();
        await Assert.That(structEnumerator == matchingStructEnumerator).IsFalse();
        await Assert.That(structEnumerator.Current).IsEqualTo(0);
        while (structEnumerator.MoveNext())
        {
            _ = structEnumerator.Current;
        }

        await Assert.That(structEnumerator.MoveNext()).IsFalse();

        using var enumerator = ((IEnumerable<int>)list).GetEnumerator();
        await Assert.That(enumerator.MoveNext()).IsTrue();
        await Assert.That(enumerator.Current).IsEqualTo(0);
        enumerator.Reset();
        await Assert.That(enumerator.MoveNext()).IsTrue();
        await Assert.That(((IEnumerator)enumerator).Current).IsEqualTo(0);
        while (enumerator.MoveNext())
        {
            _ = enumerator.Current;
        }

        await Assert.That(enumerator.MoveNext()).IsFalse();

        var nonGenericEnumerator = ((IEnumerable)list).GetEnumerator();
        await Assert.That(nonGenericEnumerator.MoveNext()).IsTrue();
        await Assert.That(nonGenericEnumerator.Current).IsEqualTo(0);

        await Assert.That(list.AsSpan().ToArray()).Contains(HighestRemainingValue);
        await Assert.That(list.AsSpan().ToArray()).DoesNotContain(RemovedTailValue);
        list.Clear();
        await Assert.That(list.Count).IsEqualTo(0);
        list.Dispose();
        list.Dispose();
    }

    /// <summary>Verifies QuadList guard clauses for invalid indexes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task QuadList_InvalidIndexes_ShouldThrow()
    {
        using var list = new QuadList<string> { "first" };

        Action getNegative = () => _ = list[-1];
        Action getTooHigh = () => _ = list[1];
        Action setTooHigh = () => list[1] = "missing";
        Action removeTooHigh = () => list.RemoveAt(1);

        await Assert.That(getNegative).Throws<ArgumentOutOfRangeException>();
        await Assert.That(getTooHigh).Throws<ArgumentOutOfRangeException>();
        await Assert.That(setTooHigh).Throws<ArgumentOutOfRangeException>();
        await Assert.That(removeTooHigh).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Verifies QuadDictionary collision handling, ref updates, free-list reuse, and wrapper enumeration.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task QuadDictionary_ShouldHandleCollisionsAndRemovedSlots()
    {
        var dictionary = new QuadDictionary<string, int>(new ConstantHashStringComparer());

        await Assert.That(dictionary.TryAdd("one", 1)).IsTrue();
        await Assert.That(dictionary.TryAdd("two", SecondDictionaryValue)).IsTrue();
        await Assert.That(dictionary.TryAdd("three", InitialDictionaryCount)).IsTrue();
        await Assert.That(dictionary.TryAdd("two", DuplicateDictionaryValue)).IsFalse();
        await Assert.That(dictionary.Count).IsEqualTo(InitialDictionaryCount);

        await Assert.That(dictionary.Remove("two", out var removedMiddle)).IsTrue();
        await Assert.That(removedMiddle).IsEqualTo(SecondDictionaryValue);
        await Assert.That(dictionary.Remove("three")).IsTrue();
        await Assert.That(dictionary.Remove("missing", out var missingValue)).IsFalse();
        await Assert.That(missingValue).IsEqualTo(default(int));

        await Assert.That(dictionary.TryAdd("four", FourthDictionaryValue)).IsTrue();
        await Assert.That(dictionary["one"]).IsEqualTo(1);
        dictionary["one"] = UpdatedDictionaryValue;
        await Assert.That(dictionary["one"]).IsEqualTo(UpdatedDictionaryValue);

        ref var valueRef = ref dictionary.GetValueRefOrAddDefault("five", out var existed);
        var initiallyExisted = existed;
        valueRef = FifthDictionaryValue;

        var existingValue = dictionary.GetValueRefOrAddDefault("five", out existed);
        await Assert.That(initiallyExisted).IsFalse();
        await Assert.That(existed).IsTrue();
        await Assert.That(existingValue).IsEqualTo(FifthDictionaryValue);

        await Assert.That(dictionary.Keys).IsEquivalentTo(["one", "four", "five"]);
        await Assert.That(dictionary.Values).IsEquivalentTo([UpdatedDictionaryValue, FourthDictionaryValue, FifthDictionaryValue]);

        var copied = new List<KeyValuePair<string, int>>();
        dictionary.CopyTo(copied);
        var enumerated = new List<KeyValuePair<string, int>>(dictionary);
        await Assert.That(copied).IsEquivalentTo(enumerated);

        var structEnumerator = dictionary.GetEnumerator();
        var matchingStructEnumerator = dictionary.GetEnumerator();
        await Assert.That(structEnumerator == matchingStructEnumerator).IsTrue();
        await Assert.That(structEnumerator != matchingStructEnumerator).IsFalse();
        await Assert.That(structEnumerator.Equals((object)matchingStructEnumerator)).IsTrue();
        await Assert.That(structEnumerator.Equals(new object())).IsFalse();
        await Assert.That(structEnumerator.GetHashCode()).IsNotEqualTo(0);
        await Assert.That(structEnumerator.TryGetNext(out var first)).IsTrue();
        await Assert.That(structEnumerator != matchingStructEnumerator).IsTrue();
        await Assert.That(structEnumerator == matchingStructEnumerator).IsFalse();
        await Assert.That(first.Key).IsNotNull();
        while (structEnumerator.MoveNext())
        {
            _ = structEnumerator.Current;
        }

        await Assert.That(structEnumerator.TryGetNext(out var afterLast)).IsFalse();
        await Assert.That(afterLast).IsEqualTo(default(KeyValuePair<string, int>));

        using var wrapper = ((IEnumerable<KeyValuePair<string, int>>)dictionary).GetEnumerator();
        await Assert.That(wrapper.MoveNext()).IsTrue();
        await Assert.That(((IEnumerator)wrapper).Current).IsTypeOf<KeyValuePair<string, int>>();
        wrapper.Reset();
        await Assert.That(wrapper.MoveNext()).IsTrue();

        var nonGenericWrapper = ((IEnumerable)dictionary).GetEnumerator();
        await Assert.That(nonGenericWrapper.MoveNext()).IsTrue();
        await Assert.That(nonGenericWrapper.Current).IsTypeOf<KeyValuePair<string, int>>();

        dictionary.Dispose();
        dictionary.Dispose();
    }

    /// <summary>Verifies QuadDictionary duplicate, missing-key, capacity, resize, and clear behavior.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task QuadDictionary_ShouldCoverGuardsCapacityAndClear()
    {
        using var dictionary = new QuadDictionary<int, string>();

        dictionary.EnsureCapacity(InitialDictionaryCapacity);
        dictionary.EnsureCapacity(ExpandedDictionaryCapacity);

        for (var key = 0; key < DictionaryPopulationCount; key++)
        {
            dictionary.Add(key, $"value-{key}");
        }

        await Assert.That(dictionary.Count).IsEqualTo(DictionaryPopulationCount);
        await Assert.That(dictionary.ContainsKey(ExistingDictionaryKey)).IsTrue();
        await Assert.That(dictionary.TryGetValue(MissingLookupValue, out var missing)).IsFalse();
        await Assert.That(missing).IsNull();

        Action duplicateAdd = () => dictionary.Add(ExistingDictionaryKey, "duplicate");
        Action missingIndexer = () => _ = dictionary[MissingLookupValue];
        Action nullCopyTarget = () => dictionary.CopyTo(null!);
        Action nullKeysTarget = () => dictionary.CopyKeysTo(null!);
        Action nullValuesTarget = () => dictionary.CopyValuesTo(null!);

        await Assert.That(duplicateAdd).Throws<ArgumentException>();
        await Assert.That(missingIndexer).Throws<KeyNotFoundException>();
        await Assert.That(nullCopyTarget).Throws<ArgumentNullException>();
        await Assert.That(nullKeysTarget).Throws<ArgumentNullException>().WithParameterName("list");
        await Assert.That(nullValuesTarget).Throws<ArgumentNullException>().WithParameterName("list");

        dictionary.Clear();
        await Assert.That(dictionary.Count).IsEqualTo(0);
        dictionary.Clear();

        using var autoResize = new QuadDictionary<int, int>();
        for (var i = 0; i < AutoResizeItemCount; i++)
        {
            autoResize.Add(i, i);
        }

        await Assert.That(autoResize.Remove(1)).IsTrue();
        autoResize.EnsureCapacity(AutoResizeCapacity);
        await Assert.That(autoResize.Keys).Contains(AutoResizeLastKey);

        using var nullableKeyDictionary = new QuadDictionary<string?, int>();
        await Assert.That(nullableKeyDictionary.TryAdd(null, 1)).IsTrue();
        await Assert.That(nullableKeyDictionary.TryGetValue(null, out var nullKeyValue)).IsTrue();
        await Assert.That(nullKeyValue).IsEqualTo(1);
    }

    /// <summary>Verifies pooled batch change tracking including growth and reset on disposal.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BatchChangeTracker_ShouldTrackGrowAndDispose()
    {
        var tracker = default(BatchChangeTracker<string>);

        for (var i = 0; i < AddedTrackerItemCount; i++)
        {
            tracker.TrackAdded($"added-{i}");
        }

        for (var i = 0; i < RemovedTrackerItemCount; i++)
        {
            tracker.TrackRemoved($"removed-{i}");
        }

        await Assert.That(tracker.HasChanges).IsTrue();
        await Assert.That(tracker.AddedItems[0]).IsEqualTo("added-0");
        await Assert.That(tracker.AddedItems[tracker.AddedItems.Length - 1]).IsEqualTo("added-23");
        await Assert.That(tracker.RemovedItems[0]).IsEqualTo("removed-0");
        await Assert.That(tracker.RemovedItems[tracker.RemovedItems.Length - 1]).IsEqualTo("removed-19");

        tracker.Dispose();

        await Assert.That(tracker.HasChanges).IsFalse();
        await Assert.That(tracker.AddedItems.Length).IsEqualTo(0);
        await Assert.That(tracker.RemovedItems.Length).IsEqualTo(0);
    }

    /// <summary>Verifies ChangeToken value storage and change detection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChangeToken_ShouldReportVersionChanges()
    {
        var token = new ChangeToken(version: 7, count: 3);

        await Assert.That(token.Version).IsEqualTo(InitialTokenVersion);
        await Assert.That(token.Count).IsEqualTo(TrackedItemCount);
        await Assert.That(token.HasChanged(InitialTokenVersion)).IsFalse();
        await Assert.That(token.HasChanged(NextTokenVersion)).IsTrue();
        await Assert.That(token).IsEqualTo(new(InitialTokenVersion, TrackedItemCount));
    }

    /// <summary>Verifies PooledBuffer list copying and idempotent disposal.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PooledBuffer_FromList_ShouldExposeCopiedSpanAndDispose()
    {
        var source = new List<string> { "alpha", "beta", "gamma" };
        var buffer = PooledBuffer<string>.FromList(source);

        await Assert.That(buffer.Span.ToArray()).IsEquivalentTo(source, CollectionOrdering.Matching);

        buffer.Dispose();
        buffer.Dispose();
    }

    /// <summary>Verifies ValueBuffer stack, rent, growth, and disposal paths.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ValueBuffer_ShouldUseStackThenRentedStorage()
    {
        Span<int> stack = stackalloc int[2];
        var buffer = new ValueBuffer<int>(in stack);

        buffer.Add(1);
        buffer.Add(SecondBufferedValue);
        var initialItems = buffer.Span.ToArray();

        buffer.Add(ThirdBufferedValue);
        for (var i = 4; i <= ValueBufferFinalCount; i++)
        {
            buffer.Add(i);
        }

        var finalCount = buffer.Count;
        var finalItems = buffer.Span.ToArray();
        buffer.Dispose();
        buffer.Dispose();

        await Assert.That(initialItems).IsEquivalentTo([1, SecondBufferedValue], CollectionOrdering.Matching);
        await Assert.That(finalCount).IsEqualTo(ValueBufferFinalCount);
        await Assert.That(finalItems).IsEquivalentTo(Enumerable.Range(1, ValueBufferFinalCount), CollectionOrdering.Matching);
    }

    /// <summary>Verifies shard hashing produces stable in-range shard indexes for null, positive, and negative hash codes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ShardHash_ShouldReturnExpectedShardRanges()
    {
        await Assert.That(ShardHash.GetShardIndex<string?>(null, FourWayShardCount)).IsEqualTo(0);
        await Assert.That(ShardHash.GetShardIndex4<string?>(null)).IsEqualTo(0);

        var positive = new FixedHash(1);
        var negative = new FixedHash(int.MinValue);

        await Assert.That(ShardHash.GetShardIndex(positive, EightWayShardCount)).IsBetween(0, EightWayMaximumIndex);
        await Assert.That(ShardHash.GetShardIndex(negative, SixteenWayShardCount)).IsBetween(0, SixteenWayMaximumIndex);
        await Assert.That(ShardHash.GetShardIndex4(positive)).IsEqualTo(ShardHash.GetShardIndex(positive, FourWayShardCount));
        await Assert.That(ShardHash.GetShardIndex4(negative)).IsBetween(0, FourWayMaximumIndex);
    }

    /// <summary>Provides ConstantHashStringComparer.</summary>
    private sealed class ConstantHashStringComparer : IEqualityComparer<string>
    {
        /// <summary>Provides Equals.</summary>
        /// <param name="x">The x value.</param>
        /// <param name="y">The y value.</param>
        /// <returns>The result.</returns>
        public bool Equals(string? x, string? y) => StringComparer.Ordinal.Equals(x, y);

        /// <summary>Provides GetHashCode.</summary>
        /// <param name="obj">The obj value.</param>
        /// <returns>The result.</returns>
        public int GetHashCode(string obj) => 17;
    }

    /// <summary>Provides FixedHash.</summary>
    private sealed class FixedHash
    {
        /// <summary>The fixed hash code returned by this instance.</summary>
        private readonly int _hashCode;

        /// <summary>Initializes a new instance of the <see cref="FixedHash"/> class.</summary>
        /// <param name="hashCode">The hashCode value.</param>
        public FixedHash(int hashCode) => _hashCode = hashCode;

        public override int GetHashCode() => _hashCode;
    }
}
#endif
