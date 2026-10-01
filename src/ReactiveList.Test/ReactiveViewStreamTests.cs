// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Core;
using CP.Reactive.Views;
#else
using CP.Primitives.Core;
using CP.Primitives.Views;
#endif
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;
using TUnit.Assertions;

namespace ReactiveList.Test;

/// <summary>Verifies streamed updates and refreshes against filtered cache views.</summary>
public class ReactiveViewStreamTests
{
    /// <summary>Verifies updates preserve membership when previous values are missing or filtered out.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Update_FilterMembershipAndMissingPrevious_PreserveSnapshot()
    {
        var original = new FilterItem(true);
        var included = new FilterItem(true);
        var excluded = new FilterItem(false);
        using var fixture = new ViewFixture([original]);
        await fixture.Publish(new(CacheAction.Updated, excluded, Previous: original));
        await Assert.That(fixture.View.Items.Count).IsEqualTo(0);
        await fixture.Publish(new(CacheAction.Updated, included, Previous: original));
        await Assert.That(fixture.View.Items.Count).IsEqualTo(1);
        await Assert.That(ReferenceEquals(fixture.View.Items[0], included)).IsTrue();
        await fixture.Publish(new(CacheAction.Updated, null, Previous: included));
        await Assert.That(fixture.View.Items.Count).IsEqualTo(0);
        await fixture.Publish(new(CacheAction.Updated, included));
        await Assert.That(fixture.View.Items.Count).IsEqualTo(0);
        await fixture.Publish(new(CacheAction.Added, included));
        await fixture.Publish(new(CacheAction.Updated, included));
        await Assert.That(fixture.View.Items.Count).IsEqualTo(1);
        await fixture.Publish(new(CacheAction.Updated, excluded));
        await fixture.Publish(new(CacheAction.Updated, null));
        await Assert.That(fixture.View.Items.Count).IsEqualTo(1);
        await Assert.That(ReferenceEquals(fixture.View.Items[0], included)).IsTrue();
    }

    /// <summary>Verifies refreshes can add, retain and remove the same mutable item without duplication.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Refresh_MutablePredicateMembership_AddsAndRemovesItems()
    {
        var item = new FilterItem(false);
        using var fixture = new ViewFixture([]);
        await fixture.Publish(new(CacheAction.Refreshed, item));
        await Assert.That(fixture.View.Items.Count).IsEqualTo(0);
        item.Included = true;
        await fixture.Publish(new(CacheAction.Refreshed, item));
        await Assert.That(fixture.View.Items.Count).IsEqualTo(1);
        await fixture.Publish(new(CacheAction.Refreshed, item));
        await Assert.That(fixture.View.Items.Count).IsEqualTo(1);
        item.Included = false;
        await fixture.Publish(new(CacheAction.Refreshed, item));
        await Assert.That(fixture.View.Items.Count).IsEqualTo(0);
    }

    /// <summary>Verifies notifications without a usable item or batch leave the view unchanged.</summary>
    /// <param name="action">The action whose absent payload is being tested.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(CacheAction.Added)]
    [Arguments(CacheAction.Removed)]
    [Arguments(CacheAction.Refreshed)]
    [Arguments(CacheAction.BatchAdded)]
    [Arguments(CacheAction.BatchRemoved)]
    [Arguments(CacheAction.Moved)]
    public async Task Notification_MissingPayload_PreservesItems(CacheAction action)
    {
        var item = new FilterItem(true);
        using var fixture = new ViewFixture([item]);
        await fixture.Publish(new(action, null));
        await Assert.That(fixture.View.Items.Count).IsEqualTo(1);
        await Assert.That(ReferenceEquals(fixture.View.Items[0], item)).IsTrue();
    }

    /// <summary>A mutable item whose predicate membership can change independently of its identity.</summary>
    /// <param name="included">The initial filter membership.</param>
    private sealed class FilterItem(bool included)
    {
        /// <summary>Gets or sets whether the item belongs to the filtered view.</summary>
        internal bool Included { get; set; } = included;
    }

    /// <summary>Owns a cache stream and waits for complete buffered notification processing.</summary>
    private sealed class ViewFixture : IDisposable
    {
        /// <summary>The maximum wait for a buffered notification to finish.</summary>
        private const int TimeoutSeconds = 10;

        /// <summary>The manually published cache stream.</summary>
        private readonly Signal<CacheNotify<FilterItem>> _stream = new();

        /// <summary>The number of completed notification batches.</summary>
        private int _revision;

        /// <summary>Initializes a new instance of the fixture.</summary>
        /// <param name="snapshot">The initial included or excluded items.</param>
        internal ViewFixture(FilterItem[] snapshot)
        {
            View = new(_stream, snapshot, static item => item.Included, TimeSpan.FromMilliseconds(1), Sequencer.CurrentThread);
            View.PropertyChanged += OnPropertyChanged;
        }

        /// <summary>Gets the observed filtered view.</summary>
        internal ReactiveView<FilterItem> View { get; }

        /// <summary>Releases the view and its manually published stream.</summary>
        public void Dispose()
        {
            View.Dispose();
            _stream.Dispose();
        }

        /// <summary>Publishes one notification and waits for its complete application.</summary>
        /// <param name="notification">The cache notification to publish.</param>
        /// <returns>A task representing the completed notification.</returns>
        internal async Task Publish(CacheNotify<FilterItem> notification)
        {
            var previousRevision = Volatile.Read(ref _revision);
            _stream.OnNext(notification);
            await Assert.That(() => Volatile.Read(ref _revision))
                .WaitsFor(assertion => assertion.IsGreaterThan(previousRevision), TimeSpan.FromSeconds(TimeoutSeconds));
        }

        /// <summary>Records that the buffered notification finished updating the view.</summary>
        /// <param name="sender">The notifying view.</param>
        /// <param name="eventArgs">The property notification.</param>
        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) => _ = Interlocked.Increment(ref _revision);
    }
}
