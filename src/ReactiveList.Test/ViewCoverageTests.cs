// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using CP.Primitives.Views;
using FluentAssertions;
using ReactiveList.Test;
using TUnit.Core;
using TestConstants = ReactiveList.Test.TestData;

namespace ReactiveList.Tests;

/// <summary>Coverage tests for reactive view implementations.</summary>
public class ViewCoverageTests
{
    /// <summary>Filtered views should track update transitions and refreshes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task FilteredReactiveView_UpdateTransitions_ShouldAddRemoveReplaceAndRefresh()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { TestConstants.TestValueTwo, TestConstants.TestValueThree, TestConstants.TestValueFour });

        using var view = new FilteredReactiveView<int>(
            list,
            static item => item % TestConstants.TestValueTwo == 0,
            Sequencer.Immediate,
            TimeSpan.Zero);

        view.Items.Should().Equal(TestConstants.TestValueTwo, TestConstants.TestValueFour);
        view[0].Should().Be(TestConstants.TestValueTwo);
        ((IEnumerable)view).GetEnumerator().MoveNext().Should().BeTrue();
        var filteredProperties = new List<string?>();
        view.PropertyChanged += (_, args) => filteredProperties.Add(args.PropertyName);

        list.Update(TestConstants.TestValueTwo, TestConstants.TestValueFive);
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueFour);

        list.Update(TestConstants.TestValueThree, TestConstants.TestValueSix);
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueFour, TestConstants.TestValueSix);

        list.Update(TestConstants.TestValueFour, TestConstants.TestValueEight);
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueEight, TestConstants.TestValueSix);

        list.Move(TestConstants.TestValueTwo, 0);
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueEight, TestConstants.TestValueSix);

        view.Refresh();
        view.ToArray().Should().Equal(TestConstants.TestValueEight, TestConstants.TestValueSix);

        list.Remove(TestConstants.TestValueSix);
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueEight);

        list.Clear();
        await WaitForPipeline();
        InvokePrivate(view, TestConstants.SourceChangedMethodName, new ChangeSet<int>(new Change<int>(ChangeReason.Clear, default)));
        view.Items.Should().BeEmpty();
        filteredProperties.Should().Contain(nameof(view.Count));
    }

    /// <summary>Sorted views should maintain comparer order through source changes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SortedReactiveView_Changes_ShouldKeepItemsSorted()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { TestConstants.TestValueThree, 1 });

        using var view = new SortedReactiveView<int>(
            list,
            Comparer<int>.Default,
            Sequencer.Immediate,
            TimeSpan.Zero);

        view.Items.Should().Equal(1, TestConstants.TestValueThree);
        view[1].Should().Be(TestConstants.TestValueThree);
        ((IEnumerable)view).GetEnumerator().MoveNext().Should().BeTrue();

        list.Add(TestConstants.TestValueTwo);
        await WaitForPipeline();
        view.Items.Should().Equal(1, TestConstants.TestValueTwo, TestConstants.TestValueThree);

        list.Add(TestConstants.TestValueTwo);
        await WaitForPipeline();
        view.Items.Should().Equal(1, TestConstants.TestValueTwo, TestConstants.TestValueTwo, TestConstants.TestValueThree);

        list.Update(TestConstants.TestValueThree, 0);
        await WaitForPipeline();
        view.Items.Should().Equal(0, 1, TestConstants.TestValueTwo, TestConstants.TestValueTwo);

        list.Move(0, TestConstants.TestValueTwo);
        await WaitForPipeline();
        view.Items.Should().Equal(0, 1, TestConstants.TestValueTwo, TestConstants.TestValueTwo);

        list.Remove(1);
        await WaitForPipeline();
        view.Items.Should().Equal(0, TestConstants.TestValueTwo, TestConstants.TestValueTwo);

        InvokePrivate(view, TestConstants.SourceChangedMethodName, new ChangeSet<int>(new Change<int>(ChangeReason.Clear, default)));
        view.Items.Should().BeEmpty();

        view.Refresh();
        view.Items.Should().Equal(0, TestConstants.TestValueTwo, TestConstants.TestValueTwo);
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

        view.Keys.Should().BeEquivalentTo([TestConstants.NorthRegion, TestConstants.SouthRegion]);
        view.Values.SelectMany(static group => group).Should().BeEquivalentTo([north, south]);
        view[TestConstants.NorthRegion].Should().ContainSingle().Which.Should().Be(north);
        view.TryGetValue(TestConstants.NorthRegion, out var northGroup).Should().BeTrue();
        northGroup.Should().ContainSingle().Which.Should().Be(north);
        view.TryGetValue(TestConstants.MissingKey, out var missing).Should().BeFalse();
        missing.Should().BeEmpty();
        ((IEnumerable)view).Cast<KeyValuePair<string, IReadOnlyList<ViewItem>>>()
            .Should().HaveCount(TestConstants.TestValueTwo);
        ((IEnumerable)view).GetEnumerator().MoveNext().Should().BeTrue();
        view.Refresh();

        var changedScore = north with { Score = TestConstants.TestValueTen };
        list.Update(north, changedScore);
        await WaitForPipeline();
        view[TestConstants.NorthRegion].Should().ContainSingle().Which.Should().Be(changedScore);

        var movedRegion = changedScore with { Region = TestConstants.SouthRegion };
        list.Update(changedScore, movedRegion);
        await WaitForPipeline();
        view.ContainsKey(TestConstants.NorthRegion).Should().BeFalse();
        view[TestConstants.SouthRegion].Should().BeEquivalentTo([south, movedRegion]);

        list.Remove(south);
        await WaitForPipeline();
        view[TestConstants.SouthRegion].Should().ContainSingle().Which.Should().Be(movedRegion);

        list.Remove(movedRegion);
        await WaitForPipeline();
        view.Should().BeEmpty();

        list.Add(north);
        await WaitForPipeline();
        list.Clear();
        await WaitForPipeline();
        view.Should().BeEmpty();

        var west = new ViewItem(TestConstants.TestValueThree, "west");
        InvokePrivate(view, TestConstants.SourceChangedMethodName, new ChangeSet<ViewItem>(new Change<ViewItem>(ChangeReason.Update, west)));
        view.ContainsKey("west").Should().BeTrue();

        InvokePrivate(view, TestConstants.SourceChangedMethodName, new ChangeSet<ViewItem>(new Change<ViewItem>(ChangeReason.Clear, default!)));
        view.Should().BeEmpty();

        list.Add(north);
        await WaitForPipeline();
        InvokePrivate(view, TestConstants.SourceChangedMethodName, new ChangeSet<ViewItem>(Change<ViewItem>.CreateRefresh(north)));
        view.ContainsKey(TestConstants.NorthRegion).Should().BeTrue();

        InvokePrivate(view, "RemoveFromGroup", new ViewItem(TestConstants.TestValueFourHundredFour, TestConstants.MissingKey));
        ((IList)GetPrivateField(view, "_groupCollection")).Clear();
        InvokePrivate(view, "RemoveFromGroup", north);
    }

    /// <summary>Dynamic filtered views should rebuild on filter changes and track source changes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DynamicFilteredReactiveView_FilterAndSourceChanges_ShouldRebuildAndTrackTransitions()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, TestConstants.TestValueTwo, TestConstants.TestValueThree });
        using var filters = new BehaviorSignal<Func<int, bool>>(static item => item >= TestConstants.TestValueTwo);

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filters,
            Sequencer.Immediate,
            TimeSpan.Zero);

        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueTwo, TestConstants.TestValueThree);
        view[0].Should().Be(TestConstants.TestValueTwo);
        ((IEnumerable)view).GetEnumerator().MoveNext().Should().BeTrue();
        var dynamicFilteredProperties = new List<string?>();
        view.PropertyChanged += (_, args) => dynamicFilteredProperties.Add(args.PropertyName);

        filters.OnNext(null!);
        await WaitForPipeline();
        view.Items.Should().Equal(1, TestConstants.TestValueTwo, TestConstants.TestValueThree);

        filters.OnNext(static item => item % TestConstants.TestValueTwo == 0);
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueTwo);

        list.Add(TestConstants.TestValueFour);
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueTwo, TestConstants.TestValueFour);

        list.Update(TestConstants.TestValueTwo, TestConstants.TestValueFive);
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueFour);

        list.Update(1, TestConstants.TestValueSix);
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueFour, TestConstants.TestValueSix);

        view.Refresh();
        view.Items.Should().Equal(TestConstants.TestValueSix, TestConstants.TestValueFour);

        list.Update(TestConstants.TestValueFour, TestConstants.TestValueEight);
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueSix, TestConstants.TestValueEight);

        list.Remove(TestConstants.TestValueSix);
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueEight);

        list.Move(TestConstants.TestValueTwo, 0);
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueEight);

        list.Clear();
        await WaitForPipeline();
        InvokePrivate(view, TestConstants.SourceChangedMethodName, new ChangeSet<int>(new Change<int>(ChangeReason.Clear, default)));
        view.Items.Should().BeEmpty();
        dynamicFilteredProperties.Should().Contain(nameof(view.Count));
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

        view.Items.Should().Equal(TestConstants.TestValueTwo);

        source.AddItem(TestConstants.TestValueFour);
        source.Emit(new CacheNotify<int>(CacheAction.Added, TestConstants.TestValueFour));
        source.AddItem(TestConstants.TestValueFive);
        source.Emit(new CacheNotify<int>(CacheAction.Added, TestConstants.TestValueFive));
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueTwo, TestConstants.TestValueFour);

        source.RemoveItem(TestConstants.TestValueTwo);
        source.Emit(new CacheNotify<int>(CacheAction.Removed, TestConstants.TestValueTwo));
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueFour);

        source.AddItems([TestConstants.TestValueSix, TestConstants.TestValueSeven]);
        source.Emit(new CacheNotify<int>(CacheAction.BatchAdded, default, CreateBatch(TestConstants.TestValueSix, TestConstants.TestValueSeven)));
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueFour, TestConstants.TestValueSix);

        source.RemoveItems([TestConstants.TestValueFour, TestConstants.TestValueSix]);
        source.Emit(new CacheNotify<int>(CacheAction.BatchRemoved, default, CreateBatch(TestConstants.TestValueFour, TestConstants.TestValueSix)));
        await WaitForPipeline();
        view.Items.Should().BeEmpty();

        source.ClearItems();
        source.Emit(new CacheNotify<int>(CacheAction.Cleared, default));
        await WaitForPipeline();
        view.Items.Should().BeEmpty();

        filters.OnNext(static _ => true);
        await WaitForPipeline();
        source.AddItem(TestConstants.TestValueNine);
        source.Emit(new CacheNotify<int>(CacheAction.Added, TestConstants.TestValueNine));
        await WaitForPipeline();
        view.Items.Should().Equal(TestConstants.TestValueNine);
        dynamicProperties.Should().Contain(nameof(view.Items));
        view.Dispose();
        view.Dispose();
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

            applied.TrySetResult(true);
        };

        source.RemoveItem(TestConstants.TestValueTwo);
        source.AddItem(TestConstants.TestValueFour);
        source.Emit(new CacheNotify<int>(
            CacheAction.Updated,
            TestConstants.TestValueFour,
            Previous: TestConstants.TestValueTwo));
        source.AddItem(TestConstants.TestValueSix);
        source.Emit(new CacheNotify<int>(CacheAction.Added, TestConstants.TestValueSix));

        await applied.Task;

        view.Items.Should().Equal(TestConstants.TestValueFour, TestConstants.TestValueSix);

        applied = new(TaskCreationOptions.RunContinuationsAsynchronously);
        source.ClearItems();
        source.AddItem(TestConstants.TestValueEight);
        source.Emit(new CacheNotify<int>(CacheAction.BatchOperation, default));

        await applied.Task;

        view.Items.Should().Equal(TestConstants.TestValueEight);
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

        view.Items.Should().Equal(1);

        source.AddItem(TestConstants.TestValueTwo);
        filters.OnNext(null!);
        await WaitForPipeline();

        view.Items.Should().Equal(1, TestConstants.TestValueTwo);
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
        dictionary.Add(TestConstants.TestValueTwo, new ViewItem(TestConstants.TestValueTwo, TestConstants.SouthRegion));
        dictionary.AddValueIndex(TestConstants.RegionPropertyName, static item => item.Region);

        using var view = SecondaryIndexReactiveView<int, ViewItem>.Create<string>(
            dictionary,
            TestConstants.RegionPropertyName,
            TestConstants.NorthRegion,
            Sequencer.Immediate,
            TimeSpan.Zero);

        view.Items.Should().ContainSingle().Which.Should().Be(north);
        view.Count.Should().Be(1);
        view[0].Should().Be(north);
        view.ToProperty(out var outCollection).Should().BeSameAs(view);
        outCollection.Should().BeSameAs(view.Items);
        view.ToProperty(collection => collection.Should().BeSameAs(view.Items)).Should().BeSameAs(view);
        view.Refresh();
        view.GetEnumerator().MoveNext().Should().BeTrue();
        ((IEnumerable)view).GetEnumerator().MoveNext().Should().BeTrue();
        var secondaryProperties = new List<string?>();
        view.PropertyChanged += (_, args) => secondaryProperties.Add(args.PropertyName);

        InvokePrivate(view, TestConstants.SourceChangedMethodName, new CacheNotify<KeyValuePair<int, ViewItem>>(CacheAction.Refreshed, default));
        view.Items.Should().ContainSingle().Which.Should().Be(north);

        dictionary.AddOrUpdate(1, north with { Region = TestConstants.SouthRegion });
        await WaitForPipeline();
        view.Items.Should().BeEmpty();

        var newNorth = new ViewItem(TestConstants.TestValueThree, TestConstants.NorthRegion);
        dictionary.AddOrUpdate(TestConstants.TestValueThree, newNorth);
        await WaitForPipeline();
        view.Items.Should().ContainSingle().Which.Should().Be(newNorth);

        dictionary.Remove(TestConstants.TestValueThree);
        await WaitForPipeline();
        view.Items.Should().BeEmpty();

        dictionary.AddOrUpdate(TestConstants.TestValueFour, new ViewItem(TestConstants.TestValueFour, TestConstants.NorthRegion));
        await WaitForPipeline();
        dictionary.Clear();
        await WaitForPipeline();
        view.Items.Should().BeEmpty();
        secondaryProperties.Should().Contain(nameof(view.Count));
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

        listView.Items.Should().ContainSingle().Which.Should().Be(north);
        listView.Count.Should().Be(1);
        listView[0].Should().Be(north);
        listView.ToProperty(out var listOutCollection).Should().BeSameAs(listView);
        listOutCollection.Should().BeSameAs(listView.Items);
        listView.ToProperty(collection => collection.Should().BeSameAs(listView.Items)).Should().BeSameAs(listView);
        listView.Refresh();
        listView.GetEnumerator().MoveNext().Should().BeTrue();
        ((IEnumerable)listView).GetEnumerator().MoveNext().Should().BeTrue();
        var listViewProperties = new List<string?>();
        listView.PropertyChanged += (_, args) => listViewProperties.Add(args.PropertyName);

        list.Remove(north);
        await WaitForPipeline();
        listView.Items.Should().BeEmpty();

        list.Add(north);
        await WaitForPipeline();
        listView.Items.Should().ContainSingle().Which.Should().Be(north);

        listKeys.OnNext([TestConstants.SouthRegion]);
        await WaitForPipeline();
        listView.Items.Should().ContainSingle().Which.Should().Be(south);

        var secondSouth = new ViewItem(TestConstants.TestValueThree, TestConstants.SouthRegion);
        list.Add(secondSouth);
        await WaitForPipeline();
        listView.Items.Should().BeEquivalentTo([south, secondSouth]);

        list.ReplaceAll([north]);
        await WaitForPipeline();
        listView.Items.Should().BeEmpty();
        listViewProperties.Should().Contain(nameof(listView.Count));

        using var dictionary = new QuaternaryDictionary<int, ViewItem> { { 1, north } };
        dictionary.Add(TestConstants.TestValueTwo, south);
        dictionary.AddValueIndex(TestConstants.RegionPropertyName, static item => item.Region);
        using var dictionaryKeys = new BehaviorSignal<string[]>([TestConstants.NorthRegion]);

        using var dictionaryView = DynamicSecondaryIndexDictionaryReactiveView<int, ViewItem>.Create<string>(
            dictionary,
            TestConstants.RegionPropertyName,
            dictionaryKeys,
            Sequencer.Immediate,
            TimeSpan.Zero);

        dictionaryView.Items.Should().ContainSingle().Which.Should().Be(new KeyValuePair<int, ViewItem>(1, north));
        dictionaryView.Count.Should().Be(1);
        dictionaryView[0].Key.Should().Be(1);
        dictionaryView.ToProperty(out var dictionaryOutCollection).Should().BeSameAs(dictionaryView);
        dictionaryOutCollection.Should().BeSameAs(dictionaryView.Items);
        dictionaryView.ToProperty(collection => collection.Should().BeSameAs(dictionaryView.Items)).Should().BeSameAs(dictionaryView);
        dictionaryView.Refresh();
        dictionaryView.GetEnumerator().MoveNext().Should().BeTrue();
        ((IEnumerable)dictionaryView).GetEnumerator().MoveNext().Should().BeTrue();
        var dictionaryViewProperties = new List<string?>();
        dictionaryView.PropertyChanged += (_, args) => dictionaryViewProperties.Add(args.PropertyName);

        dictionary.AddOrUpdate(1, north with { Region = TestConstants.SouthRegion });
        await WaitForPipeline();
        dictionaryView.Items.Should().BeEmpty();

        dictionary.AddOrUpdate(TestConstants.TestValueFour, new ViewItem(TestConstants.TestValueFour, TestConstants.NorthRegion));
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().Contain(TestConstants.TestValueFour);

        dictionary.AddOrUpdate(TestConstants.TestValueFour, new ViewItem(TestConstants.TestValueFour, TestConstants.NorthRegion, Score: 10));
        await WaitForPipeline();
        dictionaryView.Items.Single(static item => item.Key == TestConstants.TestValueFour).Value.Score.Should().Be(TestConstants.TestValueTen);

        dictionaryKeys.OnNext([TestConstants.SouthRegion]);
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().BeEquivalentTo([1, TestConstants.TestValueTwo]);

        dictionary.AddOrUpdate(TestConstants.TestValueFive, new ViewItem(TestConstants.TestValueFive, TestConstants.NorthRegion));
        await WaitForPipeline();
        dictionary.AddOrUpdate(TestConstants.TestValueFive, new ViewItem(TestConstants.TestValueFive, TestConstants.SouthRegion));
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().Contain(TestConstants.TestValueFive);

        var thirdSouth = new ViewItem(TestConstants.TestValueThree, TestConstants.SouthRegion);
        dictionary.AddOrUpdate(TestConstants.TestValueThree, thirdSouth);
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().Contain(TestConstants.TestValueThree);

        dictionary.Remove(1);
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().NotContain(1);

        dictionary.Clear();
        await WaitForPipeline();
        dictionaryView.Items.Should().BeEmpty();
        dictionaryViewProperties.Should().Contain(nameof(dictionaryView.Count));
    }

    /// <summary>Dynamic view constructors should ignore initial probe errors and keep default state.</summary>
    [Test]
    public void DynamicViews_InitialProbeErrors_ShouldUseDefaultValues()
    {
        using var source = new ReactiveSourceHarness<int>([1]);
        using var dynamicView = new DynamicReactiveView<int>(
            source,
            new FirstSubscriptionErrorObservable<Func<int, bool>>(),
            TimeSpan.Zero,
            Sequencer.Immediate);

        dynamicView.Items.Should().Equal(1);
        using var twoValueDynamicView = new DynamicReactiveView<int>(
            source,
            new TwoValueObservable<Func<int, bool>>(static item => item == 1, static _ => false),
            TimeSpan.Zero,
            Sequencer.Immediate);

        twoValueDynamicView.Items.Should().BeEmpty();

        using var list = new QuaternaryList<MutableViewItem> { new(1, TestConstants.NorthRegion) };
        list.AddIndex(TestConstants.RegionPropertyName, static item => item.Region);
        using var listView = new DynamicSecondaryIndexReactiveView<MutableViewItem, string>(
            list,
            TestConstants.RegionPropertyName,
            new FirstSubscriptionErrorObservable<string[]>(),
            Sequencer.Immediate,
            TimeSpan.Zero);

        listView.Items.Should().BeEmpty();
        using var twoValueListView = new DynamicSecondaryIndexReactiveView<MutableViewItem, string>(
            list,
            TestConstants.RegionPropertyName,
            new TwoValueObservable<string[]>([TestConstants.NorthRegion], [TestConstants.SouthRegion]),
            Sequencer.Immediate,
            TimeSpan.Zero);

        twoValueListView.Items.Should().BeEmpty();

        using var dictionary = new QuaternaryDictionary<int, MutableViewItem> { { 1, new MutableViewItem(1, TestConstants.NorthRegion) } };
        dictionary.AddValueIndex(TestConstants.RegionPropertyName, static item => item.Region);
        using var dictionaryView = DynamicSecondaryIndexDictionaryReactiveView<int, MutableViewItem>.Create<string>(
            dictionary,
            TestConstants.RegionPropertyName,
            new FirstSubscriptionErrorObservable<string[]>(),
            Sequencer.Immediate,
            TimeSpan.Zero);

        dictionaryView.Items.Should().BeEmpty();
        using var twoValueDictionaryView = DynamicSecondaryIndexDictionaryReactiveView<int, MutableViewItem>.Create<string>(
            dictionary,
            TestConstants.RegionPropertyName,
            new TwoValueObservable<string[]>([TestConstants.NorthRegion], [TestConstants.SouthRegion]),
            Sequencer.Immediate,
            TimeSpan.Zero);

        twoValueDictionaryView.Items.Should().BeEmpty();
    }

    /// <summary>Dynamic secondary-index views should handle mutable update transitions directly.</summary>
    [Test]
    public void DynamicSecondaryIndexViews_MutableUpdates_ShouldAddRemoveClearAndRebuild()
    {
        using var list = new QuaternaryList<MutableViewItem>();
        var listNorth = new MutableViewItem(1, TestConstants.NorthRegion);
        var listSouth = new MutableViewItem(TestConstants.TestValueTwo, TestConstants.SouthRegion);
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

        listView.Items.Should().ContainSingle().Which.Should().BeSameAs(listNorth);

        listNorth.Region = TestConstants.SouthRegion;
        InvokePrivate(listView, TestConstants.SourceChangedMethodName, new CacheNotify<MutableViewItem>(CacheAction.Updated, listNorth));
        listView.Items.Should().BeEmpty();

        listSouth.Region = TestConstants.NorthRegion;
        InvokePrivate(listView, TestConstants.SourceChangedMethodName, new CacheNotify<MutableViewItem>(CacheAction.Updated, listSouth));
        listView.Items.Should().ContainSingle().Which.Should().BeSameAs(listSouth);

        InvokePrivate(listView, TestConstants.SourceChangedMethodName, new CacheNotify<MutableViewItem>(CacheAction.Removed, listSouth));
        listView.Items.Should().BeEmpty();

        InvokePrivate(listView, TestConstants.SourceChangedMethodName, new CacheNotify<MutableViewItem>(CacheAction.BatchOperation, default));
        listView.Items.Count.Should().Be(1);

        InvokePrivate(listView, TestConstants.SourceChangedMethodName, new CacheNotify<MutableViewItem>(CacheAction.Cleared, default));
        listView.Items.Should().BeEmpty();

        using var dictionary = new QuaternaryDictionary<int, MutableViewItem>();
        var dictionaryNorth = new MutableViewItem(1, TestConstants.NorthRegion);
        var dictionarySouth = new MutableViewItem(TestConstants.TestValueTwo, TestConstants.SouthRegion);
        dictionary.Add(1, dictionaryNorth);
        dictionary.Add(TestConstants.TestValueTwo, dictionarySouth);
        dictionary.AddValueIndex(TestConstants.RegionPropertyName, static item => item.Region);
        using var dictionaryKeys = new BehaviorSignal<string[]>([TestConstants.NorthRegion]);
        using var dictionaryView = DynamicSecondaryIndexDictionaryReactiveView<int, MutableViewItem>.Create<string>(
            dictionary,
            TestConstants.RegionPropertyName,
            dictionaryKeys,
            Sequencer.Immediate,
            TimeSpan.Zero);

        dictionaryView.Items.Should().ContainSingle()
            .Which.Value.Should().BeSameAs(dictionaryNorth);

        dictionaryNorth.Region = TestConstants.SouthRegion;
        InvokePrivate(dictionaryView, TestConstants.SourceChangedMethodName, new CacheNotify<KeyValuePair<int, MutableViewItem>>(
            CacheAction.Updated,
            new KeyValuePair<int, MutableViewItem>(1, dictionaryNorth)));
        dictionaryView.Items.Should().BeEmpty();

        dictionarySouth.Region = TestConstants.NorthRegion;
        InvokePrivate(dictionaryView, TestConstants.SourceChangedMethodName, new CacheNotify<KeyValuePair<int, MutableViewItem>>(
            CacheAction.Updated,
            new KeyValuePair<int, MutableViewItem>(TestConstants.TestValueTwo, dictionarySouth)));
        dictionaryView.Items.Should().ContainSingle()
            .Which.Value.Should().BeSameAs(dictionarySouth);

        dictionarySouth.Score = TestConstants.TestValueTen;
        InvokePrivate(dictionaryView, TestConstants.SourceChangedMethodName, new CacheNotify<KeyValuePair<int, MutableViewItem>>(
            CacheAction.Updated,
            new KeyValuePair<int, MutableViewItem>(TestConstants.TestValueTwo, dictionarySouth)));
        dictionaryView.Items.Single().Value.Score.Should().Be(TestConstants.TestValueTen);

        InvokePrivate(dictionaryView, TestConstants.SourceChangedMethodName, new CacheNotify<KeyValuePair<int, MutableViewItem>>(
            CacheAction.Removed,
            new KeyValuePair<int, MutableViewItem>(TestConstants.TestValueTwo, dictionarySouth)));
        dictionaryView.Items.Should().BeEmpty();

        InvokePrivate(dictionaryView, TestConstants.SourceChangedMethodName, new CacheNotify<KeyValuePair<int, MutableViewItem>>(
            CacheAction.Refreshed,
            default));
        dictionaryView.Items.Count.Should().Be(1);

        InvokePrivate(dictionaryView, TestConstants.SourceChangedMethodName, new CacheNotify<KeyValuePair<int, MutableViewItem>>(
            CacheAction.Cleared,
            default));
        dictionaryView.Items.Should().BeEmpty();

        using var nullableKeyDictionary = new QuaternaryDictionary<string, MutableViewItem>
        {
            { "north-1", new MutableViewItem(TestConstants.TestValueThree, TestConstants.NorthRegion) },
        };
        nullableKeyDictionary.AddValueIndex(TestConstants.RegionPropertyName, static item => item.Region);
        using var nullableKeyKeys = new BehaviorSignal<string[]>([TestConstants.NorthRegion]);
        using var nullableKeyView = DynamicSecondaryIndexDictionaryReactiveView<string, MutableViewItem>.Create<string>(
            nullableKeyDictionary,
            TestConstants.RegionPropertyName,
            nullableKeyKeys,
            Sequencer.Immediate,
            TimeSpan.Zero);

        InvokePrivate(nullableKeyView, TestConstants.SourceChangedMethodName, new CacheNotify<KeyValuePair<string, MutableViewItem>>(
            CacheAction.Removed,
            new KeyValuePair<string, MutableViewItem>(null!, new MutableViewItem(TestConstants.TestValueFour, TestConstants.NorthRegion))));
        nullableKeyView.Items.Count.Should().Be(1);

        InvokePrivate(nullableKeyView, TestConstants.SourceChangedMethodName, new CacheNotify<KeyValuePair<string, MutableViewItem>>(
            CacheAction.Removed,
            new KeyValuePair<string, MutableViewItem>(TestConstants.MissingKey, new MutableViewItem(TestConstants.TestValueFive, TestConstants.NorthRegion))));
        nullableKeyView.Items.Count.Should().Be(1);
    }
#endif

    /// <summary>Provides WaitForPipeline.</summary>
    /// <returns>The result.</returns>
    private static async Task WaitForPipeline() => await Task.Delay(TestConstants.TestValueThirty);

    /// <summary>Provides InvokePrivate.</summary>
    /// <param name="target">The target value.</param>
    /// <param name="methodName">The methodName value.</param>
    /// <param name="args">The args value.</param>
    /// <returns>The result.</returns>
    private static object? InvokePrivate(object target, string methodName, params object?[] args)
    {
        var targetType = target.GetType();
        var method = targetType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(targetType.FullName, methodName);
        return method.Invoke(target, args);
    }

    /// <summary>Provides GetPrivateField.</summary>
    /// <param name="target">The target value.</param>
    /// <param name="fieldName">The fieldName value.</param>
    /// <returns>The result.</returns>
    private static object GetPrivateField(object target, string fieldName)
    {
        var targetType = target.GetType();
        var field = targetType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(targetType.FullName, fieldName);
        return field.GetValue(target) ?? throw new InvalidOperationException($"Field '{fieldName}' returned null.");
    }

    /// <summary>Provides CreateBatch.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <returns>The result.</returns>
    /// <param name="items">The items value.</param>
    private static PooledBatch<T> CreateBatch<T>(params T[] items)
    {
        var array = ArrayPool<T>.Shared.Rent(items.Length);
        Array.Copy(items, array, items.Length);
        return new PooledBatch<T>(array, items.Length);
    }

    /// <summary>Provides MutableViewItem.</summary>
    /// <param name="id">The id value.</param>
    /// <param name="region">The region value.</param>
    private sealed class MutableViewItem(int id, string region)
    {
        /// <summary>Gets Id.</summary>
        public int Id { get; } = id;

        /// <summary>Gets or sets Region.</summary>
        public string Region { get; set; } = region;

        /// <summary>Gets or sets Score.</summary>
        public int Score { get; set; }
    }

    /// <summary>Provides FirstSubscriptionErrorObservable.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    private sealed class FirstSubscriptionErrorObservable<T> : IObservable<T>
    {
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
        private readonly List<T> _items;

        private readonly Signal<CacheNotify<T>> _stream = new();

        /// <summary>Initializes a new instance of the ReactiveSourceHarness class.</summary>
        /// <param name="items">The items value.</param>
        public ReactiveSourceHarness(IEnumerable<T> items) => _items = new(items);

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
        }

        /// <summary>Provides AddItems.</summary>
        /// <param name="items">The items value.</param>
        public void AddItems(IEnumerable<T> items)
        {
            _items.AddRange(items);
            Version++;
        }

        /// <summary>Provides ClearItems.</summary>
        public void ClearItems()
        {
            _items.Clear();
            Version++;
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
            _items.Remove(item);
            Version++;
        }

        /// <summary>Provides RemoveItems.</summary>
        /// <param name="items">The items value.</param>
        public void RemoveItems(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                _items.Remove(item);
            }

            Version++;
        }

        /// <summary>Provides ToArray.</summary>
        /// <returns>The result.</returns>
        public T[] ToArray() => _items.ToArray();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Provides RaiseReset.</summary>
        public void RaiseReset() =>
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>Provides ViewItem.</summary>
    /// <param name="Id">The Id value.</param>
    /// <param name="Region">The Region value.</param>
    /// <param name="Score">The Score value.</param>
    private sealed record ViewItem(int Id, string Region, int Score = 0);
}
