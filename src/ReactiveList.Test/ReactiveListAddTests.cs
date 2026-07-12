// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CP.Primitives.Collections;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>ReactiveList Add Tests.</summary>
public class ReactiveListAddTests
{
    /// <summary>Determines whether this instance [can add array item].</summary>
    [Test]
    public void CanAddArrayItem()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(TestData.TestValueTwo);
    }

    /// <summary>Determines whether this instance [can add complex array item].</summary>
    [Test]
    public void CanAddComplexArrayItem()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        fixture.Count.Should().Be(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add multiple single complex items].</summary>
    [Test]
    public void CanAddMultipleSingleComplexItems()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add(new TestData(TestData.CelineName, TestData.TestValueFive));
        fixture.Count.Should().Be(1);
        fixture.Add(new TestData(TestData.ClarenceName, TestData.TestValueFive));
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.Add(new TestData(TestData.CliffordName, TestData.TestValueFive));
        fixture.Count.Should().Be(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add multiple single complex items and edit].</summary>
    [Test]
    public void CanAddMultipleSingleComplexItemsAndEdit()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add(TestData.CelineName);
        fixture.Count.Should().Be(1);
        fixture.Add(TestData.ClarenceName);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.Add("Cliffordddd");
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.Update(fixture.Items[TestData.TestValueTwo], TestData.CliffordName);
        fixture.Count.Should().Be(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add multiple single items].</summary>
    [Test]
    public void CanAddMultipleSingleItems()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add("one");
        fixture.Count.Should().Be(1);
        fixture.Add("two");
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.Add(TestData.ThreeText);
        fixture.Count.Should().Be(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add single complex item].</summary>
    [Test]
    public void CanAddSingleComplexItem()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add(new TestData("Chris", TestData.TestValueFortyFour));
        fixture.Count.Should().Be(1);
    }

    /// <summary>Determines whether this instance [can add single item].</summary>
    [Test]
    public void CanAddSingleItem()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add("one");
        fixture.Count.Should().Be(1);
    }

    /// <summary>Determines whether this instance [can clear and add item].</summary>
    [Test]
    public void CanClearAndAddItem()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be("one");
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueTwo);
        fixture.Add(TestData.ThreeText);
        fixture.Count.Should().Be(1);
        fixture.ItemsAdded.Count.Should().Be(1);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be(TestData.ThreeText);
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(1);
    }

    /// <summary>Determines whether this instance [can observe add array of item asynchronous].</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CanObserveAddArrayOfItemAsync()
    {
        ReactiveList<string> fixture = [];
        var a = false;
        fixture.Added.Subscribe(items =>
        {
            items.Count().Should().Be(TestData.TestValueTwo);
            a = true;
        });
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        while (!a)
        {
            await Task.Delay(1);
        }
    }

    /// <summary>Determines whether this instance [can observe add single item asynchronous].</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
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

    /// <summary>Determines whether this instance [can replace all items].</summary>
    [Test]
    public void CanReplaceAllItems()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be("one");
        fixture.ReplaceAll([TestData.ThreeText, "four", "five"]);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueTwo);
        fixture.Items[0].Should().Be(TestData.ThreeText);
    }

    /// <summary>Determines whether this instance [can replace all items many times].</summary>
    [Test]
    public void CanReplaceAllItemsManyTimes()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be("one");
        fixture.ReplaceAll([TestData.ThreeText, "four", "five"]);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueTwo);
        fixture.Items[0].Should().Be(TestData.ThreeText);
        fixture.ReplaceAll(["six", "seven", "eight"]);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        fixture.Items[0].Should().Be("six");
    }

    /// <summary>Determines whether this instance [can replace all items with complex items].</summary>
    [Test]
    public void CanReplaceAllItemsWithComplexItems()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        fixture.Items[0].Name.Should().Be(TestData.CelineName);
    }

    /// <summary>Determines whether this instance [can replace all items with complex items and edit].</summary>
    [Test]
    public void CanReplaceAllItemsWithComplexItemsAndEdit()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.Update(fixture.Items[TestData.TestValueTwo], new TestData(TestData.CliffordName, TestData.TestValueFive));
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        fixture.Items[TestData.TestValueTwo].Name.Should().Be(TestData.CliffordName);
    }

    /// <summary>Determines whether this instance [can replace all items with complex items and edit and remove].</summary>
    [Test]
    public void CanReplaceAllItemsWithComplexItemsAndEditAndRemove()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.Update(fixture.Items[TestData.TestValueTwo], new TestData(TestData.CliffordName, TestData.TestValueFive));
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        fixture.Items[TestData.TestValueTwo].Name.Should().Be(TestData.CliffordName);
        fixture.Remove(fixture.Items[TestData.TestValueTwo]);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(1);
    }

    /// <summary>Determines whether this instance [can replace all items with complex items and edit and remove and add].</summary>
    [Test]
    public void CanReplaceAllItemsWithComplexItemsAndEditAndRemoveAndAdd()
    {
        ReactiveList<TestData> fixture = [];
        var inpcName = string.Empty;
        fixture.PropertyChanged += (sender, args) => inpcName += args.PropertyName;
        fixture.Clear();
        fixture.Count.Should().Be(0);
        inpcName.Should().Be("CountItem[]");
        inpcName = string.Empty;
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        fixture.Count.Should().Be(TestData.TestValueThree);
        inpcName.Should().Be("CountItem[]");
        inpcName = string.Empty;
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.Update(fixture.Items[TestData.TestValueTwo], new TestData(TestData.CliffordName, TestData.TestValueFive));
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        fixture.Items[TestData.TestValueTwo].Name.Should().Be(TestData.CliffordName);
        fixture.Remove(fixture.Items[TestData.TestValueTwo]);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(1);
        fixture.Add(new TestData(TestData.CliffordName, TestData.TestValueFive));
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(1);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(0);
    }

    /// <summary>Determines whether this instance [can replace all items with complex items and edit and remove and add and clear].</summary>
    [Test]
    public void CanReplaceAllItemsWithComplexItemsAndEditAndRemoveAndAddAndClear()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.Update(fixture.Items[TestData.TestValueTwo], new TestData(TestData.CliffordName, TestData.TestValueFive));
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        fixture.Items[TestData.TestValueTwo].Name.Should().Be(TestData.CliffordName);
        fixture.Remove(fixture.Items[TestData.TestValueTwo]);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(1);
        fixture.Add(new TestData(TestData.CliffordName, TestData.TestValueFive));
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(1);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add items and insert items].</summary>
    [Test]
    public void CanAddItemsAndInsertItems()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be("one");
        fixture.Insert(1, TestData.ThreeText);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(1);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[1].Should().Be(TestData.ThreeText);
    }

    /// <summary>Determines whether this instance [can add items and insert items and remove at index].</summary>
    [Test]
    public void CanAddItemsAndInsertItemsAndRemoveAtIndex()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsAdded.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be("one");
        fixture.Insert(1, TestData.ThreeText);
        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.ItemsAdded.Count.Should().Be(1);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[1].Should().Be(TestData.ThreeText);
        fixture.RemoveAt(1);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(1);
    }

    /// <summary>Determines whether this instance can enumerate.</summary>
    [Test]
    public void CanEnumerate()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        foreach (var item in fixture)
        {
            item.Should().NotBeNullOrEmpty();
        }
    }

    /// <summary>Determines whether this instance [can get an element at the index or return default].</summary>
    [Test]
    public void CanGetElementAtOrDefault()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        Assert.Equal("one", fixture.ElementAtOrDefault(0));
        Assert.Equal("two", fixture.ElementAtOrDefault(1));
        Assert.Equal<string?>(null, fixture.ElementAtOrDefault(TestData.TestValueTwo));
    }

    /// <summary>Determines whether this instance [can add items to a list then add to fixture].</summary>
    [Test]
    public void CanAddItemsToAListThenAddToFixture()
    {
        List<string> fixture = [];
        fixture.Clear();
        fixture.AddRange(["one", "two"]);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("two");
        ReactiveList<string> fixture2 = [];
        fixture2.AddRange(fixture);
        fixture2.Count.Should().Be(TestData.TestValueTwo);
        fixture2.ItemsAdded.Count.Should().Be(TestData.TestValueTwo);
        fixture2.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        fixture2.ItemsRemoved.Count.Should().Be(0);
        fixture2.Items[0].Should().Be("one");
        fixture2.Items[1].Should().Be("two");
    }
}
