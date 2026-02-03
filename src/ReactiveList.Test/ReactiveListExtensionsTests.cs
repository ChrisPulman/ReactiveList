// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CP.Reactive;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Tests;

/// <summary>
/// Tests for ReactiveListExtensions.
/// </summary>
public class ReactiveListExtensionsTests
{
    /// <summary>
    /// Tests that WhereChanges filters changes by predicate.
    /// </summary>
    [Fact]
    public void WhereChanges_FiltersChangesByPredicate()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        var addedItems = new List<int>();

        using var subscription = list.Connect()
            .WhereChanges(c => c.Current > 5)
            .Subscribe((Action<ChangeSet<int>>)(cs =>
            {
                for (var i = 0; i < cs.Count; i++)
                {
                    addedItems.Add(cs[i].Current);
                }
            }));

        // Act
        list.Add(3);
        list.Add(7);
        list.Add(2);
        list.Add(10);

        // Assert
        addedItems.Should().BeEquivalentTo(new[] { 7, 10 });
    }

    /// <summary>
    /// Tests that WhereReason filters by specific change reason.
    /// </summary>
    [Fact]
    public void WhereReason_FiltersAddOnly()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<string>();
        var addCount = 0;

        using var subscription = list.Connect()
            .WhereReason(CP.Reactive.ChangeReason.Add)
            .Subscribe((Action<ChangeSet<string>>)(cs => addCount++));

        // Act
        list.Add("one");
        list.Add("two");
        list.Remove("one");

        // Assert - should see 2 adds, not the remove
        addCount.Should().Be(2);
    }

    /// <summary>
    /// Tests that OnAdd returns only added items.
    /// </summary>
    [Fact]
    public void OnAdd_ReturnsAddedItems()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        var addedItems = new List<int>();

        using var subscription = list.Connect()
            .OnAdd()
            .Subscribe((Action<int>)(item => addedItems.Add(item)));

        // Act
        list.Add(1);
        list.Add(2);
        list.Add(3);

        // Assert
        addedItems.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// Tests that OnRemove returns only removed items.
    /// </summary>
    [Fact]
    public void OnRemove_ReturnsRemovedItems()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        var removedItems = new List<int>();

        using var subscription = list.Connect()
            .OnRemove()
            .Subscribe((Action<int>)(item => removedItems.Add(item)));

        // Act
        list.Add(1);
        list.Add(2);
        list.Remove(1);

        // Assert
        removedItems.Should().BeEquivalentTo(new[] { 1 });
    }

    /// <summary>
    /// Tests that SelectChanges transforms items correctly.
    /// </summary>
    [Fact]
    public void SelectChanges_TransformsItems()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        var transformedItems = new List<string>();

        using var subscription = list.Connect()
            .SelectChanges((int i) => $"Item_{i}")
            .Subscribe((Action<string>)(item => transformedItems.Add(item)));

        // Act
        list.Add(1);
        list.Add(2);
        list.Add(3);

        // Assert
        transformedItems.Should().BeEquivalentTo(new[] { "Item_1", "Item_2", "Item_3" });
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Tests that CreateView creates a filtered view.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
    public async Task CreateView_CreatesFilteredView()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

        // Act
        using var view = list.CreateView(x => x > 5, ImmediateScheduler.Instance, 0);

        // Allow time for initial sync
        await Task.Delay(50);

        // Assert
        view.Count.Should().Be(5);
        view.Should().BeEquivalentTo(new[] { 6, 7, 8, 9, 10 });
    }

    /// <summary>
    /// Tests that CreateView updates when source changes.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
    public async Task CreateView_UpdatesOnSourceChange()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3 });

        using var view = list.CreateView(x => x > 1, ImmediateScheduler.Instance, 0);
        await Task.Delay(50);

        // Act
        list.Add(5);
        await Task.Delay(100);

        // Assert
        view.Should().BeEquivalentTo(new[] { 2, 3, 5 });
    }

    /// <summary>
    /// Tests that DynamicFilteredView updates when filter changes.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
    public async Task DynamicCreateView_UpdatesOnFilterChange()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5 });

        var filterSubject = new BehaviorSubject<Func<int, bool>>(_ => true);

        using var view = list.CreateView(filterSubject, ImmediateScheduler.Instance, 0);
        await Task.Delay(50);

        view.Count.Should().Be(5);

        // Act - change filter
        filterSubject.OnNext(x => x > 3);
        await Task.Delay(100);

        // Assert
        view.Should().BeEquivalentTo(new[] { 4, 5 });
    }

    /// <summary>
    /// Tests that SortBy creates a sorted view.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
    public async Task SortBy_CreatesSortedView()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        list.AddRange(new[] { 5, 2, 8, 1, 9, 3 });

        // Act - sort ascending
        using var view = list.SortBy(Comparer<int>.Default, ImmediateScheduler.Instance, 0);
        await Task.Delay(50);

        // Assert
        view.Should().BeEquivalentTo(new[] { 1, 2, 3, 5, 8, 9 }, options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Tests that SortBy with key selector creates a sorted view.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
    public async Task SortBy_WithKeySelector_CreatesSortedView()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<string>();
        list.AddRange(new[] { "banana", "apple", "cherry" });

        // Act - sort by length
        using var view = list.SortBy((string s) => s.Length, scheduler: ImmediateScheduler.Instance, throttleMs: 0);
        await Task.Delay(50);

        // Assert (apple=5, cherry=6, banana=6 - but banana comes before cherry alphabetically when lengths equal)
        view.Count.Should().Be(3);
        view[0].Should().Be("apple");
    }

    /// <summary>
    /// Tests that GroupBy creates a grouped view.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
    public async Task GroupBy_CreatesGroupedView()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5, 6 });

        // Act - group by even/odd
        using var view = list.GroupBy((int x) => x % 2 == 0 ? "even" : "odd", ImmediateScheduler.Instance, 0);
        await Task.Delay(50);

        // Assert
        view.Count.Should().Be(2);
        view.ContainsKey("odd").Should().BeTrue();
        view.ContainsKey("even").Should().BeTrue();
        view["odd"].Should().BeEquivalentTo(new[] { 1, 3, 5 });
        view["even"].Should().BeEquivalentTo(new[] { 2, 4, 6 });
    }

    /// <summary>
    /// Tests that GroupBy updates when items are added.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
    public async Task GroupBy_UpdatesOnAdd()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3 });

        using var view = list.GroupBy((int x) => x % 2 == 0 ? "even" : "odd", ImmediateScheduler.Instance, 0);
        await Task.Delay(50);

        // Act
        list.Add(4);
        await Task.Delay(100);

        // Assert
        view["even"].Should().BeEquivalentTo(new[] { 2, 4 });
    }

    /// <summary>
    /// Tests that AddRange with ReadOnlySpan works correctly.
    /// </summary>
    [Fact]
    public void AddRange_WithSpan_AddsItems()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        ReadOnlySpan<int> items = stackalloc int[] { 1, 2, 3, 4, 5 };

        // Act
        list.AddRange(items);

        // Assert
        list.Count.Should().Be(5);
        list.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    /// <summary>
    /// Tests that CopyTo with Span works correctly.
    /// </summary>
    [Fact]
    public void CopyTo_WithSpan_CopiesItems()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5 });
        Span<int> destination = stackalloc int[5];

        // Act
        list.CopyTo(destination);

        // Assert
        destination.ToArray().Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    /// <summary>
    /// Tests that AsSpan returns correct data.
    /// </summary>
    [Fact]
    public void AsSpan_ReturnsItems()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3 });

        // Act
        var span = list.AsSpan();

        // Assert
        span.Length.Should().Be(3);
        span[0].Should().Be(1);
        span[1].Should().Be(2);
        span[2].Should().Be(3);
    }

    /// <summary>
    /// Tests that AsMemory returns correct data.
    /// </summary>
    [Fact]
    public void AsMemory_ReturnsItems()
    {
        // Arrange
        using var list = new CP.Reactive.ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3 });

        // Act
        var memory = list.AsMemory();

        // Assert
        memory.Length.Should().Be(3);
        memory.Span[0].Should().Be(1);
        memory.Span[1].Should().Be(2);
        memory.Span[2].Should().Be(3);
    }
#endif
}
