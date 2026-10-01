// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using CP.Reactive;
using CP.Reactive.Core;
using CP.Reactive.Views;
using ReactiveIntList = CP.Reactive.Collections.ReactiveList<int>;

namespace ReactiveList.Reactive.Test;

/// <summary>Verifies Rx subscriptions, dynamic predicates and collection projections.</summary>
public sealed class ReactiveListTests
{
    /// <summary>The value of the second initial item.</summary>
    private const int SecondItem = 2;

    /// <summary>The value of the third initial item.</summary>
    private const int ThirdItem = 3;

    /// <summary>The value of the fourth initial item.</summary>
    private const int FourthItem = 4;

    /// <summary>The additional even item used by filtering tests.</summary>
    private const int AdditionalEvenItem = 6;

    /// <summary>The replacement value and projection multiplier.</summary>
    private const int ReplacementItem = 10;

    /// <summary>The value exceeding the dynamic threshold.</summary>
    private const int LargeItem = 20;

    /// <summary>The maximum wait for a native Rx predicate emission.</summary>
    private const int PredicateTimeoutSeconds = 5;

    /// <summary>The expected projected additions.</summary>
    private static readonly int[] ProjectedAdditions = [ReplacementItem, LargeItem, 30];

    /// <summary>The expected previous and current update values.</summary>
    private static readonly (int Previous, int Current)[] ExpectedUpdates = [(1, ReplacementItem)];

    /// <summary>The expected move payload.</summary>
    private static readonly (int Item, int OldIndex, int NewIndex)[] ExpectedMoves = [(ReplacementItem, 0, SecondItem)];

    /// <summary>The expected removed item.</summary>
    private static readonly int[] ExpectedRemoved = [ReplacementItem];

    /// <summary>The expected action before subscription disposal.</summary>
    private static readonly CacheAction[] ExpectedActions = [CacheAction.Added];

    /// <summary>The values accepted across successive dynamic predicates.</summary>
    private static readonly int[] DynamicValues = [1, FourthItem, LargeItem];

    /// <summary>The initial even projection.</summary>
    private static readonly int[] EvenValues = [SecondItem, FourthItem];

    /// <summary>The even projection after adding an item.</summary>
    private static readonly int[] ExtendedEvenValues = [SecondItem, FourthItem, AdditionalEvenItem];

    /// <summary>The values greater than the initial threshold.</summary>
    private static readonly int[] HighValues = [ThirdItem, FourthItem];

    /// <summary>The values selected by the replacement predicate.</summary>
    private static readonly int[] LowValues = [1, SecondItem];

    /// <summary>The expected descending projection.</summary>
    private static readonly int[] DescendingValues = [FourthItem, ThirdItem, SecondItem, 1];

    /// <summary>Delivers initial and subsequent additions through the Rx change pipeline.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_ExistingAndAddedItems_EmitsRxPipelineValues()
    {
        using var list = new ReactiveIntList { 1, SecondItem };
        List<int> additions = [];
        using var subscription = list.Connect().OnAdd().Select(static value => value * ReplacementItem).Subscribe(additions.Add);
        list.Add(ThirdItem);
        await Assert.That(additions).IsEquivalentTo(ProjectedAdditions);
    }

    /// <summary>Preserves update and move metadata when observed with native Rx subscriptions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Connect_UpdateMoveRemove_PreservesMetadata()
    {
        using var list = new ReactiveIntList { 1, SecondItem, ThirdItem };
        List<(int Previous, int Current)> updates = [];
        List<(int Item, int OldIndex, int NewIndex)> moves = [];
        List<int> removals = [];
        using var updated = list.Connect().OnUpdate().Subscribe(updates.Add);
        using var moved = list.Connect().OnMove().Subscribe(moves.Add);
        using var removed = list.Connect().OnRemove().Subscribe(removals.Add);
        list[0] = ReplacementItem;
        list.Move(0, SecondItem);
        list.RemoveAt(SecondItem);
        await Assert.That(updates).IsEquivalentTo(ExpectedUpdates);
        await Assert.That(moves).IsEquivalentTo(ExpectedMoves);
        await Assert.That(removals).IsEquivalentTo(ExpectedRemoved);
    }

    /// <summary>Stops stream delivery after disposing the native Rx subscription.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Stream_DisposedSubscription_StopsNotifications()
    {
        using var list = new ReactiveIntList();
        List<CacheAction> actions = [];
        var subscription = list.Stream.Subscribe(notification => actions.Add(notification.Action));
        list.Add(1);
        subscription.Dispose();
        list.Add(SecondItem);
        await Assert.That(actions).IsEquivalentTo(ExpectedActions);
    }

    /// <summary>Filters future notifications using predicates emitted by a native Rx subject.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FilterDynamic_RxSubject_ChangesFutureSelection()
    {
        using var list = new ReactiveIntList();
        using var predicates = new Subject<Func<int, bool>>();
        List<int> values = [];
        using var subscription = list.Stream.FilterDynamic(predicates).Subscribe(notification => values.Add(notification.Item));
        list.Add(1);
        predicates.OnNext(static value => value % SecondItem == 0);
        list.Add(ThirdItem);
        list.Add(FourthItem);
        predicates.OnNext(static value => value > ReplacementItem);
        list.Add(AdditionalEvenItem);
        list.Add(LargeItem);
        await Assert.That(values).IsEquivalentTo(DynamicValues);
    }

    /// <summary>Constructs filtered snapshots with CurrentThreadScheduler and refreshes explicit mutations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateView_CurrentThreadScheduler_FiltersAndRefreshes()
    {
        using var list = new ReactiveIntList { 1, SecondItem, ThirdItem, FourthItem };
        using var view = list.CreateView(static value => value % SecondItem == 0, CurrentThreadScheduler.Instance, 0);
        await Assert.That(view.Items).IsEquivalentTo(EvenValues);
        list.Add(AdditionalEvenItem);
        view.Refresh();
        await Assert.That(view.Items).IsEquivalentTo(ExtendedEvenValues);
        await Assert.That(view[0]).IsEqualTo(SecondItem);
    }

    /// <summary>Accepts native Rx predicates for rebuilding an existing list snapshot.</summary>
    /// <param name="cancellationToken">Cancels the predicate notification wait.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateView_RxSubject_RebuildsOnPredicate(CancellationToken cancellationToken)
    {
        using var list = new ReactiveIntList { 1, SecondItem, ThirdItem, FourthItem };
        using var predicates = new Subject<Func<int, bool>>();
        using var view = list.CreateView(predicates, ImmediateScheduler.Instance, 0);
        await PublishPredicateAsync(view, predicates, static value => value > SecondItem, SecondItem, cancellationToken);
        await Assert.That(view.Items).IsEquivalentTo(HighValues);
        await PublishPredicateAsync(view, predicates, static value => value <= SecondItem, SecondItem, cancellationToken);
        await Assert.That(view.Items).IsEquivalentTo(LowValues);
    }

    /// <summary>Sorts and groups native Rx-backed collection snapshots without wall-clock waits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SortByAndGroupBy_RxScheduler_ProjectsSnapshot()
    {
        using var list = new ReactiveIntList { ThirdItem, 1, FourthItem, SecondItem };
        using var sorted = list.SortBy(static value => value, true, ImmediateScheduler.Instance, 0);
        using var grouped = list.GroupBy(static value => value % SecondItem, ImmediateScheduler.Instance, 0);
        var groupedCount = 0;
        foreach (var group in grouped.Groups)
        {
            groupedCount += group.Count;
        }

        await Assert.That(sorted.Items).IsEquivalentTo(DescendingValues);
        await Assert.That(sorted[0]).IsEqualTo(FourthItem);
        await Assert.That(sorted[1]).IsEqualTo(ThirdItem);
        await Assert.That(grouped.Groups.Count).IsEqualTo(SecondItem);
        await Assert.That(groupedCount).IsEqualTo(FourthItem);
    }

    /// <summary>Transforms current and previous values while retaining change reasons and positions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SelectChanges_RxSubject_PreservesChangeMetadata()
    {
        using var source = new Subject<ChangeSet<int>>();
        List<ChangeSet<string>> results = [];
        using var subscription = source.SelectChanges(static (int value) => value.ToString()).Subscribe(results.Add);
        source.OnNext(new([new(ChangeReason.Update, SecondItem, 1, ThirdItem, ThirdItem)]));
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0][0].Current).IsEqualTo("2");
        await Assert.That(results[0][0].Previous).IsEqualTo("1");
        await Assert.That(results[0][0].Reason).IsEqualTo(ChangeReason.Update);
        await Assert.That(results[0][0].CurrentIndex).IsEqualTo(ThirdItem);
        await Assert.That(results[0][0].PreviousIndex).IsEqualTo(ThirdItem);
    }

    /// <summary>Suppresses empty change sets and excludes changes with other reasons.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WhereReason_RxSubject_OnlyEmitsMatchingChanges()
    {
        using var source = new Subject<ChangeSet<int>>();
        List<ChangeSet<int>> results = [];
        using var subscription = source.WhereReason(ChangeReason.Add).Subscribe(results.Add);
        source.OnNext(ChangeSet<int>.Empty);
        source.OnNext(new([Change<int>.CreateRemove(1)]));
        source.OnNext(new([Change<int>.CreateAdd(SecondItem), Change<int>.CreateRemove(ThirdItem)]));
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Count).IsEqualTo(1);
        await Assert.That(results[0][0].Current).IsEqualTo(SecondItem);
    }

    /// <summary>Waits for the property notification following a native Rx filter rebuild.</summary>
    /// <param name="view">The projection that receives the predicate.</param>
    /// <param name="predicates">The native Rx predicate source.</param>
    /// <param name="predicate">The selection to publish.</param>
    /// <param name="count">The count expected after rebuilding.</param>
    /// <param name="cancellationToken">Cancels the bounded event wait.</param>
    /// <returns>A task representing the asynchronous notification wait.</returns>
    private static async Task PublishPredicateAsync(
        DynamicFilteredReactiveView<int> view,
        Subject<Func<int, bool>> predicates,
        Func<int, bool> predicate,
        int count,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler handler = (_, _) =>
        {
            if (view.Count != count)
            {
                return;
            }

            _ = completed.TrySetResult(true);
        };
        view.PropertyChanged += handler;
        try
        {
            predicates.OnNext(predicate);
            await Assert.That(() => (Task)completed.Task).CompletesWithin(TimeSpan.FromSeconds(PredicateTimeoutSeconds));
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            view.PropertyChanged -= handler;
        }
    }
}
