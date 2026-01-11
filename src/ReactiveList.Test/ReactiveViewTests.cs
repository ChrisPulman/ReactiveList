// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CP.Reactive;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Tests for ReactiveView.
/// </summary>
public class ReactiveViewTests
{
    /// <summary>
    /// Constructor should throw when stream is null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullStream_ShouldThrow()
    {
        var act = () => new ReactiveView<string>(
            null!,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("stream");
    }

    /// <summary>
    /// Constructor should throw when filter is null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullFilter_ShouldThrow()
    {
        var subject = new Subject<CacheNotify<string>>();

        var act = () => new ReactiveView<string>(
            subject,
            [],
            null!,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("filter");
    }

    /// <summary>
    /// Constructor should load initial snapshot.
    /// </summary>
    [Fact]
    public void Constructor_WithSnapshot_ShouldLoadItems()
    {
        var subject = new Subject<CacheNotify<string>>();
        var snapshot = new[] { "one", "two", "three" };

        using var view = new ReactiveView<string>(
            subject,
            snapshot,
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        view.Items.Should().BeEquivalentTo(["one", "two", "three"]);
    }

    /// <summary>
    /// Constructor should filter snapshot items.
    /// </summary>
    [Fact]
    public void Constructor_WithFilter_ShouldFilterSnapshot()
    {
        var subject = new Subject<CacheNotify<string>>();
        var snapshot = new[] { "apple", "banana", "apricot", "cherry" };

        using var view = new ReactiveView<string>(
            subject,
            snapshot,
            s => s.StartsWith("a"),
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        view.Items.Should().BeEquivalentTo(["apple", "apricot"]);
    }

    /// <summary>
    /// Constructor with null snapshot should not throw.
    /// </summary>
    [Fact]
    public void Constructor_WithNullSnapshot_ShouldNotThrow()
    {
        var subject = new Subject<CacheNotify<string>>();

        var act = () =>
        {
            using var view = new ReactiveView<string>(
                subject,
                null!,
                _ => true,
                TimeSpan.FromMilliseconds(10),
                ImmediateScheduler.Instance);
        };

        act.Should().NotThrow();
    }

    /// <summary>
    /// Items property should be read-only.
    /// </summary>
    [Fact]
    public void Items_ShouldBeReadOnly()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["test"],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        view.Items.Should().BeOfType<System.Collections.ObjectModel.ReadOnlyObservableCollection<string>>();
    }

    /// <summary>
    /// Added notification should add item to view.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task AddedNotification_ShouldAddItemToView()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "newItem"));

        await Task.Delay(50); // Wait for buffer

        view.Items.Should().Contain("newItem");
    }

    /// <summary>
    /// Added notification with filter should only add matching items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task AddedNotification_WithFilter_ShouldOnlyAddMatchingItems()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            s => s.Length > 3,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "ab"));
        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "abcd"));

        await Task.Delay(50);

        view.Items.Should().BeEquivalentTo(["abcd"]);
    }

    /// <summary>
    /// Removed notification should remove item from view.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task RemovedNotification_ShouldRemoveItemFromView()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["one", "two", "three"],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        subject.OnNext(new CacheNotify<string>(CacheAction.Removed, "two"));

        await Task.Delay(50);

        view.Items.Should().BeEquivalentTo(["one", "three"]);
    }

    /// <summary>
    /// Cleared notification should clear view.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ClearedNotification_ShouldClearView()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["one", "two", "three"],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        subject.OnNext(new CacheNotify<string>(CacheAction.Cleared, null));

        await Task.Delay(50);

        view.Items.Should().BeEmpty();
    }

    /// <summary>
    /// BatchOperation notification should add batch items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task BatchOperationNotification_ShouldAddBatchItems()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        var array = ArrayPool<string>.Shared.Rent(10);
        array[0] = "item1";
        array[1] = "item2";
        array[2] = "item3";
        var batch = new PooledBatch<string>(array, 3);

        subject.OnNext(new CacheNotify<string>(CacheAction.BatchOperation, null, batch));

        await Task.Delay(50);

        view.Items.Should().BeEquivalentTo(["item1", "item2", "item3"]);
    }

    /// <summary>
    /// BatchOperation with filter should only add matching items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task BatchOperationNotification_WithFilter_ShouldFilterItems()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            s => s.StartsWith("a"),
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        var array = ArrayPool<string>.Shared.Rent(10);
        array[0] = "apple";
        array[1] = "banana";
        array[2] = "apricot";
        var batch = new PooledBatch<string>(array, 3);

        subject.OnNext(new CacheNotify<string>(CacheAction.BatchOperation, null, batch));

        await Task.Delay(50);

        view.Items.Should().BeEquivalentTo(["apple", "apricot"]);
    }

    /// <summary>
    /// ToProperty should set property.
    /// </summary>
    [Fact]
    public void ToProperty_ShouldSetProperty()
    {
        var subject = new Subject<CacheNotify<string>>();
        System.Collections.ObjectModel.ReadOnlyObservableCollection<string>? capturedItems = null;

        using var view = new ReactiveView<string>(
            subject,
            ["test"],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>
    /// ToProperty should throw when setter is null.
    /// </summary>
    [Fact]
    public void ToProperty_WithNullSetter_ShouldThrow()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        var act = () => view.ToProperty(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>
    /// Dispose should clean up subscription.
    /// </summary>
    [Fact]
    public void Dispose_ShouldCleanUpSubscription()
    {
        var subject = new Subject<CacheNotify<string>>();

        var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        var act = () => view.Dispose();

        act.Should().NotThrow();
    }

    /// <summary>
    /// Multiple dispose should be safe.
    /// </summary>
    [Fact]
    public void Dispose_MultipleCalls_ShouldBeSafe()
    {
        var subject = new Subject<CacheNotify<string>>();

        var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        view.Dispose();
        var act = () => view.Dispose();

        act.Should().NotThrow();
    }

    /// <summary>
    /// PropertyChanged should fire when items updated.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task PropertyChanged_ShouldFireWhenItemsUpdated()
    {
        var subject = new Subject<CacheNotify<string>>();
        var propertyChangedFired = false;

        using var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        view.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Items")
            {
                propertyChangedFired = true;
            }
        };

        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "test"));

        await Task.Delay(50);

        propertyChangedFired.Should().BeTrue();
    }

    /// <summary>
    /// Added notification with null item should not add anything.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task AddedNotification_WithNullItem_ShouldNotAdd()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        subject.OnNext(new CacheNotify<string>(CacheAction.Added, null));

        await Task.Delay(50);

        view.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Removed notification with null item should not throw.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task RemovedNotification_WithNullItem_ShouldNotThrow()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["test"],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        var act = async () =>
        {
            subject.OnNext(new CacheNotify<string>(CacheAction.Removed, null));
            await Task.Delay(50);
        };

        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// Batch notification with null batch should not throw.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task BatchNotification_WithNullBatch_ShouldNotThrow()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        var act = async () =>
        {
            subject.OnNext(new CacheNotify<string>(CacheAction.BatchOperation, null, null));
            await Task.Delay(50);
        };

        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// View should buffer multiple notifications.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task View_ShouldBufferMultipleNotifications()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(50),
            ImmediateScheduler.Instance);

        // Send multiple notifications quickly
        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "one"));
        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "two"));
        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "three"));

        await Task.Delay(100);

        view.Items.Should().BeEquivalentTo(["one", "two", "three"]);
    }

    /// <summary>
    /// Updated action should not add or remove.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task UpdatedAction_ShouldNotChangeItems()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["original"],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        // Updated action is not handled in ApplyChange, so items should remain
        subject.OnNext(new CacheNotify<string>(CacheAction.Updated, "updated"));

        await Task.Delay(50);

        view.Items.Should().BeEquivalentTo(["original"]);
    }
}
#endif
