// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FluentAssertions;
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
    [Test]
    public void Constructor_WithListOnly_ShouldInitialize()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Count.Should().Be(SecondOrdinal);
        wrapper[0].Should().Be("one");
        wrapper[1].Should().Be("two");
    }

    /// <summary>Constructor should initialize with list and observable collection.</summary>
    [Test]
    public void Constructor_WithListAndObservableCollection_ShouldInitialize()
    {
        var list = new List<string> { "one", "two" };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Count.Should().Be(SecondOrdinal);
        observable.Count.Should().Be(SecondOrdinal);
    }

    /// <summary>IsReadOnly should return false.</summary>
    [Test]
    public void IsReadOnly_ShouldReturnFalse()
    {
        var wrapper = new EditableListWrapper<string>([]);
        wrapper.IsReadOnly.Should().BeFalse();
    }

    /// <summary>Indexer get should return correct item.</summary>
    [Test]
    public void Indexer_Get_ShouldReturnCorrectItem()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper[1].Should().Be("two");
    }

    /// <summary>Indexer set should update list only when no observable collection.</summary>
    [Test]
    public void Indexer_Set_WithoutObservable_ShouldUpdateList()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper[0] = UpdatedItem;

        list[0].Should().Be(UpdatedItem);
    }

    /// <summary>Indexer set should update both list and observable collection.</summary>
    [Test]
    public void Indexer_Set_WithObservable_ShouldUpdateBoth()
    {
        var list = new List<string> { "one", "two" };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper[0] = UpdatedItem;

        list[0].Should().Be(UpdatedItem);
        observable[0].Should().Be(UpdatedItem);
    }

    /// <summary>Add should add to list only when no observable collection.</summary>
    [Test]
    public void Add_WithoutObservable_ShouldAddToList()
    {
        var list = new List<string>();
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Add("item");

        list.Should().Contain("item");
    }

    /// <summary>Add should add to both list and observable collection.</summary>
    [Test]
    public void Add_WithObservable_ShouldAddToBoth()
    {
        var list = new List<string>();
        var observable = new ObservableCollection<string>();
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Add("item");

        list.Should().Contain("item");
        observable.Should().Contain("item");
    }

    /// <summary>AddRange should add array items to list.</summary>
    [Test]
    public void AddRange_WithArray_ShouldAddItems()
    {
        var list = new List<string>();
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.AddRange(["one", "two", ThirdItem]);

        list.Should().BeEquivalentTo(["one", "two", ThirdItem]);
    }

    /// <summary>AddRange should add items to both list and observable collection.</summary>
    [Test]
    public void AddRange_WithObservable_ShouldAddToBoth()
    {
        var list = new List<string>();
        var observable = new ObservableCollection<string>();
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.AddRange(["one", "two"]);

        list.Should().BeEquivalentTo(["one", "two"]);
        observable.Should().BeEquivalentTo(["one", "two"]);
    }

    /// <summary>AddRange should handle enumerable that is not array.</summary>
    [Test]
    public void AddRange_WithEnumerable_ShouldAddItems()
    {
        var list = new List<string>();
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.AddRange(Enumerable.Range(1, RangeItemCount).Select(i => $"item{i}"));

        list.Should().BeEquivalentTo(["item1", "item2", "item3"]);
    }

    /// <summary>Clear should clear list only when no observable collection.</summary>
    [Test]
    public void Clear_WithoutObservable_ShouldClearList()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Clear();

        list.Should().BeEmpty();
    }

    /// <summary>Clear should clear both list and observable collection.</summary>
    [Test]
    public void Clear_WithObservable_ShouldClearBoth()
    {
        var list = new List<string> { "one", "two" };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Clear();

        list.Should().BeEmpty();
        observable.Should().BeEmpty();
    }

    /// <summary>Contains should return true for existing item.</summary>
    [Test]
    public void Contains_WithExistingItem_ShouldReturnTrue()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Contains("one").Should().BeTrue();
    }

    /// <summary>Contains should return false for non-existing item.</summary>
    [Test]
    public void Contains_WithNonExistingItem_ShouldReturnFalse()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Contains(ThirdItem).Should().BeFalse();
    }

    /// <summary>CopyTo should copy items to array.</summary>
    [Test]
    public void CopyTo_ShouldCopyItemsToArray()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);
        var array = new string[3];

        wrapper.CopyTo(array, 1);

        array[0].Should().BeNull();
        array[1].Should().Be("one");
        array[SecondOrdinal].Should().Be("two");
    }

    /// <summary>GetEnumerator should enumerate items.</summary>
    [Test]
    public void GetEnumerator_ShouldEnumerateItems()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        var items = wrapper.ToList();

        items.Should().BeEquivalentTo(["one", "two", ThirdItem]);
    }

    /// <summary>IndexOf should return correct index.</summary>
    [Test]
    public void IndexOf_ShouldReturnCorrectIndex()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.IndexOf("two").Should().Be(1);
    }

    /// <summary>IndexOf should return -1 for non-existing item.</summary>
    [Test]
    public void IndexOf_WithNonExistingItem_ShouldReturnNegativeOne()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.IndexOf(ThirdItem).Should().Be(-1);
    }

    /// <summary>Insert should insert at correct position without observable.</summary>
    [Test]
    public void Insert_WithoutObservable_ShouldInsertAtPosition()
    {
        var list = new List<string> { "one", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Insert(1, "two");

        list.Should().BeEquivalentTo(["one", "two", ThirdItem], options => options.WithStrictOrdering());
    }

    /// <summary>Insert should insert at correct position with observable.</summary>
    [Test]
    public void Insert_WithObservable_ShouldInsertInBoth()
    {
        var list = new List<string> { "one", ThirdItem };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Insert(1, "two");

        list.Should().BeEquivalentTo(["one", "two", ThirdItem], options => options.WithStrictOrdering());
        observable.Should().BeEquivalentTo(["one", "two", ThirdItem], options => options.WithStrictOrdering());
    }

    /// <summary>Move should move item to new position.</summary>
    [Test]
    public void Move_ShouldMoveItemToNewPosition()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Move(0, SecondOrdinal);

        list.Should().BeEquivalentTo(["two", ThirdItem, "one"], options => options.WithStrictOrdering());
    }

    /// <summary>Move should move item in both list and observable collection.</summary>
    [Test]
    public void Move_WithObservable_ShouldMoveInBoth()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Move(0, SecondOrdinal);

        list.Should().BeEquivalentTo(["two", ThirdItem, "one"], options => options.WithStrictOrdering());
        observable.Should().BeEquivalentTo(["two", ThirdItem, "one"], options => options.WithStrictOrdering());
    }

    /// <summary>Move should do nothing when old and new index are same.</summary>
    [Test]
    public void Move_WhenSameIndex_ShouldDoNothing()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Move(1, 1);

        list.Should().BeEquivalentTo(["one", "two", ThirdItem], options => options.WithStrictOrdering());
    }

    /// <summary>Move should throw when old index is out of range.</summary>
    [Test]
    public void Move_WhenOldIndexOutOfRange_ShouldThrow()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        var act = () => wrapper.Move(-1, 0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("oldIndex");
    }

    /// <summary>Move should throw when new index is out of range.</summary>
    [Test]
    public void Move_WhenNewIndexOutOfRange_ShouldThrow()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        var act = () => wrapper.Move(0, OutOfRangeIndex);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("newIndex");
    }

    /// <summary>Remove should remove existing item and return true.</summary>
    [Test]
    public void Remove_ExistingItem_ShouldRemoveAndReturnTrue()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        var result = wrapper.Remove("two");

        result.Should().BeTrue();
        list.Should().BeEquivalentTo(["one", ThirdItem]);
    }

    /// <summary>Remove should return false for non-existing item.</summary>
    [Test]
    public void Remove_NonExistingItem_ShouldReturnFalse()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        var result = wrapper.Remove(ThirdItem);

        result.Should().BeFalse();
        list.Count.Should().Be(SecondOrdinal);
    }

    /// <summary>Remove should remove from both list and observable collection.</summary>
    [Test]
    public void Remove_WithObservable_ShouldRemoveFromBoth()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Remove("two");

        list.Should().BeEquivalentTo(["one", ThirdItem]);
        observable.Should().BeEquivalentTo(["one", ThirdItem]);
    }

    /// <summary>RemoveAt should remove item at index without observable.</summary>
    [Test]
    public void RemoveAt_WithoutObservable_ShouldRemoveAtIndex()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.RemoveAt(1);

        list.Should().BeEquivalentTo(["one", ThirdItem]);
    }

    /// <summary>RemoveAt should remove from both list and observable collection.</summary>
    [Test]
    public void RemoveAt_WithObservable_ShouldRemoveFromBoth()
    {
        var list = new List<string> { "one", "two", ThirdItem };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.RemoveAt(1);

        list.Should().BeEquivalentTo(["one", ThirdItem]);
        observable.Should().BeEquivalentTo(["one", ThirdItem]);
    }

    /// <summary>Non-generic GetEnumerator should enumerate items.</summary>
    [Test]
    public void NonGenericGetEnumerator_ShouldEnumerateItems()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        var items = new List<object?>();
        foreach (var item in ((System.Collections.IEnumerable)wrapper))
        {
            items.Add(item);
        }

        items.Should().BeEquivalentTo(["one", "two"]);
    }
}
