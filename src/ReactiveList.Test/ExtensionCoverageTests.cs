// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Tests;

/// <summary>Coverage tests for extension pipelines.</summary>
public class ExtensionCoverageTests
{
    /// <summary>Change-set operators should handle empty, partial, all-match, and projection paths.</summary>
    [Test]
    public void ChangeSetOperators_ShouldHandleEmptyNoMatchPartialAllAndPreviousValues()
    {
        var source = new Signal<ChangeSet<int>>();
        var filtered = new List<ChangeSet<int>>();

        using var filterSubscription = source
            .WhereChanges(static change => change.Current % 2 == 0)
            .Subscribe(filtered.Add);

        source.OnNext(ChangeSet<int>.Empty);
        source.OnNext(new ChangeSet<int>([Change<int>.CreateAdd(1), Change<int>.CreateAdd(3)]));
        source.OnNext(new ChangeSet<int>([Change<int>.CreateAdd(1), Change<int>.CreateAdd(2), Change<int>.CreateAdd(4)]));
        var allMatch = new ChangeSet<int>([Change<int>.CreateAdd(6), Change<int>.CreateAdd(8)]);
        source.OnNext(allMatch);

        filtered.Should().HaveCount(2);
        filtered[0].Select(static change => change.Current).Should().Equal(2, 4);
        filtered[1].Equals(allMatch).Should().BeTrue();

        Func<string, string> itemSelector = static item => $"value-{item}";
        var projectedSets = new List<ChangeSet<string>>();
        using var projectionSubscription = Signal.Emit(new ChangeSet<string>([
                Change<string>.CreateUpdate("twenty", "ten", 0),
                Change<string>.CreateAdd("thirty", 1),
            ]))
            .SelectChanges(itemSelector)
            .Subscribe(projectedSets.Add);

        projectedSets.Should().ContainSingle();
        projectedSets[0][0].Previous.Should().Be("value-ten");
        projectedSets[0][0].Current.Should().Be("value-twenty");
        projectedSets[0][1].Previous.Should().BeNull();

        Func<Change<int>, string> changeSelector = static change => $"{change.Reason}:{change.Current}";
        var flattened = new List<string>();
        using var flattenSubscription = Signal.Emit(new ChangeSet<int>([
                Change<int>.CreateRemove(5),
                Change<int>.CreateMove(6, 2, 0),
            ]))
            .SelectChanges(changeSelector)
            .Subscribe(flattened.Add);

        flattened.Should().Equal("Remove:5", "Move:6");

        var emptyFlattened = new List<int>();
        using var emptySubscription = Signal.Emit(ChangeSet<int>.Empty)
            .SelectChanges(static change => change.Current)
            .Subscribe(emptyFlattened.Add);

        emptyFlattened.Should().BeEmpty();
    }

    /// <summary>Change-set operators should reject null arguments.</summary>
    [Test]
    public void ChangeSetOperators_WithNullArguments_ShouldThrow()
    {
        IObservable<ChangeSet<int>> nullSource = null!;

        var whereSource = () => nullSource.WhereChanges(static _ => true);
        var wherePredicate = () => ReactiveListExtensions.WhereChanges(Signal.None<ChangeSet<int>>(), null!);
        var selectSource = () => ReactiveListExtensions.SelectChanges(nullSource, (Func<int, string>)(static item => item.ToString()));
        var selectItemSelector = () => ReactiveListExtensions.SelectChanges(Signal.None<ChangeSet<int>>(), (Func<int, string>)null!);
        var selectChangeSelector = () => ReactiveListExtensions.SelectChanges(Signal.None<ChangeSet<int>>(), (Func<Change<int>, string>)null!);

        whereSource.Should().Throw<ArgumentNullException>().WithParameterName("source");
        wherePredicate.Should().Throw<ArgumentNullException>().WithParameterName("predicate");
        selectSource.Should().Throw<ArgumentNullException>().WithParameterName("source");
        selectItemSelector.Should().Throw<ArgumentNullException>().WithParameterName("selector");
        selectChangeSelector.Should().Throw<ArgumentNullException>().WithParameterName("selector");
    }

    /// <summary>Generic dynamic stream filters should handle single, batch, remove, and clear notifications.</summary>
    [Test]
    public void FilterDynamic_GenericStream_ShouldFilterAddsBatchesAndPassRemovesAndClears()
    {
        using var stream = new Signal<CacheNotify<int>>();
        using var filters = new BehaviorSignal<Func<int, bool>>(static item => item % 2 == 0);
        var received = new List<CacheNotify<int>>();

        using var subscription = stream
            .FilterDynamic(filters)
            .Subscribe(received.Add);

        stream.OnNext(new CacheNotify<int>(CacheAction.Added, 2));
        stream.OnNext(new CacheNotify<int>(CacheAction.Added, 3));
        stream.OnNext(new CacheNotify<int>(CacheAction.Removed, 3));
        stream.OnNext(new CacheNotify<int>(CacheAction.BatchOperation, default, CreateBatch(4, 5, 6)));
        stream.OnNext(new CacheNotify<int>(CacheAction.BatchOperation, default, CreateBatch(5, 7)));
        stream.OnNext(new CacheNotify<int>(CacheAction.Cleared, default));

        received.Select(static notification => notification.Action)
            .Should().Equal(CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation, CacheAction.Cleared);
        received[0].Item.Should().Be(2);
        received[1].Item.Should().Be(3);
        received[2].Batch.Should().NotBeNull();
        var genericBatch = received[2].Batch!;
        genericBatch.Items.Take(genericBatch.Count).Should().Equal(4, 6);
        received[3].Action.Should().Be(CacheAction.Cleared);

        DisposeBatches(received);
    }

    /// <summary>Dictionary dynamic stream filters should handle single, batch, remove, and clear notifications.</summary>
    [Test]
    public void FilterDynamic_DictionaryStream_ShouldFilterAddsBatchesAndPassRemoves()
    {
        using var stream = new Signal<CacheNotify<KeyValuePair<int, string>>>();
        using var filters = new BehaviorSignal<Func<KeyValuePair<int, string>, bool>>(static item => item.Value.StartsWith("a", StringComparison.Ordinal));
        var received = new List<CacheNotify<KeyValuePair<int, string>>>();

        using var subscription = stream
            .FilterDynamic(filters)
            .Subscribe(received.Add);

        stream.OnNext(new CacheNotify<KeyValuePair<int, string>>(CacheAction.Added, new KeyValuePair<int, string>(1, "alpha")));
        stream.OnNext(new CacheNotify<KeyValuePair<int, string>>(CacheAction.Added, new KeyValuePair<int, string>(2, "beta")));
        stream.OnNext(new CacheNotify<KeyValuePair<int, string>>(CacheAction.Removed, new KeyValuePair<int, string>(2, "beta")));
        stream.OnNext(new CacheNotify<KeyValuePair<int, string>>(CacheAction.BatchAdded, default, CreateBatch(
            new KeyValuePair<int, string>(3, "atlas"),
            new KeyValuePair<int, string>(4, "beta"))));
        stream.OnNext(new CacheNotify<KeyValuePair<int, string>>(CacheAction.BatchRemoved, default, CreateBatch(
            new KeyValuePair<int, string>(5, "apex"),
            new KeyValuePair<int, string>(6, "cedar"))));
        stream.OnNext(new CacheNotify<KeyValuePair<int, string>>(CacheAction.Cleared, default));

        received.Select(static notification => notification.Action)
            .Should().Equal(CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation, CacheAction.BatchOperation, CacheAction.Cleared);
        received[0].Item.Value.Should().Be("alpha");
        received[1].Item.Value.Should().Be("beta");
        var addedBatch = received[2].Batch!;
        var removedBatch = received[3].Batch!;
        addedBatch.Items.Take(addedBatch.Count).Should().ContainSingle().Which.Value.Should().Be("atlas");
        removedBatch.Items.Take(removedBatch.Count).Should().ContainSingle().Which.Value.Should().Be("apex");

        DisposeBatches(received);
    }

    /// <summary>Internal batch filter helpers should handle null, empty, and matching results.</summary>
    [Test]
    public void BatchFilterHelpers_ShouldReturnNullForNoBatchOrNoMatchesAndFilterMatches()
    {
        var noBatch = new CacheNotify<int>(CacheAction.BatchOperation, default);
        ReactiveListExtensions.FilterBatchByPredicate(noBatch, static _ => true).Should().BeNull();
        ReactiveListExtensions.FilterBatch(noBatch, [1]).Should().BeNull();

        var noMatch = new CacheNotify<int>(CacheAction.BatchOperation, default, CreateBatch(1, 3, 5));
        ReactiveListExtensions.FilterBatchByPredicate(noMatch, static item => item % 2 == 0).Should().BeNull();
        noMatch.Batch!.Dispose();

        var predicateMatch = new CacheNotify<int>(CacheAction.BatchOperation, default, CreateBatch(1, 2, 4));
        var predicateResult = ReactiveListExtensions.FilterBatchByPredicate(predicateMatch, static item => item > 1);
        predicateResult.Should().NotBeNull();
        predicateResult!.Batch!.Items.Take(predicateResult.Batch.Count).Should().Equal(2, 4);
        predicateMatch.Batch!.Dispose();
        predicateResult.Batch.Dispose();

        var setMatch = new CacheNotify<int>(CacheAction.BatchOperation, default, CreateBatch(1, 2, 3));
        var setResult = ReactiveListExtensions.FilterBatch(setMatch, [1, 3]);
        setResult.Should().NotBeNull();
        setResult!.Batch!.Items.Take(setResult.Batch.Count).Should().Equal(1, 3);
        setMatch.Batch!.Dispose();
        setResult.Batch.Dispose();
    }

    /// <summary>Grouping and auto-refresh operators should emit grouped changes and property refreshes.</summary>
    [Test]
    public void GroupingAndAutoRefresh_ShouldGroupChangesAndEmitPropertyRefreshes()
    {
        var north = new MutableItem(1, "north", "alpha");
        var south = new MutableItem(2, "south", "beta");
        var changes = new ChangeSet<MutableItem>([
            Change<MutableItem>.CreateAdd(north),
            Change<MutableItem>.CreateAdd(south),
            Change<MutableItem>.CreateUpdate(north, north),
        ]);

        var groupings = new List<IGrouping<string, Change<MutableItem>>>();
        using var groupingSubscription = Signal.Emit(changes)
            .GroupingByChanges(static item => item.Region)
            .Subscribe(groupings.Add);

        groupings.Should().HaveCount(2);
        groupings.Single(static group => group.Key == "north").Should().HaveCount(2);
        groupings.Single(static group => group.Key == "south").Should().ContainSingle();

        var groupedValues = new Dictionary<string, List<MutableItem>>();
        using var groupBySubscription = Signal.Emit(changes)
            .GroupByChanges(static item => item.Region)
            .Subscribe(group =>
            {
                groupedValues[group.Key] = [];
                group.Subscribe(item => groupedValues[group.Key].Add(item));
            });

        groupedValues["north"].Should().HaveCount(2);
        groupedValues["south"].Should().ContainSingle().Which.Should().Be(south);

        using var refreshSource = new Signal<ChangeSet<MutableItem>>();
        var received = new List<ChangeSet<MutableItem>>();
        using var refreshSubscription = refreshSource
            .AutoRefresh(nameof(MutableItem.Name))
            .Subscribe(received.Add);

        refreshSource.OnNext(new ChangeSet<MutableItem>(Change<MutableItem>.CreateAdd(north, 0)));
        north.RaisePropertyChanged(nameof(MutableItem.Region));
        north.RaisePropertyChanged(nameof(MutableItem.Name));

        received.Should().HaveCount(2);
        received[0][0].Reason.Should().Be(ChangeReason.Add);
        received[1][0].Reason.Should().Be(ChangeReason.Refresh);
        received[1][0].Current.Should().Be(north);
        received[1][0].CurrentIndex.Should().Be(0);

        var allProperties = new List<ChangeSet<MutableItem>>();
        using var allSubscription = refreshSource
            .AutoRefresh(null)
            .Subscribe(allProperties.Add);

        refreshSource.OnNext(new ChangeSet<MutableItem>(Change<MutableItem>.CreateUpdate(south, south, 1)));
        south.RaisePropertyChanged(nameof(MutableItem.Region));

        allProperties.Should().HaveCount(2);
        allProperties[1][0].Reason.Should().Be(ChangeReason.Refresh);
    }

    /// <summary>Source auto-refresh expression overload should validate property expressions and return the source stream.</summary>
    [Test]
    public void AutoRefresh_SourceExpression_ShouldValidatePropertyAndReturnSourceStream()
    {
        using var list = new ReactiveList<MutableItem>();
        var received = new List<CacheNotify<MutableItem>>();

        using var subscription = list
            .AutoRefresh(static item => item.Name)
            .Subscribe(received.Add);

        var item = new MutableItem(1, "north", "alpha");
        list.Add(item);

        received.Should().ContainSingle();
        received[0].Action.Should().Be(CacheAction.Added);

        var invalidExpression = () => list.AutoRefresh(static _ => new object());
        invalidExpression.Should().Throw<ArgumentException>().WithParameterName("property");
    }

    /// <summary>View factory extensions should create filtered, sorted, grouped, and dynamic views.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ViewFactoryExtensions_ShouldCreateViewsWithFallbackSchedulersAndDynamicFilters()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([3, 1, 2]);

        using var filtered = list.CreateView(static item => item > 1, scheduler: null, throttleMs: 0);
        filtered.Items.Should().BeEquivalentTo([2, 3]);

        using var dynamicFilters = new BehaviorSignal<Func<int, bool>>(static item => item == 1);
        using var dynamicFiltered = list.CreateView(dynamicFilters, scheduler: null, throttleMs: 0);
        await WaitForPipeline();
        dynamicFiltered.Items.Should().Equal(1);

        using var sorted = list.SortBy(static item => item, descending: true, scheduler: null, throttleMs: 0);
        sorted.Items.Should().Equal(3, 2, 1);

        using var grouped = list.GroupBy(static item => item % 2, scheduler: null, throttleMs: 0);
        grouped.Keys.Should().BeEquivalentTo([0, 1]);

#if NET8_0_OR_GREATER || NETFRAMEWORK
        using var quaternary = new QuaternaryList<string>();
        quaternary.Add("apple");
        quaternary.Add("banana");

        using var query = new BehaviorSignal<string>("app");
        using var queryView = quaternary.CreateView(
            query,
            static (queryText, item) => item.StartsWith(queryText, StringComparison.Ordinal),
            Sequencer.Immediate,
            throttleMs: 0);
        queryView.Items.Should().Equal("apple");

        using var sourceFilters = new BehaviorSignal<Func<string, bool>>(static item => item.Contains("a", StringComparison.Ordinal));
        using var sourceView = quaternary.CreateView(sourceFilters, Sequencer.Immediate, throttleMs: 0);
        sourceView.Items.Should().BeEquivalentTo(["apple", "banana"]);
#endif
    }

#if NET8_0_OR_GREATER || NETFRAMEWORK

    /// <summary>Quaternary list secondary-index filters should pass matching single and batch notifications.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task QuaternaryListSecondaryIndexFilter_ShouldPassMatchingSingleAndBatchNotifications()
    {
        using var list = new QuaternaryList<IndexedItem>();
        list.AddIndex("region", static item => item.Region);
        var singleKey = new List<CacheNotify<IndexedItem>>();
        var multipleKeys = new List<CacheNotify<IndexedItem>>();

        using var singleSubscription = list.Stream
            .FilterBySecondaryIndex(list, "region", "north")
            .Subscribe(singleKey.Add);
        using var multipleSubscription = list.Stream
            .FilterBySecondaryIndex(list, "region", "north", "east")
            .Subscribe(multipleKeys.Add);

        var north = new IndexedItem(1, "north");
        var east = new IndexedItem(2, "east");
        var south = new IndexedItem(3, "south");

        list.Add(north);
        list.Add(east);
        list.Add(south);
        list.Remove(north);

        var northBatch = new IndexedItem(4, "north");
        var eastBatch = new IndexedItem(5, "east");
        var southBatch = new IndexedItem(6, "south");
        list.AddRange([northBatch, eastBatch, southBatch]);
        list.RemoveRange([northBatch, eastBatch, southBatch]);

        await WaitForPipeline();

        singleKey.Select(static notification => notification.Action)
            .Should().Equal(CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation, CacheAction.BatchOperation);
        singleKey[0].Item.Should().Be(north);
        singleKey[1].Item.Should().Be(north);
        var singleAddedBatch = singleKey[2].Batch!;
        var singleRemovedBatch = singleKey[3].Batch!;
        singleAddedBatch.Items.Take(singleAddedBatch.Count).Should().ContainSingle().Which.Should().Be(northBatch);
        singleRemovedBatch.Items.Take(singleRemovedBatch.Count).Should().ContainSingle().Which.Should().Be(northBatch);

        multipleKeys.Select(static notification => notification.Action)
            .Should().Equal(
                CacheAction.Added,
                CacheAction.Added,
                CacheAction.Removed,
                CacheAction.BatchOperation,
                CacheAction.BatchOperation);
        var multipleAddedBatch = multipleKeys[3].Batch!;
        var multipleRemovedBatch = multipleKeys[4].Batch!;
        multipleAddedBatch.Items.Take(multipleAddedBatch.Count).Should().BeEquivalentTo([northBatch, eastBatch]);
        multipleRemovedBatch.Items.Take(multipleRemovedBatch.Count).Should().BeEquivalentTo([northBatch, eastBatch]);

        DisposeBatches(singleKey);
        DisposeBatches(multipleKeys);
    }

    /// <summary>Quaternary dictionary secondary-index filters should pass matching single and batch notifications.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task QuaternaryDictionarySecondaryIndexFilter_ShouldPassMatchingSingleAndBatchNotifications()
    {
        using var dictionary = new QuaternaryDictionary<int, IndexedItem>();
        dictionary.AddValueIndex("region", static item => item.Region);
        var singleKey = new List<CacheNotify<KeyValuePair<int, IndexedItem>>>();
        var multipleKeys = new List<CacheNotify<KeyValuePair<int, IndexedItem>>>();

        using var singleSubscription = dictionary.Stream
            .FilterBySecondaryIndex(dictionary, "region", "north")
            .Subscribe(singleKey.Add);
        using var multipleSubscription = dictionary.Stream
            .FilterBySecondaryIndex(dictionary, "region", "north", "east")
            .Subscribe(multipleKeys.Add);

        var north = new IndexedItem(1, "north");
        var east = new IndexedItem(2, "east");
        var south = new IndexedItem(3, "south");

        dictionary.Add(1, north);
        dictionary.Add(2, east);
        dictionary.Add(3, south);
        dictionary.Remove(1);

        var northBatch = new KeyValuePair<int, IndexedItem>(4, new IndexedItem(4, "north"));
        var eastBatch = new KeyValuePair<int, IndexedItem>(5, new IndexedItem(5, "east"));
        var southBatch = new KeyValuePair<int, IndexedItem>(6, new IndexedItem(6, "south"));
        dictionary.AddRange([northBatch, eastBatch, southBatch]);

        await WaitForPipeline();

        singleKey.Select(static notification => notification.Action)
            .Should().Equal(CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation);
        singleKey[0].Item.Value.Should().Be(north);
        singleKey[1].Item.Value.Should().Be(north);
        var dictionarySingleBatch = singleKey[2].Batch!;
        dictionarySingleBatch.Items.Take(dictionarySingleBatch.Count).Should().ContainSingle().Which.Should().Be(northBatch);

        multipleKeys.Select(static notification => notification.Action)
            .Should().Equal(CacheAction.Added, CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation);
        var dictionaryMultipleBatch = multipleKeys[3].Batch!;
        dictionaryMultipleBatch.Items.Take(dictionaryMultipleBatch.Count).Should().BeEquivalentTo([northBatch, eastBatch]);

        DisposeBatches(singleKey);
        DisposeBatches(multipleKeys);
    }
#endif

    /// <summary>Provides WaitForPipeline.</summary>
    /// <returns>The result.</returns>
    private static async Task WaitForPipeline() => await Task.Delay(30);

    /// <summary>Provides CreateBatch.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="items">The items value.</param>
    /// <returns>The result.</returns>
    private static PooledBatch<T> CreateBatch<T>(params T[] items)
    {
        var array = ArrayPool<T>.Shared.Rent(items.Length);
        Array.Copy(items, array, items.Length);
        return new PooledBatch<T>(array, items.Length);
    }

    /// <summary>Provides DisposeBatches.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="notifications">The notifications value.</param>
    private static void DisposeBatches<T>(IEnumerable<CacheNotify<T>> notifications)
    {
        foreach (var batch in notifications.Select(static notification => notification.Batch).Where(static batch => batch is not null))
        {
            batch!.Dispose();
        }
    }

    /// <summary>Provides MutableItem.</summary>
    private sealed class MutableItem : INotifyPropertyChanged
    {
        /// <summary>Initializes a new instance of the <see cref="MutableItem"/> class.</summary>
        /// <param name="id">The id value.</param>
        /// <param name="region">The region value.</param>
        /// <param name="name">The name value.</param>
        public MutableItem(int id, string region, string name)
        {
            Id = id;
            Region = region;
            Name = name;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Gets Id.</summary>
        public int Id { get; }

        /// <summary>Gets Name.</summary>
        public string Name { get; }

        /// <summary>Gets Region.</summary>
        public string Region { get; }

        /// <summary>Provides RaisePropertyChanged.</summary>
        /// <param name="propertyName">The propertyName value.</param>
        public void RaisePropertyChanged(string? propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>Provides IndexedItem.</summary>
    /// <param name="Id">The Id value.</param>
    /// <param name="Region">The Region value.</param>
    private sealed record IndexedItem(int Id, string Region);
}
