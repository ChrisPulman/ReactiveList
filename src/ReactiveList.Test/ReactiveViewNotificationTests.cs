// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
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
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;
using TUnit.Assertions;

namespace ReactiveList.Test;

/// <summary>Verifies subscription lifetimes and sender identity on reactive view facades.</summary>
public class ReactiveViewNotificationTests
{
    /// <summary>Verifies that filtered views relay notifications only to registered handlers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FilteredView_Subscriptions_PreserveFacadeSender()
    {
        using var source = new ReactiveList<int>();
        using var view = new FilteredReactiveView<int>(source, static _ => true, Sequencer.CurrentThread, TimeSpan.Zero);
        await VerifySubscriptions(view, view, source.Add, () => view.Count);
    }

    /// <summary>Verifies that dynamically filtered views release their final relay subscriptions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DynamicFilteredView_Subscriptions_PreserveFacadeSender()
    {
        using var source = new ReactiveList<int>();
        using var predicates = new BehaviorSignal<Func<int, bool>>(static _ => true);
        using var view = new DynamicFilteredReactiveView<int>(source, predicates, Sequencer.CurrentThread, TimeSpan.Zero);
        await VerifySubscriptions(view, view, source.Add, () => view.Count);
    }

    /// <summary>Verifies that sorted views relay notifications without exposing their internal collections.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SortedView_Subscriptions_PreserveFacadeSender()
    {
        using var source = new ReactiveList<int>();
        using var view = new SortedReactiveView<int>(source, System.Collections.Generic.Comparer<int>.Default, Sequencer.CurrentThread, TimeSpan.Zero);
        await VerifySubscriptions(view, view, source.Add, () => view.Count);
    }

    /// <summary>Verifies that grouped views preserve subscriber lifetimes as groups are added.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GroupedView_Subscriptions_PreserveFacadeSender()
    {
        using var source = new ReactiveList<int>();
        using var view = new GroupedReactiveView<int, int>(source, static item => item, Sequencer.CurrentThread, TimeSpan.Zero);
        await VerifySubscriptions(view, view, source.Add, () => view.Count);
    }

    /// <summary>Verifies that secondary-index views detach the final collection and property handlers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DynamicSecondaryIndexView_Subscriptions_PreserveFacadeSender()
    {
        using var source = new QuaternaryList<int>();
        source.AddIndex("All", static _ => 0);
        using var keys = new BehaviorSignal<int[]>([0]);
        using var view = new DynamicSecondaryIndexReactiveView<int, int>(source, "All", keys, Sequencer.CurrentThread, TimeSpan.Zero);
        await VerifySubscriptions(view, view, source.Add, () => view.Count);
    }

    /// <summary>Verifies group facades honor handler removal and preserve their public sender.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReactiveGroup_Subscriptions_PreserveFacadeSender()
    {
        var items = new ObservableCollection<int>();
        var group = new ReactiveGroup<string, int>("group", items);
        await VerifySubscriptions(group, group, items.Add, () => group.Count);
    }

    /// <summary>Verifies that individual removals from multicast subscriptions follow native event semantics.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NotificationRelay_MulticastRemoval_RemovesTheLastMatchingHandler()
    {
        const int DuplicateInvocations = 2;
        var firstCalls = 0;
        var secondCalls = 0;
        var facade = new object();
        var relay = new TypedNotificationRelay<NotifyCollectionChangedEventArgs, NotifyCollectionChangedEventHandler>(
            facade,
            static (handler, sender, eventArgs) => handler(sender, eventArgs));
        NotifyCollectionChangedEventHandler first = (_, _) => firstCalls++;
        NotifyCollectionChangedEventHandler second = (_, _) => secondCalls++;
        var notification = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
        await Assert.That(relay.Add(null)).IsFalse();
        await Assert.That(relay.Remove(null)).IsFalse();
        await Assert.That(relay.Add(first + second)).IsTrue();
        await Assert.That(relay.Add(first)).IsFalse();
        relay.OnEvent(new(), notification);
        await Assert.That(firstCalls).IsEqualTo(DuplicateInvocations);
        await Assert.That(secondCalls).IsEqualTo(1);
        await Assert.That(relay.Remove(first)).IsFalse();
        await Assert.That(relay.Remove(second)).IsFalse();
        await Assert.That(relay.Remove(first)).IsTrue();
        relay.Dispatch(notification);
        await Assert.That(firstCalls).IsEqualTo(DuplicateInvocations);
        await Assert.That(secondCalls).IsEqualTo(1);
    }

    /// <summary>Verifies cache-stream view property handlers can be removed independently.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReactiveView_PropertySubscriptions_RemoveHandlers()
    {
        const int SecondItem = 2;
        const int TimeoutSeconds = 10;
        using var source = new QuaternaryList<int>();
        using var view = new ReactiveView<int>(source.Stream, [], static _ => true, TimeSpan.FromMilliseconds(1), Sequencer.CurrentThread);
        var state = new NotificationState(view);
        var timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        view.PropertyChanged += null;
        view.PropertyChanged -= null;
        view.PropertyChanged -= state.FirstProperty;
        view.PropertyChanged += state.FirstProperty;
        view.PropertyChanged += state.SecondProperty;
        source.Add(1);
        await Assert.That(() => state.SecondProperties).WaitsFor(static assertion => assertion.IsGreaterThan(0), timeout);
        view.PropertyChanged -= state.FirstProperty;
        var firstCalls = state.FirstProperties;
        var secondCalls = state.SecondProperties;
        source.Add(SecondItem);
        await Assert.That(() => state.SecondProperties).WaitsFor(assertion => assertion.IsGreaterThan(secondCalls), timeout);
        await Assert.That(state.FirstProperties).IsEqualTo(firstCalls);
        await Assert.That(state.WrongSenders).IsEqualTo(0);
        view.PropertyChanged -= state.SecondProperty;
        view.PropertyChanged -= state.SecondProperty;
    }

    /// <summary>Exercises first, additional, removed and null handlers against a live view.</summary>
    /// <param name="collection">The facade's collection notifications.</param>
    /// <param name="properties">The facade's property notifications.</param>
    /// <param name="mutate">Adds one visible item or group to the source.</param>
    /// <param name="getCount">Returns the number of visible items or groups.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    private static async Task VerifySubscriptions(
        INotifyCollectionChanged collection,
        INotifyPropertyChanged properties,
        Action<int> mutate,
        Func<int> getCount)
    {
        const int SecondMutation = 2;
        const int ThirdMutation = 3;
        const int TimeoutSeconds = 10;
        var timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        var state = new NotificationState(collection);
        var initialCount = getCount();
        collection.CollectionChanged -= state.FirstCollection;
        properties.PropertyChanged -= state.FirstProperty;
        collection.CollectionChanged += null;
        collection.CollectionChanged -= null;
        properties.PropertyChanged += null;
        properties.PropertyChanged -= null;
        collection.CollectionChanged += state.FirstCollection;
        collection.CollectionChanged += state.SecondCollection;
        properties.PropertyChanged += state.FirstProperty;
        properties.PropertyChanged += state.SecondProperty;
        mutate(initialCount + 1);
        await Assert.That(getCount).WaitsFor(assertion => assertion.IsEqualTo(initialCount + 1), timeout);
        await Assert.That(() => state.SecondProperties).WaitsFor(static assertion => assertion.IsGreaterThan(0), timeout);
        await Assert.That(state.FirstCollections).IsGreaterThan(0);
        await Assert.That(state.SecondCollections).IsGreaterThan(0);
        await Assert.That(state.FirstProperties).IsGreaterThan(0);
        await Assert.That(state.WrongSenders).IsEqualTo(0);

        collection.CollectionChanged -= state.FirstCollection;
        properties.PropertyChanged -= state.FirstProperty;
        var firstCollections = state.FirstCollections;
        var firstProperties = state.FirstProperties;
        var secondProperties = state.SecondProperties;
        mutate(initialCount + SecondMutation);
        await Assert.That(getCount).WaitsFor(assertion => assertion.IsEqualTo(initialCount + SecondMutation), timeout);
        await Assert.That(() => state.SecondProperties).WaitsFor(assertion => assertion.IsGreaterThan(secondProperties), timeout);
        await Assert.That(state.FirstCollections).IsEqualTo(firstCollections);
        await Assert.That(state.FirstProperties).IsEqualTo(firstProperties);

        collection.CollectionChanged -= state.SecondCollection;
        properties.PropertyChanged -= state.SecondProperty;
        var secondCollections = state.SecondCollections;
        secondProperties = state.SecondProperties;
        mutate(initialCount + ThirdMutation);
        await Assert.That(getCount).WaitsFor(assertion => assertion.IsEqualTo(initialCount + ThirdMutation), timeout);
        await Assert.That(state.SecondCollections).IsEqualTo(secondCollections);
        await Assert.That(state.SecondProperties).IsEqualTo(secondProperties);
        await Assert.That(state.WrongSenders).IsEqualTo(0);
        collection.CollectionChanged -= state.SecondCollection;
        properties.PropertyChanged -= state.SecondProperty;
    }

    /// <summary>Captures notification counts atomically across source dispatch threads.</summary>
    /// <param name="expectedSender">The public facade that must be reported as the sender.</param>
    private sealed class NotificationState(object expectedSender)
    {
        /// <summary>The first collection handler's notification count.</summary>
        private int _firstCollections;

        /// <summary>The second collection handler's notification count.</summary>
        private int _secondCollections;

        /// <summary>The first property handler's notification count.</summary>
        private int _firstProperties;

        /// <summary>The second property handler's notification count.</summary>
        private int _secondProperties;

        /// <summary>The number of notifications attributed to a private state object.</summary>
        private int _wrongSenders;

        /// <summary>Gets the first collection handler's notification count.</summary>
        internal int FirstCollections => Volatile.Read(ref _firstCollections);

        /// <summary>Gets the second collection handler's notification count.</summary>
        internal int SecondCollections => Volatile.Read(ref _secondCollections);

        /// <summary>Gets the first property handler's notification count.</summary>
        internal int FirstProperties => Volatile.Read(ref _firstProperties);

        /// <summary>Gets the second property handler's notification count.</summary>
        internal int SecondProperties => Volatile.Read(ref _secondProperties);

        /// <summary>Gets the number of notifications with an incorrect sender.</summary>
        internal int WrongSenders => Volatile.Read(ref _wrongSenders);

        /// <summary>Records the first collection handler's notification.</summary>
        /// <param name="sender">The notification sender.</param>
        /// <param name="eventArgs">The collection notification.</param>
        internal void FirstCollection(object? sender, NotifyCollectionChangedEventArgs eventArgs)
        {
            _ = Interlocked.Increment(ref _firstCollections);
            CheckSender(sender);
        }

        /// <summary>Records the second collection handler's notification.</summary>
        /// <param name="sender">The notification sender.</param>
        /// <param name="eventArgs">The collection notification.</param>
        internal void SecondCollection(object? sender, NotifyCollectionChangedEventArgs eventArgs)
        {
            _ = Interlocked.Increment(ref _secondCollections);
            CheckSender(sender);
        }

        /// <summary>Records the first property handler's notification.</summary>
        /// <param name="sender">The notification sender.</param>
        /// <param name="eventArgs">The property notification.</param>
        internal void FirstProperty(object? sender, PropertyChangedEventArgs eventArgs)
        {
            _ = Interlocked.Increment(ref _firstProperties);
            CheckSender(sender);
        }

        /// <summary>Records the second property handler's notification.</summary>
        /// <param name="sender">The notification sender.</param>
        /// <param name="eventArgs">The property notification.</param>
        internal void SecondProperty(object? sender, PropertyChangedEventArgs eventArgs)
        {
            _ = Interlocked.Increment(ref _secondProperties);
            CheckSender(sender);
        }

        /// <summary>Rejects notification senders other than the public facade.</summary>
        /// <param name="sender">The reported notification sender.</param>
        private void CheckSender(object? sender)
        {
            if (ReferenceEquals(sender, expectedSender))
            {
                return;
            }

            _ = Interlocked.Increment(ref _wrongSenders);
        }
    }
}
