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
    /// <summary>The coverage value two.</summary>
    private const int CoverageValueTwo = 2;

    /// <summary>The coverage value three.</summary>
    private const int CoverageValueThree = 3;

    /// <summary>The coverage value four.</summary>
    private const int CoverageValueFour = 4;

    /// <summary>The coverage value five.</summary>
    private const int CoverageValueFive = 5;

    /// <summary>The coverage value six.</summary>
    private const int CoverageValueSix = 6;

    /// <summary>The coverage value seven.</summary>
    private const int CoverageValueSeven = 7;

    /// <summary>The coverage value eight.</summary>
    private const int CoverageValueEight = 8;

    /// <summary>The coverage timeout milliseconds.</summary>
    private const int CoverageTimeoutMilliseconds = 30;

    /// <summary>The alpha item.</summary>
    private const string AlphaItem = "alpha";

    /// <summary>The north region.</summary>
    private const string NorthRegion = "north";

    /// <summary>The south region.</summary>
    private const string SouthRegion = "south";

    /// <summary>The apple item.</summary>
    private const string AppleItem = "apple";

    /// <summary>The region property name.</summary>
    private const string RegionPropertyName = "region";

    /// <summary>Change-set operators should handle empty, partial, all-match, and projection paths.</summary>
    [Test]
    public void ChangeSetOperators_ShouldHandleEmptyNoMatchPartialAllAndPreviousValues()
    {
        using var source = new Signal<ChangeSet<int>>();
        var filtered = new List<ChangeSet<int>>();

        using var filterSubscription = source
            .WhereChanges(static change => change.Current % CoverageValueTwo == 0)
            .Subscribe(filtered.Add);

        source.OnNext(ChangeSet<int>.Empty);
        source.OnNext(new([Change<int>.CreateAdd(1), Change<int>.CreateAdd(CoverageValueThree)]));
        source.OnNext(new([Change<int>.CreateAdd(1), Change<int>.CreateAdd(CoverageValueTwo), Change<int>.CreateAdd(CoverageValueFour)]));
        var allMatch = new ChangeSet<int>([Change<int>.CreateAdd(CoverageValueSix), Change<int>.CreateAdd(CoverageValueEight)]);
        source.OnNext(allMatch);

        _ = filtered.Should().HaveCount(CoverageValueTwo);
        _ = GetCurrentValues(filtered[0]).Should().Equal(CoverageValueTwo, CoverageValueFour);
        _ = filtered[1].Equals(allMatch).Should().BeTrue();

        Func<string, string> itemSelector = static item => $"value-{item}";
        var projectedSets = new List<ChangeSet<string>>();
        using var projectionSubscription = Signal.Emit(new ChangeSet<string>([
                Change<string>.CreateUpdate("twenty", "ten", 0),
                Change<string>.CreateAdd("thirty", 1),
            ]))
            .SelectChanges(itemSelector)
            .Subscribe(projectedSets.Add);

        _ = projectedSets.Should().ContainSingle();
        _ = projectedSets[0][0].Previous.Should().Be("value-ten");
        _ = projectedSets[0][0].Current.Should().Be("value-twenty");
        _ = projectedSets[0][1].Previous.Should().BeNull();

        Func<Change<int>, string> changeSelector = static change => $"{change.Reason}:{change.Current}";
        var flattened = new List<string>();
        using var flattenSubscription = Signal.Emit(new ChangeSet<int>([
                Change<int>.CreateRemove(CoverageValueFive),
                Change<int>.CreateMove(CoverageValueSix, CoverageValueTwo, 0),
            ]))
            .SelectChanges(changeSelector)
            .Subscribe(flattened.Add);

        _ = flattened.Should().Equal("Remove:5", "Move:6");

        var emptyFlattened = new List<int>();
        using var emptySubscription = Signal.Emit(ChangeSet<int>.Empty)
            .SelectChanges(static change => change.Current)
            .Subscribe(emptyFlattened.Add);

        _ = emptyFlattened.Should().BeEmpty();
    }

    /// <summary>Change-set operators should reject null arguments.</summary>
    [Test]
    public void ChangeSetOperators_WithNullArguments_ShouldThrow()
    {
        IObservable<ChangeSet<int>> nullSource = null!;

        var whereSource = () => nullSource.WhereChanges(static _ => true);
        var wherePredicate = static () => ReactiveListExtensions.WhereChanges(Signal.None<ChangeSet<int>>(), null!);
        var selectSource = () => ReactiveListExtensions.SelectChanges(nullSource, (Func<int, string>)(static item => item.ToString()));
        var selectItemSelector = static () => ReactiveListExtensions.SelectChanges(Signal.None<ChangeSet<int>>(), (Func<int, string>)null!);
        var selectChangeSelector = static () => ReactiveListExtensions.SelectChanges(Signal.None<ChangeSet<int>>(), (Func<Change<int>, string>)null!);

        _ = whereSource.Should().Throw<ArgumentNullException>().WithParameterName("source");
        _ = wherePredicate.Should().Throw<ArgumentNullException>().WithParameterName("predicate");
        _ = selectSource.Should().Throw<ArgumentNullException>().WithParameterName("source");
        _ = selectItemSelector.Should().Throw<ArgumentNullException>().WithParameterName("selector");
        _ = selectChangeSelector.Should().Throw<ArgumentNullException>().WithParameterName("selector");
    }

    /// <summary>Generic dynamic stream filters should handle single, batch, remove, and clear notifications.</summary>
    [Test]
    public void FilterDynamic_GenericStream_ShouldFilterAddsBatchesAndPassRemovesAndClears()
    {
        using var stream = new Signal<CacheNotify<int>>();
        using var filters = new BehaviorSignal<Func<int, bool>>(static item => item % CoverageValueTwo == 0);
        var received = new List<CacheNotify<int>>();

        using var subscription = stream
            .FilterDynamic(filters)
            .Subscribe(received.Add);

        stream.OnNext(new(CacheAction.Added, CoverageValueTwo));
        stream.OnNext(new(CacheAction.Added, CoverageValueThree));
        stream.OnNext(new(CacheAction.Removed, CoverageValueThree));
        stream.OnNext(new(CacheAction.BatchOperation, default, CreateBatch(CoverageValueFour, CoverageValueFive, CoverageValueSix)));
        stream.OnNext(new(CacheAction.BatchOperation, default, CreateBatch(CoverageValueFive, CoverageValueSeven)));
        stream.OnNext(new(CacheAction.Cleared, default));

        _ = GetActions(received).Should().Equal(CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation, CacheAction.Cleared);
        _ = received[0].Item.Should().Be(CoverageValueTwo);
        _ = received[1].Item.Should().Be(CoverageValueThree);
        _ = received[CoverageValueTwo].Batch.Should().NotBeNull();
        var genericBatch = received[CoverageValueTwo].Batch!;
        _ = CopyBatchItems(genericBatch).Should().Equal(CoverageValueFour, CoverageValueSix);
        _ = received[CoverageValueThree].Action.Should().Be(CacheAction.Cleared);

        DisposeBatches(received);
    }

    /// <summary>Dictionary dynamic stream filters should handle single, batch, remove, and clear notifications.</summary>
    [Test]
    public void FilterDynamic_DictionaryStream_ShouldFilterAddsBatchesAndPassRemoves()
    {
        using var stream = new Signal<CacheNotify<KeyValuePair<int, string>>>();
        using var filters = new BehaviorSignal<Func<KeyValuePair<int, string>, bool>>(static item => item.Value.Length > 0 && item.Value[0] == 'a');
        var received = new List<CacheNotify<KeyValuePair<int, string>>>();

        using var subscription = stream
            .FilterDynamic(filters)
            .Subscribe(received.Add);

        stream.OnNext(new(CacheAction.Added, new(1, AlphaItem)));
        stream.OnNext(new(CacheAction.Added, new(CoverageValueTwo, "beta")));
        stream.OnNext(new(CacheAction.Removed, new(CoverageValueTwo, "beta")));
        stream.OnNext(new(CacheAction.BatchAdded, default, CreateBatch<KeyValuePair<int, string>>(
            new(CoverageValueThree, "atlas"),
            new(CoverageValueFour, "beta"))));
        stream.OnNext(new(CacheAction.BatchRemoved, default, CreateBatch<KeyValuePair<int, string>>(
            new(CoverageValueFive, "apex"),
            new(CoverageValueSix, "cedar"))));
        stream.OnNext(new(CacheAction.Cleared, default));

        _ = GetActions(received).Should().Equal(
            CacheAction.Added,
            CacheAction.Removed,
            CacheAction.BatchOperation,
            CacheAction.BatchOperation,
            CacheAction.Cleared);
        _ = received[0].Item.Value.Should().Be(AlphaItem);
        _ = received[1].Item.Value.Should().Be("beta");
        var addedBatch = received[CoverageValueTwo].Batch!;
        var removedBatch = received[CoverageValueThree].Batch!;
        _ = CopyBatchItems(addedBatch).Should().ContainSingle().Which.Value.Should().Be("atlas");
        _ = CopyBatchItems(removedBatch).Should().ContainSingle().Which.Value.Should().Be("apex");

        DisposeBatches(received);
    }

    /// <summary>Internal batch filter helpers should handle null, empty, and matching results.</summary>
    [Test]
    public void BatchFilterHelpers_ShouldReturnNullForNoBatchOrNoMatchesAndFilterMatches()
    {
        var noBatch = new CacheNotify<int>(CacheAction.BatchOperation, default);
        _ = ReactiveListExtensions.FilterBatchByPredicate(noBatch, static _ => true).Should().BeNull();
        _ = ReactiveListExtensions.FilterBatch(noBatch, [1]).Should().BeNull();

        var noMatch = new CacheNotify<int>(CacheAction.BatchOperation, default, CreateBatch(1, CoverageValueThree, CoverageValueFive));
        _ = ReactiveListExtensions.FilterBatchByPredicate(noMatch, static item => item % CoverageValueTwo == 0).Should().BeNull();
        noMatch.Batch!.Dispose();

        var predicateMatch = new CacheNotify<int>(CacheAction.BatchOperation, default, CreateBatch(1, CoverageValueTwo, CoverageValueFour));
        var predicateResult = ReactiveListExtensions.FilterBatchByPredicate(predicateMatch, static item => item > 1);
        _ = predicateResult.Should().NotBeNull();
        _ = CopyBatchItems(predicateResult!.Batch!).Should().Equal(CoverageValueTwo, CoverageValueFour);
        predicateMatch.Batch!.Dispose();
        predicateResult.Batch!.Dispose();

        var setMatch = new CacheNotify<int>(CacheAction.BatchOperation, default, CreateBatch(1, CoverageValueTwo, CoverageValueThree));
        var setResult = ReactiveListExtensions.FilterBatch(setMatch, [1, CoverageValueThree]);
        _ = setResult.Should().NotBeNull();
        _ = CopyBatchItems(setResult!.Batch!).Should().Equal(1, CoverageValueThree);
        setMatch.Batch!.Dispose();
        setResult.Batch!.Dispose();
    }

    /// <summary>Grouping and auto-refresh operators should emit grouped changes and property refreshes.</summary>
    [Test]
    public void GroupingAndAutoRefresh_ShouldGroupChangesAndEmitPropertyRefreshes()
    {
        var north = new MutableItem(NorthRegion, AlphaItem);
        var south = new MutableItem(SouthRegion, "beta");
        var changes = new ChangeSet<MutableItem>([
            Change<MutableItem>.CreateAdd(north),
            Change<MutableItem>.CreateAdd(south),
            Change<MutableItem>.CreateUpdate(north, north),
        ]);

        var groupings = new List<IGrouping<string, Change<MutableItem>>>();
        using var groupingSubscription = Signal.Emit(changes)
            .GroupingByChanges(static item => item.Region)
            .Subscribe(groupings.Add);

        _ = groupings.Should().HaveCount(CoverageValueTwo);
        _ = FindGrouping(groupings, NorthRegion).Should().HaveCount(CoverageValueTwo);
        _ = FindGrouping(groupings, SouthRegion).Should().ContainSingle();

        var groupedValues = new Dictionary<string, List<MutableItem>>();
        using var groupBySubscription = Signal.Emit(changes)
            .GroupByChanges(static item => item.Region)
            .Subscribe(group =>
            {
                var valuesForGroup = new List<MutableItem>();
                groupedValues[group.Key] = valuesForGroup;
                _ = group.Subscribe(valuesForGroup.Add);
            });

        _ = groupedValues[NorthRegion].Should().HaveCount(CoverageValueTwo);
        _ = groupedValues[SouthRegion].Should().ContainSingle().Which.Should().Be(south);

        using var refreshSource = new Signal<ChangeSet<MutableItem>>();
        var received = new List<ChangeSet<MutableItem>>();
        using var refreshSubscription = refreshSource
            .AutoRefresh(nameof(MutableItem.Name))
            .Subscribe(received.Add);

        refreshSource.OnNext(new(Change<MutableItem>.CreateAdd(north, 0)));
        north.RaisePropertyChanged(nameof(MutableItem.Region));
        north.RaisePropertyChanged(nameof(MutableItem.Name));

        _ = received.Should().HaveCount(CoverageValueTwo);
        _ = received[0][0].Reason.Should().Be(ChangeReason.Add);
        _ = received[1][0].Reason.Should().Be(ChangeReason.Refresh);
        _ = received[1][0].Current.Should().Be(north);
        _ = received[1][0].CurrentIndex.Should().Be(0);

        var allProperties = new List<ChangeSet<MutableItem>>();
        using var allSubscription = refreshSource
            .AutoRefresh(null)
            .Subscribe(allProperties.Add);

        refreshSource.OnNext(new(Change<MutableItem>.CreateUpdate(south, south, 1)));
        south.RaisePropertyChanged(nameof(MutableItem.Region));

        _ = allProperties.Should().HaveCount(CoverageValueTwo);
        _ = allProperties[1][0].Reason.Should().Be(ChangeReason.Refresh);
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

        var item = new MutableItem(NorthRegion, AlphaItem);
        list.Add(item);

        _ = received.Should().ContainSingle();
        _ = received[0].Action.Should().Be(CacheAction.Added);

        var invalidExpression = () => list.AutoRefresh(static _ => new object());
        _ = invalidExpression.Should().Throw<ArgumentException>().WithParameterName("property");
    }

    /// <summary>View factory extensions should create filtered, sorted, grouped, and dynamic views.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ViewFactoryExtensions_ShouldCreateViewsWithFallbackSchedulersAndDynamicFilters()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([CoverageValueThree, 1, CoverageValueTwo]);

        using var filtered = list.CreateView(static item => item > 1, scheduler: null, throttleMs: 0);
        _ = filtered.Items.Should().BeEquivalentTo([CoverageValueTwo, CoverageValueThree]);

        using var dynamicFilters = new BehaviorSignal<Func<int, bool>>(static item => item == 1);
        using var dynamicFiltered = list.CreateView(dynamicFilters, scheduler: null, throttleMs: 0);
        await WaitForPipeline();
        _ = dynamicFiltered.Items.Should().Equal(1);

        using var sorted = list.SortBy(static item => item, descending: true, scheduler: null, throttleMs: 0);
        _ = sorted.Items.Should().Equal(CoverageValueThree, CoverageValueTwo, 1);

        using var grouped = list.GroupBy(static item => item % CoverageValueTwo, scheduler: null, throttleMs: 0);
        _ = grouped.Keys.Should().BeEquivalentTo([0, 1]);

#if NET8_0_OR_GREATER || NETFRAMEWORK
        using var quaternary = new QuaternaryList<string> { AppleItem, "banana" };

        using var query = new BehaviorSignal<string>("app");
        using var queryView = quaternary.CreateView(
            query,
            static (queryText, item) => item.StartsWith(queryText, StringComparison.Ordinal),
            Sequencer.Immediate,
            throttleMs: 0);
        _ = queryView.Items.Should().Equal(AppleItem);

        using var sourceFilters = new BehaviorSignal<Func<string, bool>>(static item => item.Contains("a", StringComparison.Ordinal));
        using var sourceView = quaternary.CreateView(sourceFilters, Sequencer.Immediate, throttleMs: 0);
        _ = sourceView.Items.Should().BeEquivalentTo([AppleItem, "banana"]);
#endif
    }

#if NET8_0_OR_GREATER || NETFRAMEWORK

    /// <summary>Quaternary list secondary-index filters should pass matching single and batch notifications.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task QuaternaryListSecondaryIndexFilter_ShouldPassMatchingSingleAndBatchNotifications()
    {
        using var list = new QuaternaryList<IndexedItem>();
        list.AddIndex(RegionPropertyName, static item => item.Region);
        var singleKey = new List<CacheNotify<IndexedItem>>();
        var multipleKeys = new List<CacheNotify<IndexedItem>>();

        using var singleSubscription = list.Stream
            .FilterBySecondaryIndex(list, RegionPropertyName, NorthRegion)
            .Subscribe(singleKey.Add);
        using var multipleSubscription = list.Stream
            .FilterBySecondaryIndex(list, RegionPropertyName, NorthRegion, "east")
            .Subscribe(multipleKeys.Add);

        var north = new IndexedItem(1, NorthRegion);
        var east = new IndexedItem(CoverageValueTwo, "east");
        var south = new IndexedItem(CoverageValueThree, SouthRegion);

        list.Add(north);
        list.Add(east);
        list.Add(south);
        _ = list.Remove(north);

        var northBatch = new IndexedItem(CoverageValueFour, NorthRegion);
        var eastBatch = new IndexedItem(CoverageValueFive, "east");
        var southBatch = new IndexedItem(CoverageValueSix, SouthRegion);
        list.AddRange([northBatch, eastBatch, southBatch]);
        list.RemoveRange([northBatch, eastBatch, southBatch]);

        await WaitForPipeline();

        _ = GetActions(singleKey).Should().Equal(CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation, CacheAction.BatchOperation);
        _ = singleKey[0].Item.Should().Be(north);
        _ = singleKey[1].Item.Should().Be(north);
        var singleAddedBatch = singleKey[CoverageValueTwo].Batch!;
        var singleRemovedBatch = singleKey[CoverageValueThree].Batch!;
        _ = CopyBatchItems(singleAddedBatch).Should().ContainSingle().Which.Should().Be(northBatch);
        _ = CopyBatchItems(singleRemovedBatch).Should().ContainSingle().Which.Should().Be(northBatch);

        _ = GetActions(multipleKeys).Should().Equal(
                CacheAction.Added,
                CacheAction.Added,
                CacheAction.Removed,
                CacheAction.BatchOperation,
                CacheAction.BatchOperation);
        var multipleAddedBatch = multipleKeys[CoverageValueThree].Batch!;
        var multipleRemovedBatch = multipleKeys[CoverageValueFour].Batch!;
        _ = CopyBatchItems(multipleAddedBatch).Should().BeEquivalentTo([northBatch, eastBatch]);
        _ = CopyBatchItems(multipleRemovedBatch).Should().BeEquivalentTo([northBatch, eastBatch]);

        DisposeBatches(singleKey);
        DisposeBatches(multipleKeys);
    }

    /// <summary>Quaternary dictionary secondary-index filters should pass matching single and batch notifications.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task QuaternaryDictionarySecondaryIndexFilter_ShouldPassMatchingSingleAndBatchNotifications()
    {
        using var dictionary = new QuaternaryDictionary<int, IndexedItem>();
        dictionary.AddValueIndex(RegionPropertyName, static item => item.Region);
        var singleKey = new List<CacheNotify<KeyValuePair<int, IndexedItem>>>();
        var multipleKeys = new List<CacheNotify<KeyValuePair<int, IndexedItem>>>();

        using var singleSubscription = dictionary.Stream
            .FilterBySecondaryIndex(dictionary, RegionPropertyName, NorthRegion)
            .Subscribe(singleKey.Add);
        using var multipleSubscription = dictionary.Stream
            .FilterBySecondaryIndex(dictionary, RegionPropertyName, NorthRegion, "east")
            .Subscribe(multipleKeys.Add);

        var north = new IndexedItem(1, NorthRegion);
        var east = new IndexedItem(CoverageValueTwo, "east");
        var south = new IndexedItem(CoverageValueThree, SouthRegion);

        dictionary.Add(1, north);
        dictionary.Add(CoverageValueTwo, east);
        dictionary.Add(CoverageValueThree, south);
        _ = dictionary.Remove(1);

        var northBatch = new KeyValuePair<int, IndexedItem>(CoverageValueFour, new IndexedItem(CoverageValueFour, NorthRegion));
        var eastBatch = new KeyValuePair<int, IndexedItem>(CoverageValueFive, new IndexedItem(CoverageValueFive, "east"));
        var southBatch = new KeyValuePair<int, IndexedItem>(CoverageValueSix, new IndexedItem(CoverageValueSix, SouthRegion));
        dictionary.AddRange([northBatch, eastBatch, southBatch]);

        await WaitForPipeline();

        _ = GetActions(singleKey).Should().Equal(CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation);
        _ = singleKey[0].Item.Value.Should().Be(north);
        _ = singleKey[1].Item.Value.Should().Be(north);
        var dictionarySingleBatch = singleKey[CoverageValueTwo].Batch!;
        _ = CopyBatchItems(dictionarySingleBatch).Should().ContainSingle().Which.Should().Be(northBatch);

        _ = GetActions(multipleKeys).Should().Equal(CacheAction.Added, CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation);
        var dictionaryMultipleBatch = multipleKeys[CoverageValueThree].Batch!;
        _ = CopyBatchItems(dictionaryMultipleBatch).Should().BeEquivalentTo([northBatch, eastBatch]);

        DisposeBatches(singleKey);
        DisposeBatches(multipleKeys);
    }
#endif

    /// <summary>Provides WaitForPipeline.</summary>
    /// <returns>The result.</returns>
    private static Task WaitForPipeline() => Task.Delay(CoverageTimeoutMilliseconds);

    /// <summary>Provides CreateBatch.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="items">The items value.</param>
    /// <returns>The result.</returns>
    private static PooledBatch<T> CreateBatch<T>(params T[] items)
    {
        var array = ArrayPool<T>.Shared.Rent(items.Length);
        Array.Copy(items, array, items.Length);
        return new(array, items.Length);
    }

    /// <summary>Provides DisposeBatches.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="notifications">The notifications value.</param>
    private static void DisposeBatches<T>(IEnumerable<CacheNotify<T>> notifications)
    {
        foreach (var notification in notifications)
        {
            notification.Batch?.Dispose();
        }
    }

    /// <summary>Copies the active items from a pooled batch.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="batch">The pooled batch.</param>
    /// <returns>The active batch items.</returns>
    private static List<T> CopyBatchItems<T>(PooledBatch<T> batch)
    {
        var items = new List<T>(batch.Count);
        for (var index = 0; index < batch.Count; index++)
        {
            items.Add(batch.Items[index]);
        }

        return items;
    }

    /// <summary>Gets the actions from a notification list.</summary>
    /// <typeparam name="T">The notification item type.</typeparam>
    /// <param name="notifications">The notifications.</param>
    /// <returns>The actions.</returns>
    private static List<CacheAction> GetActions<T>(List<CacheNotify<T>> notifications)
    {
        var actions = new List<CacheAction>(notifications.Count);
        foreach (var notification in notifications)
        {
            actions.Add(notification.Action);
        }

        return actions;
    }

    /// <summary>Gets current values from a change set.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="changes">The changes.</param>
    /// <returns>The current values.</returns>
    private static List<T> GetCurrentValues<T>(ChangeSet<T> changes)
    {
        var values = new List<T>(changes.Count);
        foreach (var change in changes)
        {
            values.Add(change.Current);
        }

        return values;
    }

    /// <summary>Finds a grouping by key.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TElement">The element type.</typeparam>
    /// <param name="groupings">The available groupings.</param>
    /// <param name="key">The requested key.</param>
    /// <returns>The matching grouping.</returns>
    private static IGrouping<TKey, TElement> FindGrouping<TKey, TElement>(
        IEnumerable<IGrouping<TKey, TElement>> groupings,
        TKey key)
    {
        foreach (var grouping in groupings)
        {
            if (EqualityComparer<TKey>.Default.Equals(grouping.Key, key))
            {
                return grouping;
            }
        }

        throw new InvalidOperationException("The requested grouping was not found.");
    }

    /// <summary>Provides MutableItem.</summary>
    private sealed class MutableItem : INotifyPropertyChanged
    {
        /// <summary>Initializes a new instance of the <see cref="MutableItem"/> class.</summary>
        /// <param name="region">The region value.</param>
        /// <param name="name">The name value.</param>
        public MutableItem(string region, string name)
        {
            Region = region;
            Name = name;
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

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
