// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using CP.Reactive;
using CP.Reactive.Collections;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Contains unit tests for the QuaternaryExtensions class.
/// </summary>
public class QuaternaryExtensionsTests
{
    /// <summary>
    /// Verifies that CreateView returns a view with all items when no filter is applied.
    /// </summary>
    [Fact]
    public void CreateView_WithoutFilter_ShouldContainAllItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3, 4, 5]);

        using var view = list.CreateView(TaskPoolScheduler.Default, throttleMs: 10);

        Assert.Equal(5, view.Items.Count);
    }

    /// <summary>
    /// Verifies that CreateView with filter returns only matching items.
    /// </summary>
    [Fact]
    public void CreateView_WithFilter_ShouldContainOnlyMatchingItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3, 4, 5]);

        using var view = list.CreateView(x => x % 2 == 0, TaskPoolScheduler.Default, throttleMs: 10);

        Assert.Equal(2, view.Items.Count);
        Assert.Contains(2, view.Items);
        Assert.Contains(4, view.Items);
    }

    /// <summary>
    /// Verifies that CreateViewBySecondaryIndex filters items by the secondary index key.
    /// </summary>
    [Fact]
    public void CreateViewBySecondaryIndex_ShouldFilterByKey()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex("ByCity", p => p.City);
        list.AddRange([
            new TestPerson("Alice", "NYC"),
            new TestPerson("Bob", "LA"),
            new TestPerson("Charlie", "NYC")
        ]);

        using var view = list.CreateViewBySecondaryIndex("ByCity", "NYC", TaskPoolScheduler.Default, throttleMs: 10);

        Assert.Equal(2, view.Items.Count);
        Assert.All(view.Items, p => Assert.Equal("NYC", p.City));
    }

    /// <summary>
    /// Verifies that CreateViewBySecondaryIndex with multiple keys includes items matching any key.
    /// </summary>
    [Fact]
    public void CreateViewBySecondaryIndex_WithMultipleKeys_ShouldIncludeAllMatches()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex("ByCity", p => p.City);
        list.AddRange([
            new TestPerson("Alice", "NYC"),
            new TestPerson("Bob", "LA"),
            new TestPerson("Charlie", "Chicago"),
            new TestPerson("Diana", "NYC")
        ]);

        using var view = list.CreateViewBySecondaryIndex("ByCity", new[] { "NYC", "LA" }, TaskPoolScheduler.Default, throttleMs: 10);

        Assert.Equal(3, view.Items.Count);
    }

    /// <summary>
    /// Verifies that ToProperty sets the property correctly.
    /// </summary>
    [Fact]
    public void ToProperty_ShouldSetProperty()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3]);

        System.Collections.ObjectModel.ReadOnlyObservableCollection<int>? result = null;
        using var view = list.CreateView(TaskPoolScheduler.Default, throttleMs: 10)
            .ToProperty(x => result = x);

        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
    }

    /// <summary>
    /// Verifies that ReactiveView updates when items are added to the source list.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ReactiveView_ShouldUpdateOnAdd()
    {
        using var list = new QuaternaryList<int>();
        using var view = list.CreateView(TaskPoolScheduler.Default, throttleMs: 50);

        list.Add(42);

        // Wait for throttle + processing
        await Task.Delay(200);

        Assert.Single(view.Items);
        Assert.Contains(42, view.Items);
    }

    /// <summary>
    /// Verifies that ReactiveView updates when items are removed from the source list.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ReactiveView_ShouldUpdateOnRemove()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3]);

        using var view = list.CreateView(TaskPoolScheduler.Default, throttleMs: 50);

        // Initial state
        Assert.Equal(3, view.Items.Count);

        list.Remove(2);

        // Wait for throttle + processing
        await Task.Delay(200);

        Assert.Equal(2, view.Items.Count);
        Assert.DoesNotContain(2, view.Items);
    }

    /// <summary>
    /// Verifies that ReactiveView updates when RemoveRange is called.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ReactiveView_ShouldUpdateOnRemoveRange()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, 2, 3, 4, 5]);

        using var view = list.CreateView(TaskPoolScheduler.Default, throttleMs: 50);

        // Initial state
        Assert.Equal(5, view.Items.Count);

        list.RemoveRange([2, 4]);

        // Wait for throttle + processing
        await Task.Delay(200);

        Assert.Equal(3, view.Items.Count);
        Assert.DoesNotContain(2, view.Items);
        Assert.DoesNotContain(4, view.Items);
    }

    /// <summary>
    /// Verifies that CreateViewBySecondaryIndex updates when new matching items are added.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CreateViewBySecondaryIndex_ShouldUpdateOnAdd()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex("ByCity", p => p.City);
        list.Add(new TestPerson("Alice", "NYC"));

        using var view = list.CreateViewBySecondaryIndex("ByCity", "NYC", TaskPoolScheduler.Default, throttleMs: 50);

        Assert.Single(view.Items);

        list.Add(new TestPerson("Bob", "NYC"));

        // Wait for throttle + processing
        await Task.Delay(200);

        Assert.Equal(2, view.Items.Count);
    }

    /// <summary>
    /// Verifies that CreateViewBySecondaryIndex doesn't include non-matching items when added.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CreateViewBySecondaryIndex_ShouldNotIncludeNonMatchingItems()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex("ByCity", p => p.City);
        list.Add(new TestPerson("Alice", "NYC"));

        using var view = list.CreateViewBySecondaryIndex("ByCity", "NYC", TaskPoolScheduler.Default, throttleMs: 50);

        Assert.Single(view.Items);

        list.Add(new TestPerson("Bob", "LA"));

        // Wait for throttle + processing
        await Task.Delay(200);

        // Should still be only 1 item (Alice from NYC)
        Assert.Single(view.Items);
    }

    private record TestPerson(string Name, string City);
}
#endif
