// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Specialized;
using CP.Primitives.Collections;
using CP.Primitives.Views;

namespace ReactiveListTestApp.Tests;

/// <summary>Verifies lossless filtered projections when source changes precede dispatch.</summary>
public sealed class FilteredReactiveViewTests
{
    /// <summary>The odd item moved to the front of the source.</summary>
    private const int ThirdItem = 3;

    /// <summary>The even item inserted before dispatch.</summary>
    private const int FourthItem = 4;

    /// <summary>The excluded replacement used before a membership-changing update.</summary>
    private const int FifthItem = 5;

    /// <summary>The replacement that enters the filter after a queued move.</summary>
    private const int SixthItem = 6;

    /// <summary>The divisor used to select odd values.</summary>
    private const int ParityDivisor = 2;

    /// <summary>The duplicate-containing source before queued edits.</summary>
    private static readonly int[] StartingItems = [1, ParityDivisor, 1, ThirdItem];

    /// <summary>The filtered source order after queued inserts, moves and removals.</summary>
    private static readonly int[] OrderedDuplicates = [ThirdItem, 1, 1];

    /// <summary>The duplicate-containing source after a queued reset.</summary>
    private static readonly int[] ResetDuplicates = [ThirdItem, ThirdItem];

    /// <summary>The source whose update gains membership after a move.</summary>
    private static readonly int[] MembershipSource = [ParityDivisor, ThirdItem, FourthItem];

    /// <summary>The quiet interval used to coalesce source invalidations.</summary>
    private static readonly TimeSpan QuietInterval = TimeSpan.FromMilliseconds(1);

    /// <summary>The maximum wait for a delayed invalidation to reach the dispatch queue.</summary>
    private static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Preserves source order and genuine duplicates across queued edits and reset.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task QueuedSourceChanges_PreserveOrderDuplicatesAndReset()
    {
        using var source = new ReactiveList<int>();
        var scheduler = new QueuedViewSequencer();
        using var view = new FilteredReactiveView<int>(source, static item => item % ParityDivisor != 0, scheduler, TimeSpan.Zero);
        source.AddRange(StartingItems);
        source.Insert(0, FourthItem);
        source.Move(FourthItem, 0);
        source.RemoveAt(ThirdItem);
        source[1] = ParityDivisor;

        scheduler.RunAll();

        await Assert.That(view.Items).IsEquivalentTo(OrderedDuplicates);
        for (var i = 0; i < OrderedDuplicates.Length; i++)
        {
            await Assert.That(view.Items[i]).IsEqualTo(OrderedDuplicates[i]);
        }

        source.Clear();
        source.AddRange(ResetDuplicates);
        scheduler.RunAll();
        await Assert.That(view.Items).IsEquivalentTo(ResetDuplicates);
    }

    /// <summary>Reconciles every structural edit after a nonzero debounce quiet period.</summary>
    /// <param name="cancellationToken">Cancels the bounded dispatch wait.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NonzeroThrottle_ReconcilesTheCompleteLatestSource(CancellationToken cancellationToken)
    {
        using var source = new ReactiveList<int>();
        var scheduler = new QueuedViewSequencer();
        using var view = new FilteredReactiveView<int>(source, static item => item % ParityDivisor != 0, scheduler, QuietInterval);
        source.AddRange(StartingItems);
        source.Move(ThirdItem, 0);
        source.RemoveAt(ParityDivisor);
        await Assert.That(view.Items).IsEmpty();

        await scheduler.WorkAvailable.WaitAsync(DispatchTimeout, cancellationToken);
        scheduler.RunAll();

        await Assert.That(view.Items).IsEquivalentTo(OrderedDuplicates);
        for (var i = 0; i < OrderedDuplicates.Length; i++)
        {
            await Assert.That(view.Items[i]).IsEqualTo(OrderedDuplicates[i]);
        }
    }

    /// <summary>Retains the actual source instance when a replacement compares equal by value.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EqualReferenceReplacement_PreservesSourceIdentity()
    {
        using var source = new ReactiveList<string> { "x" };
        var scheduler = new QueuedViewSequencer();
        using var view = new FilteredReactiveView<string>(source, static _ => true, scheduler, TimeSpan.Zero);
        var replacement = new string('x', 1);
        source[0] = replacement;

        scheduler.RunAll();

        await Assert.That(view.Count).IsEqualTo(1);
        await Assert.That(view[0]).IsSameReferenceAs(replacement);
    }

    /// <summary>Preserves incremental append-on-membership behavior after a queued move.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MoveThenMembershipUpdate_PreservesIncrementalInsertionOrder()
    {
        using var source = new ReactiveList<int>(MembershipSource);
        var scheduler = new QueuedViewSequencer();
        using var view = new FilteredReactiveView<int>(source, static item => item % ParityDivisor == 0, scheduler, TimeSpan.Zero);
        source.Update(ParityDivisor, FifthItem);
        source.Move(0, 1);
        source.Update(ThirdItem, SixthItem);

        scheduler.RunAll();

        await Assert.That(view.Count).IsEqualTo(ParityDivisor);
        await Assert.That(view[0]).IsEqualTo(FourthItem);
        await Assert.That(view[1]).IsEqualTo(SixthItem);
    }

    /// <summary>Does not replay pending additions over an explicit live-source refresh.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Refresh_BeforeQueuedDispatch_ExcludesAlreadyCoveredChanges()
    {
        using var source = new ReactiveList<int>();
        var scheduler = new QueuedViewSequencer();
        using var view = new FilteredReactiveView<int>(source, static _ => true, scheduler, TimeSpan.Zero);
        source.AddRange(StartingItems);
        view.Refresh();
        source.Add(FourthItem);

        scheduler.RunAll();

        await Assert.That(view.Count).IsEqualTo(FifthItem);
        await Assert.That(view[FourthItem]).IsEqualTo(FourthItem);
        source.ClearWithoutDeallocation(false);
        view.Refresh();
        await Assert.That(view.Items).IsEmpty();
    }

    /// <summary>Does not overwrite a refresh requested from inside a collection callback.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Refresh_ReentrantSourceChange_ReconcilesTheLatestSnapshot()
    {
        using var source = new ReactiveList<int>();
        var scheduler = new QueuedViewSequencer();
        using var view = new FilteredReactiveView<int>(source, static _ => true, scheduler, TimeSpan.Zero);
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
