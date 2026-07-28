// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Additional coverage tests for <see cref="ReactiveList{T}"/>.</summary>
public class ReactiveListCoverageTests
{
    /// <summary>Reactive observables and collection metadata should reflect list changes.</summary>
    [Test]
    public void ObservablePropertiesAndMetadata_ShouldReflectChanges()
    {
        ReactiveList<string> fixture = [];
        var changed = new List<string[]>();
        var current = new List<string[]>();
        var removed = new List<string[]>();

        using var changedSubscription = fixture.Changed.Subscribe(items => AddSnapshot(changed, items));
        using var currentSubscription = fixture.CurrentItems.Subscribe(items => AddSnapshot(current, items));
        using var removedSubscription = fixture.Removed.Subscribe(items => AddSnapshot(removed, items));

        _ = fixture.IsDisposed.Should().BeFalse();
        _ = fixture.IsFixedSize.Should().BeFalse();
        _ = fixture.IsReadOnly.Should().BeFalse();
        _ = fixture.IsSynchronized.Should().BeFalse();
        _ = fixture.SyncRoot.Should().BeSameAs(fixture);

        fixture.Add("one");
        fixture.Update("one", "uno");
        _ = fixture.Remove("uno");

        var changedItems = new List<string>();
        foreach (var snapshot in changed)
        {
            changedItems.AddRange(snapshot);
        }

        _ = changedItems.Should().Contain(["one", "uno"]);
        _ = current.Should().NotBeEmpty();
        _ = current[current.Count - 1].Should().BeEmpty();
        _ = removed.Should().ContainSingle()
            .Which.Should().Equal("uno");
    }

    /// <summary>Explicit non-generic collection APIs should validate and mutate consistently.</summary>
    [Test]
    public void NonGenericCollectionMembers_ShouldValidateAndMutate()
    {
        ReactiveList<string> fixture = ["one", "two"];
        var list = (IList)fixture;

        _ = list[0].Should().Be("one");
        list[0] = "zero";
        _ = fixture[0].Should().Be("zero");

        _ = list.Add(TestData.ThreeText).Should().Be(TestData.TestValueTwo);
        list.Insert(1, "inserted");
        _ = list.Contains("two").Should().BeTrue();
        _ = list.Contains(TestData.TestValueFortyTwo).Should().BeFalse();
        _ = list.IndexOf(TestData.ThreeText).Should().Be(TestData.TestValueThree);
        _ = list.IndexOf(TestData.TestValueFortyTwo).Should().Be(-1);
        list.Remove("inserted");
        list.Remove(TestData.TestValueFortyTwo);

        var objects = new object[fixture.Count];
        ((ICollection)fixture).CopyTo(objects, 0);
        _ = objects.Should().Equal("zero", "two", TestData.ThreeText);

        var typed = new string[fixture.Count];
        ((ICollection)fixture).CopyTo(typed, 0);
        _ = typed.Should().Equal("zero", "two", TestData.ThreeText);

        Action addWrongType = () => list.Add(TestData.TestValueFortyTwo);
        Action insertWrongType = () => list.Insert(0, TestData.TestValueFortyTwo);
        Action copyNull = () => ((ICollection)fixture).CopyTo(null!, 0);
        Action copyMultiDimensional = () => ((ICollection)fixture).CopyTo(Array.CreateInstance(typeof(string), 1, 1), 0);
        Action copyNonZeroLowerBound = () => ((ICollection)fixture).CopyTo(Array.CreateInstance(typeof(string), [TestData.TestValueThree], [1]), 0);
        Action copyNegativeIndex = () => ((ICollection)fixture).CopyTo(new string[3], -1);
        Action copyTooSmall = () => ((ICollection)fixture).CopyTo(new string[2], 0);
        Action copyInvalidArrayType = static () =>
        {
            using var invalidFixture = new ReactiveList<int>([1]);
            ((ICollection)invalidFixture).CopyTo(new string[1], 0);
        };

        _ = addWrongType.Should().Throw<InvalidCastException>();
        _ = insertWrongType.Should().Throw<InvalidCastException>();
        _ = copyNull.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.ArrayParameterName);
        _ = copyMultiDimensional.Should().Throw<ArgumentException>()
            .WithParameterName(TestData.ArrayParameterName);
        _ = copyNonZeroLowerBound.Should().Throw<ArgumentException>()
            .WithParameterName(TestData.ArrayParameterName);
        _ = copyNegativeIndex.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.IndexParameterName);
        _ = copyTooSmall.Should().Throw<ArgumentException>()
            .WithParameterName(TestData.ArrayParameterName);
        _ = copyInvalidArrayType.Should().Throw<ArgumentException>();

        list.Clear();
        _ = fixture.Count.Should().Be(0);
    }

    /// <summary>Generic explicit members and empty batch branches should be no-ops.</summary>
    [Test]
    public void GenericExplicitMembersAndEmptyBatches_ShouldBehaveConsistently()
    {
        using ReactiveList<int> emptyFromEnumerable = new([]);
        ReactiveList<int> fixture = [1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour];
        var genericCollection = (ICollection<int>)fixture;
        var genericList = (IList<int>)fixture;

        _ = emptyFromEnumerable.Count.Should().Be(0);
        _ = genericList.IndexOf(TestData.TestValueThree).Should().Be(TestData.TestValueTwo);
        _ = ((IList)fixture).IndexOf(null).Should().Be(-1);
        _ = ((IList)fixture).Contains(null).Should().BeFalse();

        fixture.AddRange(Array.Empty<int>());
        fixture.InsertRange(TestData.TestValueTwo, []);
        fixture.Remove([]);
        fixture.RemoveRange(0, 0);

        _ = fixture.Count.Should().Be(TestData.TestValueFour);

        genericList.RemoveAt(0);
        ((IList)fixture).RemoveAt(0);
        _ = fixture.Should().Equal(TestData.TestValueThree, TestData.TestValueFour);

        genericCollection.Clear();
        _ = fixture.Count.Should().Be(0);
    }

    /// <summary>Reactive2DList guard branches should validate outer indexes and null row values.</summary>
    [Test]
    public void Reactive2DList_Guards_ShouldValidateOuterIndexesAndNullRows()
    {
        Reactive2DList<string> grid = [["a"]];

        Action addManyBadOuter = () => grid.AddToInner(TestData.TestValueTen, ["b"]);
        Action addSingleBadOuter = () => grid.AddToInner(-1, "b");
        Action insertNullItem = () => grid.Insert(0, (string)null!);

        _ = addManyBadOuter.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("outerIndex");
        _ = addSingleBadOuter.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("outerIndex");
        _ = insertNullItem.Should().Throw<ArgumentNullException>()
            .WithParameterName("item");
    }

#if NET6_0_OR_GREATER

    /// <summary>Span and memory helpers should copy snapshots and validate destination size.</summary>
    [Test]
    public void SpanAndMemoryHelpers_ShouldCopySnapshotsAndValidateDestination()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo, TestData.TestValueThree];

        _ = fixture.ToArray().Should().Equal(1, TestData.TestValueTwo, TestData.TestValueThree);
        _ = fixture.AsSpan().ToArray().Should().Equal(1, TestData.TestValueTwo, TestData.TestValueThree);
        _ = fixture.AsMemory().ToArray().Should().Equal(1, TestData.TestValueTwo, TestData.TestValueThree);

        var destination = new int[3];
        fixture.CopyTo(destination.AsSpan());
        _ = destination.Should().Equal(1, TestData.TestValueTwo, TestData.TestValueThree);

        Action copyTooSmall = () => fixture.CopyTo(new int[2].AsSpan());
        _ = copyTooSmall.Should().Throw<ArgumentException>()
            .WithParameterName("destination");

        fixture.AddRange(ReadOnlySpan<int>.Empty);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);

        int[] values = [TestData.TestValueFour, TestData.TestValueFive];
        fixture.AddRange(values.AsSpan());
        _ = fixture.Should().Equal(1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive);
    }
#endif

#if NET6_0_OR_GREATER || NETFRAMEWORK

    /// <summary>ClearWithoutDeallocation should support silent and notifying branches.</summary>
    [Test]
    public void ClearWithoutDeallocation_ShouldSupportSilentAndNotifyingBranches()
    {
        ReactiveList<int> fixture = [];
        var propertyNames = new List<string?>();
        fixture.PropertyChanged += (sender, args) => propertyNames.Add(args.PropertyName);

        fixture.ClearWithoutDeallocation(notifyChange: false);
        _ = propertyNames.Should().BeEmpty();

        fixture.ClearWithoutDeallocation();
        _ = propertyNames.Should().Equal(nameof(fixture.Count), "Item[]");

        fixture.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree]);
        propertyNames.Clear();
        fixture.ClearWithoutDeallocation(notifyChange: false);

        _ = fixture.Count.Should().Be(0);
        _ = fixture.Items.Should().BeEmpty();
        _ = propertyNames.Should().BeEmpty();

        fixture.AddRange([TestData.TestValueFour, TestData.TestValueFive]);
        fixture.ClearWithoutDeallocation();

        _ = fixture.Count.Should().Be(0);
        _ = fixture.ItemsRemoved.Should().Equal(TestData.TestValueFour, TestData.TestValueFive);
        _ = fixture.ItemsChanged.Should().Equal(TestData.TestValueFour, TestData.TestValueFive);
    }
#endif

    /// <summary>Removal APIs should validate ranges and report only removed items.</summary>
    [Test]
    public void RemovalBranches_ShouldValidateRangesAndReportRemovedItems()
    {
        ReactiveList<int> fixture = [.. Enumerable.Range(0, TestData.TestValueForty)];
        var removed = new List<int[]>();
        using var subscription = fixture.Removed.Subscribe(items => AddSnapshot(removed, items));

        fixture.Remove([1, TestData.TestValueOneHundred, TestData.TestValueThree]);

        _ = fixture.Count.Should().Be(TestData.TestValueThirtyEight);
        _ = removed.Should().ContainSingle()
            .Which.Should().Equal(1, TestData.TestValueThree);

        Action removeManyNull = () => fixture.RemoveMany(null!);
        Action removeAtInvalid = () => fixture.RemoveAt(-1);
        Action removeRangeBadIndex = () => fixture.RemoveRange(-1, 1);
        Action removeRangeBadCount = () => fixture.RemoveRange(0, fixture.Count + 1);

        _ = removeManyNull.Should().Throw<ArgumentNullException>()
            .WithParameterName("predicate");
        _ = removeAtInvalid.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.IndexParameterName);
        _ = removeRangeBadIndex.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.IndexParameterName);
        _ = removeRangeBadCount.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("count");

        fixture.RemoveRange(0, TestData.TestValueTwo);
        var removedCount = fixture.RemoveMany(static _ => true);

        _ = removedCount.Should().Be(TestData.TestValueThirtySix);
        _ = fixture.Count.Should().Be(0);
    }

    /// <summary>CollectionChanged should use specific actions for single changes and reset for batches.</summary>
    [Test]
    public void CollectionChanged_ShouldUseSpecificActionsForSingleChangesAndResetForBatches()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];
        var events = new List<NotifyCollectionChangedEventArgs>();
        fixture.CollectionChanged += (sender, args) => events.Add(args);

        fixture.Add("four");
        _ = fixture.Remove("four");
        fixture.Move(0, 1);
        fixture.AddRange(["five", "six"]);

        var actions = new List<NotifyCollectionChangedAction>(events.Count);
        foreach (var eventArgs in events)
        {
            actions.Add(eventArgs.Action);
        }

        _ = actions.Should().Equal(
            NotifyCollectionChangedAction.Add,
            NotifyCollectionChangedAction.Remove,
            NotifyCollectionChangedAction.Move,
            NotifyCollectionChangedAction.Reset);
        _ = events[0].NewStartingIndex.Should().Be(TestData.TestValueThree);
        _ = events[1].OldStartingIndex.Should().Be(TestData.TestValueThree);
        _ = events[TestData.TestValueTwo].OldStartingIndex.Should().Be(0);
        _ = events[TestData.TestValueTwo].NewStartingIndex.Should().Be(1);
    }

    /// <summary>ReplaceAll should emit old and new batches when either side is populated.</summary>
    [Test]
    public void ReplaceAll_ShouldEmitOldAndNewBatchesWhenPresent()
    {
        ReactiveList<string> fixture = [];
        var actions = new List<CacheAction>();
        using var subscription = fixture.Stream.Subscribe(notification =>
        {
            actions.Add(notification.Action);
            notification.Batch?.Dispose();
        });

        fixture.ReplaceAll(["one", "two"]);
        fixture.ReplaceAll([]);

        _ = fixture.Count.Should().Be(0);
        _ = actions.Should().Equal(CacheAction.BatchAdded, CacheAction.BatchRemoved);
    }

    /// <summary>Subscribe should delegate to CurrentItems and Dispose should release resources.</summary>
    [Test]
    public void SubscribeAndDispose_ShouldUseCurrentItemsAndReleaseResources()
    {
        ReactiveList<int> fixture = [];
        var observer = new RecordingObserver<int>();
        using var subscription = fixture.Subscribe(observer);

        fixture.Add(TestData.TestValueTen);
        _ = observer.Snapshots.Should().HaveCountGreaterThanOrEqualTo(TestData.TestValueTwo);
        _ = observer.Snapshots[observer.Snapshots.Count - 1].Should().Equal(TestData.TestValueTen);

        fixture.Dispose();

        _ = fixture.IsDisposed.Should().BeTrue();

        using var disposeHarness = new DisposeHarness<int>();
        disposeHarness.DisposeWithoutManagedResources();
        _ = disposeHarness.IsDisposed.Should().BeFalse();
    }

    /// <summary>Public notification paths should preserve stream behavior and handle empty batch no-ops.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NotificationPaths_ShouldHandleEmptyAndChangedBranches()
    {
        ReactiveList<int> fixture = [];
        var stream = new List<CacheNotify<int>>();
        var changed = new List<int[]>();

        using var changedSubscription = fixture.Changed.Subscribe(items => AddSnapshot(changed, items));
        using var streamSubscription = fixture.Stream.Subscribe(notification =>
        {
            stream.Add(notification);
            notification.Batch?.Dispose();
        });

        fixture.AddRange((IEnumerable<int>)Array.Empty<int>());

        Action setInvalidIndex = () => fixture[0] = 1;
        _ = setInvalidIndex.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.IndexParameterName);

        await TUnit.Assertions.Assert.That(stream.Count).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(changed.Count).IsEqualTo(0);

        fixture.Add(TestData.TestValueFortyTwo);
        fixture[0] = TestData.TestValueFortyThree;
        fixture.Clear();

        var actions = new List<CacheAction>(stream.Count);
        foreach (var notification in stream)
        {
            actions.Add(notification.Action);
        }

        await TUnit.Assertions.Assert.That(actions.Count).IsEqualTo(TestData.TestValueThree);
        await TUnit.Assertions.Assert.That(actions[0]).IsEqualTo(CacheAction.Added);
        await TUnit.Assertions.Assert.That(actions[1]).IsEqualTo(CacheAction.Updated);
        await TUnit.Assertions.Assert.That(actions[TestData.TestValueTwo]).IsEqualTo(CacheAction.Cleared);
        await TUnit.Assertions.Assert.That(changed.Count).IsEqualTo(TestData.TestValueThree);
        await TUnit.Assertions.Assert.That(changed[0][0]).IsEqualTo(TestData.TestValueFortyTwo);
        await TUnit.Assertions.Assert.That(changed[1][0]).IsEqualTo(TestData.TestValueFortyThree);
        await TUnit.Assertions.Assert.That(changed[TestData.TestValueTwo][0]).IsEqualTo(TestData.TestValueFortyThree);
    }

    /// <summary>Adds an explicit snapshot without LINQ allocation overhead.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="snapshots">The destination snapshot collection.</param>
    /// <param name="items">The items to snapshot.</param>
    private static void AddSnapshot<T>(List<T[]> snapshots, IEnumerable<T> items)
    {
        var snapshot = new List<T>(items);
        snapshots.Add([.. snapshot]);
    }

    /// <summary>Provides DisposeHarness.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    private sealed class DisposeHarness<T> : ReactiveList<T>
        where T : notnull
    {
        /// <summary>Provides DisposeWithoutManagedResources.</summary>
        public void DisposeWithoutManagedResources() => Dispose(false);
    }

    /// <summary>Provides RecordingObserver.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    private sealed class RecordingObserver<T> : IObserver<IEnumerable<T>>
    {
        /// <summary>Gets Snapshots.</summary>
        public List<T[]> Snapshots { get; } = [];

        /// <summary>Provides OnCompleted.</summary>
        public void OnCompleted()
        {
        }

        /// <summary>Provides OnError.</summary>
        /// <param name="error">The error value.</param>
        public void OnError(Exception error)
        {
        }

        /// <summary>Provides OnNext.</summary>
        /// <param name="value">The value.</param>
        public void OnNext(IEnumerable<T> value) => AddSnapshot(Snapshots, value);
    }
}
