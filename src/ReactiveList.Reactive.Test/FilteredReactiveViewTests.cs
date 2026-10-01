// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CP.Reactive.Collections;
using CP.Reactive.Views;

namespace ReactiveList.Reactive.Test;

/// <summary>Verifies queued native Rx projections use retained changes and coherent refresh barriers.</summary>
public sealed class FilteredReactiveViewTests
{
    /// <summary>The divisor used by the dynamic predicate.</summary>
    private const int ParityDivisor = 2;

    /// <summary>The final odd value included by the dynamic predicate.</summary>
    private const int ThirdItem = 3;

    /// <summary>The value appended after an explicit refresh.</summary>
    private const int FourthItem = 4;

    /// <summary>The expected count after the appended item.</summary>
    private const int FifthItem = 5;

    /// <summary>The source containing genuine duplicate occurrences.</summary>
    private static readonly int[] StartingItems = [1, ParityDivisor, 1, ThirdItem];

    /// <summary>Does not replay queued changes already represented by an explicit refresh.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Refresh_BeforeRxDispatch_UsesARevisionBarrier()
    {
        using var source = new ReactiveList<int>();
        var scheduler = new HistoricalScheduler();
        using var view = new FilteredReactiveView<int>(source, static _ => true, scheduler, TimeSpan.Zero);
        source.AddRange(StartingItems);
        view.Refresh();
        source.Add(FourthItem);

        scheduler.Start();

        await Assert.That(view.Count).IsEqualTo(FifthItem);
        await Assert.That(view[FourthItem]).IsEqualTo(FourthItem);
        source.ClearWithoutDeallocation(false);
        view.Refresh();
        await Assert.That(view.Items).IsEmpty();
    }

    /// <summary>Does not duplicate source additions when the initial Rx predicate is queued.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InitialPredicateAndQueuedSource_PreserveDuplicateOccurrences()
    {
        using var source = new ReactiveList<int>();
        using var predicates = new BehaviorSubject<Func<int, bool>>(static item => item % ParityDivisor != 0);
        var scheduler = new HistoricalScheduler();
        using var view = new DynamicFilteredReactiveView<int>(source, predicates, scheduler, TimeSpan.Zero);
        source.AddRange(StartingItems);

        scheduler.Start();

        await Assert.That(view.Count).IsEqualTo(ThirdItem);
        await Assert.That(view[0]).IsEqualTo(1);
        await Assert.That(view[1]).IsEqualTo(1);
        await Assert.That(view[ParityDivisor]).IsEqualTo(ThirdItem);
        source.Clear();
        view.Refresh();
        scheduler.Start();
        await Assert.That(view.Items).IsEmpty();
    }

    /// <summary>Removes the selected reference occurrence instead of an equal surviving reference.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EqualReferences_QueuedRemoval_PreservesTheSurvivingInstance()
    {
        var first = new string('x', 1);
        var second = new string('x', 1);
        using var source = new ReactiveList<string> { first, second };
        var scheduler = new HistoricalScheduler();
        using var view = new FilteredReactiveView<string>(source, static _ => true, scheduler, TimeSpan.Zero);
        source.RemoveAt(1);

        scheduler.Start();

        await Assert.That(view.Count).IsEqualTo(1);
        await Assert.That(view[0]).IsSameReferenceAs(first);
    }
}
