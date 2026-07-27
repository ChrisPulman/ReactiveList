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
        _ = fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
    }

    /// <summary>Determines whether this instance [can add complex array item].</summary>
    [Test]
    public void CanAddComplexArrayItem()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add multiple single complex items].</summary>
    [Test]
    public void CanAddMultipleSingleComplexItems()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.Add(new(TestData.CelineName, TestData.TestValueFive));
        _ = fixture.Count.Should().Be(1);
        fixture.Add(new(TestData.ClarenceName, TestData.TestValueFive));
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.Add(new(TestData.CliffordName, TestData.TestValueFive));
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add multiple single complex items and edit].</summary>
    [Test]
    public void CanAddMultipleSingleComplexItemsAndEdit()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.Add(TestData.CelineName);
        _ = fixture.Count.Should().Be(1);
        fixture.Add(TestData.ClarenceName);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.Add("Cliffordddd");
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.Update(fixture.Items[TestData.TestValueTwo], TestData.CliffordName);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add multiple single items].</summary>
    [Test]
    public void CanAddMultipleSingleItems()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.Add("one");
        _ = fixture.Count.Should().Be(1);
        fixture.Add("two");
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.Add(TestData.ThreeText);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add single complex item].</summary>
    [Test]
    public void CanAddSingleComplexItem()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.Add(new("Chris", TestData.TestValueFortyFour));
        _ = fixture.Count.Should().Be(1);
    }

    /// <summary>Determines whether this instance [can add single item].</summary>
    [Test]
    public void CanAddSingleItem()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.Add("one");
        _ = fixture.Count.Should().Be(1);
    }

    /// <summary>Determines whether this instance [can clear and add item].</summary>
    [Test]
    public void CanClearAndAddItem()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        _ = fixture.Items[0].Should().Be("one");
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        _ = fixture.ItemsAdded.Count.Should().Be(0);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueTwo);
        fixture.Add(TestData.ThreeText);
        _ = fixture.Count.Should().Be(1);
        _ = fixture.ItemsAdded.Count.Should().Be(1);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        _ = fixture.Items[0].Should().Be(TestData.ThreeText);
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        _ = fixture.ItemsAdded.Count.Should().Be(0);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(1);
    }

    /// <summary>Determines whether this instance [can observe add array of item asynchronous].</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CanObserveAddArrayOfItemAsync()
    {
        ReactiveList<string> fixture = [];
        var observedCount = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = fixture.Added.Subscribe(items =>
        {
            var count = 0;
            foreach (var _ in items)
            {
                count++;
            }

            _ = observedCount.TrySetResult(count);
        });
        _ = fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        await TUnit.Assertions.Assert.That(await observedCount.Task).IsEqualTo(TestData.TestValueTwo);
    }

    /// <summary>Determines whether this instance [can observe add single item asynchronous].</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CanObserveAddSingleItemAsync()
    {
        ReactiveList<string> fixture = [];
        var observedCount = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = fixture.Added.Subscribe(items =>
        {
            var count = 0;
            foreach (var _ in items)
            {
                count++;
            }

            _ = observedCount.TrySetResult(count);
        });
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.Add("one");
        _ = fixture.Count.Should().Be(1);
        await TUnit.Assertions.Assert.That(await observedCount.Task).IsEqualTo(1);
    }

    /// <summary>Determines whether this instance [can replace all items].</summary>
    [Test]
    public void CanReplaceAllItems()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        _ = fixture.Items[0].Should().Be("one");
        fixture.ReplaceAll([TestData.ThreeText, "four", "five"]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.Items[0].Should().Be(TestData.ThreeText);
    }

    /// <summary>Determines whether this instance [can replace all items many times].</summary>
    [Test]
    public void CanReplaceAllItemsManyTimes()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        _ = fixture.Items[0].Should().Be("one");
        fixture.ReplaceAll([TestData.ThreeText, "four", "five"]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.Items[0].Should().Be(TestData.ThreeText);
        fixture.ReplaceAll(["six", "seven", "eight"]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.Items[0].Should().Be("six");
    }

    /// <summary>Determines whether this instance [can replace all items with complex items].</summary>
    [Test]
    public void CanReplaceAllItemsWithComplexItems()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        _ = fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.Items[0].Name.Should().Be(TestData.CelineName);
    }

    /// <summary>Determines whether this instance [can replace all items with complex items and edit].</summary>
    [Test]
    public void CanReplaceAllItemsWithComplexItemsAndEdit()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        _ = fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.Update(fixture.Items[TestData.TestValueTwo], new(TestData.CliffordName, TestData.TestValueFive));
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.Items[TestData.TestValueTwo].Name.Should().Be(TestData.CliffordName);
    }

    /// <summary>Determines whether this instance [can replace all items with complex items and edit and remove].</summary>
    [Test]
    public void CanReplaceAllItemsWithComplexItemsAndEditAndRemove()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        _ = fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.Update(fixture.Items[TestData.TestValueTwo], new(TestData.CliffordName, TestData.TestValueFive));
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.Items[TestData.TestValueTwo].Name.Should().Be(TestData.CliffordName);
        _ = fixture.Remove(fixture.Items[TestData.TestValueTwo]);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsAdded.Count.Should().Be(0);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(1);
    }

    /// <summary>Determines whether this instance [can replace all items with complex items and edit and remove and add].</summary>
    [Test]
    public void CanReplaceAllItemsWithComplexItemsAndEditAndRemoveAndAdd()
    {
        ReactiveList<TestData> fixture = [];
        var inpcName = string.Empty;
        fixture.PropertyChanged += (sender, args) => inpcName += args.PropertyName;
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        _ = inpcName.Should().Be("CountItem[]");
        inpcName = string.Empty;
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = inpcName.Should().Be("CountItem[]");
        inpcName = string.Empty;
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        _ = fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.Update(fixture.Items[TestData.TestValueTwo], new(TestData.CliffordName, TestData.TestValueFive));
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.Items[TestData.TestValueTwo].Name.Should().Be(TestData.CliffordName);
        _ = fixture.Remove(fixture.Items[TestData.TestValueTwo]);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsAdded.Count.Should().Be(0);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(1);
        fixture.Add(new(TestData.CliffordName, TestData.TestValueFive));
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(1);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
    }

    /// <summary>Determines whether this instance [can replace all items with complex items and edit and remove and add and clear].</summary>
    [Test]
    public void CanReplaceAllItemsWithComplexItemsAndEditAndRemoveAndAddAndClear()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        _ = fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.Items[0].Name.Should().Be(TestData.CelineName);
        fixture.Update(fixture.Items[TestData.TestValueTwo], new(TestData.CliffordName, TestData.TestValueFive));
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.Items[TestData.TestValueTwo].Name.Should().Be(TestData.CliffordName);
        _ = fixture.Remove(fixture.Items[TestData.TestValueTwo]);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsAdded.Count.Should().Be(0);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(1);
        fixture.Add(new(TestData.CliffordName, TestData.TestValueFive));
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(1);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        _ = fixture.ItemsAdded.Count.Should().Be(0);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsRemoved.Count.Should().Be(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add items and insert items].</summary>
    [Test]
    public void CanAddItemsAndInsertItems()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        _ = fixture.Items[0].Should().Be("one");
        fixture.Insert(1, TestData.ThreeText);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(1);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        _ = fixture.Items[1].Should().Be(TestData.ThreeText);
    }

    /// <summary>Determines whether this instance [can add items and insert items and remove at index].</summary>
    [Test]
    public void CanAddItemsAndInsertItemsAndRemoveAtIndex()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        _ = fixture.Count.Should().Be(0);
        fixture.AddRange(["one", "two"]);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsAdded.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        _ = fixture.Items[0].Should().Be("one");
        fixture.Insert(1, TestData.ThreeText);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.ItemsAdded.Count.Should().Be(1);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(0);
        _ = fixture.Items[1].Should().Be(TestData.ThreeText);
        fixture.RemoveAt(1);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.ItemsAdded.Count.Should().Be(0);
        _ = fixture.ItemsChanged.Count.Should().Be(1);
        _ = fixture.ItemsRemoved.Count.Should().Be(1);
    }

    /// <summary>Determines whether this instance can enumerate.</summary>
    [Test]
    public void CanEnumerate()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.AddRange(["one", "two"]);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        foreach (var item in fixture)
        {
            _ = item.Should().NotBeNullOrEmpty();
        }
    }

    /// <summary>Determines whether this instance [can get an element at the index or return default].</summary>
    [Test]
    public void CanGetElementAtOrDefault()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.AddRange(["one", "two"]);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        Assert.Equal("one", fixture.ElementAtOrDefault(0));
        Assert.Equal("two", fixture.ElementAtOrDefault(1));
        Assert.Equal(null, fixture.ElementAtOrDefault(TestData.TestValueTwo));
    }

    /// <summary>Determines whether this instance [can add items to a list then add to fixture].</summary>
    [Test]
    public void CanAddItemsToAListThenAddToFixture()
    {
        List<string> fixture = [];
        fixture.Clear();
        fixture.AddRange(["one", "two"]);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture[0].Should().Be("one");
        _ = fixture[1].Should().Be("two");
        ReactiveList<string> fixture2 = [];
        fixture2.AddRange(fixture);
        _ = fixture2.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture2.ItemsAdded.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture2.ItemsChanged.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture2.ItemsRemoved.Count.Should().Be(0);
        _ = fixture2.Items[0].Should().Be("one");
        _ = fixture2.Items[1].Should().Be("two");
    }
}
