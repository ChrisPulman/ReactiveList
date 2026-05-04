// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CP.Reactive.Collections;
using CP.Reactive.Internal;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>
/// Covers low-level quad collection and pooled helper paths that are not reached by the public collection tests.
/// </summary>
public class QuadCollectionCoverageTests
{
    /// <summary>
    /// Verifies QuadList indexing, resizing, removal, copy, and enumerator wrapper behavior.
    /// </summary>
    [Test]
    public void QuadList_ShouldSupportMutationAndEnumerationPaths()
    {
        using var list = new QuadList<int>();

        list.AddRange(ReadOnlySpan<int>.Empty);
        Enumerable.Range(0, 40).ToList().ForEach(list.Add);

        list.Count.Should().Be(40);
        list[3].Should().Be(3);

        list[3] = 300;
        list[3].Should().Be(300);
        list.Contains(300).Should().BeTrue();
        list.IndexOf(300).Should().Be(3);

        list.Remove(300).Should().BeTrue();
        list.Remove(999).Should().BeFalse();
        list.RemoveAt(list.Count - 1);

        var copied = new int[list.Count + 2];
        list.CopyTo(copied, 1);
        copied[1].Should().Be(0);

        var structEnumerator = list.GetEnumerator();
        structEnumerator.MoveNext().Should().BeTrue();
        structEnumerator.Current.Should().Be(0);
        while (structEnumerator.MoveNext())
        {
        }

        structEnumerator.MoveNext().Should().BeFalse();

        using var enumerator = ((IEnumerable<int>)list).GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be(0);
        enumerator.Reset();
        enumerator.MoveNext().Should().BeTrue();
        ((IEnumerator)enumerator).Current.Should().Be(0);

        var nonGenericEnumerator = ((IEnumerable)list).GetEnumerator();
        nonGenericEnumerator.MoveNext().Should().BeTrue();
        nonGenericEnumerator.Current.Should().Be(0);

        list.AsSpan().ToArray().Should().Contain(38);
        list.AsSpan().ToArray().Should().NotContain(39);
        list.Clear();
        list.Count.Should().Be(0);
    }

    /// <summary>
    /// Verifies QuadList guard clauses for invalid indexes.
    /// </summary>
    [Test]
    public void QuadList_InvalidIndexes_ShouldThrow()
    {
        using var list = new QuadList<string>();
        list.Add("first");

        Action getNegative = () => _ = list[-1];
        Action getTooHigh = () => _ = list[1];
        Action setTooHigh = () => list[1] = "missing";
        Action removeTooHigh = () => list.RemoveAt(1);

        getNegative.Should().Throw<IndexOutOfRangeException>();
        getTooHigh.Should().Throw<IndexOutOfRangeException>();
        setTooHigh.Should().Throw<IndexOutOfRangeException>();
        removeTooHigh.Should().Throw<IndexOutOfRangeException>();
    }

    /// <summary>
    /// Verifies QuadDictionary collision handling, ref updates, free-list reuse, and wrapper enumeration.
    /// </summary>
    [Test]
    public void QuadDictionary_ShouldHandleCollisionsAndRemovedSlots()
    {
        using var dictionary = new QuadDictionary<string, int>(new ConstantHashStringComparer());

        dictionary.TryAdd("one", 1).Should().BeTrue();
        dictionary.TryAdd("two", 2).Should().BeTrue();
        dictionary.TryAdd("three", 3).Should().BeTrue();
        dictionary.TryAdd("two", 22).Should().BeFalse();
        dictionary.Count.Should().Be(3);

        dictionary.Remove("two", out var removedMiddle).Should().BeTrue();
        removedMiddle.Should().Be(2);
        dictionary.Remove("three").Should().BeTrue();
        dictionary.Remove("missing", out var missingValue).Should().BeFalse();
        missingValue.Should().Be(default(int));

        dictionary.TryAdd("four", 4).Should().BeTrue();
        dictionary["one"].Should().Be(1);
        dictionary["one"] = 10;
        dictionary["one"].Should().Be(10);

        ref var valueRef = ref dictionary.GetValueRefOrAddDefault("five", out var existed);
        existed.Should().BeFalse();
        valueRef = 5;

        ref var existingRef = ref dictionary.GetValueRefOrAddDefault("five", out existed);
        existed.Should().BeTrue();
        existingRef.Should().Be(5);

        dictionary.GetKeys().Should().BeEquivalentTo(new[] { "one", "four", "five" });
        dictionary.GetValues().Should().BeEquivalentTo(new[] { 10, 4, 5 });

        var copied = new List<KeyValuePair<string, int>>();
        dictionary.CopyTo(copied);
        copied.Should().BeEquivalentTo(dictionary.ToArray());

        var structEnumerator = dictionary.GetEnumerator();
        structEnumerator.TryGetNext(out var first).Should().BeTrue();
        first.Key.Should().NotBeNull();
        while (structEnumerator.MoveNext())
        {
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
    }

    /// <summary>
    /// Verifies QuadDictionary duplicate, missing-key, capacity, resize, and clear behavior.
    /// </summary>
    [Test]
    public void QuadDictionary_ShouldCoverGuardsCapacityAndClear()
    {
        using var dictionary = new QuadDictionary<int, string>();

        dictionary.EnsureCapacity(8);
        dictionary.EnsureCapacity(128);

        Enumerable.Range(0, 120).ToList().ForEach(i => dictionary.Add(i, $"value-{i}"));

        dictionary.Count.Should().Be(120);
        dictionary.ContainsKey(42).Should().BeTrue();
        dictionary.TryGetValue(999, out var missing).Should().BeFalse();
        missing.Should().BeNull();

        Action duplicateAdd = () => dictionary.Add(42, "duplicate");
        Action missingIndexer = () => _ = dictionary[999];
        Action nullCopyTarget = () => dictionary.CopyTo(null!);

        duplicateAdd.Should().Throw<ArgumentException>();
        missingIndexer.Should().Throw<KeyNotFoundException>();
        nullCopyTarget.Should().Throw<ArgumentNullException>();

        dictionary.Clear();
        dictionary.Count.Should().Be(0);
        dictionary.Clear();
    }

    /// <summary>
    /// Verifies pooled batch change tracking including growth and reset on disposal.
    /// </summary>
    [Test]
    public void BatchChangeTracker_ShouldTrackGrowAndDispose()
    {
        var tracker = default(BatchChangeTracker<string>);

        for (var i = 0; i < 24; i++)
        {
            tracker.TrackAdded($"added-{i}");
        }

        for (var i = 0; i < 20; i++)
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

    /// <summary>
    /// Verifies ChangeToken value storage and change detection.
    /// </summary>
    [Test]
    public void ChangeToken_ShouldReportVersionChanges()
    {
        var token = new ChangeToken<int>(version: 7, count: 3);

        token.Version.Should().Be(7);
        token.Count.Should().Be(3);
        token.HasChanged(7).Should().BeFalse();
        token.HasChanged(8).Should().BeTrue();
        token.Should().Be(new ChangeToken<int>(7, 3));
    }

    /// <summary>
    /// Verifies PooledBuffer list copying and idempotent disposal.
    /// </summary>
    [Test]
    public void PooledBuffer_FromList_ShouldExposeCopiedSpanAndDispose()
    {
        var source = new List<string> { "alpha", "beta", "gamma" };
        using var buffer = PooledBuffer<string>.FromList(source);

        buffer.Span.ToArray().Should().Equal(source);

        buffer.Dispose();
        buffer.Dispose();
    }

    /// <summary>
    /// Verifies ValueBuffer stack, rent, growth, and disposal paths.
    /// </summary>
    [Test]
    public void ValueBuffer_ShouldUseStackThenRentedStorage()
    {
        Span<int> stack = stackalloc int[2];
        var buffer = new ValueBuffer<int>(in stack);

        buffer.Add(1);
        buffer.Add(2);
        buffer.Span.ToArray().Should().Equal(1, 2);

        buffer.Add(3);
        for (var i = 4; i <= 40; i++)
        {
            buffer.Add(i);
        }

        buffer.Count.Should().Be(40);
        buffer.Span.ToArray().Should().Equal(Enumerable.Range(1, 40));

        buffer.Dispose();
        buffer.Dispose();
    }

    /// <summary>
    /// Verifies shard hashing produces stable in-range shard indexes for null, positive, and negative hash codes.
    /// </summary>
    [Test]
    public void ShardHash_ShouldReturnExpectedShardRanges()
    {
        ShardHash.GetShardIndex<string?>(null, 4).Should().Be(0);
        ShardHash.GetShardIndex4<string?>(null).Should().Be(0);

        var positive = new FixedHash(1);
        var negative = new FixedHash(int.MinValue);

        ShardHash.GetShardIndex(positive, 8).Should().BeInRange(0, 7);
        ShardHash.GetShardIndex(negative, 16).Should().BeInRange(0, 15);
        ShardHash.GetShardIndex4(positive).Should().Be(ShardHash.GetShardIndex(positive, 4));
        ShardHash.GetShardIndex4(negative).Should().BeInRange(0, 3);
    }

    private sealed class ConstantHashStringComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => StringComparer.Ordinal.Equals(x, y);

        public int GetHashCode(string obj) => 17;
    }

    private sealed class FixedHash
    {
        private readonly int _hashCode;

        public FixedHash(int hashCode) => _hashCode = hashCode;

        public override int GetHashCode() => _hashCode;
    }
}
#endif
