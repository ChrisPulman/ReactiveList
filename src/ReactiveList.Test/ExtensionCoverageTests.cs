// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
#else
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
#endif
using TUnit.Assertions;
using TUnit.Core;
#if REACTIVELIST_REACTIVE
using Signal = ReactiveUI.Primitives.Reactive.Signals.Signal;
#endif

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
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChangeSetOperators_ShouldHandleEmptyNoMatchPartialAllAndPreviousValues()
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

        await Assert.That(filtered).Count().IsEqualTo(CoverageValueTwo);
        await Assert.That(GetCurrentValues(filtered[0])).IsEquivalentTo([CoverageValueTwo, CoverageValueFour], CollectionOrdering.Matching);
        await Assert.That(filtered[1].Equals(allMatch)).IsTrue();

        Func<string, string> itemSelector = static item => $"value-{item}";
        var projectedSets = new List<ChangeSet<string>>();
        using var projectionSubscription = Signal.Emit(new ChangeSet<string>([
                Change<string>.CreateUpdate("twenty", "ten", 0),
                Change<string>.CreateAdd("thirty", 1),
            ]))
            .SelectChanges(itemSelector)
            .Subscribe(projectedSets.Add);

        await Assert.That(projectedSets).HasSingleItem();
        await Assert.That(projectedSets[0][0].Previous).IsEqualTo("value-ten");
        await Assert.That(projectedSets[0][0].Current).IsEqualTo("value-twenty");
        await Assert.That(projectedSets[0][1].Previous).IsNull();

        Func<Change<int>, string> changeSelector = static change => $"{change.Reason}:{change.Current}";
        var flattened = new List<string>();
        using var flattenSubscription = Signal.Emit(new ChangeSet<int>([
                Change<int>.CreateRemove(CoverageValueFive),
                Change<int>.CreateMove(CoverageValueSix, CoverageValueTwo, 0),
            ]))
            .SelectChanges(changeSelector)
            .Subscribe(flattened.Add);

        await Assert.That(flattened).IsEquivalentTo(["Remove:5", "Move:6"], CollectionOrdering.Matching);

        var emptyFlattened = new List<int>();
        using var emptySubscription = Signal.Emit(ChangeSet<int>.Empty)
            .SelectChanges(static change => change.Current)
            .Subscribe(emptyFlattened.Add);

        await Assert.That(emptyFlattened).IsEmpty();
    }

    /// <summary>Change-set operators should reject null arguments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChangeSetOperators_WithNullArguments_ShouldThrow()
    {
        IObservable<ChangeSet<int>> nullSource = null!;

        var whereSource = () => nullSource.WhereChanges(static _ => true);
        var wherePredicate = static () => ReactiveListExtensions.WhereChanges(Signal.None<ChangeSet<int>>(), null!);
        var selectSource = () => ReactiveListExtensions.SelectChanges(nullSource, (Func<int, string>)(static item => item.ToString()));
        var selectItemSelector = static () => ReactiveListExtensions.SelectChanges(Signal.None<ChangeSet<int>>(), (Func<int, string>)null!);
        var selectChangeSelector = static () => ReactiveListExtensions.SelectChanges(Signal.None<ChangeSet<int>>(), (Func<Change<int>, string>)null!);

        await Assert.That(whereSource).Throws<ArgumentNullException>().WithParameterName("source");
        await Assert.That(wherePredicate).Throws<ArgumentNullException>().WithParameterName("predicate");
        await Assert.That(selectSource).Throws<ArgumentNullException>().WithParameterName("source");
        await Assert.That(selectItemSelector).Throws<ArgumentNullException>().WithParameterName("selector");
        await Assert.That(selectChangeSelector).Throws<ArgumentNullException>().WithParameterName("selector");
    }

    /// <summary>Generic dynamic stream filters should handle single, batch, remove, and clear notifications.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FilterDynamic_GenericStream_ShouldFilterAddsBatchesAndPassRemovesAndClears()
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

        await Assert.That(GetActions(received)).IsEquivalentTo([CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation, CacheAction.Cleared], CollectionOrdering.Matching);
        await Assert.That(received[0].Item).IsEqualTo(CoverageValueTwo);
        await Assert.That(received[1].Item).IsEqualTo(CoverageValueThree);
        await Assert.That(received[CoverageValueTwo].Batch).IsNotNull();
        var genericBatch = received[CoverageValueTwo].Batch!;
        await Assert.That(CopyBatchItems(genericBatch)).IsEquivalentTo([CoverageValueFour, CoverageValueSix], CollectionOrdering.Matching);
        await Assert.That(received[CoverageValueThree].Action).IsEqualTo(CacheAction.Cleared);

        DisposeBatches(received);
    }

    /// <summary>Dictionary dynamic stream filters should handle single, batch, remove, and clear notifications.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FilterDynamic_DictionaryStream_ShouldFilterAddsBatchesAndPassRemoves()
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

        await Assert.That(GetActions(received))
            .IsEquivalentTo([CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation, CacheAction.BatchOperation, CacheAction.Cleared], CollectionOrdering.Matching);
        await Assert.That(received[0].Item.Value).IsEqualTo(AlphaItem);
        await Assert.That(received[1].Item.Value).IsEqualTo("beta");
        var addedBatch = received[CoverageValueTwo].Batch!;
        var removedBatch = received[CoverageValueThree].Batch!;
        await Assert.That((await Assert.That(CopyBatchItems(addedBatch)).HasSingleItem()).Value).IsEqualTo("atlas");
        await Assert.That((await Assert.That(CopyBatchItems(removedBatch)).HasSingleItem()).Value).IsEqualTo("apex");

        DisposeBatches(received);
    }

    /// <summary>Internal batch filter helpers should handle null, empty, and matching results.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BatchFilterHelpers_ShouldReturnNullForNoBatchOrNoMatchesAndFilterMatches()
    {
        var noBatch = new CacheNotify<int>(CacheAction.BatchOperation, default);
        await Assert.That(ReactiveListExtensions.FilterBatchByPredicate(noBatch, static _ => true)).IsNull();
        await Assert.That(ReactiveListExtensions.FilterBatch(noBatch, [1])).IsNull();

        var noMatch = new CacheNotify<int>(CacheAction.BatchOperation, default, CreateBatch(1, CoverageValueThree, CoverageValueFive));
        await Assert.That(ReactiveListExtensions.FilterBatchByPredicate(noMatch, static item => item % CoverageValueTwo == 0)).IsNull();
        noMatch.Batch!.Dispose();

        var predicateMatch = new CacheNotify<int>(CacheAction.BatchOperation, default, CreateBatch(1, CoverageValueTwo, CoverageValueFour));
        var predicateResult = ReactiveListExtensions.FilterBatchByPredicate(predicateMatch, static item => item > 1);
        await Assert.That(predicateResult).IsNotNull();
        await Assert.That(CopyBatchItems(predicateResult!.Batch!)).IsEquivalentTo([CoverageValueTwo, CoverageValueFour], CollectionOrdering.Matching);
        predicateMatch.Batch!.Dispose();
        predicateResult.Batch!.Dispose();

        var setMatch = new CacheNotify<int>(CacheAction.BatchOperation, default, CreateBatch(1, CoverageValueTwo, CoverageValueThree));
        var setResult = ReactiveListExtensions.FilterBatch(setMatch, [1, CoverageValueThree]);
        await Assert.That(setResult).IsNotNull();
        await Assert.That(CopyBatchItems(setResult!.Batch!)).IsEquivalentTo([1, CoverageValueThree], CollectionOrdering.Matching);
        setMatch.Batch!.Dispose();
        setResult.Batch!.Dispose();
    }

    /// <summary>Grouping and auto-refresh operators should emit grouped changes and property refreshes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GroupingAndAutoRefresh_ShouldGroupChangesAndEmitPropertyRefreshes()
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

        await Assert.That(groupings).Count().IsEqualTo(CoverageValueTwo);
        await Assert.That(FindGrouping(groupings, NorthRegion)).Count().IsEqualTo(CoverageValueTwo);
        await Assert.That(FindGrouping(groupings, SouthRegion)).HasSingleItem();

        var groupedValues = new Dictionary<string, List<MutableItem>>();
        using var groupBySubscription = Signal.Emit(changes)
            .GroupByChanges(static item => item.Region)
            .Subscribe(group =>
            {
                var valuesForGroup = new List<MutableItem>();
                groupedValues[group.Key] = valuesForGroup;
                _ = group.Subscribe(valuesForGroup.Add);
            });

        await Assert.That(groupedValues[NorthRegion]).Count().IsEqualTo(CoverageValueTwo);
        await Assert.That(await Assert.That(groupedValues[SouthRegion]).HasSingleItem()).IsEqualTo(south);

        using var refreshSource = new Signal<ChangeSet<MutableItem>>();
        var received = new List<ChangeSet<MutableItem>>();
        using var refreshSubscription = refreshSource
            .AutoRefresh(nameof(MutableItem.Name))
            .Subscribe(received.Add);

        refreshSource.OnNext(new(Change<MutableItem>.CreateAdd(north, 0)));
        north.RaisePropertyChanged(nameof(MutableItem.Region));
        north.RaisePropertyChanged(nameof(MutableItem.Name));

        await Assert.That(received).Count().IsEqualTo(CoverageValueTwo);
        await Assert.That(received[0][0].Reason).IsEqualTo(ChangeReason.Add);
        await Assert.That(received[1][0].Reason).IsEqualTo(ChangeReason.Refresh);
        await Assert.That(received[1][0].Current).IsEqualTo(north);
        await Assert.That(received[1][0].CurrentIndex).IsEqualTo(0);

        var allProperties = new List<ChangeSet<MutableItem>>();
        using var allSubscription = refreshSource
            .AutoRefresh(null)
            .Subscribe(allProperties.Add);

        refreshSource.OnNext(new(Change<MutableItem>.CreateUpdate(south, south, 1)));
        south.RaisePropertyChanged(nameof(MutableItem.Region));

        await Assert.That(allProperties).Count().IsEqualTo(CoverageValueTwo);
        await Assert.That(allProperties[1][0].Reason).IsEqualTo(ChangeReason.Refresh);
    }

    /// <summary>Source auto-refresh expression overload should validate property expressions and return the source stream.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AutoRefresh_SourceExpression_ShouldValidatePropertyAndReturnSourceStream()
    {
        using var list = new ReactiveList<MutableItem>();
        var received = new List<CacheNotify<MutableItem>>();

        using var subscription = list
            .AutoRefresh(static item => item.Name)
            .Subscribe(received.Add);

        var item = new MutableItem(NorthRegion, AlphaItem);
        list.Add(item);

        await Assert.That(received).HasSingleItem();
        await Assert.That(received[0].Action).IsEqualTo(CacheAction.Added);

        var invalidExpression = () => list.AutoRefresh(static _ => new object());
        await Assert.That(invalidExpression).Throws<ArgumentException>().WithParameterName("property");
    }

    /// <summary>View factory extensions should create filtered, sorted, grouped, and dynamic views.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ViewFactoryExtensions_ShouldCreateViewsWithFallbackSchedulersAndDynamicFilters()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([CoverageValueThree, 1, CoverageValueTwo]);

        using var filtered = list.CreateView(static item => item > 1, scheduler: null, throttleMs: 0);
        await Assert.That(filtered.Items).IsEquivalentTo([CoverageValueTwo, CoverageValueThree]);

        using var dynamicFilters = new BehaviorSignal<Func<int, bool>>(static item => item == 1);
        using var dynamicFiltered = list.CreateView(dynamicFilters, scheduler: null, throttleMs: 0);
        await WaitForPipeline();
        await Assert.That(dynamicFiltered.Items).IsEquivalentTo([1], CollectionOrdering.Matching);

        using var sorted = list.SortBy(static item => item, descending: true, scheduler: null, throttleMs: 0);
        await Assert.That(sorted.Items).IsEquivalentTo([CoverageValueThree, CoverageValueTwo, 1], CollectionOrdering.Matching);

        using var grouped = list.GroupBy(static item => item % CoverageValueTwo, scheduler: null, throttleMs: 0);
        await Assert.That(grouped.Keys).IsEquivalentTo([0, 1]);

#if NET8_0_OR_GREATER || NETFRAMEWORK
        using var quaternary = new QuaternaryList<string> { AppleItem, "banana" };

        using var query = new BehaviorSignal<string>("app");
        using var queryView = quaternary.CreateView(
            query,
            static (queryText, item) => item.StartsWith(queryText, StringComparison.Ordinal),
            Sequencer.Immediate,
            throttleMs: 0);
        await Assert.That(queryView.Items).IsEquivalentTo([AppleItem], CollectionOrdering.Matching);

        using var sourceFilters = new BehaviorSignal<Func<string, bool>>(static item => item.Contains("a", StringComparison.Ordinal));
        using var sourceView = quaternary.CreateView(sourceFilters, Sequencer.Immediate, throttleMs: 0);
        await Assert.That(sourceView.Items).IsEquivalentTo([AppleItem, "banana"]);
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

        await Assert.That(GetActions(singleKey)).IsEquivalentTo([CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation, CacheAction.BatchOperation], CollectionOrdering.Matching);
        await Assert.That(singleKey[0].Item).IsEqualTo(north);
        await Assert.That(singleKey[1].Item).IsEqualTo(north);
        var singleAddedBatch = singleKey[CoverageValueTwo].Batch!;
        var singleRemovedBatch = singleKey[CoverageValueThree].Batch!;
        await Assert.That(await Assert.That(CopyBatchItems(singleAddedBatch)).HasSingleItem()).IsEqualTo(northBatch);
        await Assert.That(await Assert.That(CopyBatchItems(singleRemovedBatch)).HasSingleItem()).IsEqualTo(northBatch);

        await Assert.That(GetActions(multipleKeys))
            .IsEquivalentTo([CacheAction.Added, CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation, CacheAction.BatchOperation], CollectionOrdering.Matching);
        var multipleAddedBatch = multipleKeys[CoverageValueThree].Batch!;
        var multipleRemovedBatch = multipleKeys[CoverageValueFour].Batch!;
        await Assert.That(CopyBatchItems(multipleAddedBatch)).IsEquivalentTo([northBatch, eastBatch]);
        await Assert.That(CopyBatchItems(multipleRemovedBatch)).IsEquivalentTo([northBatch, eastBatch]);

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

        await Assert.That(GetActions(singleKey)).IsEquivalentTo([CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation], CollectionOrdering.Matching);
        await Assert.That(singleKey[0].Item.Value).IsEqualTo(north);
        await Assert.That(singleKey[1].Item.Value).IsEqualTo(north);
        var dictionarySingleBatch = singleKey[CoverageValueTwo].Batch!;
        await Assert.That(await Assert.That(CopyBatchItems(dictionarySingleBatch)).HasSingleItem()).IsEqualTo(northBatch);

        await Assert.That(GetActions(multipleKeys)).IsEquivalentTo([CacheAction.Added, CacheAction.Added, CacheAction.Removed, CacheAction.BatchOperation], CollectionOrdering.Matching);
        var dictionaryMultipleBatch = multipleKeys[CoverageValueThree].Batch!;
        await Assert.That(CopyBatchItems(dictionaryMultipleBatch)).IsEquivalentTo([northBatch, eastBatch]);

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
