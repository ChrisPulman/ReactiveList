// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>
/// Additional coverage tests for <see cref="ReactiveList{T}"/>.
/// </summary>
public class ReactiveListCoverageTests
{
    /// <summary>
    /// Reactive observables and collection metadata should reflect list changes.
    /// </summary>
    [Test]
    public void ObservablePropertiesAndMetadata_ShouldReflectChanges()
    {
        ReactiveList<string> fixture = [];
        var changed = new List<string[]>();
        var current = new List<string[]>();
        var removed = new List<string[]>();

        using var changedSubscription = fixture.Changed.Subscribe(items => changed.Add(items.ToArray()));
        using var currentSubscription = fixture.CurrentItems.Subscribe(items => current.Add(items.ToArray()));
        using var removedSubscription = fixture.Removed.Subscribe(items => removed.Add(items.ToArray()));

        fixture.IsDisposed.Should().BeFalse();
        fixture.IsFixedSize.Should().BeFalse();
        fixture.IsReadOnly.Should().BeFalse();
        fixture.IsSynchronized.Should().BeFalse();
        fixture.SyncRoot.Should().BeSameAs(fixture);

        fixture.Add("one");
        fixture.Update("one", "uno");
        fixture.Remove("uno");

        changed.SelectMany(items => items).Should().Contain(["one", "uno"]);
        current.Should().NotBeEmpty();
        current[current.Count - 1].Should().BeEmpty();
        removed.Should().ContainSingle()
            .Which.Should().Equal("uno");
    }

    /// <summary>
    /// Explicit non-generic collection APIs should validate and mutate consistently.
    /// </summary>
    [Test]
    public void NonGenericCollectionMembers_ShouldValidateAndMutate()
    {
        ReactiveList<string> fixture = ["one", "two"];
        var list = (IList)fixture;
        var collection = (ICollection)fixture;

        list[0].Should().Be("one");
        list[0] = "zero";
        fixture[0].Should().Be("zero");

        list.Add("three").Should().Be(2);
        list.Insert(1, "inserted");
        list.Contains("two").Should().BeTrue();
        list.Contains(42).Should().BeFalse();
        list.IndexOf("three").Should().Be(3);
        list.IndexOf(42).Should().Be(-1);
        list.Remove("inserted");
        list.Remove(42);

        var objects = new object[fixture.Count];
        collection.CopyTo(objects, 0);
        objects.Should().Equal("zero", "two", "three");

        var typed = new string[fixture.Count];
        collection.CopyTo(typed, 0);
        typed.Should().Equal("zero", "two", "three");

        Action addWrongType = () => list.Add(42);
        Action insertWrongType = () => list.Insert(0, 42);
        Action copyNull = () => collection.CopyTo(null!, 0);
        Action copyMultiDimensional = () => collection.CopyTo(Array.CreateInstance(typeof(string), 1, 1), 0);
        Action copyNonZeroLowerBound = () => collection.CopyTo(Array.CreateInstance(typeof(string), [3], [1]), 0);
        Action copyNegativeIndex = () => collection.CopyTo(new string[3], -1);
        Action copyTooSmall = () => collection.CopyTo(new string[2], 0);
        Action copyInvalidArrayType = () => ((ICollection)new ReactiveList<int>([1])).CopyTo(new string[1], 0);

        addWrongType.Should().Throw<InvalidCastException>();
        insertWrongType.Should().Throw<InvalidCastException>();
        copyNull.Should().Throw<ArgumentNullException>()
            .WithParameterName("array");
        copyMultiDimensional.Should().Throw<ArgumentException>()
            .WithParameterName("array");
        copyNonZeroLowerBound.Should().Throw<ArgumentException>()
            .WithParameterName("array");
        copyNegativeIndex.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("index");
        copyTooSmall.Should().Throw<ArgumentException>()
            .WithParameterName("array");
        copyInvalidArrayType.Should().Throw<ArgumentException>();

        list.Clear();
        fixture.Count.Should().Be(0);
    }

    /// <summary>
    /// Generic explicit members and empty batch branches should be no-ops.
    /// </summary>
    [Test]
    public void GenericExplicitMembersAndEmptyBatches_ShouldBehaveConsistently()
    {
        ReactiveList<int> emptyFromEnumerable = new(Array.Empty<int>());
        ReactiveList<int> fixture = [1, 2, 3, 4];
        var genericCollection = (ICollection<int>)fixture;
        var genericList = (IList<int>)fixture;

        emptyFromEnumerable.Count.Should().Be(0);
        genericList.IndexOf(3).Should().Be(2);
        ((IList)fixture).IndexOf(null).Should().Be(-1);
        ((IList)fixture).Contains(null).Should().BeFalse();

        fixture.AddRange(Array.Empty<int>());
        fixture.InsertRange(2, Array.Empty<int>());
        fixture.Remove(Array.Empty<int>());
        fixture.RemoveRange(0, 0);

        fixture.Count.Should().Be(4);

        genericList.RemoveAt(0);
        ((IList)fixture).RemoveAt(0);
        fixture.Should().Equal(3, 4);

        genericCollection.Clear();
        fixture.Count.Should().Be(0);
    }

    /// <summary>
    /// Reactive2DList guard branches should validate outer indexes and null row values.
    /// </summary>
    [Test]
    public void Reactive2DList_Guards_ShouldValidateOuterIndexesAndNullRows()
    {
        Reactive2DList<string> grid = [["a"]];

        Action addManyBadOuter = () => grid.AddToInner(10, new[] { "b" });
        Action addSingleBadOuter = () => grid.AddToInner(-1, "b");
        Action insertNullItem = () => grid.Insert(0, (string)null!);

        addManyBadOuter.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("outerIndex");
        addSingleBadOuter.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("outerIndex");
        insertNullItem.Should().Throw<ArgumentNullException>()
            .WithParameterName("item");
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Span and memory helpers should copy snapshots and validate destination size.
    /// </summary>
    [Test]
    public void SpanAndMemoryHelpers_ShouldCopySnapshotsAndValidateDestination()
    {
        ReactiveList<int> fixture = [1, 2, 3];

        fixture.ToArray().Should().Equal(1, 2, 3);
        fixture.AsSpan().ToArray().Should().Equal(1, 2, 3);
        fixture.AsMemory().ToArray().Should().Equal(1, 2, 3);

        var destination = new int[3];
        fixture.CopyTo(destination.AsSpan());
        destination.Should().Equal(1, 2, 3);

        Action copyTooSmall = () => fixture.CopyTo(new int[2].AsSpan());
        copyTooSmall.Should().Throw<ArgumentException>()
            .WithParameterName("destination");

        fixture.AddRange(ReadOnlySpan<int>.Empty);
        fixture.Count.Should().Be(3);

        int[] values = [4, 5];
        fixture.AddRange(values.AsSpan());
        fixture.Should().Equal(1, 2, 3, 4, 5);
    }
#endif

#if NET6_0_OR_GREATER || NETFRAMEWORK
    /// <summary>
    /// ClearWithoutDeallocation should support silent and notifying branches.
    /// </summary>
    [Test]
    public void ClearWithoutDeallocation_ShouldSupportSilentAndNotifyingBranches()
    {
        ReactiveList<int> fixture = [];
        var propertyNames = new List<string?>();
        fixture.PropertyChanged += (sender, args) => propertyNames.Add(args.PropertyName);

        fixture.ClearWithoutDeallocation(notifyChange: false);
        propertyNames.Should().BeEmpty();

        fixture.ClearWithoutDeallocation();
        propertyNames.Should().Equal(nameof(fixture.Count), "Item[]");

        fixture.AddRange([1, 2, 3]);
        propertyNames.Clear();
        fixture.ClearWithoutDeallocation(notifyChange: false);

        fixture.Count.Should().Be(0);
        fixture.Items.Should().BeEmpty();
        propertyNames.Should().BeEmpty();

        fixture.AddRange([4, 5]);
        fixture.ClearWithoutDeallocation();

        fixture.Count.Should().Be(0);
        fixture.ItemsRemoved.Should().Equal(4, 5);
        fixture.ItemsChanged.Should().Equal(4, 5);
    }
#endif

    /// <summary>
    /// Removal APIs should validate ranges and report only removed items.
    /// </summary>
    [Test]
    public void RemovalBranches_ShouldValidateRangesAndReportRemovedItems()
    {
        ReactiveList<int> fixture = [.. Enumerable.Range(0, 40)];
        var removed = new List<int[]>();
        using var subscription = fixture.Removed.Subscribe(items => removed.Add(items.ToArray()));

        fixture.Remove([1, 100, 3]);

        fixture.Count.Should().Be(38);
        removed.Should().ContainSingle()
            .Which.Should().Equal(1, 3);

        Action removeManyNull = () => fixture.RemoveMany(null!);
        Action removeAtInvalid = () => fixture.RemoveAt(-1);
        Action removeRangeBadIndex = () => fixture.RemoveRange(-1, 1);
        Action removeRangeBadCount = () => fixture.RemoveRange(0, fixture.Count + 1);

        removeManyNull.Should().Throw<ArgumentNullException>()
            .WithParameterName("predicate");
        removeAtInvalid.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("index");
        removeRangeBadIndex.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("index");
        removeRangeBadCount.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("count");

        fixture.RemoveRange(0, 2);
        var removedCount = fixture.RemoveMany(_ => true);

        removedCount.Should().Be(36);
        fixture.Count.Should().Be(0);
    }

    /// <summary>
    /// CollectionChanged should use specific actions for single changes and reset for batches.
    /// </summary>
    [Test]
    public void CollectionChanged_ShouldUseSpecificActionsForSingleChangesAndResetForBatches()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];
        var events = new List<NotifyCollectionChangedEventArgs>();
        fixture.CollectionChanged += (sender, args) => events.Add(args);

        fixture.Add("four");
        fixture.Remove("four");
        fixture.Move(0, 1);
        fixture.AddRange(["five", "six"]);

        events.Select(args => args.Action).Should().Equal(
            NotifyCollectionChangedAction.Add,
            NotifyCollectionChangedAction.Remove,
            NotifyCollectionChangedAction.Move,
            NotifyCollectionChangedAction.Reset);
        events[0].NewStartingIndex.Should().Be(3);
        events[1].OldStartingIndex.Should().Be(3);
        events[2].OldStartingIndex.Should().Be(0);
        events[2].NewStartingIndex.Should().Be(1);
    }

    /// <summary>
    /// ReplaceAll should emit old and new batches when either side is populated.
    /// </summary>
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
        fixture.ReplaceAll(Array.Empty<string>());

        fixture.Count.Should().Be(0);
        actions.Should().Equal(CacheAction.BatchAdded, CacheAction.BatchRemoved);
    }

    /// <summary>
    /// Subscribe should delegate to CurrentItems and Dispose should release resources.
    /// </summary>
    [Test]
    public void SubscribeAndDispose_ShouldUseCurrentItemsAndReleaseResources()
    {
        ReactiveList<int> fixture = [];
        var observer = new RecordingObserver<int>();
        using var subscription = fixture.Subscribe(observer);

        fixture.Add(10);
        observer.Snapshots.Should().HaveCountGreaterThanOrEqualTo(2);
        observer.Snapshots[observer.Snapshots.Count - 1].Should().Equal(10);

        fixture.Dispose();

        fixture.IsDisposed.Should().BeTrue();

        using var disposeHarness = new DisposeHarness<int>();
        disposeHarness.DisposeWithoutManagedResources();
        disposeHarness.IsDisposed.Should().BeFalse();
    }

    /// <summary>
    /// Private notification helpers should preserve stream and range collection behavior for otherwise unreachable no-op paths.
    /// </summary>
    [Test]
    public void InternalNotificationHelpers_ShouldHandleEmptyAndRefreshBranches()
    {
        ReactiveList<int> fixture = [];
        ReactiveList<int> deserializedFixture = [];
        var stream = new List<CacheNotify<int>>();
        ReactiveList<string> referenceFixture = [];
        var changed = new List<string[]>();

        using var streamSubscription = fixture.Stream.Subscribe(notification =>
        {
            stream.Add(notification);
            notification.Batch?.Dispose();
        });
        using var changedSubscription = referenceFixture.Changed.Subscribe(items => changed.Add(items.ToArray()));

        fixture.AddRange((IEnumerable<int>)Array.Empty<int>());

        Action setInvalidIndex = () => fixture[0] = 1;
        setInvalidIndex.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("index");

        InvokePrivate(deserializedFixture, "OnDeserialized", new StreamingContext());
        InvokePrivate(fixture, "OnPropertyChanged", "Custom");
        InvokePrivate(fixture, "NotifyCleared", Array.Empty<int>(), true);
        InvokePrivate(fixture, "NotifyCleared", Array.Empty<int>(), false);
        InvokePrivate(fixture, "NotifyAdded", 100, -1, false);
        InvokePrivate(fixture, "NotifyRemoved", 100, 0, false);
        InvokePrivate(fixture, "NotifyChangedSingle", 42, ChangeReason.Refresh, -1, -1, default(int));
        InvokePrivate(fixture, "NotifyChangedSingle", 43, (ChangeReason)999, -1, -1, default(int));
        InvokePrivate(referenceFixture, "EmitStream", CacheAction.Updated, null, null, -1, -1, null);

        var observableItems = GetPrivateField(fixture, "_observableItems");
        observableItems.GetType().GetMethod("AddRange")!.Invoke(observableItems, [Array.Empty<int>()]);
        observableItems.GetType().GetMethod("InsertRange")!.Invoke(observableItems, [0, Array.Empty<int>()]);
        observableItems.GetType().GetMethod("RemoveRange")!.Invoke(observableItems, [0, 0]);

        stream.Select(notification => notification.Action).Should().Contain(
            [CacheAction.Cleared, CacheAction.Refreshed]);
        changed.Any(static items => items.Length == 0).Should().BeTrue();
    }

    private sealed class DisposeHarness<T> : ReactiveList<T>
        where T : notnull
    {
        public void DisposeWithoutManagedResources() => Dispose(false);
    }

    private sealed class RecordingObserver<T> : IObserver<IEnumerable<T>>
    {
        public List<T[]> Snapshots { get; } = [];

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(IEnumerable<T> value) => Snapshots.Add(value.ToArray());
    }

    private static object GetPrivateField<T>(ReactiveList<T> target, string fieldName)
        where T : notnull =>
        typeof(ReactiveList<T>).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static object? InvokePrivate<T>(ReactiveList<T> target, string methodName, params object?[] args)
        where T : notnull =>
        typeof(ReactiveList<T>).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
}
