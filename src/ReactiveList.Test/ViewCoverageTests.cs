// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Views;
#else
using CP.Primitives.Collections;
using CP.Primitives.Core;
using CP.Primitives.Views;
#endif
using ReactiveList.Test;
using TUnit.Assertions;
using TUnit.Core;
using TestConstants = ReactiveList.Test.TestData;

namespace ReactiveList.Tests;

/// <summary>Coverage tests for reactive view implementations.</summary>
public class ViewCoverageTests
{
    /// <summary>Initial values used by filtered-view transition tests.</summary>
    private static readonly int[] FilteredInitialItems =
    [
        TestConstants.TestValueTwo,
        TestConstants.TestValueThree,
        TestConstants.TestValueFour
    ];

    /// <summary>Initial values used by sorted-view transition tests.</summary>
    private static readonly int[] SortedInitialItems = [TestConstants.TestValueThree, 1];

    /// <summary>Initial values used by dynamic filtered-view transition tests.</summary>
    private static readonly int[] DynamicFilteredInitialItems =
    [
        1,
        TestConstants.TestValueTwo,
        TestConstants.TestValueThree
    ];

    /// <summary>Filtered views should track update transitions and refreshes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task FilteredReactiveView_UpdateTransitions_ShouldAddRemoveReplaceAndRefresh()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(FilteredInitialItems);

        using var view = new FilteredReactiveView<int>(
            list,
            static item => item % TestConstants.TestValueTwo == 0,
            Sequencer.Immediate,
            TimeSpan.Zero);

        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueTwo, TestConstants.TestValueFour], CollectionOrdering.Matching);
        await Assert.That(view[0]).IsEqualTo(TestConstants.TestValueTwo);
        await Assert.That(((IEnumerable)view).GetEnumerator().MoveNext()).IsTrue();
        var filteredProperties = new List<string?>();
        object? filteredPropertySender = null;
        view.PropertyChanged += (sender, args) =>
        {
            filteredPropertySender = sender;
            filteredProperties.Add(args.PropertyName);
        };

        list.Update(TestConstants.TestValueTwo, TestConstants.TestValueFive);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueFour], CollectionOrdering.Matching);

        list.Update(TestConstants.TestValueThree, TestConstants.TestValueSix);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueFour, TestConstants.TestValueSix], CollectionOrdering.Matching);

        list.Update(TestConstants.TestValueFour, TestConstants.TestValueEight);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueEight, TestConstants.TestValueSix], CollectionOrdering.Matching);

        list.Move(TestConstants.TestValueTwo, 0);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueEight, TestConstants.TestValueSix], CollectionOrdering.Matching);

        view.Refresh();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueEight, TestConstants.TestValueSix], CollectionOrdering.Matching);

        _ = list.Remove(TestConstants.TestValueSix);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueEight], CollectionOrdering.Matching);

        list.Clear();
        await WaitForPipeline();
        await Assert.That(view.Items).IsEmpty();
        await Assert.That(filteredProperties).Contains(nameof(view.Count));
        await Assert.That(filteredPropertySender).IsSameReferenceAs(view);
    }

    /// <summary>Sorted views should maintain comparer order through source changes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SortedReactiveView_Changes_ShouldKeepItemsSorted()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(SortedInitialItems);

        using var view = new SortedReactiveView<int>(
            list,
            Comparer<int>.Default,
            Sequencer.Immediate,
            TimeSpan.Zero);

        await Assert.That(view.Items).IsEquivalentTo([1, TestConstants.TestValueThree], CollectionOrdering.Matching);
        await Assert.That(view[1]).IsEqualTo(TestConstants.TestValueThree);
        await Assert.That(((IEnumerable)view).GetEnumerator().MoveNext()).IsTrue();
        var sortedCollectionNotifications = 0;
        object? sortedCollectionSender = null;
        NotifyCollectionChangedEventHandler sortedCollectionHandler = (sender, _) =>
        {
            sortedCollectionSender = sender;
            sortedCollectionNotifications++;
        };
        view.CollectionChanged += sortedCollectionHandler;
        view.CollectionChanged += sortedCollectionHandler;
        view.CollectionChanged -= sortedCollectionHandler;

        list.Add(TestConstants.TestValueTwo);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([1, TestConstants.TestValueTwo, TestConstants.TestValueThree], CollectionOrdering.Matching);
        await Assert.That(sortedCollectionNotifications).IsEqualTo(1);
        await Assert.That(sortedCollectionSender).IsSameReferenceAs(view);
        view.CollectionChanged -= sortedCollectionHandler;

        list.Add(TestConstants.TestValueTwo);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([1, TestConstants.TestValueTwo, TestConstants.TestValueTwo, TestConstants.TestValueThree], CollectionOrdering.Matching);
        await Assert.That(sortedCollectionNotifications).IsEqualTo(1);

        list.Update(TestConstants.TestValueThree, 0);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([0, 1, TestConstants.TestValueTwo, TestConstants.TestValueTwo], CollectionOrdering.Matching);

        list.Move(0, TestConstants.TestValueTwo);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([0, 1, TestConstants.TestValueTwo, TestConstants.TestValueTwo], CollectionOrdering.Matching);

        _ = list.Remove(1);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([0, TestConstants.TestValueTwo, TestConstants.TestValueTwo], CollectionOrdering.Matching);

        list.Clear();
        await WaitForPipeline();
        await Assert.That(view.Items).IsEmpty();

        list.AddRange([0, TestConstants.TestValueTwo, TestConstants.TestValueTwo]);
        await WaitForPipeline();
        view.Refresh();
        await Assert.That(view.Items).IsEquivalentTo([0, TestConstants.TestValueTwo, TestConstants.TestValueTwo], CollectionOrdering.Matching);
    }

    /// <summary>Grouped views should expose dictionary members and update group membership.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task GroupedReactiveView_DictionarySurfaceAndUpdates_ShouldTrackGroups()
    {
        using var list = new ReactiveList<ViewItem>();
        var north = new ViewItem(1, TestConstants.NorthRegion);
        var south = new ViewItem(TestConstants.TestValueTwo, TestConstants.SouthRegion);
        list.AddRange(new[] { north, south });

        using var view = new GroupedReactiveView<ViewItem, string>(
            list,
            static item => item.Region,
            Sequencer.Immediate,
            TimeSpan.Zero);
        object? groupedCollectionSender = null;
        object? groupedPropertySender = null;
        view.CollectionChanged += (sender, _) => groupedCollectionSender = sender;
        view.PropertyChanged += (sender, _) => groupedPropertySender = sender;

        await Assert.That(view.Keys).IsEquivalentTo([TestConstants.NorthRegion, TestConstants.SouthRegion]);
        await Assert.That(FlattenGroups(view.Values)).IsEquivalentTo([north, south]);
        await Assert.That(await Assert.That(view[TestConstants.NorthRegion]).HasSingleItem()).IsEqualTo(north);
        await Assert.That(view.TryGetValue(TestConstants.NorthRegion, out var northGroup)).IsTrue();
        await Assert.That(await Assert.That(northGroup).HasSingleItem()).IsEqualTo(north);
        await Assert.That(view.TryGetValue(TestConstants.MissingKey, out var missing)).IsFalse();
        await Assert.That(missing).IsEmpty();
        await Assert.That(CountEntries((IEnumerable)view)).IsEqualTo(TestConstants.TestValueTwo);
        await Assert.That(((IEnumerable)view).GetEnumerator().MoveNext()).IsTrue();
        view.Refresh();

        var changedScore = north with { Score = TestConstants.TestValueTen };
        list.Update(north, changedScore);
        await WaitForPipeline();
        await Assert.That(await Assert.That(view[TestConstants.NorthRegion]).HasSingleItem()).IsEqualTo(changedScore);

        var movedRegion = changedScore with { Region = TestConstants.SouthRegion };
        list.Update(changedScore, movedRegion);
        await WaitForPipeline();
        await Assert.That(view.ContainsKey(TestConstants.NorthRegion)).IsFalse();
        await Assert.That(view[TestConstants.SouthRegion]).IsEquivalentTo([south, movedRegion]);

        _ = list.Remove(south);
        await WaitForPipeline();
        await Assert.That(await Assert.That(view[TestConstants.SouthRegion]).HasSingleItem()).IsEqualTo(movedRegion);

        _ = list.Remove(movedRegion);
        await WaitForPipeline();
        await Assert.That(view).IsEmpty();

        list.Add(north);
        await WaitForPipeline();
        list.Clear();
        await WaitForPipeline();
        await Assert.That(view).IsEmpty();

        var west = new ViewItem(TestConstants.TestValueThree, "west");
        list.Add(west);
        await WaitForPipeline();
        await Assert.That(view.ContainsKey("west")).IsTrue();

        list.Clear();
        await WaitForPipeline();
        await Assert.That(view).IsEmpty();

        list.Add(north);
        await WaitForPipeline();
        view.Refresh();
        await Assert.That(view.ContainsKey(TestConstants.NorthRegion)).IsTrue();
        await Assert.That(groupedCollectionSender).IsSameReferenceAs(view);
        await Assert.That(groupedPropertySender).IsSameReferenceAs(view);
    }

    /// <summary>Dynamic filtered views should rebuild on filter changes and track source changes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DynamicFilteredReactiveView_FilterAndSourceChanges_ShouldRebuildAndTrackTransitions()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(DynamicFilteredInitialItems);
        using var filters = new BehaviorSignal<Func<int, bool>>(static item => item >= TestConstants.TestValueTwo);

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filters,
            Sequencer.Immediate,
            TimeSpan.Zero);

        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueTwo, TestConstants.TestValueThree], CollectionOrdering.Matching);
        await Assert.That(view[0]).IsEqualTo(TestConstants.TestValueTwo);
        await Assert.That(((IEnumerable)view).GetEnumerator().MoveNext()).IsTrue();
        var dynamicFilteredProperties = new List<string?>();
        view.PropertyChanged += (_, args) => dynamicFilteredProperties.Add(args.PropertyName);

        filters.OnNext(null!);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([1, TestConstants.TestValueTwo, TestConstants.TestValueThree], CollectionOrdering.Matching);

        filters.OnNext(static item => item % TestConstants.TestValueTwo == 0);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueTwo], CollectionOrdering.Matching);

        list.Add(TestConstants.TestValueFour);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueTwo, TestConstants.TestValueFour], CollectionOrdering.Matching);

        list.Update(TestConstants.TestValueTwo, TestConstants.TestValueFive);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueFour], CollectionOrdering.Matching);

        list.Update(1, TestConstants.TestValueSix);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueFour, TestConstants.TestValueSix], CollectionOrdering.Matching);

        view.Refresh();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueSix, TestConstants.TestValueFour], CollectionOrdering.Matching);

        list.Update(TestConstants.TestValueFour, TestConstants.TestValueEight);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueSix, TestConstants.TestValueEight], CollectionOrdering.Matching);

        _ = list.Remove(TestConstants.TestValueSix);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueEight], CollectionOrdering.Matching);

        list.Move(TestConstants.TestValueTwo, 0);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueEight], CollectionOrdering.Matching);

        list.Clear();
        await WaitForPipeline();
        await Assert.That(view.Items).IsEmpty();
        await Assert.That(dynamicFilteredProperties).Contains(nameof(view.Count));
    }

    /// <summary>Dynamic reactive views should apply single and batch stream actions.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DynamicReactiveView_StreamActions_ShouldApplyCurrentFilterAndBatches()
    {
        using var source = new ReactiveSourceHarness<int>([1, TestConstants.TestValueTwo, TestConstants.TestValueThree]);
        using var filters = new BehaviorSignal<Func<int, bool>>(static item => item % TestConstants.TestValueTwo == 0);

        using var view = new DynamicReactiveView<int>(
            source,
            filters,
            TimeSpan.Zero,
            Sequencer.Immediate);
        var dynamicProperties = new List<string?>();
        view.PropertyChanged += (_, args) => dynamicProperties.Add(args.PropertyName);

        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueTwo], CollectionOrdering.Matching);

        source.AddItem(TestConstants.TestValueFour);
        source.Emit(new(CacheAction.Added, TestConstants.TestValueFour));
        source.AddItem(TestConstants.TestValueFive);
        source.Emit(new(CacheAction.Added, TestConstants.TestValueFive));
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueTwo, TestConstants.TestValueFour], CollectionOrdering.Matching);

        source.RemoveItem(TestConstants.TestValueTwo);
        source.Emit(new(CacheAction.Removed, TestConstants.TestValueTwo));
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueFour], CollectionOrdering.Matching);

        source.AddItems([TestConstants.TestValueSix, TestConstants.TestValueSeven]);
        source.Emit(new(CacheAction.BatchAdded, default, CreateBatch(TestConstants.TestValueSix, TestConstants.TestValueSeven)));
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueFour, TestConstants.TestValueSix], CollectionOrdering.Matching);

        source.RemoveItems([TestConstants.TestValueFour, TestConstants.TestValueSix]);
        source.Emit(new(CacheAction.BatchRemoved, default, CreateBatch(TestConstants.TestValueFour, TestConstants.TestValueSix)));
        await WaitForPipeline();
        await Assert.That(view.Items).IsEmpty();

        source.ClearItems();
        source.Emit(new(CacheAction.Cleared, default));
        await WaitForPipeline();
        await Assert.That(view.Items).IsEmpty();

        filters.OnNext(static _ => true);
        await WaitForPipeline();
        source.AddItem(TestConstants.TestValueNine);
        source.Emit(new(CacheAction.Added, TestConstants.TestValueNine));
        await WaitForPipeline();
        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueNine], CollectionOrdering.Matching);
        await Assert.That(dynamicProperties).Contains(nameof(view.Items));
    }

    /// <summary>Complex changes buffered with later additions should rebuild exactly once from the final source state.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DynamicReactiveView_BufferedUpdateAndAdd_ShouldNotDuplicateItems()
    {
        using var source = new ReactiveSourceHarness<int>([TestConstants.TestValueTwo]);
        using var filters = new BehaviorSignal<Func<int, bool>>(static _ => true);
        using var view = new DynamicReactiveView<int>(
            source,
            filters,
            TimeSpan.FromMilliseconds(TestConstants.TestValueTen),
            Sequencer.Immediate);
        var applied = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        view.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(view.Items))
            {
                return;
            }

            _ = applied.TrySetResult(true);
        };

        source.RemoveItem(TestConstants.TestValueTwo);
        source.AddItem(TestConstants.TestValueFour);
        source.Emit(new(
            CacheAction.Updated,
            TestConstants.TestValueFour,
            Previous: TestConstants.TestValueTwo));
        source.AddItem(TestConstants.TestValueSix);
        source.Emit(new(CacheAction.Added, TestConstants.TestValueSix));

        await applied.Task;

        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueFour, TestConstants.TestValueSix], CollectionOrdering.Matching);

        applied = new(TaskCreationOptions.RunContinuationsAsynchronously);
        source.ClearItems();
        source.AddItem(TestConstants.TestValueEight);
        source.Emit(new(CacheAction.BatchOperation, default));

        await applied.Task;

        await Assert.That(view.Items).IsEquivalentTo([TestConstants.TestValueEight], CollectionOrdering.Matching);
    }

    /// <summary>Dynamic reactive views should use the default include-all filter when null filters are emitted.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DynamicReactiveView_NullFilters_ShouldUseDefaultIncludeAllFilter()
    {
        using var source = new ReactiveSourceHarness<int>([1]);
        using var filters = new BehaviorSignal<Func<int, bool>>(null!);

        using var view = new DynamicReactiveView<int>(
            source,
            filters,
            TimeSpan.Zero,
            Sequencer.Immediate);

        await Assert.That(view.Items).IsEquivalentTo([1], CollectionOrdering.Matching);

        source.AddItem(TestConstants.TestValueTwo);
        filters.OnNext(null!);
        await WaitForPipeline();

        await Assert.That(view.Items).IsEquivalentTo([1, TestConstants.TestValueTwo], CollectionOrdering.Matching);
    }

#if NET8_0_OR_GREATER || NETFRAMEWORK

    /// <summary>Secondary-index dictionary views should remove values when updates leave the index.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SecondaryIndexReactiveView_DictionaryUpdates_ShouldRemoveValuesThatLeaveTheIndex()
    {
        using var dictionary = new QuaternaryDictionary<int, ViewItem>();
        var north = new ViewItem(1, TestConstants.NorthRegion);
        dictionary.Add(1, north);
        dictionary.Add(TestConstants.TestValueTwo, new(TestConstants.TestValueTwo, TestConstants.SouthRegion));
        dictionary.AddValueIndex(TestConstants.RegionPropertyName, static item => item.Region);

        using var view = SecondaryIndexReactiveView<int, ViewItem>.Create(
            dictionary,
            TestConstants.RegionPropertyName,
            TestConstants.NorthRegion,
            Sequencer.Immediate,
            TimeSpan.Zero);

        await Assert.That(await Assert.That(view.Items).HasSingleItem()).IsEqualTo(north);
        await Assert.That(view.Count).IsEqualTo(1);
        await Assert.That(view[0]).IsEqualTo(north);
        await Assert.That(view.ToProperty(out var outCollection)).IsSameReferenceAs(view);
        await Assert.That(outCollection).IsSameReferenceAs(view.Items);
        object? capturedCollection = null;
        await Assert.That(view.ToProperty(collection => capturedCollection = collection)).IsSameReferenceAs(view);
        await Assert.That(capturedCollection).IsSameReferenceAs(view.Items);
        view.Refresh();
        await Assert.That(view.GetEnumerator().MoveNext()).IsTrue();
        await Assert.That(((IEnumerable)view).GetEnumerator().MoveNext()).IsTrue();
        var secondaryProperties = new List<string?>();
        view.PropertyChanged += (_, args) => secondaryProperties.Add(args.PropertyName);

        view.Refresh();
        await Assert.That(await Assert.That(view.Items).HasSingleItem()).IsEqualTo(north);

        dictionary.AddOrUpdate(1, north with { Region = TestConstants.SouthRegion });
        await WaitForPipeline();
        await Assert.That(view.Items).IsEmpty();

        var newNorth = new ViewItem(TestConstants.TestValueThree, TestConstants.NorthRegion);
        dictionary.AddOrUpdate(TestConstants.TestValueThree, newNorth);
        await WaitForPipeline();
        await Assert.That(await Assert.That(view.Items).HasSingleItem()).IsEqualTo(newNorth);

        _ = dictionary.Remove(TestConstants.TestValueThree);
        await WaitForPipeline();
        await Assert.That(view.Items).IsEmpty();

        dictionary.AddOrUpdate(TestConstants.TestValueFour, new(TestConstants.TestValueFour, TestConstants.NorthRegion));
        await WaitForPipeline();
        dictionary.Clear();
        await WaitForPipeline();
        await Assert.That(view.Items).IsEmpty();
        await Assert.That(secondaryProperties).Contains(nameof(view.Count));
    }

    /// <summary>Dynamic secondary-index views should track key changes and dictionary updates.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DynamicSecondaryIndexViews_KeyChangesAndDictionaryUpdates_ShouldTrackCurrentKeys()
    {
        using var list = new QuaternaryList<ViewItem>();
        var north = new ViewItem(1, TestConstants.NorthRegion);
        var south = new ViewItem(TestConstants.TestValueTwo, TestConstants.SouthRegion);
        list.Add(north);
        list.Add(south);
        list.AddIndex(TestConstants.RegionPropertyName, static item => item.Region);
        using var listKeys = new BehaviorSignal<string[]>([TestConstants.NorthRegion]);

        using var listView = new DynamicSecondaryIndexReactiveView<ViewItem, string>(
            list,
            TestConstants.RegionPropertyName,
            listKeys,
            Sequencer.Immediate,
            TimeSpan.Zero);

        var listViewProperties = new List<string?>();
        listView.PropertyChanged += (_, args) => listViewProperties.Add(args.PropertyName);
        await VerifyDynamicSecondaryListView(list, listView, listKeys, north, south, listViewProperties);

        using var dictionary = new QuaternaryDictionary<int, ViewItem> { { 1, north } };
        dictionary.Add(TestConstants.TestValueTwo, south);
        dictionary.AddValueIndex(TestConstants.RegionPropertyName, static item => item.Region);
        using var dictionaryKeys = new BehaviorSignal<string[]>([TestConstants.NorthRegion]);

        using var dictionaryView = DynamicSecondaryIndexDictionaryReactiveView<int, ViewItem>.Create(
            dictionary,
            TestConstants.RegionPropertyName,
            dictionaryKeys,
            Sequencer.Immediate,
            TimeSpan.Zero);

        var dictionaryViewProperties = new List<string?>();
        dictionaryView.PropertyChanged += (_, args) => dictionaryViewProperties.Add(args.PropertyName);
        await VerifyDynamicSecondaryDictionaryView(
            dictionary,
            dictionaryView,
            dictionaryKeys,
            north,
            dictionaryViewProperties);
    }

    /// <summary>Dynamic view constructors should ignore initial probe errors and keep default state.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DynamicViews_InitialProbeErrors_ShouldUseDefaultValues()
    {
        using var source = new ReactiveSourceHarness<int>([1]);
        using var dynamicView = new DynamicReactiveView<int>(
            source,
            new FirstSubscriptionErrorObservable<Func<int, bool>>(),
            TimeSpan.Zero,
            Sequencer.Immediate);

        await Assert.That(dynamicView.Items).IsEquivalentTo([1], CollectionOrdering.Matching);
        using var twoValueDynamicView = new DynamicReactiveView<int>(
            source,
            new TwoValueObservable<Func<int, bool>>(static item => item == 1, static _ => false),
            TimeSpan.Zero,
            Sequencer.Immediate);

        await Assert.That(twoValueDynamicView.Items).IsEmpty();

        using var list = new QuaternaryList<MutableViewItem> { new(TestConstants.NorthRegion) };
        list.AddIndex(TestConstants.RegionPropertyName, static item => item.Region);
        using var listView = new DynamicSecondaryIndexReactiveView<MutableViewItem, string>(
            list,
            TestConstants.RegionPropertyName,
            new FirstSubscriptionErrorObservable<string[]>(),
            Sequencer.Immediate,
            TimeSpan.Zero);

        await Assert.That(listView.Items).IsEmpty();
        using var twoValueListView = new DynamicSecondaryIndexReactiveView<MutableViewItem, string>(
            list,
            TestConstants.RegionPropertyName,
            new TwoValueObservable<string[]>([TestConstants.NorthRegion], [TestConstants.SouthRegion]),
            Sequencer.Immediate,
            TimeSpan.Zero);

        await Assert.That(twoValueListView.Items).IsEmpty();

        using var dictionary = new QuaternaryDictionary<int, MutableViewItem> { { 1, new MutableViewItem(TestConstants.NorthRegion) } };
        dictionary.AddValueIndex(TestConstants.RegionPropertyName, static item => item.Region);
        using var dictionaryView = DynamicSecondaryIndexDictionaryReactiveView<int, MutableViewItem>.Create(
            dictionary,
            TestConstants.RegionPropertyName,
            new FirstSubscriptionErrorObservable<string[]>(),
            Sequencer.Immediate,
            TimeSpan.Zero);

        await Assert.That(dictionaryView.Items).IsEmpty();
        using var twoValueDictionaryView = DynamicSecondaryIndexDictionaryReactiveView<int, MutableViewItem>.Create(
            dictionary,
            TestConstants.RegionPropertyName,
            new TwoValueObservable<string[]>([TestConstants.NorthRegion], [TestConstants.SouthRegion]),
            Sequencer.Immediate,
            TimeSpan.Zero);

        await Assert.That(twoValueDictionaryView.Items).IsEmpty();
    }

    /// <summary>Dynamic secondary-index views should handle mutable update transitions directly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DynamicSecondaryIndexViews_MutableUpdates_ShouldAddRemoveClearAndRebuild()
    {
        using var list = new QuaternaryList<MutableViewItem>();
        var listNorth = new MutableViewItem(TestConstants.NorthRegion);
        var listSouth = new MutableViewItem(TestConstants.SouthRegion);
        list.Add(listNorth);
        list.Add(listSouth);
        list.AddIndex(TestConstants.RegionPropertyName, static item => item.Region);
        using var listKeys = new BehaviorSignal<string[]>([TestConstants.NorthRegion]);
        using var listView = new DynamicSecondaryIndexReactiveView<MutableViewItem, string>(
            list,
            TestConstants.RegionPropertyName,
            listKeys,
            Sequencer.Immediate,
            TimeSpan.Zero);

        await Assert.That(await Assert.That(listView.Items).HasSingleItem()).IsSameReferenceAs(listNorth);
        await VerifyMutableSecondaryListView(list, listView, listNorth, listSouth);

        using var dictionary = new QuaternaryDictionary<int, MutableViewItem>();
        var dictionaryNorth = new MutableViewItem(TestConstants.NorthRegion);
        var dictionarySouth = new MutableViewItem(TestConstants.SouthRegion);
        dictionary.Add(1, dictionaryNorth);
        dictionary.Add(TestConstants.TestValueTwo, dictionarySouth);
        dictionary.AddValueIndex(TestConstants.RegionPropertyName, static item => item.Region);
        using var dictionaryKeys = new BehaviorSignal<string[]>([TestConstants.NorthRegion]);
        using var dictionaryView = DynamicSecondaryIndexDictionaryReactiveView<int, MutableViewItem>.Create(
            dictionary,
            TestConstants.RegionPropertyName,
            dictionaryKeys,
            Sequencer.Immediate,
            TimeSpan.Zero);

        await Assert.That((await Assert.That(dictionaryView.Items).HasSingleItem()).Value).IsSameReferenceAs(dictionaryNorth);
        await VerifyMutableSecondaryDictionaryView(
            dictionary,
            dictionaryView,
            dictionaryNorth,
            dictionarySouth);
    }

    /// <summary>Verifies public dictionary operations flow through a dynamic secondary-index view.</summary>
    /// <param name="dictionary">The source dictionary.</param>
    /// <param name="dictionaryView">The view under test.</param>
    /// <param name="dictionaryKeys">The selected secondary-index keys.</param>
    /// <param name="north">The initial matching item.</param>
    /// <param name="dictionaryViewProperties">The property notifications captured from the view.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous verification.</returns>
    private static async Task VerifyDynamicSecondaryDictionaryView(
        QuaternaryDictionary<int, ViewItem> dictionary,
        DynamicSecondaryIndexDictionaryReactiveView<int, ViewItem> dictionaryView,
        BehaviorSignal<string[]> dictionaryKeys,
        ViewItem north,
        List<string?> dictionaryViewProperties)
    {
        await Assert.That(await Assert.That(dictionaryView.Items).HasSingleItem()).IsEqualTo(new(1, north));
        await Assert.That(dictionaryView.Count).IsEqualTo(1);
        await Assert.That(dictionaryView[0].Key).IsEqualTo(1);
        await Assert.That(dictionaryView.ToProperty(out var dictionaryOutCollection)).IsSameReferenceAs(dictionaryView);
        await Assert.That(dictionaryOutCollection).IsSameReferenceAs(dictionaryView.Items);
        object? capturedCollection = null;
        await Assert.That(dictionaryView.ToProperty(collection => capturedCollection = collection)).IsSameReferenceAs(dictionaryView);
        await Assert.That(capturedCollection).IsSameReferenceAs(dictionaryView.Items);
        dictionaryView.Refresh();
        await Assert.That(dictionaryView.GetEnumerator().MoveNext()).IsTrue();
        await Assert.That(((IEnumerable)dictionaryView).GetEnumerator().MoveNext()).IsTrue();
        dictionary.AddOrUpdate(1, north with { Region = TestConstants.SouthRegion });
        await WaitForPipeline();
        await Assert.That(dictionaryView.Items).IsEmpty();

        dictionary.AddOrUpdate(TestConstants.TestValueFour, new(TestConstants.TestValueFour, TestConstants.NorthRegion));
        await WaitForPipeline();
        await Assert.That(GetKeys(dictionaryView.Items)).Contains(TestConstants.TestValueFour);

        dictionary.AddOrUpdate(TestConstants.TestValueFour, new(TestConstants.TestValueFour, TestConstants.NorthRegion, Score: 10));
        await WaitForPipeline();
        await Assert.That(FindByKey(dictionaryView.Items, TestConstants.TestValueFour).Value.Score).IsEqualTo(TestConstants.TestValueTen);

        dictionaryKeys.OnNext([TestConstants.SouthRegion]);
        await WaitForPipeline();
        await Assert.That(GetKeys(dictionaryView.Items)).IsEquivalentTo([1, TestConstants.TestValueTwo]);

        dictionary.AddOrUpdate(TestConstants.TestValueFive, new(TestConstants.TestValueFive, TestConstants.NorthRegion));
        await WaitForPipeline();
        dictionary.AddOrUpdate(TestConstants.TestValueFive, new(TestConstants.TestValueFive, TestConstants.SouthRegion));
        await WaitForPipeline();
        await Assert.That(GetKeys(dictionaryView.Items)).Contains(TestConstants.TestValueFive);

        var thirdSouth = new ViewItem(TestConstants.TestValueThree, TestConstants.SouthRegion);
        dictionary.AddOrUpdate(TestConstants.TestValueThree, thirdSouth);
        await WaitForPipeline();
        await Assert.That(GetKeys(dictionaryView.Items)).Contains(TestConstants.TestValueThree);

        _ = dictionary.Remove(1);
        await WaitForPipeline();
        await Assert.That(GetKeys(dictionaryView.Items)).DoesNotContain(1);

        dictionary.Clear();
        await WaitForPipeline();
        await Assert.That(dictionaryView.Items).IsEmpty();
        await Assert.That(dictionaryViewProperties).Contains(nameof(dictionaryView.Count));
    }

    /// <summary>Verifies public list operations flow through a dynamic secondary-index view.</summary>
    /// <param name="list">The source list.</param>
    /// <param name="listView">The view under test.</param>
    /// <param name="listKeys">The selected secondary-index keys.</param>
    /// <param name="north">The initially matching item.</param>
    /// <param name="south">The item selected after the key changes.</param>
    /// <param name="listViewProperties">The property notifications captured from the view.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous verification.</returns>
    private static async Task VerifyDynamicSecondaryListView(
        QuaternaryList<ViewItem> list,
        DynamicSecondaryIndexReactiveView<ViewItem, string> listView,
        BehaviorSignal<string[]> listKeys,
        ViewItem north,
        ViewItem south,
        List<string?> listViewProperties)
    {
        await Assert.That(await Assert.That(listView.Items).HasSingleItem()).IsEqualTo(north);
        await Assert.That(listView.Count).IsEqualTo(1);
        await Assert.That(listView[0]).IsEqualTo(north);
        await Assert.That(listView.ToProperty(out var listOutCollection)).IsSameReferenceAs(listView);
        await Assert.That(listOutCollection).IsSameReferenceAs(listView.Items);
        object? capturedCollection = null;
        await Assert.That(listView.ToProperty(collection => capturedCollection = collection)).IsSameReferenceAs(listView);
        await Assert.That(capturedCollection).IsSameReferenceAs(listView.Items);
        listView.Refresh();
        await Assert.That(listView.GetEnumerator().MoveNext()).IsTrue();
        await Assert.That(((IEnumerable)listView).GetEnumerator().MoveNext()).IsTrue();
        _ = list.Remove(north);
        await WaitForPipeline();
        await Assert.That(listView.Items).IsEmpty();
        list.Add(north);
        await WaitForPipeline();
        await Assert.That(await Assert.That(listView.Items).HasSingleItem()).IsEqualTo(north);
        listKeys.OnNext([TestConstants.SouthRegion]);
        await WaitForPipeline();
        await Assert.That(await Assert.That(listView.Items).HasSingleItem()).IsEqualTo(south);
        var secondSouth = new ViewItem(TestConstants.TestValueThree, TestConstants.SouthRegion);
        list.Add(secondSouth);
        await WaitForPipeline();
        await Assert.That(listView.Items).IsEquivalentTo([south, secondSouth]);
        list.ReplaceAll([north]);
        await WaitForPipeline();
        await Assert.That(listView.Items).IsEmpty();
        await Assert.That(listViewProperties).Contains(nameof(listView.Count));
    }

    /// <summary>Verifies mutable dictionary items through public update, remove, add, and clear operations.</summary>
    /// <param name="dictionary">The source dictionary.</param>
    /// <param name="dictionaryView">The dynamic view under test.</param>
    /// <param name="dictionaryNorth">The initially matching item.</param>
    /// <param name="dictionarySouth">The item mutated into the selected index.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous verification.</returns>
    private static async Task VerifyMutableSecondaryDictionaryView(
        QuaternaryDictionary<int, MutableViewItem> dictionary,
        DynamicSecondaryIndexDictionaryReactiveView<int, MutableViewItem> dictionaryView,
        MutableViewItem dictionaryNorth,
        MutableViewItem dictionarySouth)
    {
        dictionaryNorth.Region = TestConstants.SouthRegion;
        dictionary.AddOrUpdate(1, dictionaryNorth);
        await WaitForPipeline();
        await Assert.That(dictionaryView.Items).IsEmpty();

        dictionarySouth.Region = TestConstants.NorthRegion;
        dictionary.AddOrUpdate(TestConstants.TestValueTwo, dictionarySouth);
        await WaitForPipeline();
        await Assert.That((await Assert.That(dictionaryView.Items).HasSingleItem()).Value).IsSameReferenceAs(dictionarySouth);

        dictionarySouth.Score = TestConstants.TestValueTen;
        dictionary.AddOrUpdate(TestConstants.TestValueTwo, dictionarySouth);
        await WaitForPipeline();
        await Assert.That(dictionaryView.Items[0].Value.Score).IsEqualTo(TestConstants.TestValueTen);

        _ = dictionary.Remove(TestConstants.TestValueTwo);
        await WaitForPipeline();
        await Assert.That(dictionaryView.Items).IsEmpty();

        dictionary.AddOrUpdate(TestConstants.TestValueTwo, dictionarySouth);
        await WaitForPipeline();
        await TUnit.Assertions.Assert.That(dictionaryView.Items.Count).IsEqualTo(1);

        dictionary.Clear();
        await WaitForPipeline();
        await Assert.That(dictionaryView.Items).IsEmpty();

        using var nullableKeyDictionary = new QuaternaryDictionary<string, MutableViewItem> { { "north-1", new MutableViewItem(TestConstants.NorthRegion) }, };
        nullableKeyDictionary.AddValueIndex(TestConstants.RegionPropertyName, static item => item.Region);
        using var nullableKeyKeys = new BehaviorSignal<string[]>([TestConstants.NorthRegion]);
        using var nullableKeyView = DynamicSecondaryIndexDictionaryReactiveView<string, MutableViewItem>.Create(
            nullableKeyDictionary,
            TestConstants.RegionPropertyName,
            nullableKeyKeys,
            Sequencer.Immediate,
            TimeSpan.Zero);

        await TUnit.Assertions.Assert.That(nullableKeyDictionary.Remove(TestConstants.MissingKey)).IsFalse();
        await TUnit.Assertions.Assert.That(nullableKeyView.Items.Count).IsEqualTo(1);
    }

    /// <summary>Verifies mutable list items through public rebuild, remove, add, and clear operations.</summary>
    /// <param name="list">The source list.</param>
    /// <param name="listView">The dynamic view under test.</param>
    /// <param name="listNorth">The initially matching item.</param>
    /// <param name="listSouth">The item mutated into the selected index.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous verification.</returns>
    private static async Task VerifyMutableSecondaryListView(
        QuaternaryList<MutableViewItem> list,
        DynamicSecondaryIndexReactiveView<MutableViewItem, string> listView,
        MutableViewItem listNorth,
        MutableViewItem listSouth)
    {
        listNorth.Region = TestConstants.SouthRegion;
        list.ReplaceAll([listNorth, listSouth]);
        await WaitForPipeline();
        await Assert.That(listView.Items).IsEmpty();

        listSouth.Region = TestConstants.NorthRegion;
        list.ReplaceAll([listNorth, listSouth]);
        await WaitForPipeline();
        await Assert.That(await Assert.That(listView.Items).HasSingleItem()).IsSameReferenceAs(listSouth);

        _ = list.Remove(listSouth);
        await WaitForPipeline();
        await Assert.That(listView.Items).IsEmpty();

        list.Add(listSouth);
        await WaitForPipeline();
        await TUnit.Assertions.Assert.That(listView.Items.Count).IsEqualTo(1);

        list.Clear();
        await WaitForPipeline();
        await Assert.That(listView.Items).IsEmpty();
    }
#endif

    /// <summary>Provides WaitForPipeline.</summary>
    /// <returns>The result.</returns>
    private static Task WaitForPipeline() => Task.Delay(TestConstants.TestValueThirty);

    /// <summary>Counts entries exposed through a non-generic collection surface.</summary>
    /// <param name="items">The entries to count.</param>
    /// <returns>The number of exposed entries.</returns>
    private static int CountEntries(IEnumerable items)
    {
        var count = 0;
        foreach (var _ in items)
        {
            count++;
        }

        return count;
    }

    /// <summary>Finds a dictionary-view entry by its key.</summary>
    /// <param name="items">The current dictionary-view entries.</param>
    /// <param name="key">The key to locate.</param>
    /// <returns>The matching entry.</returns>
    private static KeyValuePair<int, ViewItem> FindByKey(
        IEnumerable<KeyValuePair<int, ViewItem>> items,
        int key)
    {
        foreach (var item in items)
        {
            if (item.Key == key)
            {
                return item;
            }
        }

        throw new KeyNotFoundException($"No view item exists for key '{key}'.");
    }

    /// <summary>Flattens grouped items without relying on a LINQ iterator.</summary>
    /// <param name="groups">The groups to flatten.</param>
    /// <returns>All items in group enumeration order.</returns>
    private static List<ViewItem> FlattenGroups(IEnumerable<IReadOnlyList<ViewItem>> groups)
    {
        var items = new List<ViewItem>();
        foreach (var group in groups)
        {
            items.AddRange(group);
        }

        return items;
    }

    /// <summary>Gets dictionary-view keys without allocating a LINQ iterator.</summary>
    /// <param name="items">The current dictionary-view entries.</param>
    /// <returns>The current entry keys.</returns>
    private static List<int> GetKeys(IEnumerable<KeyValuePair<int, ViewItem>> items)
    {
        var keys = new List<int>();
        foreach (var item in items)
        {
            keys.Add(item.Key);
        }

        return keys;
    }

    /// <summary>Provides CreateBatch.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <returns>The result.</returns>
    /// <param name="items">The items value.</param>
    private static PooledBatch<T> CreateBatch<T>(params T[] items)
    {
        var array = ArrayPool<T>.Shared.Rent(items.Length);
        Array.Copy(items, array, items.Length);
        return new(array, items.Length);
    }

    /// <summary>Provides MutableViewItem.</summary>
    /// <param name="region">The region value.</param>
    private sealed class MutableViewItem(string region)
    {
        /// <summary>Gets or sets Region.</summary>
        public string Region { get; set; } = region;

        /// <summary>Gets or sets Score.</summary>
        public int Score { get; set; }
    }

    /// <summary>Provides FirstSubscriptionErrorObservable.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    private sealed class FirstSubscriptionErrorObservable<T> : IObservable<T>
    {
        /// <summary>The number of subscriptions received by this observable.</summary>
        private int _subscriptions;

        /// <summary>Provides Subscribe.</summary>
        /// <param name="observer">The observer value.</param>
        /// <returns>The result.</returns>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            if (Interlocked.Increment(ref _subscriptions) == 1)
            {
                observer.OnError(new InvalidOperationException("initial probe failed"));
            }

            return ReactiveUI.Primitives.Disposables.Scope.Empty;
        }
    }

    /// <summary>Provides TwoValueObservable.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    private sealed class TwoValueObservable<T>(T first, T second) : IObservable<T>
    {
        /// <summary>Provides Subscribe.</summary>
        /// <param name="observer">The observer value.</param>
        /// <returns>The result.</returns>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            observer.OnNext(first);
            observer.OnNext(second);
            return ReactiveUI.Primitives.Disposables.Scope.Empty;
        }
    }

    /// <summary>Provides ReactiveSourceHarness.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    private sealed class ReactiveSourceHarness<T> : IReactiveSource<T>
        where T : notnull
    {
        /// <summary>The mutable source items exposed by this harness.</summary>
        private readonly List<T> _items;

        /// <summary>The source notification stream.</summary>
        private readonly Signal<CacheNotify<T>> _stream = new();

        /// <summary>Initializes a new instance of the ReactiveSourceHarness class.</summary>
        /// <param name="items">The items value.</param>
        public ReactiveSourceHarness(IEnumerable<T> items) => _items = new(items);

        /// <summary>Occurs when the harness collection changes.</summary>
        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        /// <summary>Gets Count.</summary>
        public int Count => _items.Count;

        /// <summary>Gets IsDisposed.</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>Gets IsReadOnly.</summary>
        public bool IsReadOnly => false;

        /// <summary>Gets Stream.</summary>
        public IObservable<CacheNotify<T>> Stream => _stream.AsObservable();

        /// <summary>Gets Version.</summary>
        public long Version { get; private set; }

        /// <summary>Provides AddItem.</summary>
        /// <param name="item">The item value.</param>
        public void AddItem(T item)
        {
            _items.Add(item);
            Version++;
            RaiseReset();
        }

        /// <summary>Provides AddItems.</summary>
        /// <param name="items">The items value.</param>
        public void AddItems(IEnumerable<T> items)
        {
            _items.AddRange(items);
            Version++;
            RaiseReset();
        }

        /// <summary>Provides ClearItems.</summary>
        public void ClearItems()
        {
            _items.Clear();
            Version++;
            RaiseReset();
        }

        /// <summary>Provides Dispose.</summary>
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            _stream.Dispose();
        }

        /// <summary>Provides Emit.</summary>
        /// <param name="notification">The notification value.</param>
        public void Emit(CacheNotify<T> notification) => _stream.OnNext(notification);

        /// <summary>Provides GetEnumerator.</summary>
        /// <returns>The result.</returns>
        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        /// <summary>Provides RemoveItem.</summary>
        /// <param name="item">The item value.</param>
        public void RemoveItem(T item)
        {
            _ = _items.Remove(item);
            Version++;
            RaiseReset();
        }

        /// <summary>Provides RemoveItems.</summary>
        /// <param name="items">The items value.</param>
        public void RemoveItems(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                _ = _items.Remove(item);
            }

            Version++;
            RaiseReset();
        }

        /// <summary>Provides ToArray.</summary>
        /// <returns>The result.</returns>
        public T[] ToArray() => _items.ToArray();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Raises a reset notification after the harness mutates.</summary>
        private void RaiseReset() =>
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>Provides ViewItem.</summary>
    /// <param name="Id">The Id value.</param>
    /// <param name="Region">The Region value.</param>
    /// <param name="Score">The Score value.</param>
    private sealed record ViewItem(int Id, string Region, int Score = 0);
}
