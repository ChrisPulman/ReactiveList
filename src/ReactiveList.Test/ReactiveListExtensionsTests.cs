// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Tests;

/// <summary>Tests for ReactiveListExtensions.</summary>
public class ReactiveListExtensionsTests
{
    /// <summary>Tests that WhereChanges filters changes by predicate.</summary>
    [Test]
    public void WhereChanges_FiltersChangesByPredicate()
    {
        // Arrange
        using var list = new ReactiveList<int>();
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
        addedItems.Should().BeEquivalentTo([7, 10]);
    }

    /// <summary>Tests that WhereReason filters by specific change reason.</summary>
    [Test]
    public void WhereReason_FiltersAddOnly()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        var addCount = 0;

        using var subscription = list.Connect()
            .WhereReason(CP.Primitives.Core.ChangeReason.Add)
            .Subscribe((Action<ChangeSet<string>>)(cs => addCount++));

        // Act
        list.Add("one");
        list.Add("two");
        list.Remove("one");

        // Assert - should see 2 adds, not the remove
        addCount.Should().Be(2);
    }

    /// <summary>Tests that OnAdd returns only added items.</summary>
    [Test]
    public void OnAdd_ReturnsAddedItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var addedItems = new List<int>();

        using var subscription = list.Connect()
            .OnAdd()
            .Subscribe((Action<int>)(item => addedItems.Add(item)));

        // Act
        list.Add(1);
        list.Add(2);
        list.Add(3);

        // Assert
        addedItems.Should().BeEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests that OnRemove returns only removed items.</summary>
    [Test]
    public void OnRemove_ReturnsRemovedItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var removedItems = new List<int>();

        using var subscription = list.Connect()
            .OnRemove()
            .Subscribe((Action<int>)(item => removedItems.Add(item)));

        // Act
        list.Add(1);
        list.Add(2);
        list.Remove(1);

        // Assert
        removedItems.Should().BeEquivalentTo([1]);
    }

    /// <summary>Tests that SelectChanges transforms items correctly using change selector.</summary>
    [Test]
    public void SelectChanges_TransformsItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var transformedItems = new List<string>();

        // Use the overload that takes Func<Change<T>, TResult> to get individual transformed items
        using var subscription = list.Connect()
            .SelectChanges((Change<int> c) => $"Item_{c.Current}")
            .Subscribe(transformedItems.Add);

        // Act
        list.Add(1);
        list.Add(2);
        list.Add(3);

        // Assert
        transformedItems.Should().BeEquivalentTo(["Item_1", "Item_2", "Item_3"]);
    }

#if NET6_0_OR_GREATER || NETFRAMEWORK
    /// <summary>Tests that CreateView creates a filtered view.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateView_CreatesFilteredView()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

        // Act
        using var view = list.CreateView(x => x > 5, Sequencer.Immediate, 0);

        // Allow time for initial sync
        await Task.Delay(50);

        // Assert
        view.Count.Should().Be(5);
        view.Should().BeEquivalentTo([6, 7, 8, 9, 10]);
    }

    /// <summary>Tests that CreateView updates when source changes.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateView_UpdatesOnSourceChange()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3 });

        using var view = list.CreateView(x => x > 1, Sequencer.Immediate, 0);
        await Task.Delay(50);

        // Act
        list.Add(5);
        await Task.Delay(100);

        // Assert
        view.Should().BeEquivalentTo([2, 3, 5]);
    }

    /// <summary>Tests that DynamicFilteredView updates when filter changes.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task DynamicCreateView_UpdatesOnFilterChange()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5 });

        var filterSubject = new BehaviorSignal<Func<int, bool>>(_ => true);

        using var view = list.CreateView(filterSubject, Sequencer.Immediate, 0);
        await Task.Delay(50);

        view.Count.Should().Be(5);

        // Act - change filter
        filterSubject.OnNext(x => x > 3);
        await Task.Delay(100);

        // Assert
        view.Should().BeEquivalentTo([4, 5]);
    }

    /// <summary>Tests that SortBy creates a sorted view.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task SortBy_CreatesSortedView()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 5, 2, 8, 1, 9, 3 });

        // Act - sort ascending
        using var view = list.SortBy(Comparer<int>.Default, Sequencer.Immediate, 0);
        await Task.Delay(50);

        // Assert
        view.Should().BeEquivalentTo([1, 2, 3, 5, 8, 9], options => options.WithStrictOrdering());
    }

    /// <summary>Tests that SortBy with key selector creates a sorted view.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task SortBy_WithKeySelector_CreatesSortedView()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        list.AddRange(new[] { "banana", "apple", "cherry" });

        // Act - sort by length
        using var view = list.SortBy((string s) => s.Length, scheduler: Sequencer.Immediate, throttleMs: 0);
        await Task.Delay(50);

        // Assert (apple=5, cherry=6, banana=6 - but banana comes before cherry alphabetically when lengths equal)
        view.Count.Should().Be(3);
        view[0].Should().Be("apple");
    }

    /// <summary>Tests that GroupBy creates a grouped view.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task GroupBy_CreatesGroupedView()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5, 6 });

        // Act - group by even/odd
        using var view = list.GroupBy((int x) => x % 2 == 0 ? "even" : "odd", Sequencer.Immediate, 0);
        await Task.Delay(50);

        // Assert
        view.Count.Should().Be(2);
        view.ContainsKey("odd").Should().BeTrue();
        view.ContainsKey("even").Should().BeTrue();
        view["odd"].Should().BeEquivalentTo([1, 3, 5]);
        view["even"].Should().BeEquivalentTo([2, 4, 6]);
    }

    /// <summary>Tests that GroupBy updates when items are added.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task GroupBy_UpdatesOnAdd()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3 });

        using var view = list.GroupBy((int x) => x % 2 == 0 ? "even" : "odd", Sequencer.Immediate, 0);
        await Task.Delay(50);

        // Act
        list.Add(4);
        await Task.Delay(100);

        // Assert
        view["even"].Should().BeEquivalentTo([2, 4]);
    }

    /// <summary>Tests that AddRange with ReadOnlySpan works correctly.</summary>
    [Test]
    public void AddRange_WithSpan_AddsItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        ReadOnlySpan<int> items = stackalloc int[] { 1, 2, 3, 4, 5 };

        // Act
        list.AddRange(items);

        // Assert
        list.Count.Should().Be(5);
        list.Should().BeEquivalentTo([1, 2, 3, 4, 5]);
    }

    /// <summary>Tests that CopyTo with Span works correctly.</summary>
    [Test]
    public void CopyTo_WithSpan_CopiesItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3, 4, 5 });
        Span<int> destination = stackalloc int[5];

        // Act
        list.CopyTo(destination);

        // Assert
        destination.ToArray().Should().BeEquivalentTo([1, 2, 3, 4, 5]);
    }

    /// <summary>Tests that AsSpan returns correct data.</summary>
    [Test]
    public void AsSpan_ReturnsItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3 });

        // Act
        var span = list.AsSpan();

        // Assert
        span.Length.Should().Be(3);
        span[0].Should().Be(1);
        span[1].Should().Be(2);
        span[2].Should().Be(3);
    }

    /// <summary>Tests that AsMemory returns correct data.</summary>
    [Test]
    public void AsMemory_ReturnsItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
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
