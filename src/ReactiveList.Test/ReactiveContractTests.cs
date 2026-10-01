// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Internal;
using CP.Reactive.Views;
#else
using CP.Primitives.Collections;
using CP.Primitives.Core;
using CP.Primitives.Internal;
using CP.Primitives.Views;
#endif
using TUnit.Assertions;

namespace ReactiveList.Test;

/// <summary>Verifies value, enumeration and materialization contracts across supported frameworks.</summary>
public class ReactiveContractTests
{
    /// <summary>The second distinct item used by row and tracker fixtures.</summary>
    private const int SecondValue = 2;

    /// <summary>The third distinct item and explicit update index.</summary>
    private const int ThirdValue = 3;

    /// <summary>The fourth distinct item used by iterator fixtures.</summary>
    private const int FourthValue = 4;

    /// <summary>The single change used to verify enumeration.</summary>
    private const int ChangeValue = 42;

    /// <summary>The first immutable row fixture.</summary>
    private static readonly int[] _firstRow = [1, SecondValue];

    /// <summary>The second immutable row fixture.</summary>
    private static readonly int[] _secondRow = [ThirdValue, FourthValue];

    /// <summary>Verifies that shorter change constructors preserve indices and previous values.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Change_Constructors_PreserveDefaultAndExplicitIndices()
    {
        var update = new Change<string>(ChangeReason.Update, "new", "old");
        var indexed = new Change<string>(ChangeReason.Update, "new", "old", ThirdValue);
        await Assert.That(update.Previous).IsEqualTo("old");
        await Assert.That(update.CurrentIndex).IsEqualTo(-1);
        await Assert.That(update.PreviousIndex).IsEqualTo(-1);
        await Assert.That(indexed.CurrentIndex).IsEqualTo(ThirdValue);
        await Assert.That(indexed.PreviousIndex).IsEqualTo(-1);
        await Assert.That(Change<string>.CreateRefresh("new").CurrentIndex).IsEqualTo(-1);
    }

    /// <summary>Verifies equality and non-generic enumeration before and after resetting an enumerator.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChangeSet_Enumerators_CompareSourceAndPosition()
    {
        Change<int>[] changes = [Change<int>.CreateAdd(ChangeValue)];
        using var set = new ChangeSet<int>(changes);
        var first = set.GetEnumerator();
        var second = set.GetEnumerator();
        await Assert.That(first == second).IsTrue();
        await Assert.That(first.Equals((object)second)).IsTrue();
        await Assert.That(first.Equals(new object())).IsFalse();
        await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
        await Assert.That(first.MoveNext()).IsTrue();
        await Assert.That(first != second).IsTrue();
        await Assert.That(((IEnumerator)first).Current).IsEqualTo(changes[0]);
        await Assert.That(first.MoveNext()).IsFalse();
        first.Reset();
        await Assert.That(first == second).IsTrue();
        await Assert.That(first.MoveNext()).IsTrue();
        await Assert.That(first.Current).IsEqualTo(changes[0]);
        first.Dispose();
        second.Dispose();
    }

    /// <summary>Verifies that tracker equality includes both buffers and their valid lengths.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BatchChangeTracker_Equality_ReflectsTrackedChanges()
    {
        var tracker = default(BatchChangeTracker<int>);
        try
        {
            await Assert.That(tracker.Equals(default(BatchChangeTracker<int>))).IsTrue();
            tracker.TrackAdded(1);
            var snapshot = tracker;
            await Assert.That(tracker.Equals(snapshot)).IsTrue();
            await Assert.That(tracker.Equals((object)snapshot)).IsTrue();
            tracker.TrackRemoved(SecondValue);
            await Assert.That(tracker.Equals(snapshot)).IsFalse();
            await Assert.That(tracker.Equals(new object())).IsFalse();
            await Assert.That(tracker.GetHashCode()).IsEqualTo(0);
            tracker.Dispose();
            await Assert.That(tracker.HasChanges).IsFalse();
            await Assert.That(tracker.AddedItems.IsEmpty).IsTrue();
            await Assert.That(tracker.RemovedItems.IsEmpty).IsTrue();
        }
        finally
        {
            tracker.Dispose();
        }
    }

    /// <summary>Verifies current-thread scheduling and the default scheduler's availability.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReactiveListScheduler_Schedule_ExecutesTheAction()
    {
        var called = false;
        using var scheduled = ReactiveListScheduler.Schedule(ReactiveListScheduler.CurrentThread, TimeSpan.Zero, () => called = true);
        await Assert.That(called).IsTrue();
        await Assert.That(ReactiveListScheduler.Default).IsNotNull();
    }

    /// <summary>Verifies that scheduler guards reject missing schedulers and callbacks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReactiveListScheduler_Schedule_RejectsNullArguments()
    {
        await Assert.That(static () => ReactiveListScheduler.Schedule(null!, TimeSpan.Zero, static () => { })).Throws<ArgumentNullException>();
        await Assert.That(static () => ReactiveListScheduler.Schedule(ReactiveListScheduler.CurrentThread, TimeSpan.Zero, null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies empty removal and replacement do not create spurious collection notifications.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReactiveList_EmptyMutations_PreserveEmptySnapshot()
    {
        using var source = new ReactiveList<int>();
        var notifications = 0;
        source.CollectionChanged += (_, _) => notifications++;
        await Assert.That(source.RemoveMany(static _ => true)).IsEqualTo(0);
        source.ReplaceAll([]);
        await Assert.That(source.Count).IsEqualTo(0);
        await Assert.That(notifications).IsEqualTo(0);
        await Assert.That(() => source[-1] = 1).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => source[source.Count] = 1).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Verifies non-generic enumeration uses the same stable snapshot as generic enumeration.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReactiveList_NonGenericEnumeration_PreservesSnapshot()
    {
        using var source = new ReactiveList<int>(1);
        var enumerator = ((IEnumerable)source).GetEnumerator();
        try
        {
            source.Add(SecondValue);
            await Assert.That(enumerator.MoveNext()).IsTrue();
            await Assert.That(enumerator.Current).IsEqualTo(1);
            await Assert.That(enumerator.MoveNext()).IsFalse();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    /// <summary>Verifies the action adapter dispatches with its facade sender and removes its final handler.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NotificationRelay_ActionHandlers_PreserveSenderAndRemoval()
    {
        var facade = new object();
        var receivedSender = default(object);
        var relay = new NotificationRelay<EventArgs>(facade);
        Action<object?, EventArgs> handler = (sender, _) => receivedSender = sender;
        await Assert.That(relay.Add(null)).IsFalse();
        await Assert.That(relay.Remove(null)).IsFalse();
        await Assert.That(relay.Add(handler)).IsTrue();
        relay.Dispatch(EventArgs.Empty);
        await Assert.That(ReferenceEquals(receivedSender, facade)).IsTrue();
        await Assert.That(relay.Remove(handler)).IsTrue();
        receivedSender = null;
        relay.OnEvent(new(), EventArgs.Empty);
        await Assert.That(receivedSender).IsNull();
    }

    /// <summary>Verifies source mutations and selected keys control indexed membership.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DynamicSecondaryIndexView_SourceAndKeys_ReevaluateMembership()
    {
        const int TimeoutSeconds = 10;
        using var source = new QuaternaryList<int>();
        source.AddRange(_secondRow);
        source.AddIndex("Parity", static item => item % SecondValue);
        using var keys = new BehaviorSignal<int[]>([0]);
        using var view = new DynamicSecondaryIndexReactiveView<int, int>(source, "Parity", keys, Sequencer.CurrentThread, TimeSpan.Zero);
        var timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        await Assert.That(() => view.Count).WaitsFor(static assertion => assertion.IsEqualTo(1), timeout);
        source.Add(SecondValue);
        await Assert.That(() => view.Count).WaitsFor(static assertion => assertion.IsEqualTo(SecondValue), timeout);
        await Assert.That(source.Remove(FourthValue)).IsTrue();
        await Assert.That(() => view.Count).WaitsFor(static assertion => assertion.IsEqualTo(1), timeout);
        await Assert.That(view[0]).IsEqualTo(SecondValue);
        keys.OnNext([1]);
        await Assert.That(() => view[0]).WaitsFor(static assertion => assertion.IsEqualTo(ThirdValue), timeout);
        keys.OnNext([]);
        await Assert.That(() => view.Count).WaitsFor(static assertion => assertion.IsEqualTo(0), timeout);
        keys.OnNext([0]);
        await Assert.That(() => view.Count).WaitsFor(static assertion => assertion.IsEqualTo(1), timeout);
        await Assert.That(view[0]).IsEqualTo(SecondValue);
        await Assert.That(source.Remove(SecondValue)).IsTrue();
        await Assert.That(() => view.Count).WaitsFor(static assertion => assertion.IsEqualTo(0), timeout);
    }

    /// <summary>Verifies the smallest power of two is unchanged on each target framework.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RoundUpToPowerOf2_One_ReturnsOne() =>
        await Assert.That(BitOperationsCompat.RoundUpToPowerOf2(1)).IsEqualTo(1U);

    /// <summary>Verifies flat-row materialization from read-only collection and iterator inputs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Reactive2DList_FlatInputs_MaterializeReadOnlyAndIteratorRows()
    {
        using var readOnly = new Reactive2DList<int>(new ReadOnlySequence<int>(_firstRow));
        using var iterator = new Reactive2DList<int>(Iterate(_secondRow));
        await Assert.That(readOnly.Count).IsEqualTo(_firstRow.Length);
        await Assert.That(readOnly[0][0]).IsEqualTo(1);
        await Assert.That(readOnly[1][0]).IsEqualTo(SecondValue);
        await Assert.That(iterator.Count).IsEqualTo(_secondRow.Length);
        await Assert.That(iterator[0][0]).IsEqualTo(ThirdValue);
        await Assert.That(iterator[1][0]).IsEqualTo(FourthValue);
    }

    /// <summary>Verifies nested-row materialization from read-only collection and iterator inputs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Reactive2DList_NestedInputs_MaterializeReadOnlyAndIteratorRows()
    {
        IEnumerable<int>[] rows = [_firstRow, _secondRow];
        using var mutable = new Reactive2DList<int>(rows);
        using var readOnly = new Reactive2DList<int>(new ReadOnlySequence<IEnumerable<int>>(rows));
        using var iterator = new Reactive2DList<int>(Iterate(rows));
        await Assert.That(readOnly.Count).IsEqualTo(rows.Length);
        await Assert.That(mutable.Count).IsEqualTo(rows.Length);
        await Assert.That(mutable[0].ToArray()).IsEquivalentTo(_firstRow);
        await Assert.That(readOnly[0].ToArray()).IsEquivalentTo(_firstRow);
        await Assert.That(iterator.Count).IsEqualTo(rows.Length);
        await Assert.That(iterator[1].ToArray()).IsEquivalentTo(_secondRow);
    }

    /// <summary>Produces a sequence without either collection interface.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The items to enumerate.</param>
    /// <returns>An iterator over the supplied items.</returns>
    private static IEnumerable<T> Iterate<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }

    /// <summary>Exposes count information without the mutable collection interface.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The sequence's backing items.</param>
    private sealed class ReadOnlySequence<T>(T[] items) : IReadOnlyCollection<T>
    {
        /// <inheritdoc/>
        public int Count => items.Length;

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
