// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using CP.Primitives.Collections;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests Reactive2DList behavior.</summary>
public class Reactive2DListTests
{
    /// <summary>Constructors the should initialize empty list.</summary>
    [Test]
    public void Constructor_ShouldInitializeEmptyList()
    {
        var list = new Reactive2DList<int>();
        Assert.Empty(list);
        list.Dispose();
    }

    /// <summary>Constructors the should initialize with items.</summary>
    [Test]
    public void Constructor_ShouldInitializeWithItems()
    {
        var items = new List<List<int>> { new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour } };
        var list = new Reactive2DList<int>(items);
        Assert.Equal(TestData.TestValueTwo, list.Count);
        Assert.Equal(TestData.TestValueTwo, list[0].Count);
        Assert.Equal(TestData.TestValueTwo, list[1].Count);
        list.Dispose();
    }

    /// <summary>Constructors the should initialize with reactive lists.</summary>
    [Test]
    public void Constructor_ShouldInitializeWithReactiveLists()
    {
        var items = new List<ReactiveList<int>> { new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour } };
        var list = new Reactive2DList<int>(items);
        Assert.Equal(TestData.TestValueTwo, list.Count);
        Assert.Equal(TestData.TestValueTwo, list[0].Count);
        Assert.Equal(TestData.TestValueTwo, list[1].Count);
        list.Dispose();
    }

    /// <summary>Constructors the should initialize with single item.</summary>
    [Test]
    public void Constructor_ShouldInitializeWithSingleItem()
    {
        var list = new Reactive2DList<int>(TestData.TestValueFive);
        Assert.Single(list);
        Assert.Single(list[0]);
        Assert.Equal(TestData.TestValueFive, list[0][0]);
        list.Dispose();
    }

    /// <summary>Constructors the should initialize with item enumerable.</summary>
    [Test]
    public void Constructor_ShouldInitializeWithItemEnumerable()
    {
        IEnumerable<int> items = Enumerable.Range(TestData.TestValueSeven, TestData.TestValueTwo);
        var list = new Reactive2DList<int>(items);

        Assert.Equal(TestData.TestValueTwo, list.Count);
        Assert.Single(list[0]);
        Assert.Single(list[1]);
        Assert.Equal(TestData.TestValueSeven, list[0][0]);
        Assert.Equal(TestData.TestValueEight, list[1][0]);
        list.Dispose();
    }

    /// <summary>Constructors the should initialize with items.</summary>
    [Test]
    public void Constructor_ShouldInitializeWithReactiveList()
    {
        var items = new ReactiveList<int> { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour };
        var list = new Reactive2DList<int>(items);
        Assert.Single(list);
        Assert.Equal(TestData.TestValueFour, list[0].Count);
        list.Dispose();
    }

    /// <summary>Adds the range should add items.</summary>
    [Test]
    public void AddRange_ShouldAddItems()
    {
        var list = new Reactive2DList<int>();
        var items = new List<List<int>> { new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour } };
        list.AddRange(items);
        Assert.Equal(TestData.TestValueTwo, list.Count);
        list.Dispose();
    }

    /// <summary>Adds the range should add single items.</summary>
    [Test]
    public void AddRange_ShouldAddSingleItems()
    {
        var list = new Reactive2DList<int>();
        var items = new List<int> { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour };
        list.AddRange(items);
        Assert.Equal(TestData.TestValueFour, list.Count);
        list.Dispose();
    }

    /// <summary>Adds the index of the range should insert items at.</summary>
    [Test]
    public void AddRange_ShouldInsertItemsAtIndex()
    {
        var list = new Reactive2DList<int>([TestData.TestValueFive]);
        var items = new List<List<int>> { new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour } };
        list.AddRange(items);
        Assert.Equal(TestData.TestValueThree, list.Count);
        Assert.Equal(TestData.TestValueFive, list[0][0]);
        Assert.Equal(TestData.TestValueTwo, list[1].Count);
        Assert.Equal(TestData.TestValueTwo, list[1][1]);
        Assert.Equal(TestData.TestValueTwo, list[TestData.TestValueTwo].Count);
        Assert.Equal(TestData.TestValueFour, list[TestData.TestValueTwo][1]);
        list.Dispose();
    }

    /// <summary>Inserts the index of the should insert items at.</summary>
    [Test]
    public void Insert_ShouldInsertItemsAtIndex()
    {
        var list = new Reactive2DList<int>([TestData.TestValueFive]);
        var items = new List<int> { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour };
        list.Insert(0, items);
        Assert.Equal(TestData.TestValueTwo, list.Count);
        Assert.Equal(1, list[0][0]);
        Assert.Equal(TestData.TestValueFour, list[0][TestData.TestValueThree]);
        Assert.Equal(TestData.TestValueFive, list[1][0]);
        list.Dispose();
    }

    /// <summary>Inserts the index of the should insert single item at.</summary>
    [Test]
    public void Insert_ShouldInsertSingleItemAtIndex()
    {
        var list = new Reactive2DList<int>([TestData.TestValueFive]);
        list.Insert(0, TestData.TestValueTen);
        Assert.Equal(TestData.TestValueTwo, list.Count);
        Assert.Equal(TestData.TestValueTen, list[0][0]);
        list.Dispose();
    }

    /// <summary>Inserts the index of the should insert reactive list at.</summary>
    [Test]
    public void Insert_ShouldInsertReactiveListAtIndex()
    {
        var list = new Reactive2DList<int>([TestData.TestValueFive]);
        var reactiveList = new ReactiveList<int> { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour };
        list.Insert(0, reactiveList);
        Assert.Equal(TestData.TestValueTwo, list.Count);
        Assert.Equal(1, list[0][0]);
        list.Dispose();
    }

    /// <summary>Inserts the index of the should insert items in reactive list at.</summary>
    [Test]
    public void Insert_ShouldInsertItemsInReactiveListAtIndex()
    {
        var list = new Reactive2DList<int>([TestData.TestValueFive]);
        var items = new List<int> { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour };
        list.Insert(0, items, 0);
        Assert.Equal(TestData.TestValueFive, list[0].Count);
        Assert.Equal(1, list[0][0]);
        Assert.Equal(TestData.TestValueFour, list[0][TestData.TestValueThree]);
        Assert.Equal(TestData.TestValueFive, list[0][TestData.TestValueFour]);
        list.Dispose();
    }

    /// <summary>GetItem should return item at specified indices.</summary>
    [Test]
    public void GetItem_ShouldReturnItemAtSpecifiedIndices()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo, TestData.TestValueThree }, new() { TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix }]);

        var item = list.GetItem(1, TestData.TestValueTwo);

        item.Should().Be(TestData.TestValueSix);
        list.Dispose();
    }

    /// <summary>GetItem should throw when outer index is negative.</summary>
    [Test]
    public void GetItem_ShouldThrowWhenOuterIndexIsNegative()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.GetItem(-1, 0);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>GetItem should throw when outer index exceeds count.</summary>
    [Test]
    public void GetItem_ShouldThrowWhenOuterIndexExceedsCount()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.GetItem(TestData.TestValueFive, 0);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>GetItem should throw when inner index is negative.</summary>
    [Test]
    public void GetItem_ShouldThrowWhenInnerIndexIsNegative()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.GetItem(0, -1);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.InnerIndexParameterName);
        list.Dispose();
    }

    /// <summary>GetItem should throw when inner index exceeds count.</summary>
    [Test]
    public void GetItem_ShouldThrowWhenInnerIndexExceedsCount()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.GetItem(0, TestData.TestValueFive);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.InnerIndexParameterName);
        list.Dispose();
    }

    /// <summary>SetItem should update item at specified indices.</summary>
    [Test]
    public void SetItem_ShouldUpdateItemAtSpecifiedIndices()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo, TestData.TestValueThree }, new() { TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix }]);

        list.SetItem(1, 1, TestData.TestValueNinetyNine);

        list.GetItem(1, 1).Should().Be(TestData.TestValueNinetyNine);
        list.Dispose();
    }

    /// <summary>SetItem should throw when outer index is out of range.</summary>
    [Test]
    public void SetItem_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.SetItem(TestData.TestValueFive, 0, TestData.TestValueNinetyNine);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>SetItem should throw when inner index is out of range.</summary>
    [Test]
    public void SetItem_ShouldThrowWhenInnerIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.SetItem(0, TestData.TestValueFive, TestData.TestValueNinetyNine);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.InnerIndexParameterName);
        list.Dispose();
    }

    /// <summary>Flatten should return all items in order.</summary>
    [Test]
    public void Flatten_ShouldReturnAllItemsInOrder()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour }, new() { TestData.TestValueFive, TestData.TestValueSix }]);

        var flattened = list.Flatten().ToList();

        flattened.Should().HaveCount(TestData.TestValueSix);
        flattened.Should().ContainInOrder(1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix);
        list.Dispose();
    }

    /// <summary>Flatten should return empty for empty list.</summary>
    [Test]
    public void Flatten_ShouldReturnEmptyForEmptyList()
    {
        var list = new Reactive2DList<int>();

        var flattened = list.Flatten().ToList();

        flattened.Should().BeEmpty();
        list.Dispose();
    }

    /// <summary>Flatten should handle empty inner lists.</summary>
    [Test]
    public void Flatten_ShouldHandleEmptyInnerLists()
    {
        var list = new Reactive2DList<int>
        {
            new ReactiveList<int> { 1, TestData.TestValueTwo },
            new ReactiveList<int>(),
            new ReactiveList<int> { TestData.TestValueThree }
        };

        var flattened = list.Flatten().ToList();

        flattened.Should().HaveCount(TestData.TestValueThree);
        flattened.Should().ContainInOrder(1, TestData.TestValueTwo, TestData.TestValueThree);
        list.Dispose();
    }

    /// <summary>TotalCount should return sum of all inner list counts.</summary>
    [Test]
    public void TotalCount_ShouldReturnSumOfAllInnerListCounts()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive }, new() { TestData.TestValueSix }]);

        var total = list.TotalCount();

        total.Should().Be(TestData.TestValueSix);
        list.Dispose();
    }

    /// <summary>TotalCount should return zero for empty list.</summary>
    [Test]
    public void TotalCount_ShouldReturnZeroForEmptyList()
    {
        var list = new Reactive2DList<int>();

        var total = list.TotalCount();

        total.Should().Be(0);
        list.Dispose();
    }

    /// <summary>TotalCount should handle empty inner lists.</summary>
    [Test]
    public void TotalCount_ShouldHandleEmptyInnerLists()
    {
        var list = new Reactive2DList<int>
        {
            new ReactiveList<int> { 1, TestData.TestValueTwo },
            new ReactiveList<int>(),
            new ReactiveList<int> { TestData.TestValueThree }
        };

        var total = list.TotalCount();

        total.Should().Be(TestData.TestValueThree);
        list.Dispose();
    }

    /// <summary>AddToInner should add items to specified inner list.</summary>
    [Test]
    public void AddToInner_ShouldAddItemsToSpecifiedInnerList()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour }]);

        list.AddToInner(0, [TestData.TestValueFive, TestData.TestValueSix]);

        list[0].Count.Should().Be(TestData.TestValueFour);
        list[0][TestData.TestValueTwo].Should().Be(TestData.TestValueFive);
        list[0][TestData.TestValueThree].Should().Be(TestData.TestValueSix);
        list.Dispose();
    }

    /// <summary>AddToInner should add single item to specified inner list.</summary>
    [Test]
    public void AddToInner_ShouldAddSingleItemToSpecifiedInnerList()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour }]);

        list.AddToInner(1, TestData.TestValueNinetyNine);

        list[1].Count.Should().Be(TestData.TestValueThree);
        list[1][TestData.TestValueTwo].Should().Be(TestData.TestValueNinetyNine);
        list.Dispose();
    }

    /// <summary>AddToInner should throw when outer index is out of range.</summary>
    [Test]
    public void AddToInner_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.AddToInner(TestData.TestValueFive, TestData.TestValueNinetyNine);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>AddToInner should throw when items is null.</summary>
    [Test]
    public void AddToInner_ShouldThrowWhenItemsIsNull()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.AddToInner(0, (IEnumerable<int>)null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>RemoveFromInner should remove item at specified indices.</summary>
    [Test]
    public void RemoveFromInner_ShouldRemoveItemAtSpecifiedIndices()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo, TestData.TestValueThree }, new() { TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix }]);

        list.RemoveFromInner(0, 1);

        list[0].Count.Should().Be(TestData.TestValueTwo);
        list[0][0].Should().Be(1);
        list[0][1].Should().Be(TestData.TestValueThree);
        list.Dispose();
    }

    /// <summary>RemoveFromInner should throw when outer index is out of range.</summary>
    [Test]
    public void RemoveFromInner_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.RemoveFromInner(TestData.TestValueFive, 0);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>ClearInner should clear the specified inner list.</summary>
    [Test]
    public void ClearInner_ShouldClearTheSpecifiedInnerList()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo, TestData.TestValueThree }, new() { TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix }]);

        list.ClearInner(0);

        list[0].Count.Should().Be(0);
        list[1].Count.Should().Be(TestData.TestValueThree); // Other list unchanged
        list.Dispose();
    }

    /// <summary>ClearInner should throw when outer index is out of range.</summary>
    [Test]
    public void ClearInner_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.ClearInner(TestData.TestValueFive);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>Constructor should throw when items enumerable is null.</summary>
    [Test]
    public void Constructor_ShouldThrowWhenItemsEnumerableIsNull()
    {
        var action = () => new Reactive2DList<int>((IEnumerable<IEnumerable<int>>)null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.ItemsParameterName);
    }

    /// <summary>Constructor should throw when item enumerable is null.</summary>
    [Test]
    public void Constructor_ShouldThrowWhenItemEnumerableIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _ = new Reactive2DList<int>((IEnumerable<int>)null!));

        Assert.Equal(TestData.ItemsParameterName, exception.ParamName);
    }

    /// <summary>Constructor should throw when reactive list item is null.</summary>
    [Test]
    public void Constructor_ShouldThrowWhenReactiveListItemIsNull()
    {
        var action = () => new Reactive2DList<int>((ReactiveList<int>)null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("item");
    }

    /// <summary>AddRange with nested enumerable should throw when null.</summary>
    [Test]
    public void AddRange_NestedEnumerable_ShouldThrowWhenNull()
    {
        var list = new Reactive2DList<int>();

        var action = () => list.AddRange((IEnumerable<IEnumerable<int>>)null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>AddRange with single enumerable should throw when null.</summary>
    [Test]
    public void AddRange_SingleEnumerable_ShouldThrowWhenNull()
    {
        var list = new Reactive2DList<int>();

        var action = () => list.AddRange((IEnumerable<int>)null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>Insert with enumerable should throw when null.</summary>
    [Test]
    public void Insert_Enumerable_ShouldThrowWhenNull()
    {
        var list = new Reactive2DList<int>([1]);

        var action = () => list.Insert(0, (IEnumerable<int>)null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>Insert with inner index should throw when items null.</summary>
    [Test]
    public void Insert_WithInnerIndex_ShouldThrowWhenItemsNull()
    {
        var list = new Reactive2DList<int>([1]);

        var action = () => list.Insert(0, (IEnumerable<int>)null!, 0);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }
}
