// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.ComponentModel;
using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Core;
using CP.Reactive.Views;
#else
using CP.Primitives.Core;
using CP.Primitives.Views;
#endif
using TUnit.Assertions;
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
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_WithNullStream_ShouldThrow()
    {
        var act = static () => new ReactiveView<string>(
            null!,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        await Assert.That(act).Throws<ArgumentNullException>().WithParameterName("stream");
    }

    /// <summary>Constructor should throw when filter is null.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_WithNullFilter_ShouldThrow()
    {
        var subject = new Signal<CacheNotify<string>>();

        var act = () => new ReactiveView<string>(
            subject,
            [],
            null!,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        await Assert.That(act).Throws<ArgumentNullException>().WithParameterName("filter");
    }

    /// <summary>Constructor should load initial snapshot.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_WithSnapshot_ShouldLoadItems()
    {
        var subject = new Signal<CacheNotify<string>>();
        var snapshot = new[] { "one", "two", TestData.ThreeText };

        using var view = new ReactiveView<string>(
            subject,
            snapshot,
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        await Assert.That(view.Items).IsEquivalentTo(["one", "two", TestData.ThreeText]);
    }

    /// <summary>Constructor should filter snapshot items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_WithFilter_ShouldFilterSnapshot()
    {
        var subject = new Signal<CacheNotify<string>>();
        var snapshot = new[] { TestData.AppleText, "banana", TestData.ApricotText, "cherry" };

        using var view = new ReactiveView<string>(
            subject,
            snapshot,
            static s => s.Length > 0 && s[0] == 'a',
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        await Assert.That(view.Items).IsEquivalentTo([TestData.AppleText, TestData.ApricotText]);
    }

    /// <summary>Constructor with null snapshot should not throw.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_WithNullSnapshot_ShouldNotThrow()
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

        await Assert.That(act).ThrowsNothing();
    }

    /// <summary>Items property should be read-only.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Items_ShouldBeReadOnly()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["test"],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        await Assert.That(view.Items).IsTypeOf<System.Collections.ObjectModel.ReadOnlyObservableCollection<string>>();
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

        await Assert.That(view.Items).Contains("newItem");
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

        await Assert.That(view.Items).IsEquivalentTo(["abcd"]);
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

        await Assert.That(view.Items).IsEquivalentTo(["one", TestData.ThreeText]);
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

        await Assert.That(view.Items).IsEmpty();
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

        await Assert.That(view.Items).IsEquivalentTo(["item1", "item2", "item3"]);
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

        await Assert.That(view.Items).IsEquivalentTo([TestData.AppleText, TestData.ApricotText]);
    }

    /// <summary>ToProperty should set property.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ToProperty_ShouldSetProperty()
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

        await Assert.That(result).IsSameReferenceAs(view);
        await Assert.That(capturedItems).IsSameReferenceAs(view.Items);
    }

    /// <summary>ToProperty should throw when setter is null.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ToProperty_WithNullSetter_ShouldThrow()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var act = () => view.ToProperty(null!);

        await Assert.That(act).Throws<ArgumentNullException>().WithParameterName("propertySetter");
    }

    /// <summary>Dispose should clean up subscription.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Dispose_ShouldCleanUpSubscription()
    {
        var subject = new Signal<CacheNotify<string>>();

        var view = new ReactiveView<string>(
            subject,
            [],
            static _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var act = view.Dispose;

        await Assert.That(act).ThrowsNothing();
    }

    /// <summary>Multiple dispose should be safe.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Dispose_MultipleCalls_ShouldBeSafe()
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

        await Assert.That(act).ThrowsNothing();
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

        await Assert.That(view.Items).IsEmpty();
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

        await Assert.That(act).ThrowsNothing();
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

        await Assert.That(act).ThrowsNothing();
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

        await Assert.That(view.Items).IsEquivalentTo(["original"]);
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
