// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CP.Primitives.Collections;
using FluentAssertions;
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
    [Test]
    public void QuadList_ShouldSupportMutationAndEnumerationPaths()
    {
        using var list = new QuadList<int>();

        list.AddRange(ReadOnlySpan<int>.Empty);
        Enumerable.Range(0, QuadListItemCount).ToList().ForEach(list.Add);

        list.Count.Should().Be(QuadListItemCount);
        list[QuadListMutationIndex].Should().Be(QuadListMutationIndex);

        list[QuadListMutationIndex] = QuadListReplacementValue;
        list[QuadListMutationIndex].Should().Be(QuadListReplacementValue);
        list.Contains(QuadListReplacementValue).Should().BeTrue();
        list.IndexOf(QuadListReplacementValue).Should().Be(QuadListMutationIndex);

        list.Remove(QuadListReplacementValue).Should().BeTrue();
        list.Remove(MissingLookupValue).Should().BeFalse();
        list.RemoveAt(list.Count - 1);

        var copied = new int[list.Count + CopyPadding];
        list.CopyTo(copied, 1);
        copied[1].Should().Be(0);

        var structEnumerator = list.GetEnumerator();
        var matchingStructEnumerator = list.GetEnumerator();
        (structEnumerator == matchingStructEnumerator).Should().BeTrue();
        (structEnumerator != matchingStructEnumerator).Should().BeFalse();
        structEnumerator.Equals((object)matchingStructEnumerator).Should().BeTrue();
        structEnumerator.Equals(new object()).Should().BeFalse();
        structEnumerator.GetHashCode().Should().NotBe(0);
        structEnumerator.MoveNext().Should().BeTrue();
        (structEnumerator != matchingStructEnumerator).Should().BeTrue();
        (structEnumerator == matchingStructEnumerator).Should().BeFalse();
        structEnumerator.Current.Should().Be(0);
        while (structEnumerator.MoveNext())
        {
            _ = structEnumerator.Current;
        }

        structEnumerator.MoveNext().Should().BeFalse();

        using var enumerator = ((IEnumerable<int>)list).GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be(0);
        enumerator.Reset();
        enumerator.MoveNext().Should().BeTrue();
        ((IEnumerator)enumerator).Current.Should().Be(0);
        while (enumerator.MoveNext())
        {
            _ = enumerator.Current;
        }

        enumerator.MoveNext().Should().BeFalse();

        var nonGenericEnumerator = ((IEnumerable)list).GetEnumerator();
        nonGenericEnumerator.MoveNext().Should().BeTrue();
        nonGenericEnumerator.Current.Should().Be(0);

        list.AsSpan().ToArray().Should().Contain(HighestRemainingValue);
        list.AsSpan().ToArray().Should().NotContain(RemovedTailValue);
        list.Clear();
        list.Count.Should().Be(0);
        list.Dispose();
        list.Dispose();
    }

    /// <summary>Verifies QuadList guard clauses for invalid indexes.</summary>
    [Test]
    public void QuadList_InvalidIndexes_ShouldThrow()
    {
        using var list = new QuadList<string> { "first" };

        Action getNegative = () => _ = list[-1];
        Action getTooHigh = () => _ = list[1];
        Action setTooHigh = () => list[1] = "missing";
        Action removeTooHigh = () => list.RemoveAt(1);

        getNegative.Should().Throw<ArgumentOutOfRangeException>();
        getTooHigh.Should().Throw<ArgumentOutOfRangeException>();
        setTooHigh.Should().Throw<ArgumentOutOfRangeException>();
        removeTooHigh.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>Verifies QuadDictionary collision handling, ref updates, free-list reuse, and wrapper enumeration.</summary>
    [Test]
    public void QuadDictionary_ShouldHandleCollisionsAndRemovedSlots()
    {
        using var dictionary = new QuadDictionary<string, int>(new ConstantHashStringComparer());

        dictionary.TryAdd("one", 1).Should().BeTrue();
        dictionary.TryAdd("two", SecondDictionaryValue).Should().BeTrue();
        dictionary.TryAdd("three", InitialDictionaryCount).Should().BeTrue();
        dictionary.TryAdd("two", DuplicateDictionaryValue).Should().BeFalse();
        dictionary.Count.Should().Be(InitialDictionaryCount);

        dictionary.Remove("two", out var removedMiddle).Should().BeTrue();
        removedMiddle.Should().Be(SecondDictionaryValue);
        dictionary.Remove("three").Should().BeTrue();
        dictionary.Remove("missing", out var missingValue).Should().BeFalse();
        missingValue.Should().Be(default(int));

        dictionary.TryAdd("four", FourthDictionaryValue).Should().BeTrue();
        dictionary["one"].Should().Be(1);
        dictionary["one"] = UpdatedDictionaryValue;
        dictionary["one"].Should().Be(UpdatedDictionaryValue);

        ref var valueRef = ref dictionary.GetValueRefOrAddDefault("five", out var existed);
        existed.Should().BeFalse();
        valueRef = FifthDictionaryValue;

        ref var existingRef = ref dictionary.GetValueRefOrAddDefault("five", out existed);
        existed.Should().BeTrue();
        existingRef.Should().Be(FifthDictionaryValue);

        dictionary.Keys.Should().BeEquivalentTo(["one", "four", "five"]);
        dictionary.Values.Should().BeEquivalentTo([UpdatedDictionaryValue, FourthDictionaryValue, FifthDictionaryValue]);

        var copied = new List<KeyValuePair<string, int>>();
        dictionary.CopyTo(copied);
        copied.Should().BeEquivalentTo(dictionary.ToArray());

        var structEnumerator = dictionary.GetEnumerator();
        var matchingStructEnumerator = dictionary.GetEnumerator();
        (structEnumerator == matchingStructEnumerator).Should().BeTrue();
        (structEnumerator != matchingStructEnumerator).Should().BeFalse();
        structEnumerator.Equals((object)matchingStructEnumerator).Should().BeTrue();
        structEnumerator.Equals(new object()).Should().BeFalse();
        structEnumerator.GetHashCode().Should().NotBe(0);
        structEnumerator.TryGetNext(out var first).Should().BeTrue();
        (structEnumerator != matchingStructEnumerator).Should().BeTrue();
        (structEnumerator == matchingStructEnumerator).Should().BeFalse();
        first.Key.Should().NotBeNull();
        while (structEnumerator.MoveNext())
        {
            _ = structEnumerator.Current;
        }

        structEnumerator.TryGetNext(out var afterLast).Should().BeFalse();
        afterLast.Should().Be(default(KeyValuePair<string, int>));

        using var wrapper = ((IEnumerable<KeyValuePair<string, int>>)dictionary).GetEnumerator();
        wrapper.MoveNext().Should().BeTrue();
        ((IEnumerator)wrapper).Current.Should().BeOfType<KeyValuePair<string, int>>();
        wrapper.Reset();
        wrapper.MoveNext().Should().BeTrue();

        var nonGenericWrapper = ((IEnumerable)dictionary).GetEnumerator();
        nonGenericWrapper.MoveNext().Should().BeTrue();
        nonGenericWrapper.Current.Should().BeOfType<KeyValuePair<string, int>>();

        dictionary.Dispose();
        dictionary.Dispose();
    }

    /// <summary>Verifies QuadDictionary duplicate, missing-key, capacity, resize, and clear behavior.</summary>
    [Test]
    public void QuadDictionary_ShouldCoverGuardsCapacityAndClear()
    {
        using var dictionary = new QuadDictionary<int, string>();

        dictionary.EnsureCapacity(InitialDictionaryCapacity);
        dictionary.EnsureCapacity(ExpandedDictionaryCapacity);

        Enumerable.Range(0, DictionaryPopulationCount).ToList().ForEach(i => dictionary.Add(i, $"value-{i}"));

        dictionary.Count.Should().Be(DictionaryPopulationCount);
        dictionary.ContainsKey(ExistingDictionaryKey).Should().BeTrue();
        dictionary.TryGetValue(MissingLookupValue, out var missing).Should().BeFalse();
        missing.Should().BeNull();

        Action duplicateAdd = () => dictionary.Add(ExistingDictionaryKey, "duplicate");
        Action missingIndexer = () => _ = dictionary[MissingLookupValue];
        Action nullCopyTarget = () => dictionary.CopyTo(null!);
        Action nullKeysTarget = () => dictionary.CopyKeysTo(null!);
        Action nullValuesTarget = () => dictionary.CopyValuesTo(null!);

        duplicateAdd.Should().Throw<ArgumentException>();
        missingIndexer.Should().Throw<KeyNotFoundException>();
        nullCopyTarget.Should().Throw<ArgumentNullException>();
        nullKeysTarget.Should().Throw<ArgumentNullException>().WithParameterName("list");
        nullValuesTarget.Should().Throw<ArgumentNullException>().WithParameterName("list");

        dictionary.Clear();
        dictionary.Count.Should().Be(0);
        dictionary.Clear();

        using var autoResize = new QuadDictionary<int, int>();
        for (var i = 0; i < AutoResizeItemCount; i++)
        {
            autoResize.Add(i, i);
        }

        autoResize.Remove(1).Should().BeTrue();
        autoResize.EnsureCapacity(AutoResizeCapacity);
        autoResize.Keys.Should().Contain(AutoResizeLastKey);

        using var nullableKeyDictionary = new QuadDictionary<string?, int>();
        nullableKeyDictionary.TryAdd(null, 1).Should().BeTrue();
        nullableKeyDictionary.TryGetValue(null, out var nullKeyValue).Should().BeTrue();
        nullKeyValue.Should().Be(1);
    }

    /// <summary>Verifies pooled batch change tracking including growth and reset on disposal.</summary>
    [Test]
    public void BatchChangeTracker_ShouldTrackGrowAndDispose()
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

        tracker.HasChanges.Should().BeTrue();
        tracker.AddedItems.ToArray().Should().StartWith("added-0").And.EndWith("added-23");
        tracker.RemovedItems.ToArray().Should().StartWith("removed-0").And.EndWith("removed-19");

        tracker.Dispose();

        tracker.HasChanges.Should().BeFalse();
        tracker.AddedItems.Length.Should().Be(0);
        tracker.RemovedItems.Length.Should().Be(0);
    }

    /// <summary>Verifies ChangeToken value storage and change detection.</summary>
    [Test]
    public void ChangeToken_ShouldReportVersionChanges()
    {
        var token = new ChangeToken(version: 7, count: 3);

        token.Version.Should().Be(InitialTokenVersion);
        token.Count.Should().Be(TrackedItemCount);
        token.HasChanged(InitialTokenVersion).Should().BeFalse();
        token.HasChanged(NextTokenVersion).Should().BeTrue();
        token.Should().Be(new ChangeToken(InitialTokenVersion, TrackedItemCount));
    }

    /// <summary>Verifies PooledBuffer list copying and idempotent disposal.</summary>
    [Test]
    public void PooledBuffer_FromList_ShouldExposeCopiedSpanAndDispose()
    {
        var source = new List<string> { "alpha", "beta", "gamma" };
        using var buffer = PooledBuffer<string>.FromList(source);

        buffer.Span.ToArray().Should().Equal(source);

        buffer.Dispose();
        buffer.Dispose();
    }

    /// <summary>Verifies ValueBuffer stack, rent, growth, and disposal paths.</summary>
    [Test]
    public void ValueBuffer_ShouldUseStackThenRentedStorage()
    {
        Span<int> stack = stackalloc int[2];
        var buffer = new ValueBuffer<int>(in stack);

        buffer.Add(1);
        buffer.Add(SecondBufferedValue);
        buffer.Span.ToArray().Should().Equal(1, SecondBufferedValue);

        buffer.Add(ThirdBufferedValue);
        for (var i = 4; i <= ValueBufferFinalCount; i++)
        {
            buffer.Add(i);
        }

        buffer.Count.Should().Be(ValueBufferFinalCount);
        buffer.Span.ToArray().Should().Equal(Enumerable.Range(1, ValueBufferFinalCount));

        buffer.Dispose();
        buffer.Dispose();
    }

    /// <summary>Verifies shard hashing produces stable in-range shard indexes for null, positive, and negative hash codes.</summary>
    [Test]
    public void ShardHash_ShouldReturnExpectedShardRanges()
    {
        ShardHash.GetShardIndex<string?>(null, FourWayShardCount).Should().Be(0);
        ShardHash.GetShardIndex4<string?>(null).Should().Be(0);

        var positive = new FixedHash(1);
        var negative = new FixedHash(int.MinValue);

        ShardHash.GetShardIndex(positive, EightWayShardCount).Should().BeInRange(0, EightWayMaximumIndex);
        ShardHash.GetShardIndex(negative, SixteenWayShardCount).Should().BeInRange(0, SixteenWayMaximumIndex);
        ShardHash.GetShardIndex4(positive).Should().Be(ShardHash.GetShardIndex(positive, FourWayShardCount));
        ShardHash.GetShardIndex4(negative).Should().BeInRange(0, FourWayMaximumIndex);
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
        private readonly int _hashCode;

        /// <summary>Initializes a new instance of the <see cref="FixedHash"/> class.</summary>
        /// <param name="hashCode">The hashCode value.</param>
        public FixedHash(int hashCode) => _hashCode = hashCode;

        public override int GetHashCode() => _hashCode;
    }
}
#endif
