// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Specialized;
using CP.Primitives.Collections;
using CP.Primitives.Views;
using ReactiveUI.Primitives.Signals;

namespace ReactiveListTestApp.Tests;

/// <summary>Verifies predicate initialization and source edits share a reconciled projection.</summary>
public sealed class DynamicFilteredReactiveViewTests
{
    /// <summary>The even item excluded by the initial predicate.</summary>
    private const int ParityDivisor = 2;

    /// <summary>The odd value repeated by the reset operation.</summary>
    private const int ThirdItem = 3;

    /// <summary>The even value inserted before the initial predicate is dispatched.</summary>
    private const int FourthItem = 4;

    /// <summary>The source containing legitimate duplicate values.</summary>
    private static readonly int[] StartingItems = [1, ParityDivisor, 1, ThirdItem];

    /// <summary>The expected initial predicate projection in source order.</summary>
    private static readonly int[] FilteredDuplicates = [1, 1, ThirdItem];

    /// <summary>The replacement source containing legitimate duplicates in a different order.</summary>
    private static readonly int[] ReplacementItems = [ThirdItem, 1, ThirdItem];

    /// <summary>Does not replay queued adds over a predicate rebuild from the latest source.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InitialPredicateAndQueuedChanges_DoNotDuplicateOrRetainClearedItems()
    {
        using var source = new ReactiveList<int>();
        using var predicates = new BehaviorSignal<Func<int, bool>>(static item => item % ParityDivisor != 0);
        var scheduler = new QueuedViewSequencer();
        using var view = new DynamicFilteredReactiveView<int>(source, predicates, scheduler, TimeSpan.Zero);
        source.AddRange(StartingItems);
        source.Insert(0, FourthItem);

        scheduler.RunAll();

        await Assert.That(view.Items).IsEquivalentTo(FilteredDuplicates);
        for (var i = 0; i < FilteredDuplicates.Length; i++)
        {
            await Assert.That(view.Items[i]).IsEqualTo(FilteredDuplicates[i]);
        }

        predicates.OnNext(static _ => true);
        source.Clear();
        source.AddRange(ReplacementItems);
        scheduler.RunAll();
        await Assert.That(view.Items).IsEquivalentTo(ReplacementItems);
        for (var i = 0; i < ReplacementItems.Length; i++)
        {
            await Assert.That(view.Items[i]).IsEqualTo(ReplacementItems[i]);
        }
    }

    /// <summary>Returns the selected source instance even when it equals a replaced instance by value.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EqualReferenceReplacement_RespectsIdentityPredicate()
    {
        using var source = new ReactiveList<string> { "x" };
        using var predicates = new BehaviorSignal<Func<string, bool>>(static _ => true);
        var scheduler = new QueuedViewSequencer();
        using var view = new DynamicFilteredReactiveView<string>(source, predicates, scheduler, TimeSpan.Zero);
        var replacement = new string('x', 1);
        source[0] = replacement;
        predicates.OnNext(item => ReferenceEquals(item, replacement));

        scheduler.RunAll();

        await Assert.That(view.Count).IsEqualTo(1);
        await Assert.That(view[0]).IsSameReferenceAs(replacement);
    }

    /// <summary>Retains changes made by a callback that requests another filtered refresh.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Refresh_ReentrantSourceChange_ReconcilesTheLatestSnapshot()
    {
        using var source = new ReactiveList<int>();
        using var predicates = new BehaviorSignal<Func<int, bool>>(static _ => true);
        var scheduler = new QueuedViewSequencer();
        using var view = new DynamicFilteredReactiveView<int>(source, predicates, scheduler, TimeSpan.Zero);
        var changed = false;
        view.CollectionChanged += (_, args) =>
        {
            if (changed || args.Action != NotifyCollectionChangedAction.Add)
            {
                return;
            }

            changed = true;
            source.Add(ParityDivisor);
            view.Refresh();
        };
        source.Add(1);

        view.Refresh();

        await Assert.That(view.Count).IsEqualTo(ParityDivisor);
        await Assert.That(view[0]).IsEqualTo(1);
        await Assert.That(view[1]).IsEqualTo(ParityDivisor);
    }
}
