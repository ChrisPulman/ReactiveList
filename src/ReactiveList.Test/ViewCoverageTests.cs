// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using CP.Primitives.Views;
using FluentAssertions;
using TUnit.Core;

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
        list.AddRange(new[] { 2, 3, 4 });

        using var view = new FilteredReactiveView<int>(
            list,
            static item => item % 2 == 0,
            Sequencer.Immediate,
            TimeSpan.Zero);

        view.Items.Should().Equal(2, 4);
        view[0].Should().Be(2);
        ((IEnumerable)view).GetEnumerator().MoveNext().Should().BeTrue();
        var filteredProperties = new List<string?>();
        view.PropertyChanged += (_, args) => filteredProperties.Add(args.PropertyName);

        list.Update(2, 5);
        await WaitForPipeline();
        view.Items.Should().Equal(4);

        list.Update(3, 6);
        await WaitForPipeline();
        view.Items.Should().Equal(4, 6);

        list.Update(4, 8);
        await WaitForPipeline();
        view.Items.Should().Equal(8, 6);

        list.Move(2, 0);
        await WaitForPipeline();
        view.Items.Should().Equal(8, 6);

        view.Refresh();
        view.ToArray().Should().Equal(8, 6);

        list.Remove(6);
        await WaitForPipeline();
        view.Items.Should().Equal(8);

        list.Clear();
        await WaitForPipeline();
        InvokePrivate(view, "OnSourceChanged", new ChangeSet<int>(new Change<int>(ChangeReason.Clear, default)));
        view.Items.Should().BeEmpty();
        filteredProperties.Should().Contain(nameof(view.Count));
    }

    /// <summary>Sorted views should maintain comparer order through source changes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SortedReactiveView_Changes_ShouldKeepItemsSorted()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 3, 1 });

        using var view = new SortedReactiveView<int>(
            list,
            Comparer<int>.Default,
            Sequencer.Immediate,
            TimeSpan.Zero);

        view.Items.Should().Equal(1, 3);
        view[1].Should().Be(3);
        ((IEnumerable)view).GetEnumerator().MoveNext().Should().BeTrue();

        list.Add(2);
        await WaitForPipeline();
        view.Items.Should().Equal(1, 2, 3);

        list.Add(2);
        await WaitForPipeline();
        view.Items.Should().Equal(1, 2, 2, 3);

        list.Update(3, 0);
        await WaitForPipeline();
        view.Items.Should().Equal(0, 1, 2, 2);

        list.Move(0, 2);
        await WaitForPipeline();
        view.Items.Should().Equal(0, 1, 2, 2);

        list.Remove(1);
        await WaitForPipeline();
        view.Items.Should().Equal(0, 2, 2);

        InvokePrivate(view, "OnSourceChanged", new ChangeSet<int>(new Change<int>(ChangeReason.Clear, default)));
        view.Items.Should().BeEmpty();

        view.Refresh();
        view.Items.Should().Equal(0, 2, 2);
    }

    /// <summary>Grouped views should expose dictionary members and update group membership.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task GroupedReactiveView_DictionarySurfaceAndUpdates_ShouldTrackGroups()
    {
        using var list = new ReactiveList<ViewItem>();
        var north = new ViewItem(1, "north");
        var south = new ViewItem(2, "south");
        list.AddRange(new[] { north, south });

        using var view = new GroupedReactiveView<ViewItem, string>(
            list,
            static item => item.Region,
            Sequencer.Immediate,
            TimeSpan.Zero);

        view.Keys.Should().BeEquivalentTo(["north", "south"]);
        view.Values.SelectMany(static group => group).Should().BeEquivalentTo([north, south]);
        view["north"].Should().ContainSingle().Which.Should().Be(north);
        view.TryGetValue("north", out var northGroup).Should().BeTrue();
        northGroup.Should().ContainSingle().Which.Should().Be(north);
        view.TryGetValue("missing", out var missing).Should().BeFalse();
        missing.Should().BeEmpty();
        ((IEnumerable)view).Cast<KeyValuePair<string, IReadOnlyList<ViewItem>>>()
            .Should().HaveCount(2);
        ((IEnumerable)view).GetEnumerator().MoveNext().Should().BeTrue();
        view.Refresh();

        var changedScore = north with { Score = 10 };
        list.Update(north, changedScore);
        await WaitForPipeline();
        view["north"].Should().ContainSingle().Which.Should().Be(changedScore);

        var movedRegion = changedScore with { Region = "south" };
        list.Update(changedScore, movedRegion);
        await WaitForPipeline();
        view.ContainsKey("north").Should().BeFalse();
        view["south"].Should().BeEquivalentTo([south, movedRegion]);

        list.Remove(south);
        await WaitForPipeline();
        view["south"].Should().ContainSingle().Which.Should().Be(movedRegion);

        list.Remove(movedRegion);
        await WaitForPipeline();
        view.Should().BeEmpty();

        list.Add(north);
        await WaitForPipeline();
        list.Clear();
        await WaitForPipeline();
        view.Should().BeEmpty();

        var west = new ViewItem(3, "west");
        InvokePrivate(view, "OnSourceChanged", new ChangeSet<ViewItem>(new Change<ViewItem>(ChangeReason.Update, west)));
        view.ContainsKey("west").Should().BeTrue();

        InvokePrivate(view, "OnSourceChanged", new ChangeSet<ViewItem>(new Change<ViewItem>(ChangeReason.Clear, default!)));
        view.Should().BeEmpty();

        list.Add(north);
        await WaitForPipeline();
        InvokePrivate(view, "OnSourceChanged", new ChangeSet<ViewItem>(Change<ViewItem>.CreateRefresh(north)));
        view.ContainsKey("north").Should().BeTrue();

        InvokePrivate(view, "RemoveFromGroup", new ViewItem(404, "missing"));
        ((IList)GetPrivateField(view, "_groupCollection")).Clear();
        InvokePrivate(view, "RemoveFromGroup", north);
    }

    /// <summary>Dynamic filtered views should rebuild on filter changes and track source changes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DynamicFilteredReactiveView_FilterAndSourceChanges_ShouldRebuildAndTrackTransitions()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3 });
        using var filters = new BehaviorSignal<Func<int, bool>>(static item => item >= 2);

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filters,
            Sequencer.Immediate,
            TimeSpan.Zero);

        await WaitForPipeline();
        view.Items.Should().Equal(2, 3);
        view[0].Should().Be(2);
        ((IEnumerable)view).GetEnumerator().MoveNext().Should().BeTrue();
        var dynamicFilteredProperties = new List<string?>();
        view.PropertyChanged += (_, args) => dynamicFilteredProperties.Add(args.PropertyName);

        filters.OnNext(null!);
        await WaitForPipeline();
        view.Items.Should().Equal(1, 2, 3);

        filters.OnNext(static item => item % 2 == 0);
        await WaitForPipeline();
        view.Items.Should().Equal(2);

        list.Add(4);
        await WaitForPipeline();
        view.Items.Should().Equal(2, 4);

        list.Update(2, 5);
        await WaitForPipeline();
        view.Items.Should().Equal(4);

        list.Update(1, 6);
        await WaitForPipeline();
        view.Items.Should().Equal(4, 6);

        view.Refresh();
        view.Items.Should().Equal(6, 4);

        list.Update(4, 8);
        await WaitForPipeline();
        view.Items.Should().Equal(6, 8);

        list.Remove(6);
        await WaitForPipeline();
        view.Items.Should().Equal(8);

        list.Move(2, 0);
        await WaitForPipeline();
        view.Items.Should().Equal(8);

        list.Clear();
        await WaitForPipeline();
        InvokePrivate(view, "OnSourceChanged", new ChangeSet<int>(new Change<int>(ChangeReason.Clear, default)));
        view.Items.Should().BeEmpty();
        dynamicFilteredProperties.Should().Contain(nameof(view.Count));
    }

    /// <summary>Dynamic reactive views should apply single and batch stream actions.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DynamicReactiveView_StreamActions_ShouldApplyCurrentFilterAndBatches()
    {
        using var source = new ReactiveSourceHarness<int>([1, 2, 3]);
        using var filters = new BehaviorSignal<Func<int, bool>>(static item => item % 2 == 0);

        using var view = new DynamicReactiveView<int>(
            source,
            filters,
            TimeSpan.Zero,
            Sequencer.Immediate);
        var dynamicProperties = new List<string?>();
        view.PropertyChanged += (_, args) => dynamicProperties.Add(args.PropertyName);

        view.Items.Should().Equal(2);

        source.AddItem(4);
        source.Emit(new CacheNotify<int>(CacheAction.Added, 4));
        source.AddItem(5);
        source.Emit(new CacheNotify<int>(CacheAction.Added, 5));
        await WaitForPipeline();
        view.Items.Should().Equal(2, 4);

        source.RemoveItem(2);
        source.Emit(new CacheNotify<int>(CacheAction.Removed, 2));
        await WaitForPipeline();
        view.Items.Should().Equal(4);

        source.AddItems([6, 7]);
        source.Emit(new CacheNotify<int>(CacheAction.BatchAdded, default, CreateBatch(6, 7)));
        await WaitForPipeline();
        view.Items.Should().Equal(4, 6);

        source.RemoveItems([4, 6]);
        source.Emit(new CacheNotify<int>(CacheAction.BatchRemoved, default, CreateBatch(4, 6)));
        await WaitForPipeline();
        view.Items.Should().BeEmpty();

        source.ClearItems();
        source.Emit(new CacheNotify<int>(CacheAction.Cleared, default));
        await WaitForPipeline();
        view.Items.Should().BeEmpty();

        filters.OnNext(static _ => true);
        await WaitForPipeline();
        source.AddItem(9);
        source.Emit(new CacheNotify<int>(CacheAction.Added, 9));
        await WaitForPipeline();
        view.Items.Should().Equal(9);
        dynamicProperties.Should().Contain(nameof(view.Items));
        view.Dispose();
        view.Dispose();
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

        source.AddItem(2);
        filters.OnNext(null!);
        await WaitForPipeline();

        view.Items.Should().Equal(1, 2);
    }

#if NET8_0_OR_GREATER || NETFRAMEWORK

    /// <summary>Secondary-index dictionary views should remove values when updates leave the index.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SecondaryIndexReactiveView_DictionaryUpdates_ShouldRemoveValuesThatLeaveTheIndex()
    {
        using var dictionary = new QuaternaryDictionary<int, ViewItem>();
        var north = new ViewItem(1, "north");
        dictionary.Add(1, north);
        dictionary.Add(2, new ViewItem(2, "south"));
        dictionary.AddValueIndex("region", static item => item.Region);

        using var view = SecondaryIndexReactiveView<int, ViewItem>.Create<string>(
            dictionary,
            "region",
            "north",
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

        InvokePrivate(view, "OnSourceChanged", new CacheNotify<KeyValuePair<int, ViewItem>>(CacheAction.Refreshed, default));
        view.Items.Should().ContainSingle().Which.Should().Be(north);

        dictionary.AddOrUpdate(1, north with { Region = "south" });
        await WaitForPipeline();
        view.Items.Should().BeEmpty();

        var newNorth = new ViewItem(3, "north");
        dictionary.AddOrUpdate(3, newNorth);
        await WaitForPipeline();
        view.Items.Should().ContainSingle().Which.Should().Be(newNorth);

        dictionary.Remove(3);
        await WaitForPipeline();
        view.Items.Should().BeEmpty();

        dictionary.AddOrUpdate(4, new ViewItem(4, "north"));
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
        var north = new ViewItem(1, "north");
        var south = new ViewItem(2, "south");
        list.Add(north);
        list.Add(south);
        list.AddIndex("region", static item => item.Region);
        using var listKeys = new BehaviorSignal<string[]>(["north"]);

        using var listView = new DynamicSecondaryIndexReactiveView<ViewItem, string>(
            list,
            "region",
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

        listKeys.OnNext(["south"]);
        await WaitForPipeline();
        listView.Items.Should().ContainSingle().Which.Should().Be(south);

        var secondSouth = new ViewItem(3, "south");
        list.Add(secondSouth);
        await WaitForPipeline();
        listView.Items.Should().BeEquivalentTo([south, secondSouth]);

        list.ReplaceAll([north]);
        await WaitForPipeline();
        listView.Items.Should().BeEmpty();
        listViewProperties.Should().Contain(nameof(listView.Count));

        using var dictionary = new QuaternaryDictionary<int, ViewItem>();
        dictionary.Add(1, north);
        dictionary.Add(2, south);
        dictionary.AddValueIndex("region", static item => item.Region);
        using var dictionaryKeys = new BehaviorSignal<string[]>(["north"]);

        using var dictionaryView = DynamicSecondaryIndexDictionaryReactiveView<int, ViewItem>.Create<string>(
            dictionary,
            "region",
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

        dictionary.AddOrUpdate(1, north with { Region = "south" });
        await WaitForPipeline();
        dictionaryView.Items.Should().BeEmpty();

        dictionary.AddOrUpdate(4, new ViewItem(4, "north"));
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().Contain(4);

        dictionary.AddOrUpdate(4, new ViewItem(4, "north", Score: 10));
        await WaitForPipeline();
        dictionaryView.Items.Single(static item => item.Key == 4).Value.Score.Should().Be(10);

        dictionaryKeys.OnNext(["south"]);
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().BeEquivalentTo([1, 2]);

        dictionary.AddOrUpdate(5, new ViewItem(5, "north"));
        await WaitForPipeline();
        dictionary.AddOrUpdate(5, new ViewItem(5, "south"));
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().Contain(5);

        var thirdSouth = new ViewItem(3, "south");
        dictionary.AddOrUpdate(3, thirdSouth);
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().Contain(3);

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

        using var list = new QuaternaryList<MutableViewItem>();
        list.Add(new MutableViewItem(1, "north"));
        list.AddIndex("region", static item => item.Region);
        using var listView = new DynamicSecondaryIndexReactiveView<MutableViewItem, string>(
            list,
            "region",
            new FirstSubscriptionErrorObservable<string[]>(),
            Sequencer.Immediate,
            TimeSpan.Zero);

        listView.Items.Should().BeEmpty();
        using var twoValueListView = new DynamicSecondaryIndexReactiveView<MutableViewItem, string>(
            list,
            "region",
            new TwoValueObservable<string[]>(["north"], ["south"]),
            Sequencer.Immediate,
            TimeSpan.Zero);

        twoValueListView.Items.Should().BeEmpty();

        using var dictionary = new QuaternaryDictionary<int, MutableViewItem>();
        dictionary.Add(1, new MutableViewItem(1, "north"));
        dictionary.AddValueIndex("region", static item => item.Region);
        using var dictionaryView = DynamicSecondaryIndexDictionaryReactiveView<int, MutableViewItem>.Create<string>(
            dictionary,
            "region",
            new FirstSubscriptionErrorObservable<string[]>(),
            Sequencer.Immediate,
            TimeSpan.Zero);

        dictionaryView.Items.Should().BeEmpty();
        using var twoValueDictionaryView = DynamicSecondaryIndexDictionaryReactiveView<int, MutableViewItem>.Create<string>(
            dictionary,
            "region",
            new TwoValueObservable<string[]>(["north"], ["south"]),
            Sequencer.Immediate,
            TimeSpan.Zero);

        twoValueDictionaryView.Items.Should().BeEmpty();
    }

    /// <summary>Dynamic secondary-index views should handle mutable update transitions directly.</summary>
    [Test]
    public void DynamicSecondaryIndexViews_MutableUpdates_ShouldAddRemoveClearAndRebuild()
    {
        using var list = new QuaternaryList<MutableViewItem>();
        var listNorth = new MutableViewItem(1, "north");
        var listSouth = new MutableViewItem(2, "south");
        list.Add(listNorth);
        list.Add(listSouth);
        list.AddIndex("region", static item => item.Region);
        using var listKeys = new BehaviorSignal<string[]>(["north"]);
        using var listView = new DynamicSecondaryIndexReactiveView<MutableViewItem, string>(
            list,
            "region",
            listKeys,
            Sequencer.Immediate,
            TimeSpan.Zero);

        listView.Items.Should().ContainSingle().Which.Should().BeSameAs(listNorth);

        listNorth.Region = "south";
        InvokePrivate(listView, "OnSourceChanged", new CacheNotify<MutableViewItem>(CacheAction.Updated, listNorth));
        listView.Items.Should().BeEmpty();

        listSouth.Region = "north";
        InvokePrivate(listView, "OnSourceChanged", new CacheNotify<MutableViewItem>(CacheAction.Updated, listSouth));
        listView.Items.Should().ContainSingle().Which.Should().BeSameAs(listSouth);

        InvokePrivate(listView, "OnSourceChanged", new CacheNotify<MutableViewItem>(CacheAction.Removed, listSouth));
        listView.Items.Should().BeEmpty();

        InvokePrivate(listView, "OnSourceChanged", new CacheNotify<MutableViewItem>(CacheAction.BatchOperation, default));
        listView.Items.Count.Should().Be(1);

        InvokePrivate(listView, "OnSourceChanged", new CacheNotify<MutableViewItem>(CacheAction.Cleared, default));
        listView.Items.Should().BeEmpty();

        using var dictionary = new QuaternaryDictionary<int, MutableViewItem>();
        var dictionaryNorth = new MutableViewItem(1, "north");
        var dictionarySouth = new MutableViewItem(2, "south");
        dictionary.Add(1, dictionaryNorth);
        dictionary.Add(2, dictionarySouth);
        dictionary.AddValueIndex("region", static item => item.Region);
        using var dictionaryKeys = new BehaviorSignal<string[]>(["north"]);
        using var dictionaryView = DynamicSecondaryIndexDictionaryReactiveView<int, MutableViewItem>.Create<string>(
            dictionary,
            "region",
            dictionaryKeys,
            Sequencer.Immediate,
            TimeSpan.Zero);

        dictionaryView.Items.Should().ContainSingle()
            .Which.Value.Should().BeSameAs(dictionaryNorth);

        dictionaryNorth.Region = "south";
        InvokePrivate(dictionaryView, "OnSourceChanged", new CacheNotify<KeyValuePair<int, MutableViewItem>>(
            CacheAction.Updated,
            new KeyValuePair<int, MutableViewItem>(1, dictionaryNorth)));
        dictionaryView.Items.Should().BeEmpty();

        dictionarySouth.Region = "north";
        InvokePrivate(dictionaryView, "OnSourceChanged", new CacheNotify<KeyValuePair<int, MutableViewItem>>(
            CacheAction.Updated,
            new KeyValuePair<int, MutableViewItem>(2, dictionarySouth)));
        dictionaryView.Items.Should().ContainSingle()
            .Which.Value.Should().BeSameAs(dictionarySouth);

        dictionarySouth.Score = 10;
        InvokePrivate(dictionaryView, "OnSourceChanged", new CacheNotify<KeyValuePair<int, MutableViewItem>>(
            CacheAction.Updated,
            new KeyValuePair<int, MutableViewItem>(2, dictionarySouth)));
        dictionaryView.Items.Single().Value.Score.Should().Be(10);

        InvokePrivate(dictionaryView, "OnSourceChanged", new CacheNotify<KeyValuePair<int, MutableViewItem>>(
            CacheAction.Removed,
            new KeyValuePair<int, MutableViewItem>(2, dictionarySouth)));
        dictionaryView.Items.Should().BeEmpty();

        InvokePrivate(dictionaryView, "OnSourceChanged", new CacheNotify<KeyValuePair<int, MutableViewItem>>(
            CacheAction.Refreshed,
            default));
        dictionaryView.Items.Count.Should().Be(1);

        InvokePrivate(dictionaryView, "OnSourceChanged", new CacheNotify<KeyValuePair<int, MutableViewItem>>(
            CacheAction.Cleared,
            default));
        dictionaryView.Items.Should().BeEmpty();

        using var nullableKeyDictionary = new QuaternaryDictionary<string, MutableViewItem>();
        nullableKeyDictionary.Add("north-1", new MutableViewItem(3, "north"));
        nullableKeyDictionary.AddValueIndex("region", static item => item.Region);
        using var nullableKeyKeys = new BehaviorSignal<string[]>(["north"]);
        using var nullableKeyView = DynamicSecondaryIndexDictionaryReactiveView<string, MutableViewItem>.Create<string>(
            nullableKeyDictionary,
            "region",
            nullableKeyKeys,
            Sequencer.Immediate,
            TimeSpan.Zero);

        InvokePrivate(nullableKeyView, "OnSourceChanged", new CacheNotify<KeyValuePair<string, MutableViewItem>>(
            CacheAction.Removed,
            new KeyValuePair<string, MutableViewItem>(null!, new MutableViewItem(4, "north"))));
        nullableKeyView.Items.Count.Should().Be(1);

        InvokePrivate(nullableKeyView, "OnSourceChanged", new CacheNotify<KeyValuePair<string, MutableViewItem>>(
            CacheAction.Removed,
            new KeyValuePair<string, MutableViewItem>("missing", new MutableViewItem(5, "north"))));
        nullableKeyView.Items.Count.Should().Be(1);
    }
#endif

    /// <summary>Provides WaitForPipeline.</summary>
    /// <returns>The result.</returns>
    private static async Task WaitForPipeline() => await Task.Delay(30);

    /// <summary>Provides InvokePrivate.</summary>
    /// <param name="target">The target value.</param>
    /// <param name="methodName">The methodName value.</param>
    /// <param name="args">The args value.</param>
    /// <returns>The result.</returns>
    private static object? InvokePrivate(object target, string methodName, params object?[] args) =>
        target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);

    /// <summary>Provides GetPrivateField.</summary>
    /// <param name="target">The target value.</param>
    /// <param name="fieldName">The fieldName value.</param>
    /// <returns>The result.</returns>
    private static object GetPrivateField(object target, string fieldName) =>
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

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
        public ReactiveSourceHarness(IEnumerable<T> items) => _items = new List<T>(items);

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
