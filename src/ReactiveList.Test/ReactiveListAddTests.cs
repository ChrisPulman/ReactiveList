// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using CP.Reactive;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// ReactiveList Add Tests.
/// </summary>
public class ReactiveListAddTests
{
    /// <summary>
    /// Determines whether this instance [can add array item].
    /// </summary>
    [Fact]
    public void CanAddArrayItem()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(2);
    }

    /// <summary>
    /// Determines whether this instance [can add complex array item].
    /// </summary>
    [Fact]
    public void CanAddComplexArrayItem()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange([new("Celine", 5), new("Clarence", 5), new("Clifford", 5)]);
        fixture.Count.Should().Be(3);
    }

    /// <summary>
    /// Determines whether this instance [can add multiple single complex items].
    /// </summary>
    [Fact]
    public void CanAddMultipleSingleComplexItems()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add(new TestData("Celine", 5));
        fixture.Count.Should().Be(1);
        fixture.Add(new TestData("Clarence", 5));
        fixture.Count.Should().Be(2);
        fixture.Add(new TestData("Clifford", 5));
        fixture.Count.Should().Be(3);
    }

    /// <summary>
    /// Determines whether this instance [can add multiple single complex items and edit].
    /// </summary>
    [Fact]
    public void CanAddMultipleSingleComplexItemsAndEdit()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add("Celine");
        fixture.Count.Should().Be(1);
        fixture.Add("Clarence");
        fixture.Count.Should().Be(2);
        fixture.Add("Cliffordddd");
        fixture.Count.Should().Be(3);
        fixture.Update(fixture.Items[2], "Clifford");
        fixture.Count.Should().Be(3);
    }

    /// <summary>
    /// Determines whether this instance [can add multiple single items].
    /// </summary>
    [Fact]
    public void CanAddMultipleSingleItems()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add("one");
        fixture.Count.Should().Be(1);
        fixture.Add("two");
        fixture.Count.Should().Be(2);
        fixture.Add("three");
        fixture.Count.Should().Be(3);
    }

    /// <summary>
    /// Determines whether this instance [can add single complex item].
    /// </summary>
    [Fact]
    public void CanAddSingleComplexItem()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add(new TestData("Chris", 44));
        fixture.Count.Should().Be(1);
    }

    /// <summary>
    /// Determines whether this instance [can add single item].
    /// </summary>
    [Fact]
    public void CanAddSingleItem()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add("one");
        fixture.Count.Should().Be(1);
    }

    /// <summary>
    /// Determines whether this instance [can clear and add item].
    /// </summary>
    [Fact]
    public void CanClearAndAddItem()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(2);
        fixture.ItemsAdded.Count.Should().Be(2);
        fixture.ItemsChanged.Count.Should().Be(2);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be("one");
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(2);
        fixture.ItemsRemoved.Count.Should().Be(2);
        fixture.Add("three");
        fixture.Count.Should().Be(1);
        fixture.ItemsAdded.Count.Should().Be(1);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be("three");
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(1);
    }

    /// <summary>
    /// Determines whether this instance [can observe add array of item asynchronous].
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CanObserveAddArrayOfItemAsync()
    {
        ReactiveList<string> fixture = [];
        var a = false;
        fixture.Added.Subscribe(items =>
        {
            items.Count().Should().Be(2);
            a = true;
        });
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(2);
        while (!a)
        {
            await Task.Delay(1);
        }
    }

    /// <summary>
    /// Determines whether this instance [can observe add single item asynchronous].
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CanObserveAddSingleItemAsync()
    {
        ReactiveList<string> fixture = [];
        var a = false;
        fixture.Added.Subscribe(items =>
        {
            items.Count().Should().Be(1);
            a = true;
        });
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add("one");
        fixture.Count.Should().Be(1);
        while (!a)
        {
            await Task.Delay(1);
        }
    }

    /// <summary>
    /// Determines whether this instance [can replace all items].
    /// </summary>
    [Fact]
    public void CanReplaceAllItems()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(2);
        fixture.ItemsAdded.Count.Should().Be(2);
        fixture.ItemsChanged.Count.Should().Be(2);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be("one");
        fixture.ReplaceAll(["three", "four", "five"]);
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(2);
        fixture.ItemsRemoved.Count.Should().Be(2);
        fixture.Items[0].Should().Be("three");
    }

    /// <summary>
    /// Determines whether this instance [can replace all items many times].
    /// </summary>
    [Fact]
    public void CanReplaceAllItemsManyTimes()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(2);
        fixture.ItemsAdded.Count.Should().Be(2);
        fixture.ItemsChanged.Count.Should().Be(2);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be("one");
        fixture.ReplaceAll(["three", "four", "five"]);
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(2);
        fixture.ItemsRemoved.Count.Should().Be(2);
        fixture.Items[0].Should().Be("three");
        fixture.ReplaceAll(["six", "seven", "eight"]);
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(3);
        fixture.ItemsRemoved.Count.Should().Be(3);
        fixture.Items[0].Should().Be("six");
    }

    /// <summary>
    /// Determines whether this instance [can replace all items with complex items].
    /// </summary>
    [Fact]
    public void CanReplaceAllItemsWithComplexItems()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange([new("Celine", 5), new("Clarence", 5), new("Clifford", 5)]);
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(3);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Name.Should().Be("Celine");
        fixture.ReplaceAll([new("Celine", 5), new("Clarence", 5), new("Clifford", 5)]);
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(3);
        fixture.ItemsRemoved.Count.Should().Be(3);
        fixture.Items[0].Name.Should().Be("Celine");
    }

    /// <summary>
    /// Determines whether this instance [can replace all items with complex items and edit].
    /// </summary>
    [Fact]
    public void CanReplaceAllItemsWithComplexItemsAndEdit()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange([new("Celine", 5), new("Clarence", 5), new("Clifford", 5)]);
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(3);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Name.Should().Be("Celine");
        fixture.ReplaceAll([new("Celine", 5), new("Clarence", 5), new("Clifford", 5)]);
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(3);
        fixture.ItemsRemoved.Count.Should().Be(3);
        fixture.Items[0].Name.Should().Be("Celine");
        fixture.Update(fixture.Items[2], new TestData("Clifford", 5));
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(3);
        fixture.Items[2].Name.Should().Be("Clifford");
    }

    /// <summary>
    /// Determines whether this instance [can replace all items with complex items and edit and remove].
    /// </summary>
    [Fact]
    public void CanReplaceAllItemsWithComplexItemsAndEditAndRemove()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange([new("Celine", 5), new("Clarence", 5), new("Clifford", 5)]);
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(3);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Name.Should().Be("Celine");
        fixture.ReplaceAll([new("Celine", 5), new("Clarence", 5), new("Clifford", 5)]);
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(3);
        fixture.ItemsRemoved.Count.Should().Be(3);
        fixture.Items[0].Name.Should().Be("Celine");
        fixture.Update(fixture.Items[2], new TestData("Clifford", 5));
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(3);
        fixture.Items[2].Name.Should().Be("Clifford");
        fixture.Remove(fixture.Items[2]);
        fixture.Count.Should().Be(2);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(1);
    }

    /// <summary>
    /// Determines whether this instance [can replace all items with complex items and edit and remove and add].
    /// </summary>
    [Fact]
    public void CanReplaceAllItemsWithComplexItemsAndEditAndRemoveAndAdd()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange([new("Celine", 5), new("Clarence", 5), new("Clifford", 5)]);
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(3);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Name.Should().Be("Celine");
        fixture.ReplaceAll([new("Celine", 5), new("Clarence", 5), new("Clifford", 5)]);
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(3);
        fixture.ItemsRemoved.Count.Should().Be(3);
        fixture.Items[0].Name.Should().Be("Celine");
        fixture.Update(fixture.Items[2], new TestData("Clifford", 5));
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(3);
        fixture.Items[2].Name.Should().Be("Clifford");
        fixture.Remove(fixture.Items[2]);
        fixture.Count.Should().Be(2);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(1);
        fixture.Add(new TestData("Clifford", 5));
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(1);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(0);
    }

    /// <summary>
    /// Determines whether this instance [can replace all items with complex items and edit and remove and add and clear].
    /// </summary>
    [Fact]
    public void CanReplaceAllItemsWithComplexItemsAndEditAndRemoveAndAddAndClear()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange([new("Celine", 5), new("Clarence", 5), new("Clifford", 5)]);
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(3);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Name.Should().Be("Celine");
        fixture.ReplaceAll([new("Celine", 5), new("Clarence", 5), new("Clifford", 5)]);
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(3);
        fixture.ItemsRemoved.Count.Should().Be(3);
        fixture.Items[0].Name.Should().Be("Celine");
        fixture.Update(fixture.Items[2], new TestData("Clifford", 5));
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(3);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(3);
        fixture.Items[2].Name.Should().Be("Clifford");
        fixture.Remove(fixture.Items[2]);
        fixture.Count.Should().Be(2);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(1);
        fixture.Add(new TestData("Clifford", 5));
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(1);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(3);
        fixture.ItemsRemoved.Count.Should().Be(3);
    }

    /// <summary>
    /// Determines whether this instance [can add items and insert items].
    /// </summary>
    [Fact]
    public void CanAddItemsAndInsertItems()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(2);
        fixture.ItemsAdded.Count.Should().Be(2);
        fixture.ItemsChanged.Count.Should().Be(2);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be("one");
        fixture.Insert(1, "three");
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(1);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[1].Should().Be("three");
    }

    /// <summary>
    /// Determines whether this instance [can add items and insert items and remove at index].
    /// </summary>
    [Fact]
    public void CanAddItemsAndInsertItemsAndRemoveAtIndex()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(2);
        fixture.ItemsAdded.Count.Should().Be(2);
        fixture.ItemsChanged.Count.Should().Be(2);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be("one");
        fixture.Insert(1, "three");
        fixture.Count.Should().Be(3);
        fixture.ItemsAdded.Count.Should().Be(1);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[1].Should().Be("three");
        fixture.RemoveAt(1);
        fixture.Count.Should().Be(2);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(1);
    }
}
