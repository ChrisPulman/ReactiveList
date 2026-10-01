// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
#else
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
#endif
using ReactiveList.Test;
using TUnit.Assertions;
using TUnit.Core;

namespace ReactiveList.Tests;

/// <summary>Tests for ReactiveListExtensions.</summary>
public class ReactiveListExtensionsTests
{
    /// <summary>Tests that WhereChanges filters changes by predicate.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WhereChanges_FiltersChangesByPredicate()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var addedItems = new List<int>();

        using var subscription = list.Connect()
            .WhereChanges(static c => c.Current > TestData.TestValueFive)
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
        await Assert.That(addedItems).IsEquivalentTo([TestData.TestValueSeven, TestData.TestValueTen]);
    }

    /// <summary>Tests that WhereReason filters by specific change reason.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WhereReason_FiltersAddOnly()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        var addCount = 0;

        using var subscription = list.Connect()
            .WhereReason(ChangeReason.Add)
            .Subscribe((Action<ChangeSet<string>>)(cs => addCount++));

        // Act
        list.Add("one");
        list.Add("two");
        _ = list.Remove("one");

        // Assert - should see 2 adds, not the remove
        await Assert.That(addCount).IsEqualTo(TestData.TestValueTwo);
    }

    /// <summary>Tests that OnAdd returns only added items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnAdd_ReturnsAddedItems()
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
        await Assert.That(addedItems).IsEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree]);
    }

    /// <summary>Tests that OnRemove returns only removed items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnRemove_ReturnsRemovedItems()
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
        _ = list.Remove(1);

        // Assert
        await Assert.That(removedItems).IsEquivalentTo([1]);
    }

    /// <summary>Tests that SelectChanges transforms items correctly using change selector.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SelectChanges_TransformsItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        var transformedItems = new List<string>();

        // Use the overload that takes Func<Change<T>, TResult> to get individual transformed items
        using var subscription = list.Connect()
            .SelectChanges(static c => $"Item_{c.Current}")
            .Subscribe(transformedItems.Add);

        // Act
        list.Add(1);
        list.Add(TestData.TestValueTwo);
        list.Add(TestData.TestValueThree);

        // Assert
        await Assert.That(transformedItems).IsEquivalentTo(["Item_1", "Item_2", "Item_3"]);
    }

#if NET6_0_OR_GREATER || NETFRAMEWORK
    /// <summary>Tests that CreateView creates a filtered view.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateView_CreatesFilteredView()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange([
            1,
            TestData.TestValueTwo,
            TestData.TestValueThree,
            TestData.TestValueFour,
            TestData.TestValueFive,
            TestData.TestValueSix,
            TestData.TestValueSeven,
            TestData.TestValueEight,
            TestData.TestValueNine,
            TestData.TestValueTen,
        ]);

        // Act
        using var view = list.CreateView(static x => x > TestData.TestValueFive, Sequencer.Immediate, 0);

        // Allow time for initial sync
        await Task.Delay(TestData.TestValueFifty);

        // Assert
        await Assert.That(view.Count).IsEqualTo(TestData.TestValueFive);
        await Assert.That(view).IsEquivalentTo([
            TestData.TestValueSix,
            TestData.TestValueSeven,
            TestData.TestValueEight,
            TestData.TestValueNine,
            TestData.TestValueTen,
        ]);
    }

    /// <summary>Tests that CreateView updates when source changes.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateView_UpdatesOnSourceChange()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree]);

        using var view = list.CreateView(static x => x > 1, Sequencer.Immediate, 0);
        await Task.Delay(TestData.TestValueFifty);

        // Act
        list.Add(TestData.TestValueFive);
        await Task.Delay(TestData.TestValueOneHundred);

        // Assert
        await Assert.That(view).IsEquivalentTo([TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFive]);
    }

    /// <summary>Tests that DynamicFilteredView updates when filter changes.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task DynamicCreateView_UpdatesOnFilterChange()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);

        using var filterSubject = new BehaviorSignal<Func<int, bool>>(static _ => true);

        using var view = list.CreateView(filterSubject, Sequencer.Immediate, 0);
        await Task.Delay(TestData.TestValueFifty);

        await Assert.That(view.Count).IsEqualTo(TestData.TestValueFive);

        // Act - change filter
        filterSubject.OnNext(static x => x > TestData.TestValueThree);
        await Task.Delay(TestData.TestValueOneHundred);

        // Assert
        await Assert.That(view).IsEquivalentTo([TestData.TestValueFour, TestData.TestValueFive]);
    }

    /// <summary>Tests that SortBy creates a sorted view.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task SortBy_CreatesSortedView()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange([
            TestData.TestValueFive,
            TestData.TestValueTwo,
            TestData.TestValueEight,
            1,
            TestData.TestValueNine,
            TestData.TestValueThree,
        ]);

        // Act - sort ascending
        using var view = list.SortBy(Comparer<int>.Default, Sequencer.Immediate, 0);
        await Task.Delay(TestData.TestValueFifty);

        // Assert
        await Assert.That(view)
            .IsEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFive, TestData.TestValueEight, TestData.TestValueNine], CollectionOrdering.Matching);
    }

    /// <summary>Tests that SortBy with key selector creates a sorted view.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task SortBy_WithKeySelector_CreatesSortedView()
    {
        // Arrange
        using var list = new ReactiveList<string>();
        list.AddRange(["banana", "apple", "cherry"]);

        // Act - sort by length
        using var view = list.SortBy(static s => s.Length, scheduler: Sequencer.Immediate, throttleMs: 0);
        await Task.Delay(TestData.TestValueFifty);

        // Assert (apple=5, cherry=6, banana=6 - but banana comes before cherry alphabetically when lengths equal)
        await Assert.That(view.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(view[0]).IsEqualTo("apple");
    }

    /// <summary>Tests that GroupBy creates a grouped view.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task GroupBy_CreatesGroupedView()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange([
            1,
            TestData.TestValueTwo,
            TestData.TestValueThree,
            TestData.TestValueFour,
            TestData.TestValueFive,
            TestData.TestValueSix,
        ]);

        // Act - group by even/odd
        using var view = list.GroupBy(static x => x % TestData.TestValueTwo == 0 ? "even" : "odd", Sequencer.Immediate, 0);
        await Task.Delay(TestData.TestValueFifty);

        // Assert
        await Assert.That(view.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(view.ContainsKey("odd")).IsTrue();
        await Assert.That(view.ContainsKey("even")).IsTrue();
        await Assert.That(view["odd"]).IsEquivalentTo([1, TestData.TestValueThree, TestData.TestValueFive]);
        await Assert.That(view["even"]).IsEquivalentTo([TestData.TestValueTwo, TestData.TestValueFour, TestData.TestValueSix]);
    }

    /// <summary>Tests that GroupBy updates when items are added.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task GroupBy_UpdatesOnAdd()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree]);

        using var view = list.GroupBy(static x => x % TestData.TestValueTwo == 0 ? "even" : "odd", Sequencer.Immediate, 0);
        await Task.Delay(TestData.TestValueFifty);

        // Act
        list.Add(TestData.TestValueFour);
        await Task.Delay(TestData.TestValueOneHundred);

        // Assert
        await Assert.That(view["even"]).IsEquivalentTo([TestData.TestValueTwo, TestData.TestValueFour]);
    }

    /// <summary>Tests that AddRange with ReadOnlySpan works correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddRange_WithSpan_AddsItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        ReadOnlySpan<int> items = [1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive];

        // Act
        list.AddRange(items);

        // Assert
        await Assert.That(list.Count).IsEqualTo(TestData.TestValueFive);
        await Assert.That(list).IsEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
    }

    /// <summary>Tests that CopyTo with Span works correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CopyTo_WithSpan_CopiesItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
        Span<int> destination = stackalloc int[5];

        // Act
        list.CopyTo(destination);

        // Assert
        await Assert.That(destination.ToArray()).IsEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
    }

    /// <summary>Tests that AsSpan returns correct data.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AsSpan_ReturnsItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree]);

        // Act
        var span = list.AsSpan().ToArray();

        // Assert
        await Assert.That(span.Length).IsEqualTo(TestData.TestValueThree);
        await Assert.That(span[0]).IsEqualTo(1);
        await Assert.That(span[1]).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(span[TestData.TestValueTwo]).IsEqualTo(TestData.TestValueThree);
    }

    /// <summary>Tests that AsMemory returns correct data.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AsMemory_ReturnsItems()
    {
        // Arrange
        using var list = new ReactiveList<int>();
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree]);

        // Act
        var memory = list.AsMemory();

        // Assert
        await Assert.That(memory.Length).IsEqualTo(TestData.TestValueThree);
        await Assert.That(memory.Span[0]).IsEqualTo(1);
        await Assert.That(memory.Span[1]).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(memory.Span[TestData.TestValueTwo]).IsEqualTo(TestData.TestValueThree);
    }
#endif
}
