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
        _ = Assert.Single(list);
        _ = Assert.Single(list[0]);
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
        _ = Assert.Single(list[0]);
        _ = Assert.Single(list[1]);
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
        _ = Assert.Single(list);
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

    /// <summary>InsertRange should insert items as a new row at the requested index.</summary>
    [Test]
    public void InsertRange_ShouldInsertItemsAtIndex()
    {
        var list = new Reactive2DList<int>([TestData.TestValueFive]);
        var items = new List<int> { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour };
        list.InsertRange(0, items);
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
        ReactiveList<int>[] rows =
        [
            new() { 1, TestData.TestValueTwo, TestData.TestValueThree },
            new() { TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix }
        ];
        var list = new Reactive2DList<int>(rows);

        var item = list.GetItem(1, TestData.TestValueTwo);

        _ = item.Should().Be(TestData.TestValueSix);
        list.Dispose();
    }

    /// <summary>GetItem should throw when outer index is negative.</summary>
    [Test]
    public void GetItem_ShouldThrowWhenOuterIndexIsNegative()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.GetItem(-1, 0);

        _ = action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>GetItem should throw when outer index exceeds count.</summary>
    [Test]
    public void GetItem_ShouldThrowWhenOuterIndexExceedsCount()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.GetItem(TestData.TestValueFive, 0);

        _ = action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>GetItem should throw when inner index is negative.</summary>
    [Test]
    public void GetItem_ShouldThrowWhenInnerIndexIsNegative()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.GetItem(0, -1);

        _ = action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.InnerIndexParameterName);
        list.Dispose();
    }

    /// <summary>GetItem should throw when inner index exceeds count.</summary>
    [Test]
    public void GetItem_ShouldThrowWhenInnerIndexExceedsCount()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.GetItem(0, TestData.TestValueFive);

        _ = action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.InnerIndexParameterName);
        list.Dispose();
    }

    /// <summary>SetItem should update item at specified indices.</summary>
    [Test]
    public void SetItem_ShouldUpdateItemAtSpecifiedIndices()
    {
        ReactiveList<int>[] rows =
        [
            new() { 1, TestData.TestValueTwo, TestData.TestValueThree },
            new() { TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix }
        ];
        var list = new Reactive2DList<int>(rows);

        list.SetItem(1, 1, TestData.TestValueNinetyNine);

        _ = list.GetItem(1, 1).Should().Be(TestData.TestValueNinetyNine);
        list.Dispose();
    }

    /// <summary>SetItem should throw when outer index is out of range.</summary>
    [Test]
    public void SetItem_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.SetItem(TestData.TestValueFive, 0, TestData.TestValueNinetyNine);

        _ = action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>SetItem should throw when inner index is out of range.</summary>
    [Test]
    public void SetItem_ShouldThrowWhenInnerIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.SetItem(0, TestData.TestValueFive, TestData.TestValueNinetyNine);

        _ = action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.InnerIndexParameterName);
        list.Dispose();
    }

    /// <summary>Flatten should return all items in order.</summary>
    [Test]
    public void Flatten_ShouldReturnAllItemsInOrder()
    {
        ReactiveList<int>[] rows =
        [
            new() { 1, TestData.TestValueTwo },
            new() { TestData.TestValueThree, TestData.TestValueFour },
            new() { TestData.TestValueFive, TestData.TestValueSix }
        ];
        var list = new Reactive2DList<int>(rows);

        var flattened = FlattenToList(list);

        _ = flattened.Should().HaveCount(TestData.TestValueSix);
        _ = flattened.Should().ContainInOrder(1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix);
        list.Dispose();
    }

    /// <summary>Flatten should return empty for empty list.</summary>
    [Test]
    public void Flatten_ShouldReturnEmptyForEmptyList()
    {
        var list = new Reactive2DList<int>();

        var flattened = FlattenToList(list);

        _ = flattened.Should().BeEmpty();
        list.Dispose();
    }

    /// <summary>Flatten should handle empty inner lists.</summary>
    [Test]
    public void Flatten_ShouldHandleEmptyInnerLists()
    {
        var list = new Reactive2DList<int> { new ReactiveList<int> { 1, TestData.TestValueTwo }, new ReactiveList<int>(), new ReactiveList<int> { TestData.TestValueThree } };

        var flattened = FlattenToList(list);

        _ = flattened.Should().HaveCount(TestData.TestValueThree);
        _ = flattened.Should().ContainInOrder(1, TestData.TestValueTwo, TestData.TestValueThree);
        list.Dispose();
    }

    /// <summary>TotalCount should return sum of all inner list counts.</summary>
    [Test]
    public void TotalCount_ShouldReturnSumOfAllInnerListCounts()
    {
        ReactiveList<int>[] rows =
        [
            new() { 1, TestData.TestValueTwo },
            new() { TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive },
            new() { TestData.TestValueSix }
        ];
        var list = new Reactive2DList<int>(rows);

        var total = list.TotalCount();

        _ = total.Should().Be(TestData.TestValueSix);
        list.Dispose();
    }

    /// <summary>TotalCount should return zero for empty list.</summary>
    [Test]
    public void TotalCount_ShouldReturnZeroForEmptyList()
    {
        var list = new Reactive2DList<int>();

        var total = list.TotalCount();

        _ = total.Should().Be(0);
        list.Dispose();
    }

    /// <summary>TotalCount should handle empty inner lists.</summary>
    [Test]
    public void TotalCount_ShouldHandleEmptyInnerLists()
    {
        var list = new Reactive2DList<int> { new ReactiveList<int> { 1, TestData.TestValueTwo }, new ReactiveList<int>(), new ReactiveList<int> { TestData.TestValueThree } };

        var total = list.TotalCount();

        _ = total.Should().Be(TestData.TestValueThree);
        list.Dispose();
    }

    /// <summary>AddToInner should add items to specified inner list.</summary>
    [Test]
    public void AddToInner_ShouldAddItemsToSpecifiedInnerList()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour }]);

        list.AddToInner(0, [TestData.TestValueFive, TestData.TestValueSix]);

        _ = list[0].Count.Should().Be(TestData.TestValueFour);
        _ = list[0][TestData.TestValueTwo].Should().Be(TestData.TestValueFive);
        _ = list[0][TestData.TestValueThree].Should().Be(TestData.TestValueSix);
        list.Dispose();
    }

    /// <summary>AddToInner should add single item to specified inner list.</summary>
    [Test]
    public void AddToInner_ShouldAddSingleItemToSpecifiedInnerList()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour }]);

        list.AddToInner(1, TestData.TestValueNinetyNine);

        _ = list[1].Count.Should().Be(TestData.TestValueThree);
        _ = list[1][TestData.TestValueTwo].Should().Be(TestData.TestValueNinetyNine);
        list.Dispose();
    }

    /// <summary>AddToInner should throw when outer index is out of range.</summary>
    [Test]
    public void AddToInner_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.AddToInner(TestData.TestValueFive, TestData.TestValueNinetyNine);

        _ = action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>AddToInner should throw when items is null.</summary>
    [Test]
    public void AddToInner_ShouldThrowWhenItemsIsNull()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.AddToInner(0, (IEnumerable<int>)null!);

        _ = action.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>RemoveFromInner should remove item at specified indices.</summary>
    [Test]
    public void RemoveFromInner_ShouldRemoveItemAtSpecifiedIndices()
    {
        ReactiveList<int>[] rows =
        [
            new() { 1, TestData.TestValueTwo, TestData.TestValueThree },
            new() { TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix }
        ];
        var list = new Reactive2DList<int>(rows);

        list.RemoveFromInner(0, 1);

        _ = list[0].Count.Should().Be(TestData.TestValueTwo);
        _ = list[0][0].Should().Be(1);
        _ = list[0][1].Should().Be(TestData.TestValueThree);
        list.Dispose();
    }

    /// <summary>RemoveFromInner should throw when outer index is out of range.</summary>
    [Test]
    public void RemoveFromInner_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.RemoveFromInner(TestData.TestValueFive, 0);

        _ = action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>ClearInner should clear the specified inner list.</summary>
    [Test]
    public void ClearInner_ShouldClearTheSpecifiedInnerList()
    {
        ReactiveList<int>[] rows =
        [
            new() { 1, TestData.TestValueTwo, TestData.TestValueThree },
            new() { TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix }
        ];
        var list = new Reactive2DList<int>(rows);

        list.ClearInner(0);

        _ = list[0].Count.Should().Be(0);
        _ = list[1].Count.Should().Be(TestData.TestValueThree); // Other list unchanged
        list.Dispose();
    }

    /// <summary>ClearInner should throw when outer index is out of range.</summary>
    [Test]
    public void ClearInner_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.ClearInner(TestData.TestValueFive);

        _ = action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>Constructor should throw when items enumerable is null.</summary>
    [Test]
    public void Constructor_ShouldThrowWhenItemsEnumerableIsNull()
    {
        var action = static () => new Reactive2DList<int>((IEnumerable<IEnumerable<int>>)null!);

        _ = action.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.ItemsParameterName);
    }

    /// <summary>Constructor should throw when item enumerable is null.</summary>
    [Test]
    public void Constructor_ShouldThrowWhenItemEnumerableIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(static () => _ = new Reactive2DList<int>((IEnumerable<int>)null!));

        Assert.Equal(TestData.ItemsParameterName, exception.ParamName);
    }

    /// <summary>Constructor should throw when reactive list item is null.</summary>
    [Test]
    public void Constructor_ShouldThrowWhenReactiveListItemIsNull()
    {
        var action = static () => new Reactive2DList<int>((ReactiveList<int>)null!);

        _ = action.Should().Throw<ArgumentNullException>()
            .WithParameterName("item");
    }

    /// <summary>AddRange with nested enumerable should throw when null.</summary>
    [Test]
    public void AddRange_NestedEnumerable_ShouldThrowWhenNull()
    {
        var list = new Reactive2DList<int>();

        var action = () => list.AddRange((IEnumerable<IEnumerable<int>>)null!);

        _ = action.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>AddRange with single enumerable should throw when null.</summary>
    [Test]
    public void AddRange_SingleEnumerable_ShouldThrowWhenNull()
    {
        var list = new Reactive2DList<int>();

        var action = () => list.AddRange((IEnumerable<int>)null!);

        _ = action.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>InsertRange with enumerable should throw when null.</summary>
    [Test]
    public void InsertRange_Enumerable_ShouldThrowWhenNull()
    {
        var list = new Reactive2DList<int>([1]);

        var action = () => list.InsertRange(0, (IEnumerable<int>)null!);

        _ = action.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>Insert with inner index should throw when items null.</summary>
    [Test]
    public void Insert_WithInnerIndex_ShouldThrowWhenItemsNull()
    {
        var list = new Reactive2DList<int>([1]);

        var action = () => list.Insert(0, (IEnumerable<int>)null!, 0);

        _ = action.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>Copies the flattened sequence without a LINQ allocation.</summary>
    /// <param name="list">The two-dimensional reactive list.</param>
    /// <returns>The flattened values.</returns>
    private static List<int> FlattenToList(Reactive2DList<int> list) => new(list.Flatten());
}
