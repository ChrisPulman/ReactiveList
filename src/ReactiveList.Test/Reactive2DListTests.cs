// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Collections;
#else
using CP.Primitives.Collections;
#endif
using TUnit.Assertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests Reactive2DList behavior.</summary>
public class Reactive2DListTests
{
    /// <summary>Constructors the should initialize empty list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_ShouldInitializeEmptyList()
    {
        var list = new Reactive2DList<int>();
        await Assert.That(list).IsEmpty();
        list.Dispose();
    }

    /// <summary>Constructors the should initialize with items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_ShouldInitializeWithItems()
    {
        var items = new List<List<int>> { new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour } };
        var list = new Reactive2DList<int>(items);
        await Assert.That(list.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(list[0].Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(list[1].Count).IsEqualTo(TestData.TestValueTwo);
        list.Dispose();
    }

    /// <summary>Constructors the should initialize with reactive lists.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_ShouldInitializeWithReactiveLists()
    {
        var items = new List<ReactiveList<int>> { new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour } };
        var list = new Reactive2DList<int>(items);
        await Assert.That(list.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(list[0].Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(list[1].Count).IsEqualTo(TestData.TestValueTwo);
        list.Dispose();
    }

    /// <summary>Constructors the should initialize with single item.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_ShouldInitializeWithSingleItem()
    {
        var list = new Reactive2DList<int>(TestData.TestValueFive);
        await Assert.That(list).HasSingleItem();
        await Assert.That(list[0]).HasSingleItem();
        await Assert.That(list[0][0]).IsEqualTo(TestData.TestValueFive);
        list.Dispose();
    }

    /// <summary>Constructors the should initialize with item enumerable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_ShouldInitializeWithItemEnumerable()
    {
        IEnumerable<int> items = Enumerable.Range(TestData.TestValueSeven, TestData.TestValueTwo);
        var list = new Reactive2DList<int>(items);

        await Assert.That(list.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(list[0]).HasSingleItem();
        await Assert.That(list[1]).HasSingleItem();
        await Assert.That(list[0][0]).IsEqualTo(TestData.TestValueSeven);
        await Assert.That(list[1][0]).IsEqualTo(TestData.TestValueEight);
        list.Dispose();
    }

    /// <summary>Constructors the should initialize with items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_ShouldInitializeWithReactiveList()
    {
        var items = new ReactiveList<int> { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour };
        var list = new Reactive2DList<int>(items);
        await Assert.That(list).HasSingleItem();
        await Assert.That(list[0].Count).IsEqualTo(TestData.TestValueFour);
        list.Dispose();
    }

    /// <summary>Adds the range should add items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddRange_ShouldAddItems()
    {
        var list = new Reactive2DList<int>();
        var items = new List<List<int>> { new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour } };
        list.AddRange(items);
        await Assert.That(list.Count).IsEqualTo(TestData.TestValueTwo);
        list.Dispose();
    }

    /// <summary>Adds the range should add single items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddRange_ShouldAddSingleItems()
    {
        var list = new Reactive2DList<int>();
        var items = new List<int> { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour };
        list.AddRange(items);
        await Assert.That(list.Count).IsEqualTo(TestData.TestValueFour);
        list.Dispose();
    }

    /// <summary>Adds the index of the range should insert items at.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddRange_ShouldInsertItemsAtIndex()
    {
        var list = new Reactive2DList<int>([TestData.TestValueFive]);
        var items = new List<List<int>> { new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour } };
        list.AddRange(items);
        await Assert.That(list.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(list[0][0]).IsEqualTo(TestData.TestValueFive);
        await Assert.That(list[1].Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(list[1][1]).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(list[TestData.TestValueTwo].Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(list[TestData.TestValueTwo][1]).IsEqualTo(TestData.TestValueFour);
        list.Dispose();
    }

    /// <summary>InsertRange should insert items as a new row at the requested index.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InsertRange_ShouldInsertItemsAtIndex()
    {
        var list = new Reactive2DList<int>([TestData.TestValueFive]);
        var items = new List<int> { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour };
        list.InsertRange(0, items);
        await Assert.That(list.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(list[0][0]).IsEqualTo(1);
        await Assert.That(list[0][TestData.TestValueThree]).IsEqualTo(TestData.TestValueFour);
        await Assert.That(list[1][0]).IsEqualTo(TestData.TestValueFive);
        list.Dispose();
    }

    /// <summary>Inserts the index of the should insert single item at.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Insert_ShouldInsertSingleItemAtIndex()
    {
        var list = new Reactive2DList<int>([TestData.TestValueFive]);
        list.Insert(0, TestData.TestValueTen);
        await Assert.That(list.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(list[0][0]).IsEqualTo(TestData.TestValueTen);
        list.Dispose();
    }

    /// <summary>Inserts the index of the should insert reactive list at.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Insert_ShouldInsertReactiveListAtIndex()
    {
        var list = new Reactive2DList<int>([TestData.TestValueFive]);
        var reactiveList = new ReactiveList<int> { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour };
        list.Insert(0, reactiveList);
        await Assert.That(list.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(list[0][0]).IsEqualTo(1);
        list.Dispose();
    }

    /// <summary>Inserts the index of the should insert items in reactive list at.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Insert_ShouldInsertItemsInReactiveListAtIndex()
    {
        var list = new Reactive2DList<int>([TestData.TestValueFive]);
        var items = new List<int> { 1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour };
        list.Insert(0, items, 0);
        await Assert.That(list[0].Count).IsEqualTo(TestData.TestValueFive);
        await Assert.That(list[0][0]).IsEqualTo(1);
        await Assert.That(list[0][TestData.TestValueThree]).IsEqualTo(TestData.TestValueFour);
        await Assert.That(list[0][TestData.TestValueFour]).IsEqualTo(TestData.TestValueFive);
        list.Dispose();
    }

    /// <summary>GetItem should return item at specified indices.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetItem_ShouldReturnItemAtSpecifiedIndices()
    {
        ReactiveList<int>[] rows =
        [
            new() { 1, TestData.TestValueTwo, TestData.TestValueThree },
            new() { TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix }
        ];
        var list = new Reactive2DList<int>(rows);

        var item = list.GetItem(1, TestData.TestValueTwo);

        await Assert.That(item).IsEqualTo(TestData.TestValueSix);
        list.Dispose();
    }

    /// <summary>GetItem should throw when outer index is negative.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetItem_ShouldThrowWhenOuterIndexIsNegative()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.GetItem(-1, 0);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>().WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>GetItem should throw when outer index exceeds count.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetItem_ShouldThrowWhenOuterIndexExceedsCount()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.GetItem(TestData.TestValueFive, 0);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>().WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>GetItem should throw when inner index is negative.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetItem_ShouldThrowWhenInnerIndexIsNegative()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.GetItem(0, -1);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>().WithParameterName(TestData.InnerIndexParameterName);
        list.Dispose();
    }

    /// <summary>GetItem should throw when inner index exceeds count.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetItem_ShouldThrowWhenInnerIndexExceedsCount()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.GetItem(0, TestData.TestValueFive);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>().WithParameterName(TestData.InnerIndexParameterName);
        list.Dispose();
    }

    /// <summary>SetItem should update item at specified indices.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SetItem_ShouldUpdateItemAtSpecifiedIndices()
    {
        ReactiveList<int>[] rows =
        [
            new() { 1, TestData.TestValueTwo, TestData.TestValueThree },
            new() { TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix }
        ];
        var list = new Reactive2DList<int>(rows);

        list.SetItem(1, 1, TestData.TestValueNinetyNine);

        await Assert.That(list.GetItem(1, 1)).IsEqualTo(TestData.TestValueNinetyNine);
        list.Dispose();
    }

    /// <summary>SetItem should throw when outer index is out of range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SetItem_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.SetItem(TestData.TestValueFive, 0, TestData.TestValueNinetyNine);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>().WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>SetItem should throw when inner index is out of range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SetItem_ShouldThrowWhenInnerIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.SetItem(0, TestData.TestValueFive, TestData.TestValueNinetyNine);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>().WithParameterName(TestData.InnerIndexParameterName);
        list.Dispose();
    }

    /// <summary>Flatten should return all items in order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Flatten_ShouldReturnAllItemsInOrder()
    {
        ReactiveList<int>[] rows =
        [
            new() { 1, TestData.TestValueTwo },
            new() { TestData.TestValueThree, TestData.TestValueFour },
            new() { TestData.TestValueFive, TestData.TestValueSix }
        ];
        var list = new Reactive2DList<int>(rows);

        var flattened = FlattenToList(list);

        await Assert.That(flattened).Count().IsEqualTo(TestData.TestValueSix);
        await Assert.That(TestSequences.ContainsInOrder(flattened, [1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix]))
            .IsTrue();
        list.Dispose();
    }

    /// <summary>Flatten should return empty for empty list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Flatten_ShouldReturnEmptyForEmptyList()
    {
        var list = new Reactive2DList<int>();

        var flattened = FlattenToList(list);

        await Assert.That(flattened).IsEmpty();
        list.Dispose();
    }

    /// <summary>Flatten should handle empty inner lists.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Flatten_ShouldHandleEmptyInnerLists()
    {
        var list = new Reactive2DList<int> { new ReactiveList<int> { 1, TestData.TestValueTwo }, new ReactiveList<int>(), new ReactiveList<int> { TestData.TestValueThree } };

        var flattened = FlattenToList(list);

        await Assert.That(flattened).Count().IsEqualTo(TestData.TestValueThree);
        await Assert.That(TestSequences.ContainsInOrder(flattened, [1, TestData.TestValueTwo, TestData.TestValueThree])).IsTrue();
        list.Dispose();
    }

    /// <summary>TotalCount should return sum of all inner list counts.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TotalCount_ShouldReturnSumOfAllInnerListCounts()
    {
        ReactiveList<int>[] rows =
        [
            new() { 1, TestData.TestValueTwo },
            new() { TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive },
            new() { TestData.TestValueSix }
        ];
        var list = new Reactive2DList<int>(rows);

        var total = list.TotalCount();

        await Assert.That(total).IsEqualTo(TestData.TestValueSix);
        list.Dispose();
    }

    /// <summary>TotalCount should return zero for empty list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TotalCount_ShouldReturnZeroForEmptyList()
    {
        var list = new Reactive2DList<int>();

        var total = list.TotalCount();

        await Assert.That(total).IsEqualTo(0);
        list.Dispose();
    }

    /// <summary>TotalCount should handle empty inner lists.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TotalCount_ShouldHandleEmptyInnerLists()
    {
        var list = new Reactive2DList<int> { new ReactiveList<int> { 1, TestData.TestValueTwo }, new ReactiveList<int>(), new ReactiveList<int> { TestData.TestValueThree } };

        var total = list.TotalCount();

        await Assert.That(total).IsEqualTo(TestData.TestValueThree);
        list.Dispose();
    }

    /// <summary>AddToInner should add items to specified inner list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddToInner_ShouldAddItemsToSpecifiedInnerList()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour }]);

        list.AddToInner(0, [TestData.TestValueFive, TestData.TestValueSix]);

        await Assert.That(list[0].Count).IsEqualTo(TestData.TestValueFour);
        await Assert.That(list[0][TestData.TestValueTwo]).IsEqualTo(TestData.TestValueFive);
        await Assert.That(list[0][TestData.TestValueThree]).IsEqualTo(TestData.TestValueSix);
        list.Dispose();
    }

    /// <summary>AddToInner should add single item to specified inner list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddToInner_ShouldAddSingleItemToSpecifiedInnerList()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }, new() { TestData.TestValueThree, TestData.TestValueFour }]);

        list.AddToInner(1, TestData.TestValueNinetyNine);

        await Assert.That(list[1].Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(list[1][TestData.TestValueTwo]).IsEqualTo(TestData.TestValueNinetyNine);
        list.Dispose();
    }

    /// <summary>AddToInner should throw when outer index is out of range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddToInner_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.AddToInner(TestData.TestValueFive, TestData.TestValueNinetyNine);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>().WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>AddToInner should throw when items is null.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddToInner_ShouldThrowWhenItemsIsNull()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.AddToInner(0, (IEnumerable<int>)null!);

        await Assert.That(action).Throws<ArgumentNullException>().WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>RemoveFromInner should remove item at specified indices.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveFromInner_ShouldRemoveItemAtSpecifiedIndices()
    {
        ReactiveList<int>[] rows =
        [
            new() { 1, TestData.TestValueTwo, TestData.TestValueThree },
            new() { TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix }
        ];
        var list = new Reactive2DList<int>(rows);

        list.RemoveFromInner(0, 1);

        await Assert.That(list[0].Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(list[0][0]).IsEqualTo(1);
        await Assert.That(list[0][1]).IsEqualTo(TestData.TestValueThree);
        list.Dispose();
    }

    /// <summary>RemoveFromInner should throw when outer index is out of range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveFromInner_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.RemoveFromInner(TestData.TestValueFive, 0);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>().WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>ClearInner should clear the specified inner list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClearInner_ShouldClearTheSpecifiedInnerList()
    {
        ReactiveList<int>[] rows =
        [
            new() { 1, TestData.TestValueTwo, TestData.TestValueThree },
            new() { TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix }
        ];
        var list = new Reactive2DList<int>(rows);

        list.ClearInner(0);

        await Assert.That(list[0].Count).IsEqualTo(0);
        await Assert.That(list[1].Count).IsEqualTo(TestData.TestValueThree); // Other list unchanged
        list.Dispose();
    }

    /// <summary>ClearInner should throw when outer index is out of range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClearInner_ShouldThrowWhenOuterIndexIsOutOfRange()
    {
        var list = new Reactive2DList<int>((ReactiveList<int>[])[new() { 1, TestData.TestValueTwo }]);

        var action = () => list.ClearInner(TestData.TestValueFive);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>().WithParameterName(TestData.OuterIndexParameterName);
        list.Dispose();
    }

    /// <summary>Constructor should throw when items enumerable is null.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_ShouldThrowWhenItemsEnumerableIsNull()
    {
        var action = static () => new Reactive2DList<int>((IEnumerable<IEnumerable<int>>)null!);

        await Assert.That(action).Throws<ArgumentNullException>().WithParameterName(TestData.ItemsParameterName);
    }

    /// <summary>Constructor should throw when item enumerable is null.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_ShouldThrowWhenItemEnumerableIsNull()
    {
        var exception = await Assert.That(static () => _ = new Reactive2DList<int>((IEnumerable<int>)null!)).Throws<ArgumentNullException>();

        await Assert.That(exception).IsNotNull().And.WithParameterName(TestData.ItemsParameterName);
    }

    /// <summary>Constructor should throw when reactive list item is null.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_ShouldThrowWhenReactiveListItemIsNull()
    {
        var action = static () => new Reactive2DList<int>((ReactiveList<int>)null!);

        await Assert.That(action).Throws<ArgumentNullException>().WithParameterName("item");
    }

    /// <summary>AddRange with nested enumerable should throw when null.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddRange_NestedEnumerable_ShouldThrowWhenNull()
    {
        var list = new Reactive2DList<int>();

        var action = () => list.AddRange((IEnumerable<IEnumerable<int>>)null!);

        await Assert.That(action).Throws<ArgumentNullException>().WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>AddRange with single enumerable should throw when null.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddRange_SingleEnumerable_ShouldThrowWhenNull()
    {
        var list = new Reactive2DList<int>();

        var action = () => list.AddRange((IEnumerable<int>)null!);

        await Assert.That(action).Throws<ArgumentNullException>().WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>InsertRange with enumerable should throw when null.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InsertRange_Enumerable_ShouldThrowWhenNull()
    {
        var list = new Reactive2DList<int>([1]);

        var action = () => list.InsertRange(0, (IEnumerable<int>)null!);

        await Assert.That(action).Throws<ArgumentNullException>().WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>Insert with inner index should throw when items null.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Insert_WithInnerIndex_ShouldThrowWhenItemsNull()
    {
        var list = new Reactive2DList<int>([1]);

        var action = () => list.Insert(0, (IEnumerable<int>)null!, 0);

        await Assert.That(action).Throws<ArgumentNullException>().WithParameterName(TestData.ItemsParameterName);
        list.Dispose();
    }

    /// <summary>Copies the flattened sequence without a LINQ allocation.</summary>
    /// <param name="list">The two-dimensional reactive list.</param>
    /// <returns>The flattened values.</returns>
    private static List<int> FlattenToList(Reactive2DList<int> list) => new(list.Flatten());
}
