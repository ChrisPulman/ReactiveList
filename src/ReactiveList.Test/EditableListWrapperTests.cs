// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CP.Reactive;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Tests for EditableListWrapper.
/// </summary>
public class EditableListWrapperTests
{
    /// <summary>
    /// Constructor should initialize with list only.
    /// </summary>
    [Fact]
    public void Constructor_WithListOnly_ShouldInitialize()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Count.Should().Be(2);
        wrapper[0].Should().Be("one");
        wrapper[1].Should().Be("two");
    }

    /// <summary>
    /// Constructor should initialize with list and observable collection.
    /// </summary>
    [Fact]
    public void Constructor_WithListAndObservableCollection_ShouldInitialize()
    {
        var list = new List<string> { "one", "two" };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Count.Should().Be(2);
        observable.Count.Should().Be(2);
    }

    /// <summary>
    /// IsReadOnly should return false.
    /// </summary>
    [Fact]
    public void IsReadOnly_ShouldReturnFalse()
    {
        var wrapper = new EditableListWrapper<string>([]);
        wrapper.IsReadOnly.Should().BeFalse();
    }

    /// <summary>
    /// Indexer get should return correct item.
    /// </summary>
    [Fact]
    public void Indexer_Get_ShouldReturnCorrectItem()
    {
        var list = new List<string> { "one", "two", "three" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper[1].Should().Be("two");
    }

    /// <summary>
    /// Indexer set should update list only when no observable collection.
    /// </summary>
    [Fact]
    public void Indexer_Set_WithoutObservable_ShouldUpdateList()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper[0] = "updated";

        list[0].Should().Be("updated");
    }

    /// <summary>
    /// Indexer set should update both list and observable collection.
    /// </summary>
    [Fact]
    public void Indexer_Set_WithObservable_ShouldUpdateBoth()
    {
        var list = new List<string> { "one", "two" };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper[0] = "updated";

        list[0].Should().Be("updated");
        observable[0].Should().Be("updated");
    }

    /// <summary>
    /// Add should add to list only when no observable collection.
    /// </summary>
    [Fact]
    public void Add_WithoutObservable_ShouldAddToList()
    {
        var list = new List<string>();
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Add("item");

        list.Should().Contain("item");
    }

    /// <summary>
    /// Add should add to both list and observable collection.
    /// </summary>
    [Fact]
    public void Add_WithObservable_ShouldAddToBoth()
    {
        var list = new List<string>();
        var observable = new ObservableCollection<string>();
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Add("item");

        list.Should().Contain("item");
        observable.Should().Contain("item");
    }

    /// <summary>
    /// AddRange should add array items to list.
    /// </summary>
    [Fact]
    public void AddRange_WithArray_ShouldAddItems()
    {
        var list = new List<string>();
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.AddRange(new[] { "one", "two", "three" });

        list.Should().BeEquivalentTo(["one", "two", "three"]);
    }

    /// <summary>
    /// AddRange should add items to both list and observable collection.
    /// </summary>
    [Fact]
    public void AddRange_WithObservable_ShouldAddToBoth()
    {
        var list = new List<string>();
        var observable = new ObservableCollection<string>();
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.AddRange(new[] { "one", "two" });

        list.Should().BeEquivalentTo(["one", "two"]);
        observable.Should().BeEquivalentTo(["one", "two"]);
    }

    /// <summary>
    /// AddRange should handle enumerable that is not array.
    /// </summary>
    [Fact]
    public void AddRange_WithEnumerable_ShouldAddItems()
    {
        var list = new List<string>();
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.AddRange(Enumerable.Range(1, 3).Select(i => $"item{i}"));

        list.Should().BeEquivalentTo(["item1", "item2", "item3"]);
    }

    /// <summary>
    /// Clear should clear list only when no observable collection.
    /// </summary>
    [Fact]
    public void Clear_WithoutObservable_ShouldClearList()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Clear();

        list.Should().BeEmpty();
    }

    /// <summary>
    /// Clear should clear both list and observable collection.
    /// </summary>
    [Fact]
    public void Clear_WithObservable_ShouldClearBoth()
    {
        var list = new List<string> { "one", "two" };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Clear();

        list.Should().BeEmpty();
        observable.Should().BeEmpty();
    }

    /// <summary>
    /// Contains should return true for existing item.
    /// </summary>
    [Fact]
    public void Contains_WithExistingItem_ShouldReturnTrue()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Contains("one").Should().BeTrue();
    }

    /// <summary>
    /// Contains should return false for non-existing item.
    /// </summary>
    [Fact]
    public void Contains_WithNonExistingItem_ShouldReturnFalse()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Contains("three").Should().BeFalse();
    }

    /// <summary>
    /// CopyTo should copy items to array.
    /// </summary>
    [Fact]
    public void CopyTo_ShouldCopyItemsToArray()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);
        var array = new string[3];

        wrapper.CopyTo(array, 1);

        array[0].Should().BeNull();
        array[1].Should().Be("one");
        array[2].Should().Be("two");
    }

    /// <summary>
    /// GetEnumerator should enumerate items.
    /// </summary>
    [Fact]
    public void GetEnumerator_ShouldEnumerateItems()
    {
        var list = new List<string> { "one", "two", "three" };
        var wrapper = new EditableListWrapper<string>(list);

        var items = wrapper.ToList();

        items.Should().BeEquivalentTo(["one", "two", "three"]);
    }

    /// <summary>
    /// IndexOf should return correct index.
    /// </summary>
    [Fact]
    public void IndexOf_ShouldReturnCorrectIndex()
    {
        var list = new List<string> { "one", "two", "three" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.IndexOf("two").Should().Be(1);
    }

    /// <summary>
    /// IndexOf should return -1 for non-existing item.
    /// </summary>
    [Fact]
    public void IndexOf_WithNonExistingItem_ShouldReturnNegativeOne()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.IndexOf("three").Should().Be(-1);
    }

    /// <summary>
    /// Insert should insert at correct position without observable.
    /// </summary>
    [Fact]
    public void Insert_WithoutObservable_ShouldInsertAtPosition()
    {
        var list = new List<string> { "one", "three" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Insert(1, "two");

        list.Should().BeEquivalentTo(["one", "two", "three"], options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Insert should insert at correct position with observable.
    /// </summary>
    [Fact]
    public void Insert_WithObservable_ShouldInsertInBoth()
    {
        var list = new List<string> { "one", "three" };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Insert(1, "two");

        list.Should().BeEquivalentTo(["one", "two", "three"], options => options.WithStrictOrdering());
        observable.Should().BeEquivalentTo(["one", "two", "three"], options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Move should move item to new position.
    /// </summary>
    [Fact]
    public void Move_ShouldMoveItemToNewPosition()
    {
        var list = new List<string> { "one", "two", "three" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Move(0, 2);

        list.Should().BeEquivalentTo(["two", "three", "one"], options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Move should move item in both list and observable collection.
    /// </summary>
    [Fact]
    public void Move_WithObservable_ShouldMoveInBoth()
    {
        var list = new List<string> { "one", "two", "three" };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Move(0, 2);

        list.Should().BeEquivalentTo(["two", "three", "one"], options => options.WithStrictOrdering());
        observable.Should().BeEquivalentTo(["two", "three", "one"], options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Move should do nothing when old and new index are same.
    /// </summary>
    [Fact]
    public void Move_WhenSameIndex_ShouldDoNothing()
    {
        var list = new List<string> { "one", "two", "three" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.Move(1, 1);

        list.Should().BeEquivalentTo(["one", "two", "three"], options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Move should throw when old index is out of range.
    /// </summary>
    [Fact]
    public void Move_WhenOldIndexOutOfRange_ShouldThrow()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        var act = () => wrapper.Move(-1, 0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("oldIndex");
    }

    /// <summary>
    /// Move should throw when new index is out of range.
    /// </summary>
    [Fact]
    public void Move_WhenNewIndexOutOfRange_ShouldThrow()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        var act = () => wrapper.Move(0, 5);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("newIndex");
    }

    /// <summary>
    /// Remove should remove existing item and return true.
    /// </summary>
    [Fact]
    public void Remove_ExistingItem_ShouldRemoveAndReturnTrue()
    {
        var list = new List<string> { "one", "two", "three" };
        var wrapper = new EditableListWrapper<string>(list);

        var result = wrapper.Remove("two");

        result.Should().BeTrue();
        list.Should().BeEquivalentTo(["one", "three"]);
    }

    /// <summary>
    /// Remove should return false for non-existing item.
    /// </summary>
    [Fact]
    public void Remove_NonExistingItem_ShouldReturnFalse()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        var result = wrapper.Remove("three");

        result.Should().BeFalse();
        list.Count.Should().Be(2);
    }

    /// <summary>
    /// Remove should remove from both list and observable collection.
    /// </summary>
    [Fact]
    public void Remove_WithObservable_ShouldRemoveFromBoth()
    {
        var list = new List<string> { "one", "two", "three" };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.Remove("two");

        list.Should().BeEquivalentTo(["one", "three"]);
        observable.Should().BeEquivalentTo(["one", "three"]);
    }

    /// <summary>
    /// RemoveAt should remove item at index without observable.
    /// </summary>
    [Fact]
    public void RemoveAt_WithoutObservable_ShouldRemoveAtIndex()
    {
        var list = new List<string> { "one", "two", "three" };
        var wrapper = new EditableListWrapper<string>(list);

        wrapper.RemoveAt(1);

        list.Should().BeEquivalentTo(["one", "three"]);
    }

    /// <summary>
    /// RemoveAt should remove from both list and observable collection.
    /// </summary>
    [Fact]
    public void RemoveAt_WithObservable_ShouldRemoveFromBoth()
    {
        var list = new List<string> { "one", "two", "three" };
        var observable = new ObservableCollection<string>(list);
        var wrapper = new EditableListWrapper<string>(list, observable);

        wrapper.RemoveAt(1);

        list.Should().BeEquivalentTo(["one", "three"]);
        observable.Should().BeEquivalentTo(["one", "three"]);
    }

    /// <summary>
    /// Non-generic GetEnumerator should enumerate items.
    /// </summary>
    [Fact]
    public void NonGenericGetEnumerator_ShouldEnumerateItems()
    {
        var list = new List<string> { "one", "two" };
        var wrapper = new EditableListWrapper<string>(list);

        var items = new List<object?>();
        var enumerator = ((System.Collections.IEnumerable)wrapper).GetEnumerator();
        while (enumerator.MoveNext())
        {
            items.Add(enumerator.Current);
        }

        items.Should().BeEquivalentTo(new object[] { "one", "two" });
    }
}
