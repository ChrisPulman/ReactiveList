// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.ComponentModel;
using System.Threading.Tasks;
using CP.Primitives.Core;
using CP.Primitives.Views;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for ReactiveView.</summary>
public class ReactiveViewTests
{
    /// <summary>The notification timeout in seconds.</summary>
    private const int NotificationTimeoutSeconds = 5;

    /// <summary>The maximum time to wait for a buffered view notification.</summary>
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(NotificationTimeoutSeconds);

    /// <summary>Constructor should throw when stream is null.</summary>
    [Test]
    public void Constructor_WithNullStream_ShouldThrow()
    {
        var act = static () => new ReactiveView<string>(
            null!,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("stream");
    }

    /// <summary>Constructor should throw when filter is null.</summary>
    [Test]
    public void Constructor_WithNullFilter_ShouldThrow()
    {
        var subject = new Signal<CacheNotify<string>>();

        var act = () => new ReactiveView<string>(
            subject,
            [],
            null!,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("filter");
    }

    /// <summary>Constructor should load initial snapshot.</summary>
    [Test]
    public void Constructor_WithSnapshot_ShouldLoadItems()
    {
        var subject = new Signal<CacheNotify<string>>();
        var snapshot = new[] { "one", "two", TestData.ThreeText };

        using var view = new ReactiveView<string>(
            subject,
            snapshot,
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        _ = view.Items.Should().BeEquivalentTo(["one", "two", TestData.ThreeText]);
    }

    /// <summary>Constructor should filter snapshot items.</summary>
    [Test]
    public void Constructor_WithFilter_ShouldFilterSnapshot()
    {
        var subject = new Signal<CacheNotify<string>>();
        var snapshot = new[] { TestData.AppleText, "banana", TestData.ApricotText, "cherry" };

        using var view = new ReactiveView<string>(
            subject,
            snapshot,
            static s => s.Length > 0 && s[0] == 'a',
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        _ = view.Items.Should().BeEquivalentTo([TestData.AppleText, TestData.ApricotText]);
    }

    /// <summary>Constructor with null snapshot should not throw.</summary>
    [Test]
    public void Constructor_WithNullSnapshot_ShouldNotThrow()
    {
        var subject = new Signal<CacheNotify<string>>();

        var act = () =>
        {
            using var view = new ReactiveView<string>(
                subject,
                null!,
                static _ => true,
                TimeSpan.FromMilliseconds(TestData.TestValueTen),
                Sequencer.Immediate);
        };

        _ = act.Should().NotThrow();
    }

    /// <summary>Items property should be read-only.</summary>
    [Test]
    public void Items_ShouldBeReadOnly()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["test"],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        _ = view.Items.Should().BeOfType<System.Collections.ObjectModel.ReadOnlyObservableCollection<string>>();
    }

    /// <summary>Added notification should add item to view.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task AddedNotification_ShouldAddItemToView()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        subject.OnNext(new(CacheAction.Added, "newItem"));

        await Task.Delay(TestData.TestValueFifty); // Wait for buffer

        _ = view.Items.Should().Contain("newItem");
    }

    /// <summary>Added notification with filter should only add matching items.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task AddedNotification_WithFilter_ShouldOnlyAddMatchingItems()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            static s => s.Length > 3,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        subject.OnNext(new(CacheAction.Added, "ab"));
        subject.OnNext(new(CacheAction.Added, "abcd"));

        await Task.Delay(TestData.TestValueFifty);

        _ = view.Items.Should().BeEquivalentTo(["abcd"]);
    }

    /// <summary>Removed notification should remove item from view.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task RemovedNotification_ShouldRemoveItemFromView()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["one", "two", TestData.ThreeText],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        subject.OnNext(new(CacheAction.Removed, "two"));

        await Task.Delay(TestData.TestValueFifty);

        _ = view.Items.Should().BeEquivalentTo(["one", TestData.ThreeText]);
    }

    /// <summary>Cleared notification should clear view.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ClearedNotification_ShouldClearView()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["one", "two", TestData.ThreeText],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        subject.OnNext(new(CacheAction.Cleared, null));

        await Task.Delay(TestData.TestValueFifty);

        _ = view.Items.Should().BeEmpty();
    }

    /// <summary>BatchOperation notification should add batch items.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task BatchOperationNotification_ShouldAddBatchItems()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var array = ArrayPool<string>.Shared.Rent(TestData.TestValueTen);
        array[0] = "item1";
        array[1] = "item2";
        array[TestData.TestValueTwo] = "item3";
        var batch = new PooledBatch<string>(array, TestData.TestValueThree);

        subject.OnNext(new(CacheAction.BatchOperation, null, batch));

        await Task.Delay(TestData.TestValueFifty);

        _ = view.Items.Should().BeEquivalentTo(["item1", "item2", "item3"]);
    }

    /// <summary>BatchOperation with filter should only add matching items.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task BatchOperationNotification_WithFilter_ShouldFilterItems()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            static s => s.Length > 0 && s[0] == 'a',
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var array = ArrayPool<string>.Shared.Rent(TestData.TestValueTen);
        array[0] = TestData.AppleText;
        array[1] = "banana";
        array[TestData.TestValueTwo] = TestData.ApricotText;
        var batch = new PooledBatch<string>(array, TestData.TestValueThree);

        subject.OnNext(new(CacheAction.BatchOperation, null, batch));

        await Task.Delay(TestData.TestValueFifty);

        _ = view.Items.Should().BeEquivalentTo([TestData.AppleText, TestData.ApricotText]);
    }

    /// <summary>ToProperty should set property.</summary>
    [Test]
    public void ToProperty_ShouldSetProperty()
    {
        var subject = new Signal<CacheNotify<string>>();
        System.Collections.ObjectModel.ReadOnlyObservableCollection<string>? capturedItems = null;

        using var view = new ReactiveView<string>(
            subject,
            ["test"],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var result = view.ToProperty(items => capturedItems = items);

        _ = result.Should().BeSameAs(view);
        _ = capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>ToProperty should throw when setter is null.</summary>
    [Test]
    public void ToProperty_WithNullSetter_ShouldThrow()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var act = () => view.ToProperty(null!);

        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>Dispose should clean up subscription.</summary>
    [Test]
    public void Dispose_ShouldCleanUpSubscription()
    {
        var subject = new Signal<CacheNotify<string>>();

        var view = new ReactiveView<string>(
            subject,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var act = view.Dispose;

        _ = act.Should().NotThrow();
    }

    /// <summary>Multiple dispose should be safe.</summary>
    [Test]
    public void Dispose_MultipleCalls_ShouldBeSafe()
    {
        var subject = new Signal<CacheNotify<string>>();

        var view = new ReactiveView<string>(
            subject,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        view.Dispose();
        var act = view.Dispose;

        _ = act.Should().NotThrow();
    }

    /// <summary>PropertyChanged should fire when items updated.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task PropertyChanged_ShouldFireWhenItemsUpdated()
    {
        var subject = new Signal<CacheNotify<string>>();
        var propertyChanged = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var view = new ReactiveView<string>(
            subject,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        view.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(view.Items))
            {
                return;
            }

            _ = propertyChanged.TrySetResult(true);
        };

        subject.OnNext(new(CacheAction.Added, "test"));

        await AssertCompletesAsync(propertyChanged.Task);
        await TUnit.Assertions.Assert.That(await propertyChanged.Task).IsTrue();
    }

    /// <summary>PropertyChanged should preserve facade sender and unsubscription semantics.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task PropertyChanged_ShouldRelayFacadeSenderAndAllowUnsubscription()
    {
        var subject = new Signal<CacheNotify<string>>();
        var notificationCount = 0;
        object? sender = null;

        using var view = new ReactiveView<string>(
            subject,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        PropertyChangedEventHandler handler = (eventSender, eventArgs) =>
        {
            if (eventArgs.PropertyName != nameof(view.Items))
            {
                return;
            }

            sender = eventSender;
            notificationCount++;
        };

        view.PropertyChanged += handler;
        subject.OnNext(new(CacheAction.Added, "first"));
        await Task.Delay(TestData.TestValueFifty);

        await TUnit.Assertions.Assert.That(ReferenceEquals(sender, view)).IsTrue();
        await TUnit.Assertions.Assert.That(notificationCount).IsEqualTo(1);

        view.PropertyChanged -= handler;
        subject.OnNext(new(CacheAction.Added, "second"));
        await Task.Delay(TestData.TestValueFifty);

        await TUnit.Assertions.Assert.That(notificationCount).IsEqualTo(1);
    }

    /// <summary>Added notification with null item should not add anything.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task AddedNotification_WithNullItem_ShouldNotAdd()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        subject.OnNext(new(CacheAction.Added, null));

        await Task.Delay(TestData.TestValueFifty);

        _ = view.Items.Should().BeEmpty();
    }

    /// <summary>Removed notification with null item should not throw.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task RemovedNotification_WithNullItem_ShouldNotThrow()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["test"],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var act = async () =>
        {
            subject.OnNext(new(CacheAction.Removed, null));
            await Task.Delay(TestData.TestValueFifty);
        };

        await act.Should().NotThrowAsync();
    }

    /// <summary>Batch notification with null batch should not throw.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task BatchNotification_WithNullBatch_ShouldNotThrow()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var act = async () =>
        {
            subject.OnNext(new(CacheAction.BatchOperation, null));
            await Task.Delay(TestData.TestValueFifty);
        };

        await act.Should().NotThrowAsync();
    }

    /// <summary>View should buffer multiple notifications.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task View_ShouldBufferMultipleNotifications()
    {
        var subject = new Signal<CacheNotify<string>>();
        var propertyChanged = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var view = new ReactiveView<string>(
            subject,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueFifty),
            Sequencer.Immediate);

        view.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(view.Items))
            {
                return;
            }

            _ = propertyChanged.TrySetResult(true);
        };

        // Send multiple notifications quickly
        subject.OnNext(new(CacheAction.Added, "one"));
        subject.OnNext(new(CacheAction.Added, "two"));
        subject.OnNext(new(CacheAction.Added, TestData.ThreeText));

        await AssertCompletesAsync(propertyChanged.Task);
        await TUnit.Assertions.Assert.That(view.Items.Count).IsEqualTo(TestData.TestValueThree);
        await TUnit.Assertions.Assert.That(view.Items[0]).IsEqualTo("one");
        await TUnit.Assertions.Assert.That(view.Items[1]).IsEqualTo("two");
        await TUnit.Assertions.Assert.That(view.Items[TestData.TestValueTwo]).IsEqualTo(TestData.ThreeText);
    }

    /// <summary>Updated action should not add or remove.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task UpdatedAction_ShouldNotChangeItems()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["original"],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        // Updated action is not handled in ApplyChange, so items should remain
        subject.OnNext(new(CacheAction.Updated, "updated"));

        await Task.Delay(TestData.TestValueFifty);

        _ = view.Items.Should().BeEquivalentTo(["original"]);
    }

    /// <summary>Waits for an asynchronous view notification without relying on a scheduler-sensitive fixed delay.</summary>
    /// <param name="task">The notification task to await.</param>
    /// <returns>A task that completes when the notification arrives or the timeout is asserted.</returns>
    private static async Task AssertCompletesAsync(Task task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(NotificationTimeout));
        await TUnit.Assertions.Assert.That(ReferenceEquals(completed, task)).IsTrue();
    }
}
