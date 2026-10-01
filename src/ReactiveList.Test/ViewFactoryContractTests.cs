// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Views;
#else
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using CP.Primitives.Views;
#endif
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;
using TUnit.Assertions;

namespace ReactiveList.Test;

/// <summary>Verifies convenience factories preserve their initial snapshots and ordering.</summary>
public class ViewFactoryContractTests
{
    /// <summary>The shared secondary-index name.</summary>
    private const string ParityIndex = "Parity";

    /// <summary>The divisor used to distinguish even and odd items.</summary>
    private const int ParityDivisor = 2;

    /// <summary>The maximum wait for the initial dynamic index predicate.</summary>
    private const int TimeoutSeconds = 10;

    /// <summary>The unsorted source fixture.</summary>
    private static readonly int[] _sourceItems = [3, 1, 2];

    /// <summary>The ascending snapshot expected from sorting factories.</summary>
    private static readonly int[] _ascending = [1, 2, 3];

    /// <summary>The descending snapshot expected from sorting factories.</summary>
    private static readonly int[] _descending = [3, 2, 1];

    /// <summary>The keys that include every parity-indexed source item.</summary>
    private static readonly int[] _parityKeys = [0, 1];

    /// <summary>Verifies filtered and unfiltered overloads initialize equivalent snapshots.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateView_ConvenienceOverloads_PreserveInitialSnapshot()
    {
        using var source = new ReactiveList<int>(_sourceItems);
        foreach (var view in CreateFilteredViews(source))
        {
            using (view)
            {
                await Assert.That(view.Items).IsEquivalentTo(_sourceItems);
            }
        }
    }

    /// <summary>Verifies dynamic-filter overloads initialize the same unfiltered source snapshot.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateView_DynamicOverloads_PreserveInitialSnapshot()
    {
        using var source = new ReactiveList<int>(_sourceItems);
        using var predicates = new BehaviorSignal<Func<int, bool>>(static _ => true);
        using var defaultView = source.CreateView(predicates);
        using var scheduled = source.CreateView(predicates, Sequencer.CurrentThread);
        using var immediate = source.CreateView(predicates, 0);
        await Assert.That(defaultView.Items).IsEquivalentTo(_sourceItems);
        await Assert.That(scheduled.Items).IsEquivalentTo(_sourceItems);
        await Assert.That(immediate.Items).IsEquivalentTo(_sourceItems);
    }

    /// <summary>Verifies comparer and key-selector overloads initialize ascending snapshots.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SortBy_AscendingOverloads_PreserveOrdering()
    {
        using var source = new ReactiveList<int>(_sourceItems);
        foreach (var view in CreateAscendingViews(source))
        {
            using (view)
            {
                await VerifyOrdering(view.Items, _ascending);
            }
        }
    }

    /// <summary>Verifies every descending convenience overload preserves its requested direction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SortBy_DescendingOverloads_PreserveOrdering()
    {
        using var source = new ReactiveList<int>(_sourceItems);
        using var defaultView = source.SortBy(static item => item, true);
        using var scheduled = source.SortBy(static item => item, true, Sequencer.CurrentThread);
        using var immediate = source.SortBy(static item => item, true, 0);
        await VerifyOrdering(defaultView.Items, _descending);
        await VerifyOrdering(scheduled.Items, _descending);
        await VerifyOrdering(immediate.Items, _descending);
    }

    /// <summary>Verifies grouping convenience overloads preserve all initial groups.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GroupBy_ConvenienceOverloads_PreserveGroups()
    {
        using var source = new ReactiveList<int>(_sourceItems);
        using var defaultView = source.GroupBy(static item => item);
        using var scheduled = source.GroupBy(static item => item, Sequencer.CurrentThread);
        using var immediate = source.GroupBy(static item => item, 0);
        await Assert.That(defaultView.Count).IsEqualTo(_sourceItems.Length);
        await Assert.That(scheduled.Count).IsEqualTo(_sourceItems.Length);
        await Assert.That(immediate.Count).IsEqualTo(_sourceItems.Length);
    }

    /// <summary>Verifies list secondary-index factories use the requested single and multiple keys.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task QuaternaryList_IndexFactories_PreserveMatchingSnapshots()
    {
        using var source = new QuaternaryList<int>();
        source.AddRange(_sourceItems);
        source.AddIndex(ParityIndex, static item => item % ParityDivisor);
        using var single = source.CreateViewBySecondaryIndex(ParityIndex, 0, Sequencer.CurrentThread);
        using var multiple = source.CreateViewBySecondaryIndex(ParityIndex, _parityKeys, Sequencer.CurrentThread);
        using var keys = new BehaviorSignal<int[]>(_parityKeys);
        using var dynamicView = source.CreateDynamicViewBySecondaryIndex(ParityIndex, keys, Sequencer.CurrentThread);
        await Assert.That(single.Items.Count).IsEqualTo(1);
        await Assert.That(single.Items[0]).IsEqualTo(_sourceItems[_sourceItems.Length - 1]);
        await Assert.That(multiple.Items).IsEquivalentTo(_sourceItems);
        await Assert.That(() => dynamicView.Count).WaitsFor(static assertion => assertion.IsEqualTo(_sourceItems.Length), TimeSpan.FromSeconds(TimeoutSeconds));
        dynamicView.Refresh();
        await Assert.That(dynamicView.Count).IsEqualTo(_sourceItems.Length);
    }

    /// <summary>Verifies dictionary secondary-index factories retain the corresponding primary keys.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task QuaternaryDictionary_IndexFactories_PreservePrimaryKeys()
    {
        using var source = new QuaternaryDictionary<int, int>();
        foreach (var item in _sourceItems)
        {
            source.Add(item, item);
        }

        source.AddValueIndex(ParityIndex, static item => item % ParityDivisor);
        using var single = QuaternaryExtensions.CreateViewBySecondaryIndex(source, ParityIndex, 0, Sequencer.CurrentThread);
        using var multiple = QuaternaryExtensions.CreateViewBySecondaryIndex(source, ParityIndex, _parityKeys, Sequencer.CurrentThread);
        using var keys = new BehaviorSignal<int[]>(_parityKeys);
        using var dynamicView = source.CreateDynamicViewBySecondaryIndex(ParityIndex, keys, Sequencer.CurrentThread);
        await Assert.That(single.Items.Count).IsEqualTo(1);
        await Assert.That(single.Items[0].Key).IsEqualTo(_sourceItems[_sourceItems.Length - 1]);
        await Assert.That(multiple.Items.Count).IsEqualTo(_sourceItems.Length);
        await Assert.That(() => dynamicView.Items.Count).WaitsFor(static assertion => assertion.IsEqualTo(_sourceItems.Length), TimeSpan.FromSeconds(TimeoutSeconds));
        dynamicView.Refresh();
        await Assert.That(dynamicView.Items.Count).IsEqualTo(_sourceItems.Length);
    }

    /// <summary>Verifies quaternary convenience overloads initialize static and query-driven projections.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task QuaternaryList_CreateViewOverloads_PreserveSnapshot()
    {
        using var source = new QuaternaryList<int>();
        source.AddRange(_sourceItems);
        using var all = source.CreateView(Sequencer.CurrentThread);
        using var filtered = source.CreateView(static _ => true, Sequencer.CurrentThread);
        using var predicate = new BehaviorSignal<Func<int, bool>>(static _ => true);
        using var dynamicView = source.CreateView(predicate, Sequencer.CurrentThread);
        using var query = new BehaviorSignal<int>(0);
        using var queried = source.CreateView(query, static (_, _) => true, Sequencer.CurrentThread);
        await Assert.That(all.Items).IsEquivalentTo(_sourceItems);
        await Assert.That(filtered.Items).IsEquivalentTo(_sourceItems);
        await Assert.That(dynamicView.Items).IsEquivalentTo(_sourceItems);
        await Assert.That(queried.Items).IsEquivalentTo(_sourceItems);
    }

    /// <summary>Verifies list factories reject null sources and projection callbacks.</summary>
    /// <returns>A task representing the asynchronous assertions.</returns>
    [Test]
    public async Task ListFactories_NullArguments_Throw()
    {
        using var source = new ReactiveList<int>();
        using var predicates = new BehaviorSignal<Func<int, bool>>(static _ => true);
        await Assert.That(static () => ((ReactiveList<int>)null!).CreateView(static _ => true, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => source.CreateView((Func<int, bool>)null!, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => ((ReactiveList<int>)null!).CreateView(predicates, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => source.CreateView((IObservable<Func<int, bool>>)null!, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(static () => ((ReactiveList<int>)null!).SortBy(Comparer<int>.Default, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => source.SortBy((IComparer<int>)null!, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(static () => ((ReactiveList<int>)null!).SortBy(static item => item, false, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => source.SortBy((Func<int, int>)null!, false, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(static () => ((ReactiveList<int>)null!).GroupBy(static item => item, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => source.GroupBy((Func<int, int>)null!, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies reactive-source factories reject null sources, queries and filters.</summary>
    /// <returns>A task representing the asynchronous assertions.</returns>
    [Test]
    public async Task SourceFactories_NullArguments_Throw()
    {
        using var source = new QuaternaryList<int>();
        using var predicates = new BehaviorSignal<Func<int, bool>>(static _ => true);
        using var query = new BehaviorSignal<int>(0);
        await Assert.That(() => ((QuaternaryList<int>)null!).CreateView(query, static (_, _) => true, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => source.CreateView((IObservable<int>)null!, static (_, _) => true, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => source.CreateView(query, (Func<int, int, bool>)null!, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => ((QuaternaryList<int>)null!).CreateView(predicates, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => source.CreateView((IObservable<Func<int, bool>>)null!, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(static () => ((QuaternaryList<int>)null!).CreateView(Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(static () => ((QuaternaryList<int>)null!).CreateView(static _ => true, Sequencer.CurrentThread, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => source.AutoRefresh((Expression<Func<int, object>>)null!)).Throws<ArgumentNullException>();
        await Assert.That(static () => ((QuaternaryList<int>)null!).AutoRefresh(static item => item)).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies cache-stream dynamic filters and projections validate their inputs.</summary>
    /// <returns>A task representing the asynchronous assertions.</returns>
    [Test]
    public async Task StreamFactories_NullArguments_Throw()
    {
        using var stream = new Signal<CacheNotify<int>>();
        using var predicates = new BehaviorSignal<Func<int, bool>>(static _ => true);
        using var pairs = new Signal<CacheNotify<KeyValuePair<int, int>>>();
        using var pairPredicates = new BehaviorSignal<Func<KeyValuePair<int, int>, bool>>(static _ => true);
        using var changes = new Signal<ChangeSet<int>>();
        Func<Change<int>, int> currentSelector = static change => change.Current;
        await Assert.That(() => ((IObservable<CacheNotify<int>>)null!).FilterDynamic(predicates)).Throws<ArgumentNullException>();
        await Assert.That(() => stream.FilterDynamic(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => ((IObservable<CacheNotify<KeyValuePair<int, int>>>)null!).FilterDynamic(pairPredicates)).Throws<ArgumentNullException>();
        await Assert.That(() => pairs.FilterDynamic(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => ((IObservable<ChangeSet<int>>)null!).SelectChanges(currentSelector)).Throws<ArgumentNullException>();
        await Assert.That(() => changes.SelectChanges((Func<Change<int>, int>)null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies both item count and the value at every sorted position.</summary>
    /// <param name="items">The sorted view items.</param>
    /// <param name="expected">The expected ordered values.</param>
    /// <returns>A task representing the asynchronous assertions.</returns>
    private static async Task VerifyOrdering(ReadOnlyObservableCollection<int> items, int[] expected)
    {
        await Assert.That(items.Count).IsEqualTo(expected.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            await Assert.That(items[index]).IsEqualTo(expected[index]);
        }
    }

    /// <summary>Creates every filtered and unfiltered shorthand overload.</summary>
    /// <param name="source">The list to project.</param>
    /// <returns>The views that the caller must dispose.</returns>
    private static IEnumerable<FilteredReactiveView<int>> CreateFilteredViews(ReactiveList<int> source)
    {
        yield return source.CreateView();
        yield return source.CreateView(Sequencer.CurrentThread);
        yield return source.CreateView(0);
        yield return source.CreateView(static _ => true);
        yield return source.CreateView(static _ => true, Sequencer.CurrentThread);
        yield return source.CreateView(static _ => true, 0);
    }

    /// <summary>Creates every ascending comparer and key-selector shorthand overload.</summary>
    /// <param name="source">The list to project.</param>
    /// <returns>The views that the caller must dispose.</returns>
    private static IEnumerable<SortedReactiveView<int>> CreateAscendingViews(ReactiveList<int> source)
    {
        yield return source.SortBy(Comparer<int>.Default);
        yield return source.SortBy(Comparer<int>.Default, Sequencer.CurrentThread);
        yield return source.SortBy(Comparer<int>.Default, 0);
        yield return source.SortBy(static item => item);
        yield return source.SortBy(static item => item, Sequencer.CurrentThread);
        yield return source.SortBy(static item => item, 0);
        yield return source.SortBy(static item => item, Sequencer.CurrentThread, 0);
    }
}
