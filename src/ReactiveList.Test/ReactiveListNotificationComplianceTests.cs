// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CP.Primitives.Collections;
using CP.Primitives.Views;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests notification contracts used by UI binding and DynamicData-style pipelines.</summary>
public class ReactiveListNotificationComplianceTests
{
    /// <summary>Indexer replacement should be one replace notification and should not report a count change.</summary>
    [Test]
    public void IndexerSet_ShouldEmitSingleReplaceAndNoCountPropertyChange()
    {
        using var list = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        var collectionEvents = new List<NotifyCollectionChangedEventArgs>();
        var propertyNames = new List<string?>();
        list.CollectionChanged += (_, args) => collectionEvents.Add(args);
        list.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName);

        list[1] = TestData.TestValueTwenty;

        _ = list.Should().Equal(1, TestData.TestValueTwenty, TestData.TestValueThree);
        _ = collectionEvents.Should().ContainSingle();
        _ = collectionEvents[0].Action.Should().Be(NotifyCollectionChangedAction.Replace);
        var oldItems = collectionEvents[0].OldItems ?? throw new InvalidOperationException("Replace notification did not include old items.");
        var newItems = collectionEvents[0].NewItems ?? throw new InvalidOperationException("Replace notification did not include new items.");
        var oldValues = new List<int>(oldItems.Count);
        foreach (var item in oldItems)
        {
            oldValues.Add((int)item!);
        }

        var newValues = new List<int>(newItems.Count);
        foreach (var item in newItems)
        {
            newValues.Add((int)item!);
        }

        _ = oldValues.Should().Equal(TestData.TestValueTwo);
        _ = newValues.Should().Equal(TestData.TestValueTwenty);
        _ = propertyNames.Should().Equal(TestData.IndexerPropertyName);
    }

    /// <summary>Bulk operations on the UI-facing Items collection should coalesce to one collection notification.</summary>
    [Test]
    public void BulkOperations_ShouldRaiseSingleItemsCollectionChangedNotification()
    {
        using var list = new ReactiveList<int>();
        var itemEvents = new List<NotifyCollectionChangedEventArgs>();
        ((INotifyCollectionChanged)list.Items).CollectionChanged += (_, args) => itemEvents.Add(args);

        var values = new[] { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour };
        list.AddRange(values.AsSpan());

        _ = itemEvents.Should().ContainSingle();
        _ = itemEvents[0].Action.Should().Be(NotifyCollectionChangedAction.Reset);

        itemEvents.Clear();
        list.Remove([1, TestData.TestValueThree]);
        _ = itemEvents.Should().ContainSingle();
        _ = itemEvents[0].Action.Should().Be(NotifyCollectionChangedAction.Reset);

        itemEvents.Clear();
        _ = list.RemoveMany(static item => item > 0);
        _ = itemEvents.Should().ContainSingle();
        _ = itemEvents[0].Action.Should().Be(NotifyCollectionChangedAction.Reset);
    }

    /// <summary>ReplaceAll to empty should not suppress tracking for the following notification.</summary>
    [Test]
    public void ReplaceAllToEmpty_ShouldNotSuppressNextNotification()
    {
        using var list = new ReactiveList<string>(["seed"]);
        var snapshots = new List<string[]>();
        using var subscription = list.CurrentItems.Subscribe(items => AddSnapshot(snapshots, items));

        list.ReplaceAll([]);
        list.Add("next");

        _ = list.ItemsAdded.Should().Equal("next");
        _ = list.ItemsChanged.Should().Equal("next");
        _ = snapshots[snapshots.Count - 1].Should().Equal("next");
    }

    /// <summary>Dynamic views should not block construction when no initial filter has been published.</summary>
    [Test]
    public void DynamicReactiveView_WithColdFilterSubject_ShouldConstructImmediately()
    {
        using var source = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree]);
        using var filters = new Signal<Func<int, bool>>();

        using var view = new DynamicReactiveView<int>(
            source,
            filters,
            TimeSpan.Zero,
            Sequencer.Immediate);

        _ = view.Items.Should().Equal(1, TestData.TestValueTwo, TestData.TestValueThree);
        filters.OnNext(static item => item > 1);
        _ = view.Items.Should().Equal(TestData.TestValueTwo, TestData.TestValueThree);
    }

#if NET8_0_OR_GREATER || NETFRAMEWORK

    /// <summary>Dynamic secondary-index views should not block construction without an initial key emission.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DynamicSecondaryIndexView_WithColdKeysSubject_ShouldConstructImmediately()
    {
        using var source = new QuaternaryList<IndexedItem>();
        var north = new IndexedItem(1, "north");
        source.Add(north);
        source.AddIndex("region", static item => item.Region);
        using var keys = new Signal<string[]>();

        using var view = new DynamicSecondaryIndexReactiveView<IndexedItem, string>(
            source,
            "region",
            keys,
            Sequencer.Immediate,
            TimeSpan.Zero);

        _ = view.Items.Should().BeEmpty();
        keys.OnNext(["north"]);
        await Task.Delay(TestData.TestValueTwentyFive);
        _ = view.Items.Should().ContainSingle().Which.Should().Be(north);
    }

    /// <summary>Quaternary collections should raise INPC notifications for UI-bound count/indexer properties.</summary>
    [Test]
    public void QuaternaryCollections_ShouldRaisePropertyChangedForMutations()
    {
        using var list = new QuaternaryList<int>();
        var listProperties = new List<string?>();
        ((INotifyPropertyChanged)list).PropertyChanged += (_, args) => listProperties.Add(args.PropertyName);

        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree]);

        _ = listProperties.Should().Contain(nameof(list.Count));
        _ = listProperties.Should().Contain(TestData.IndexerPropertyName);

        using var dictionary = new QuaternaryDictionary<int, string>();
        var dictionaryProperties = new List<string?>();
        ((INotifyPropertyChanged)dictionary).PropertyChanged += (_, args) => dictionaryProperties.Add(args.PropertyName);

        dictionary.AddRange([new KeyValuePair<int, string>(1, "one")]);

        _ = dictionaryProperties.Should().Contain(nameof(dictionary.Count));
        _ = dictionaryProperties.Should().Contain(TestData.IndexerPropertyName);
    }

    /// <summary>Optimized quaternary list range removal should preserve multiset semantics for duplicate values.</summary>
    [Test]
    public void QuaternaryList_RemoveRange_ShouldRemoveOnlyRequestedDuplicateCount()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 1, 1, TestData.TestValueTwo, TestData.TestValueThree]);

        list.RemoveRange([1, 1, TestData.TestValueFour]);

        _ = list.Count.Should().Be(TestData.TestValueThree);
        _ = list.ToArray().Should().BeEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree]);
    }

    /// <summary>Dictionary range operations should keep count exact for overwrites and no-op removals.</summary>
    [Test]
    public void QuaternaryDictionary_RangeOperations_ShouldMaintainCountAndSkipNoOpRemoveNotification()
    {
        using var dictionary = new QuaternaryDictionary<int, string>();
        var notifications = 0;
        using var received = new ManualResetEventSlim();
        using var subscription = dictionary.Stream.Subscribe(notification =>
        {
            _ = notification;
            _ = Interlocked.Increment(ref notifications);
            received.Set();
        });

        dictionary.AddRange(
        [
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(1, "uno"),
            new KeyValuePair<int, string>(TestData.TestValueTwo, "two")
        ]);

        _ = dictionary.Count.Should().Be(TestData.TestValueTwo);
        _ = received.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        _ = notifications.Should().Be(1);

        received.Reset();
        dictionary.RemoveKeys([TestData.TestValueNinetyNine]);
        _ = dictionary.Count.Should().Be(TestData.TestValueTwo);
        _ = received.Wait(TimeSpan.FromMilliseconds(TestData.TestValueFifty)).Should().BeFalse();
        _ = notifications.Should().Be(1);

        received.Reset();
        dictionary.RemoveKeys([1]);
        _ = dictionary.Count.Should().Be(1);
        _ = received.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        _ = notifications.Should().Be(TestData.TestValueTwo);
    }
#endif

    /// <summary>Adds an explicit snapshot without LINQ allocation overhead.</summary>
    /// <param name="snapshots">The destination snapshot collection.</param>
    /// <param name="items">The items to snapshot.</param>
    private static void AddSnapshot(List<string[]> snapshots, IEnumerable<string> items)
    {
        var snapshot = new List<string>(items);
        snapshots.Add([.. snapshot]);
    }

    /// <summary>Provides IndexedItem.</summary>
    /// <param name="Id">The Id value.</param>
    /// <param name="Region">The Region value.</param>
    private sealed record IndexedItem(int Id, string Region);
}
