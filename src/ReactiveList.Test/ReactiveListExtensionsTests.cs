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
using ReactiveList.Test;
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
            .WhereChanges(c => c.Current > TestData.TestValueFive)
            .Subscribe((Action<ChangeSet<int>>)(cs =>
            {
                for (var i = 0; i < cs.Count; i++)
                {
                    addedItems.Add(cs[i].Current);
                }
            }));

        // Act
        list.Add(TestData.TestValueThree);
        list.Add(TestData.TestValueSeven);
        list.Add(TestData.TestValueTwo);
        list.Add(TestData.TestValueTen);

        // Assert
        addedItems.Should().BeEquivalentTo([TestData.TestValueSeven, TestData.TestValueTen]);
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
        addCount.Should().Be(TestData.TestValueTwo);
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
            .Subscribe((Action<int>)addedItems.Add);

        // Act
        list.Add(1);
        list.Add(TestData.TestValueTwo);
        list.Add(TestData.TestValueThree);

        // Assert
        addedItems.Should().BeEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree]);
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
            .Subscribe((Action<int>)removedItems.Add);

        // Act
        list.Add(1);
        list.Add(TestData.TestValueTwo);
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
            .SelectChanges((c) => $"Item_{c.Current}")
            .Subscribe(transformedItems.Add);

        // Act
        list.Add(1);
        list.Add(TestData.TestValueTwo);
        list.Add(TestData.TestValueThree);

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
        list.AddRange(new[] { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix, TestData.TestValueSeven, TestData.TestValueEight, TestData.TestValueNine, TestData.TestValueTen });

        // Act
        using var view = list.CreateView(x => x > TestData.TestValueFive, Sequencer.Immediate, 0);

        // Allow time for initial sync
        await Task.Delay(TestData.TestValueFifty);

        // Assert
        view.Count.Should().Be(TestData.TestValueFive);
        view.Should().BeEquivalentTo([TestData.TestValueSix, TestData.TestValueSeven, TestData.TestValueEight, TestData.TestValueNine, TestData.TestValueTen]);
    }

    /// <summary>Tests that CreateView updates when source changes.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateView_UpdatesOnSourceChange()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, TestData.TestValueTwo, TestData.TestValueThree });

        using var view = list.CreateView(x => x > 1, Sequencer.Immediate, 0);
        await Task.Delay(TestData.TestValueFifty);

        // Act
        list.Add(TestData.TestValueFive);
        await Task.Delay(TestData.TestValueOneHundred);

        // Assert
        view.Should().BeEquivalentTo([TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFive]);
    }

    /// <summary>Tests that DynamicFilteredView updates when filter changes.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task DynamicCreateView_UpdatesOnFilterChange()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive });

        var filterSubject = new BehaviorSignal<Func<int, bool>>(_ => true);

        using var view = list.CreateView(filterSubject, Sequencer.Immediate, 0);
        await Task.Delay(TestData.TestValueFifty);

        view.Count.Should().Be(TestData.TestValueFive);

        // Act - change filter
        filterSubject.OnNext(x => x > TestData.TestValueThree);
        await Task.Delay(TestData.TestValueOneHundred);

        // Assert
        view.Should().BeEquivalentTo([TestData.TestValueFour, TestData.TestValueFive]);
    }

    /// <summary>Tests that SortBy creates a sorted view.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task SortBy_CreatesSortedView()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { TestData.TestValueFive, TestData.TestValueTwo, TestData.TestValueEight, 1, TestData.TestValueNine, TestData.TestValueThree });

        // Act - sort ascending
        using var view = list.SortBy(Comparer<int>.Default, Sequencer.Immediate, 0);
        await Task.Delay(TestData.TestValueFifty);

        // Assert
        view.Should().BeEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFive, TestData.TestValueEight, TestData.TestValueNine], options => options.WithStrictOrdering());
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
        using var view = list.SortBy((s) => s.Length, scheduler: Sequencer.Immediate, throttleMs: 0);
        await Task.Delay(TestData.TestValueFifty);

        // Assert (apple=5, cherry=6, banana=6 - but banana comes before cherry alphabetically when lengths equal)
        view.Count.Should().Be(TestData.TestValueThree);
        view[0].Should().Be("apple");
    }

    /// <summary>Tests that GroupBy creates a grouped view.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task GroupBy_CreatesGroupedView()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix });

        // Act - group by even/odd
        using var view = list.GroupBy((x) => x % TestData.TestValueTwo == 0 ? "even" : "odd", Sequencer.Immediate, 0);
        await Task.Delay(TestData.TestValueFifty);

        // Assert
        view.Count.Should().Be(TestData.TestValueTwo);
        view.ContainsKey("odd").Should().BeTrue();
        view.ContainsKey("even").Should().BeTrue();
        view["odd"].Should().BeEquivalentTo([1, TestData.TestValueThree, TestData.TestValueFive]);
        view["even"].Should().BeEquivalentTo([TestData.TestValueTwo, TestData.TestValueFour, TestData.TestValueSix]);
    }

    /// <summary>Tests that GroupBy updates when items are added.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task GroupBy_UpdatesOnAdd()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, TestData.TestValueTwo, TestData.TestValueThree });

        using var view = list.GroupBy((x) => x % TestData.TestValueTwo == 0 ? "even" : "odd", Sequencer.Immediate, 0);
        await Task.Delay(TestData.TestValueFifty);

        // Act
        list.Add(TestData.TestValueFour);
        await Task.Delay(TestData.TestValueOneHundred);

        // Assert
        view["even"].Should().BeEquivalentTo([TestData.TestValueTwo, TestData.TestValueFour]);
    }

    /// <summary>Tests that AddRange with ReadOnlySpan works correctly.</summary>
    [Test]
    public void AddRange_WithSpan_AddsItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        ReadOnlySpan<int> items = [1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive];

        // Act
        list.AddRange(items);

        // Assert
        list.Count.Should().Be(TestData.TestValueFive);
        list.Should().BeEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
    }

    /// <summary>Tests that CopyTo with Span works correctly.</summary>
    [Test]
    public void CopyTo_WithSpan_CopiesItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive });
        Span<int> destination = stackalloc int[5];

        // Act
        list.CopyTo(destination);

        // Assert
        destination.ToArray().Should().BeEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
    }

    /// <summary>Tests that AsSpan returns correct data.</summary>
    [Test]
    public void AsSpan_ReturnsItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, TestData.TestValueTwo, TestData.TestValueThree });

        // Act
        var span = list.AsSpan();

        // Assert
        span.Length.Should().Be(TestData.TestValueThree);
        span[0].Should().Be(1);
        span[1].Should().Be(TestData.TestValueTwo);
        span[TestData.TestValueTwo].Should().Be(TestData.TestValueThree);
    }

    /// <summary>Tests that AsMemory returns correct data.</summary>
    [Test]
    public void AsMemory_ReturnsItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, TestData.TestValueTwo, TestData.TestValueThree });

        // Act
        var memory = list.AsMemory();

        // Assert
        memory.Length.Should().Be(TestData.TestValueThree);
        memory.Span[0].Should().Be(1);
        memory.Span[1].Should().Be(TestData.TestValueTwo);
        memory.Span[TestData.TestValueTwo].Should().Be(TestData.TestValueThree);
    }
#endif
}
