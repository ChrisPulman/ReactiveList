// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using CP.Reactive;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Reactive2DListTests.
/// </summary>
public class Reactive2DListTests
{
    /// <summary>
    /// Constructors the should initialize empty list.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeEmptyList()
    {
        var list = new Reactive2DList<int>();
        Assert.Empty(list);
        list.Dispose();
    }

    /// <summary>
    /// Constructors the should initialize with items.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithItems()
    {
        var items = new List<List<int>> { new() { 1, 2 }, new() { 3, 4 } };
        var list = new Reactive2DList<int>(items);
        Assert.Equal(2, list.Count);
        Assert.Equal(2, list[0].Count);
        Assert.Equal(2, list[1].Count);
        list.Dispose();
    }

    /// <summary>
    /// Constructors the should initialize with reactive lists.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithReactiveLists()
    {
        var items = new List<ReactiveList<int>> { new() { 1, 2 }, new() { 3, 4 } };
        var list = new Reactive2DList<int>(items);
        Assert.Equal(2, list.Count);
        Assert.Equal(2, list[0].Count);
        Assert.Equal(2, list[1].Count);
        list.Dispose();
    }

    /// <summary>
    /// Constructors the should initialize with single item.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithSingleItem()
    {
        var list = new Reactive2DList<int>(5);
        Assert.Single(list);
        Assert.Single(list[0]);
        Assert.Equal(5, list[0][0]);
        list.Dispose();
    }

    /// <summary>
    /// Constructors the should initialize with items.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithReactiveList()
    {
        var items = new ReactiveList<int> { 1, 2, 3, 4 };
        var list = new Reactive2DList<int>(items);
        Assert.Single(list);
        Assert.Equal(4, list[0].Count);
        list.Dispose();
    }

    /// <summary>
    /// Adds the range should add items.
    /// </summary>
    [Fact]
    public void AddRange_ShouldAddItems()
    {
        var list = new Reactive2DList<int>();
        var items = new List<List<int>> { new() { 1, 2 }, new() { 3, 4 } };
        list.AddRange(items);
        Assert.Equal(2, list.Count);
        list.Dispose();
    }

    /// <summary>
    /// Adds the range should add single items.
    /// </summary>
    [Fact]
    public void AddRange_ShouldAddSingleItems()
    {
        var list = new Reactive2DList<int>();
        var items = new List<int> { 1, 2, 3, 4 };
        list.AddRange(items);
        Assert.Equal(4, list.Count);
        list.Dispose();
    }

    /// <summary>
    /// Adds the index of the range should insert items at.
    /// </summary>
    [Fact]
    public void AddRange_ShouldInsertItemsAtIndex()
    {
        var list = new Reactive2DList<int>(new List<int> { 5 });
        var items = new List<List<int>> { new() { 1, 2 }, new() { 3, 4 } };
        list.AddRange(items);
        Assert.Equal(3, list.Count);
        Assert.Equal(5, list[0][0]);
        Assert.Equal(2, list[1].Count);
        Assert.Equal(2, list[1][1]);
        Assert.Equal(2, list[2].Count);
        Assert.Equal(4, list[2][1]);
        list.Dispose();
    }

    /// <summary>
    /// Inserts the index of the should insert items at.
    /// </summary>
    [Fact]
    public void Insert_ShouldInsertItemsAtIndex()
    {
        var list = new Reactive2DList<int>(new List<int> { 5 });
        var items = new List<int> { 1, 2, 3, 4 };
        list.Insert(0, items);
        Assert.Equal(2, list.Count);
        Assert.Equal(1, list[0][0]);
        Assert.Equal(4, list[0][3]);
        Assert.Equal(5, list[1][0]);
        list.Dispose();
    }

    /// <summary>
    /// Inserts the index of the should insert single item at.
    /// </summary>
    [Fact]
    public void Insert_ShouldInsertSingleItemAtIndex()
    {
        var list = new Reactive2DList<int>(new List<int> { 5 });
        list.Insert(0, 10);
        Assert.Equal(2, list.Count);
        Assert.Equal(10, list[0][0]);
        list.Dispose();
    }

    /// <summary>
    /// Inserts the index of the should insert reactive list at.
    /// </summary>
    [Fact]
    public void Insert_ShouldInsertReactiveListAtIndex()
    {
        var list = new Reactive2DList<int>(new List<int> { 5 });
        var reactiveList = new ReactiveList<int> { 1, 2, 3, 4 };
        list.Insert(0, reactiveList);
        Assert.Equal(2, list.Count);
        Assert.Equal(1, list[0][0]);
        list.Dispose();
    }

    /// <summary>
    /// Inserts the index of the should insert items in reactive list at.
    /// </summary>
    [Fact]
    public void Insert_ShouldInsertItemsInReactiveListAtIndex()
    {
        var list = new Reactive2DList<int>(new List<int> { 5 });
        var items = new List<int> { 1, 2, 3, 4 };
        list.Insert(0, items, 0);
        Assert.Equal(5, list[0].Count);
        Assert.Equal(1, list[0][0]);
        Assert.Equal(4, list[0][3]);
        Assert.Equal(5, list[0][4]);
        list.Dispose();
    }

    // ==================== New Tests for Added Methods ====================

    /// <summary>
    /// GetItem should return item at specified indices.
    /// </summary>
    [Fact]
    public void GetItem_ShouldReturnItemAtSpecifiedIndices()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2, 3 }, new() { 4, 5, 6 } });

        var item = list.GetItem(1, 2);

        item.Should().Be(6);
        list.Dispose();
    }

    /// <summary>
    /// GetItem should throw when outer index is negative.
    /// </summary>
    [Fact]
    public void GetItem_ShouldThrowWhenOuterIndexIsNegative()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 } });

        var action = () => list.GetItem(-1, 0);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("outerIndex");
        list.Dispose();
    }

    /// <summary>
    /// GetItem should throw when outer index exceeds count.
    /// </summary>
    [Fact]
    public void GetItem_ShouldThrowWhenOuterIndexExceedsCount()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 } });

        var action = () => list.GetItem(5, 0);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("outerIndex");
        list.Dispose();
    }

    /// <summary>
    /// GetItem should throw when inner index is negative.
    /// </summary>
    [Fact]
    public void GetItem_ShouldThrowWhenInnerIndexIsNegative()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 } });

        var action = () => list.GetItem(0, -1);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("innerIndex");
        list.Dispose();
    }

    /// <summary>
    /// GetItem should throw when inner index exceeds count.
    /// </summary>
    [Fact]
    public void GetItem_ShouldThrowWhenInnerIndexExceedsCount()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 } });

        var action = () => list.GetItem(0, 5);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("innerIndex");
        list.Dispose();
    }

    /// <summary>
    /// SetItem should update item at specified indices.
    /// </summary>
    [Fact]
    public void SetItem_ShouldUpdateItemAtSpecifiedIndices()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2, 3 }, new() { 4, 5, 6 } });

        list.SetItem(1, 1, 99);

        list.GetItem(1, 1).Should().Be(99);
        list.Dispose();
    }

    /// <summary>
    /// SetItem should throw when outer index is out of range.
    /// </summary>
    [Fact]
    public void SetItem_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 } });

        var action = () => list.SetItem(5, 0, 99);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("outerIndex");
        list.Dispose();
    }

    /// <summary>
    /// SetItem should throw when inner index is out of range.
    /// </summary>
    [Fact]
    public void SetItem_ShouldThrowWhenInnerIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 } });

        var action = () => list.SetItem(0, 5, 99);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("innerIndex");
        list.Dispose();
    }

    /// <summary>
    /// Flatten should return all items in order.
    /// </summary>
    [Fact]
    public void Flatten_ShouldReturnAllItemsInOrder()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 }, new() { 3, 4 }, new() { 5, 6 } });

        var flattened = list.Flatten().ToList();

        flattened.Should().HaveCount(6);
        flattened.Should().ContainInOrder(1, 2, 3, 4, 5, 6);
        list.Dispose();
    }

    /// <summary>
    /// Flatten should return empty for empty list.
    /// </summary>
    [Fact]
    public void Flatten_ShouldReturnEmptyForEmptyList()
    {
        var list = new Reactive2DList<int>();

        var flattened = list.Flatten().ToList();

        flattened.Should().BeEmpty();
        list.Dispose();
    }

    /// <summary>
    /// Flatten should handle empty inner lists.
    /// </summary>
    [Fact]
    public void Flatten_ShouldHandleEmptyInnerLists()
    {
        var list = new Reactive2DList<int>
        {
            new ReactiveList<int> { 1, 2 },
            new ReactiveList<int>(),
            new ReactiveList<int> { 3 }
        };

        var flattened = list.Flatten().ToList();

        flattened.Should().HaveCount(3);
        flattened.Should().ContainInOrder(1, 2, 3);
        list.Dispose();
    }

    /// <summary>
    /// TotalCount should return sum of all inner list counts.
    /// </summary>
    [Fact]
    public void TotalCount_ShouldReturnSumOfAllInnerListCounts()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 }, new() { 3, 4, 5 }, new() { 6 } });

        var total = list.TotalCount();

        total.Should().Be(6);
        list.Dispose();
    }

    /// <summary>
    /// TotalCount should return zero for empty list.
    /// </summary>
    [Fact]
    public void TotalCount_ShouldReturnZeroForEmptyList()
    {
        var list = new Reactive2DList<int>();

        var total = list.TotalCount();

        total.Should().Be(0);
        list.Dispose();
    }

    /// <summary>
    /// TotalCount should handle empty inner lists.
    /// </summary>
    [Fact]
    public void TotalCount_ShouldHandleEmptyInnerLists()
    {
        var list = new Reactive2DList<int>
        {
            new ReactiveList<int> { 1, 2 },
            new ReactiveList<int>(),
            new ReactiveList<int> { 3 }
        };

        var total = list.TotalCount();

        total.Should().Be(3);
        list.Dispose();
    }

    /// <summary>
    /// AddToInner should add items to specified inner list.
    /// </summary>
    [Fact]
    public void AddToInner_ShouldAddItemsToSpecifiedInnerList()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 }, new() { 3, 4 } });

        list.AddToInner(0, [5, 6]);

        list[0].Count.Should().Be(4);
        list[0][2].Should().Be(5);
        list[0][3].Should().Be(6);
        list.Dispose();
    }

    /// <summary>
    /// AddToInner should add single item to specified inner list.
    /// </summary>
    [Fact]
    public void AddToInner_ShouldAddSingleItemToSpecifiedInnerList()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 }, new() { 3, 4 } });

        list.AddToInner(1, 99);

        list[1].Count.Should().Be(3);
        list[1][2].Should().Be(99);
        list.Dispose();
    }

    /// <summary>
    /// AddToInner should throw when outer index is out of range.
    /// </summary>
    [Fact]
    public void AddToInner_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 } });

        var action = () => list.AddToInner(5, 99);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("outerIndex");
        list.Dispose();
    }

    /// <summary>
    /// AddToInner should throw when items is null.
    /// </summary>
    [Fact]
    public void AddToInner_ShouldThrowWhenItemsIsNull()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 } });

        var action = () => list.AddToInner(0, (IEnumerable<int>)null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("items");
        list.Dispose();
    }

    /// <summary>
    /// RemoveFromInner should remove item at specified indices.
    /// </summary>
    [Fact]
    public void RemoveFromInner_ShouldRemoveItemAtSpecifiedIndices()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2, 3 }, new() { 4, 5, 6 } });

        list.RemoveFromInner(0, 1);

        list[0].Count.Should().Be(2);
        list[0][0].Should().Be(1);
        list[0][1].Should().Be(3);
        list.Dispose();
    }

    /// <summary>
    /// RemoveFromInner should throw when outer index is out of range.
    /// </summary>
    [Fact]
    public void RemoveFromInner_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 } });

        var action = () => list.RemoveFromInner(5, 0);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("outerIndex");
        list.Dispose();
    }

    /// <summary>
    /// ClearInner should clear the specified inner list.
    /// </summary>
    [Fact]
    public void ClearInner_ShouldClearTheSpecifiedInnerList()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2, 3 }, new() { 4, 5, 6 } });

        list.ClearInner(0);

        list[0].Count.Should().Be(0);
        list[1].Count.Should().Be(3); // Other list unchanged
        list.Dispose();
    }

    /// <summary>
    /// ClearInner should throw when outer index is out of range.
    /// </summary>
    [Fact]
    public void ClearInner_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>(new List<List<int>> { new() { 1, 2 } });

        var action = () => list.ClearInner(5);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("outerIndex");
        list.Dispose();
    }

    /// <summary>
    /// Constructor should throw when items enumerable is null.
    /// </summary>
    [Fact]
    public void Constructor_ShouldThrowWhenItemsEnumerableIsNull()
    {
        var action = () => new Reactive2DList<int>((IEnumerable<IEnumerable<int>>)null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("items");
    }

    /// <summary>
    /// Constructor should throw when reactive list item is null.
    /// </summary>
    [Fact]
    public void Constructor_ShouldThrowWhenReactiveListItemIsNull()
    {
        var action = () => new Reactive2DList<int>((ReactiveList<int>)null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("item");
    }

    /// <summary>
    /// AddRange with nested enumerable should throw when null.
    /// </summary>
    [Fact]
    public void AddRange_NestedEnumerable_ShouldThrowWhenNull()
    {
        var list = new Reactive2DList<int>();

        var action = () => list.AddRange((IEnumerable<IEnumerable<int>>)null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("items");
        list.Dispose();
    }

    /// <summary>
    /// AddRange with single enumerable should throw when null.
    /// </summary>
    [Fact]
    public void AddRange_SingleEnumerable_ShouldThrowWhenNull()
    {
        var list = new Reactive2DList<int>();

        var action = () => list.AddRange((IEnumerable<int>)null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("items");
        list.Dispose();
    }

    /// <summary>
    /// Insert with enumerable should throw when null.
    /// </summary>
    [Fact]
    public void Insert_Enumerable_ShouldThrowWhenNull()
    {
        var list = new Reactive2DList<int>(new List<int> { 1 });

        var action = () => list.Insert(0, (IEnumerable<int>)null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("items");
        list.Dispose();
    }

    /// <summary>
    /// Insert with inner index should throw when items null.
    /// </summary>
    [Fact]
    public void Insert_WithInnerIndex_ShouldThrowWhenItemsNull()
    {
        var list = new Reactive2DList<int>(new List<int> { 1 });

        var action = () => list.Insert(0, (IEnumerable<int>)null!, 0);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("items");
        list.Dispose();
    }
}
