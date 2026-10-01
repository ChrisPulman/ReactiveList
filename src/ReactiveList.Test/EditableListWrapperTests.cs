// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TUnit.Assertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for EditableListWrapper.</summary>
public class EditableListWrapperTests
{
    /// <summary>The second ordinal.</summary>
    private const int SecondOrdinal = 2;

    /// <summary>The range item count.</summary>
    private const int RangeItemCount = 3;

    /// <summary>The out of range index.</summary>
    private const int OutOfRangeIndex = 5;

    /// <summary>The third item.</summary>
    private const string ThirdItem = "three";

    /// <summary>The updated item.</summary>
    private const string UpdatedItem = "updated";

    /// <summary>Constructor should initialize with list only.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_WithListOnly_ShouldInitialize()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        await Assert.That(wrapper.Count).IsEqualTo(SecondOrdinal);
        await Assert.That(wrapper[0]).IsEqualTo("one");
        await Assert.That(wrapper[1]).IsEqualTo("two");
    }

    /// <summary>Constructor should initialize with list and observable collection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_WithListAndObservableCollection_ShouldInitialize()
    {
        var list = new List<string> { "one", "two" };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        await Assert.That(wrapper.Count).IsEqualTo(SecondOrdinal);
        await Assert.That(observable.Count).IsEqualTo(SecondOrdinal);
    }

    /// <summary>IsReadOnly should return false.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IsReadOnly_ShouldReturnFalse()
    {
        var wrapper = new EditableListWrapper<string>([]);
        await Assert.That(wrapper.IsReadOnly).IsFalse();
    }

    /// <summary>Indexer get should return correct item.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Indexer_Get_ShouldReturnCorrectItem()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        await Assert.That(wrapper[1]).IsEqualTo("two");
    }

    /// <summary>Indexer set should update list only when no observable collection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Indexer_Set_WithoutObservable_ShouldUpdateList()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper[0] = UpdatedItem;

        await Assert.That(list[0]).IsEqualTo(UpdatedItem);
    }

    /// <summary>Indexer set should update both list and observable collection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Indexer_Set_WithObservable_ShouldUpdateBoth()
    {
        var list = new List<string> { "one", "two" };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper[0] = UpdatedItem;

        await Assert.That(list[0]).IsEqualTo(UpdatedItem);
        await Assert.That(observable[0]).IsEqualTo(UpdatedItem);
    }

    /// <summary>Add should add to list only when no observable collection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Add_WithoutObservable_ShouldAddToList()
    {
        var list = new List<string>();
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Add("item");

        await Assert.That(list).Contains("item");
    }

    /// <summary>Add should add to both list and observable collection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Add_WithObservable_ShouldAddToBoth()
    {
        var list = new List<string>();
        var observable = new ObservableCollection<string>();
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Add("item");

        await Assert.That(list).Contains("item");
        await Assert.That(observable).Contains("item");
    }

    /// <summary>AddRange should add array items to list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddRange_WithArray_ShouldAddItems()
    {
        var list = new List<string>();
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.AddRange(["one", "two", ThirdItem]);

        await Assert.That(list).IsEquivalentTo(["one", "two", ThirdItem]);
    }

    /// <summary>AddRange should add items to both list and observable collection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddRange_WithObservable_ShouldAddToBoth()
    {
        var list = new List<string>();
        var observable = new ObservableCollection<string>();
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.AddRange(["one", "two"]);

        await Assert.That(list).IsEquivalentTo(["one", "two"]);
        await Assert.That(observable).IsEquivalentTo(["one", "two"]);
    }

    /// <summary>AddRange should handle enumerable that is not array.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddRange_WithEnumerable_ShouldAddItems()
    {
        var list = new List<string>();
        var wrapper = new EditableListWrapper<string>(list);

        var items = EnumerateRangeItems();
        wrapper.AddRange(items);

        await Assert.That(list).IsEquivalentTo(["item1", "item2", "item3"]);
    }

    /// <summary>Clear should clear list only when no observable collection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Clear_WithoutObservable_ShouldClearList()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Clear();

        await Assert.That(list).IsEmpty();
    }

    /// <summary>Clear should clear both list and observable collection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Clear_WithObservable_ShouldClearBoth()
    {
        var list = new List<string> { "one", "two" };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Clear();

        await Assert.That(list).IsEmpty();
        await Assert.That(observable).IsEmpty();
    }

    /// <summary>Contains should return true for existing item.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Contains_WithExistingItem_ShouldReturnTrue()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        await Assert.That(wrapper.Contains("one")).IsTrue();
    }

    /// <summary>Contains should return false for non-existing item.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Contains_WithNonExistingItem_ShouldReturnFalse()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        await Assert.That(wrapper.Contains(ThirdItem)).IsFalse();
    }

    /// <summary>CopyTo should copy items to array.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CopyTo_ShouldCopyItemsToArray()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);
        var array = new string[3];

        wrapper.CopyTo(array, 1);

        await Assert.That(array[0]).IsNull();
        await Assert.That(array[1]).IsEqualTo("one");
        await Assert.That(array[SecondOrdinal]).IsEqualTo("two");
    }

    /// <summary>GetEnumerator should enumerate items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetEnumerator_ShouldEnumerateItems()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        var items = new List<string>(wrapper);

        await Assert.That(items).IsEquivalentTo(["one", "two", ThirdItem]);
    }

    /// <summary>IndexOf should return correct index.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IndexOf_ShouldReturnCorrectIndex()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        await Assert.That(wrapper.IndexOf("two")).IsEqualTo(1);
    }

    /// <summary>IndexOf should return -1 for non-existing item.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IndexOf_WithNonExistingItem_ShouldReturnNegativeOne()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        await Assert.That(wrapper.IndexOf(ThirdItem)).IsEqualTo(-1);
    }

    /// <summary>Insert should insert at correct position without observable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Insert_WithoutObservable_ShouldInsertAtPosition()
    {
        var list = new List<string> { "one", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Insert(1, "two");

        await Assert.That(list).IsEquivalentTo(["one", "two", ThirdItem], CollectionOrdering.Matching);
    }

    /// <summary>Insert should insert at correct position with observable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Insert_WithObservable_ShouldInsertInBoth()
    {
        var list = new List<string> { "one", ThirdItem };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Insert(1, "two");

        await Assert.That(list).IsEquivalentTo(["one", "two", ThirdItem], CollectionOrdering.Matching);
        await Assert.That(observable).IsEquivalentTo(["one", "two", ThirdItem], CollectionOrdering.Matching);
    }

    /// <summary>Move should move item to new position.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldMoveItemToNewPosition()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Move(0, SecondOrdinal);

        await Assert.That(list).IsEquivalentTo(["two", ThirdItem, "one"], CollectionOrdering.Matching);
    }

    /// <summary>Move should move item in both list and observable collection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_WithObservable_ShouldMoveInBoth()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Move(0, SecondOrdinal);

        await Assert.That(list).IsEquivalentTo(["two", ThirdItem, "one"], CollectionOrdering.Matching);
        await Assert.That(observable).IsEquivalentTo(["two", ThirdItem, "one"], CollectionOrdering.Matching);
    }

    /// <summary>Move should do nothing when old and new index are same.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_WhenSameIndex_ShouldDoNothing()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Move(1, 1);

        await Assert.That(list).IsEquivalentTo(["one", "two", ThirdItem], CollectionOrdering.Matching);
    }

    /// <summary>Move should throw when old index is out of range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_WhenOldIndexOutOfRange_ShouldThrow()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        var act = () => wrapper.Move(-1, 0);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>().WithParameterName("oldIndex");
    }

    /// <summary>Move should throw when new index is out of range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_WhenNewIndexOutOfRange_ShouldThrow()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        var act = () => wrapper.Move(0, OutOfRangeIndex);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>().WithParameterName("newIndex");
    }

    /// <summary>Remove should remove existing item and return true.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_ExistingItem_ShouldRemoveAndReturnTrue()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        var result = wrapper.Remove("two");

        await Assert.That(result).IsTrue();
        await Assert.That(list).IsEquivalentTo(["one", ThirdItem]);
    }

    /// <summary>Remove should return false for non-existing item.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_NonExistingItem_ShouldReturnFalse()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        var result = wrapper.Remove(ThirdItem);

        await Assert.That(result).IsFalse();
        await Assert.That(list.Count).IsEqualTo(SecondOrdinal);
    }

    /// <summary>Remove should remove from both list and observable collection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_WithObservable_ShouldRemoveFromBoth()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        _ = wrapper.Remove("two");

        await Assert.That(list).IsEquivalentTo(["one", ThirdItem]);
        await Assert.That(observable).IsEquivalentTo(["one", ThirdItem]);
    }

    /// <summary>RemoveAt should remove item at index without observable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveAt_WithoutObservable_ShouldRemoveAtIndex()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.RemoveAt(1);

        await Assert.That(list).IsEquivalentTo(["one", ThirdItem]);
    }

    /// <summary>RemoveAt should remove from both list and observable collection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveAt_WithObservable_ShouldRemoveFromBoth()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.RemoveAt(1);

        await Assert.That(list).IsEquivalentTo(["one", ThirdItem]);
        await Assert.That(observable).IsEquivalentTo(["one", ThirdItem]);
    }

    /// <summary>Non-generic GetEnumerator should enumerate items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NonGenericGetEnumerator_ShouldEnumerateItems()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        var items = new List<object?>();
        foreach (var item in ((System.Collections.IEnumerable)wrapper))
        {
            items.Add(item);
        }

        await Assert.That(items).IsEquivalentTo(ExpectedSequences.WrapperItems);
    }

    /// <summary>Produces a non-array enumerable for AddRange coverage.</summary>
    /// <returns>The range fixture items.</returns>
    private static IEnumerable<string> EnumerateRangeItems()
    {
        for (var item = 1; item <= RangeItemCount; item++)
        {
            yield return $"item{item}";
        }
    }
}
