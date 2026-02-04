// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using CP.Reactive;
using CP.Reactive.Collections;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Contains unit tests for the QuaternaryDictionary extension methods in QuaternaryExtensions.
/// </summary>
public class QuaternaryDictionaryExtensionsTests
{
    /// <summary>
    /// Verifies that CreateView returns a view with all items when no filter is applied.
    /// </summary>
    [Fact]
    public void CreateView_WithoutFilter_ShouldContainAllItems()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddRange([
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three")
        ]);

        using var view = dict.CreateView(TaskPoolScheduler.Default, throttleMs: 10);

        Assert.Equal(3, view.Items.Count);
    }

    /// <summary>
    /// Verifies that CreateView with filter returns only matching items.
    /// </summary>
    [Fact]
    public void CreateView_WithFilter_ShouldContainOnlyMatchingItems()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddRange([
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three")
        ]);

        using var view = dict.CreateView(kvp => kvp.Value.Length == 3, TaskPoolScheduler.Default, throttleMs: 10);

        Assert.Equal(2, view.Items.Count);
        Assert.Contains(view.Items, kvp => kvp.Value == "one");
        Assert.Contains(view.Items, kvp => kvp.Value == "two");
    }

    /// <summary>
    /// Verifies that CreateViewBySecondaryIndex filters items by the secondary value index key.
    /// </summary>
    [Fact]
    public void CreateViewBySecondaryIndex_ShouldFilterByKey()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex("ByCity", p => p.City);
        dict.AddRange([
            new KeyValuePair<int, TestPerson>(1, new TestPerson("Alice", "NYC")),
            new KeyValuePair<int, TestPerson>(2, new TestPerson("Bob", "LA")),
            new KeyValuePair<int, TestPerson>(3, new TestPerson("Charlie", "NYC"))
        ]);

        using var view = dict.CreateViewBySecondaryIndex<int, TestPerson, string>("ByCity", "NYC", TaskPoolScheduler.Default, throttleMs: 10);

        Assert.Equal(2, view.Items.Count);
        Assert.All(view.Items, kvp => Assert.Equal("NYC", kvp.Value.City));
    }

    /// <summary>
    /// Verifies that CreateViewBySecondaryIndex with multiple keys includes items matching any key.
    /// </summary>
    [Fact]
    public void CreateViewBySecondaryIndex_WithMultipleKeys_ShouldIncludeAllMatches()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex("ByCity", p => p.City);
        dict.AddRange([
            new KeyValuePair<int, TestPerson>(1, new TestPerson("Alice", "NYC")),
            new KeyValuePair<int, TestPerson>(2, new TestPerson("Bob", "LA")),
            new KeyValuePair<int, TestPerson>(3, new TestPerson("Charlie", "Chicago")),
            new KeyValuePair<int, TestPerson>(4, new TestPerson("Diana", "NYC"))
        ]);

        using var view = dict.CreateViewBySecondaryIndex<int, TestPerson, string>("ByCity", new[] { "NYC", "LA" }, TaskPoolScheduler.Default, throttleMs: 10);

        Assert.Equal(3, view.Items.Count);
    }

    /// <summary>
    /// Verifies that ToProperty sets the property correctly.
    /// </summary>
    [Fact]
    public void ToProperty_ShouldSetProperty()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddRange([
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three")
        ]);

        System.Collections.ObjectModel.ReadOnlyObservableCollection<KeyValuePair<int, string>>? result = null;
        using var view = dict.CreateView(TaskPoolScheduler.Default, throttleMs: 10)
            .ToProperty(x => result = x);

        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
    }

    /// <summary>
    /// Verifies that ReactiveView updates when items are added to the source dictionary.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ReactiveView_ShouldUpdateOnAdd()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        using var view = dict.CreateView(TaskPoolScheduler.Default, throttleMs: 50);

        dict.Add(1, "one");

        // Wait for throttle + processing
        await Task.Delay(200);

        Assert.Single(view.Items);
        Assert.Contains(view.Items, kvp => kvp.Key == 1 && kvp.Value == "one");
    }

    /// <summary>
    /// Verifies that ReactiveView updates when items are removed from the source dictionary.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ReactiveView_ShouldUpdateOnRemove()
    {
        using var dict = new QuaternaryDictionary<int, string>();
        dict.AddRange([
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three")
        ]);

        using var view = dict.CreateView(TaskPoolScheduler.Default, throttleMs: 50);

        // Initial state
        Assert.Equal(3, view.Items.Count);

        dict.Remove(2);

        // Wait for throttle + processing
        await Task.Delay(200);

        Assert.Equal(2, view.Items.Count);
        Assert.DoesNotContain(view.Items, kvp => kvp.Key == 2);
    }

    /// <summary>
    /// Verifies that CreateViewBySecondaryIndex updates when new matching items are added.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CreateViewBySecondaryIndex_ShouldUpdateOnAdd()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex("ByCity", p => p.City);
        dict.Add(1, new TestPerson("Alice", "NYC"));

        using var view = dict.CreateViewBySecondaryIndex<int, TestPerson, string>("ByCity", "NYC", TaskPoolScheduler.Default, throttleMs: 50);

        Assert.Single(view.Items);

        dict.Add(2, new TestPerson("Bob", "NYC"));

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
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex("ByCity", p => p.City);
        dict.Add(1, new TestPerson("Alice", "NYC"));

        using var view = dict.CreateViewBySecondaryIndex<int, TestPerson, string>("ByCity", "NYC", TaskPoolScheduler.Default, throttleMs: 50);

        Assert.Single(view.Items);

        dict.Add(2, new TestPerson("Bob", "LA"));

        // Wait for throttle + processing
        await Task.Delay(200);

        // Should still be only 1 item (Alice from NYC)
        Assert.Single(view.Items);
    }

    private record TestPerson(string Name, string City);
}
#endif
