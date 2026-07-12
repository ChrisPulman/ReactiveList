// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Threading.Tasks;
using CP.Primitives.Core;
using CP.Primitives.Views;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for ReactiveView.</summary>
public class ReactiveViewTests
{
    /// <summary>Constructor should throw when stream is null.</summary>
    [Test]
    public void Constructor_WithNullStream_ShouldThrow()
    {
        var act = () => new ReactiveView<string>(
            null!,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        act.Should().Throw<ArgumentNullException>()
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

        act.Should().Throw<ArgumentNullException>()
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
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        view.Items.Should().BeEquivalentTo(["one", "two", TestData.ThreeText]);
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
            s => s.StartsWith("a"),
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        view.Items.Should().BeEquivalentTo([TestData.AppleText, TestData.ApricotText]);
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
                _ => true,
                TimeSpan.FromMilliseconds(TestData.TestValueTen),
                Sequencer.Immediate);
        };

        act.Should().NotThrow();
    }

    /// <summary>Items property should be read-only.</summary>
    [Test]
    public void Items_ShouldBeReadOnly()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["test"],
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        view.Items.Should().BeOfType<System.Collections.ObjectModel.ReadOnlyObservableCollection<string>>();
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
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "newItem"));

        await Task.Delay(TestData.TestValueFifty); // Wait for buffer

        view.Items.Should().Contain("newItem");
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
            s => s.Length > 3,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "ab"));
        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "abcd"));

        await Task.Delay(TestData.TestValueFifty);

        view.Items.Should().BeEquivalentTo(["abcd"]);
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
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        subject.OnNext(new CacheNotify<string>(CacheAction.Removed, "two"));

        await Task.Delay(TestData.TestValueFifty);

        view.Items.Should().BeEquivalentTo(["one", TestData.ThreeText]);
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
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        subject.OnNext(new CacheNotify<string>(CacheAction.Cleared, null));

        await Task.Delay(TestData.TestValueFifty);

        view.Items.Should().BeEmpty();
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
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var array = ArrayPool<string>.Shared.Rent(TestData.TestValueTen);
        array[0] = "item1";
        array[1] = "item2";
        array[TestData.TestValueTwo] = "item3";
        var batch = new PooledBatch<string>(array, TestData.TestValueThree);

        subject.OnNext(new CacheNotify<string>(CacheAction.BatchOperation, null, batch));

        await Task.Delay(TestData.TestValueFifty);

        view.Items.Should().BeEquivalentTo(["item1", "item2", "item3"]);
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
            s => s.StartsWith("a"),
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var array = ArrayPool<string>.Shared.Rent(TestData.TestValueTen);
        array[0] = TestData.AppleText;
        array[1] = "banana";
        array[TestData.TestValueTwo] = TestData.ApricotText;
        var batch = new PooledBatch<string>(array, TestData.TestValueThree);

        subject.OnNext(new CacheNotify<string>(CacheAction.BatchOperation, null, batch));

        await Task.Delay(TestData.TestValueFifty);

        view.Items.Should().BeEquivalentTo([TestData.AppleText, TestData.ApricotText]);
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
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>ToProperty should throw when setter is null.</summary>
    [Test]
    public void ToProperty_WithNullSetter_ShouldThrow()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var act = () => view.ToProperty(null!);

        act.Should().Throw<ArgumentNullException>()
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
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var act = view.Dispose;

        act.Should().NotThrow();
    }

    /// <summary>Multiple dispose should be safe.</summary>
    [Test]
    public void Dispose_MultipleCalls_ShouldBeSafe()
    {
        var subject = new Signal<CacheNotify<string>>();

        var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        view.Dispose();
        var act = view.Dispose;

        act.Should().NotThrow();
    }

    /// <summary>PropertyChanged should fire when items updated.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task PropertyChanged_ShouldFireWhenItemsUpdated()
    {
        var subject = new Signal<CacheNotify<string>>();
        var propertyChangedFired = false;

        using var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        view.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != "Items")
            {
                return;
            }

            propertyChangedFired = true;
        };

        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "test"));

        await Task.Delay(TestData.TestValueFifty);

        propertyChangedFired.Should().BeTrue();
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
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        subject.OnNext(new CacheNotify<string>(CacheAction.Added, null));

        await Task.Delay(TestData.TestValueFifty);

        view.Items.Should().BeEmpty();
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
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var act = async () =>
        {
            subject.OnNext(new CacheNotify<string>(CacheAction.Removed, null));
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
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var act = async () =>
        {
            subject.OnNext(new CacheNotify<string>(CacheAction.BatchOperation, null, null));
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

        using var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueFifty),
            Sequencer.Immediate);

        // Send multiple notifications quickly
        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "one"));
        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "two"));
        subject.OnNext(new CacheNotify<string>(CacheAction.Added, TestData.ThreeText));

        await Task.Delay(TestData.TestValueOneHundred);

        view.Items.Should().BeEquivalentTo(["one", "two", TestData.ThreeText]);
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
            _ => true,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        // Updated action is not handled in ApplyChange, so items should remain
        subject.OnNext(new CacheNotify<string>(CacheAction.Updated, "updated"));

        await Task.Delay(TestData.TestValueFifty);

        view.Items.Should().BeEquivalentTo(["original"]);
    }
}
