// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Collections;
#else
using CP.Primitives.Collections;
#endif
using TUnit.Assertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>ReactiveList Add Tests.</summary>
public class ReactiveListAddTests
{
    /// <summary>Determines whether this instance [can add array item].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanAddArrayItem()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.AddRange(["one", "two"]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
    }

    /// <summary>Determines whether this instance [can add complex array item].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanAddComplexArrayItem()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add multiple single complex items].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanAddMultipleSingleComplexItems()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.Add(new(TestData.CelineName, TestData.TestValueFive));
        await Assert.That(fixture.Count).IsEqualTo(1);
        fixture.Add(new(TestData.ClarenceName, TestData.TestValueFive));
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        fixture.Add(new(TestData.CliffordName, TestData.TestValueFive));
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add multiple single complex items and edit].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanAddMultipleSingleComplexItemsAndEdit()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.Add(TestData.CelineName);
        await Assert.That(fixture.Count).IsEqualTo(1);
        fixture.Add(TestData.ClarenceName);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        fixture.Add("Cliffordddd");
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        fixture.Update(fixture.Items[TestData.TestValueTwo], TestData.CliffordName);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add multiple single items].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanAddMultipleSingleItems()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.Add("one");
        await Assert.That(fixture.Count).IsEqualTo(1);
        fixture.Add("two");
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        fixture.Add(TestData.ThreeText);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add single complex item].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanAddSingleComplexItem()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.Add(new("Chris", TestData.TestValueFortyFour));
        await Assert.That(fixture.Count).IsEqualTo(1);
    }

    /// <summary>Determines whether this instance [can add single item].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanAddSingleItem()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.Add("one");
        await Assert.That(fixture.Count).IsEqualTo(1);
    }

    /// <summary>Determines whether this instance [can clear and add item].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanClearAndAddItem()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.AddRange(["one", "two"]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture.Items[0]).IsEqualTo("one");
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(0);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueTwo);
        fixture.Add(TestData.ThreeText);
        await Assert.That(fixture.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture.Items[0]).IsEqualTo(TestData.ThreeText);
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(0);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(1);
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
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.AddRange(["one", "two"]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
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
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.Add("one");
        await Assert.That(fixture.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(await observedCount.Task).IsEqualTo(1);
    }

    /// <summary>Determines whether this instance [can replace all items].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanReplaceAllItems()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.AddRange(["one", "two"]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture.Items[0]).IsEqualTo("one");
        fixture.ReplaceAll([TestData.ThreeText, "four", "five"]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.Items[0]).IsEqualTo(TestData.ThreeText);
    }

    /// <summary>Determines whether this instance [can replace all items many times].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanReplaceAllItemsManyTimes()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.AddRange(["one", "two"]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture.Items[0]).IsEqualTo("one");
        fixture.ReplaceAll([TestData.ThreeText, "four", "five"]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.Items[0]).IsEqualTo(TestData.ThreeText);
        fixture.ReplaceAll(["six", "seven", "eight"]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.Items[0]).IsEqualTo("six");
    }

    /// <summary>Determines whether this instance [can replace all items with complex items].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanReplaceAllItemsWithComplexItems()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture.Items[0].Name).IsEqualTo(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.Items[0].Name).IsEqualTo(TestData.CelineName);
    }

    /// <summary>Determines whether this instance [can replace all items with complex items and edit].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanReplaceAllItemsWithComplexItemsAndEdit()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture.Items[0].Name).IsEqualTo(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.Items[0].Name).IsEqualTo(TestData.CelineName);
        fixture.Update(fixture.Items[TestData.TestValueTwo], new(TestData.CliffordName, TestData.TestValueFive));
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.Items[TestData.TestValueTwo].Name).IsEqualTo(TestData.CliffordName);
    }

    /// <summary>Determines whether this instance [can replace all items with complex items and edit and remove].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanReplaceAllItemsWithComplexItemsAndEditAndRemove()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture.Items[0].Name).IsEqualTo(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.Items[0].Name).IsEqualTo(TestData.CelineName);
        fixture.Update(fixture.Items[TestData.TestValueTwo], new(TestData.CliffordName, TestData.TestValueFive));
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.Items[TestData.TestValueTwo].Name).IsEqualTo(TestData.CliffordName);
        _ = fixture.Remove(fixture.Items[TestData.TestValueTwo]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(0);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(1);
    }

    /// <summary>Determines whether this instance [can replace all items with complex items and edit and remove and add].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanReplaceAllItemsWithComplexItemsAndEditAndRemoveAndAdd()
    {
        ReactiveList<TestData> fixture = [];
        var inpcName = string.Empty;
        fixture.PropertyChanged += (sender, args) => inpcName += args.PropertyName;
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        await Assert.That(inpcName).IsEqualTo("CountItem[]");
        inpcName = string.Empty;
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(inpcName).IsEqualTo("CountItem[]");
        inpcName = string.Empty;
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture.Items[0].Name).IsEqualTo(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.Items[0].Name).IsEqualTo(TestData.CelineName);
        fixture.Update(fixture.Items[TestData.TestValueTwo], new(TestData.CliffordName, TestData.TestValueFive));
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.Items[TestData.TestValueTwo].Name).IsEqualTo(TestData.CliffordName);
        _ = fixture.Remove(fixture.Items[TestData.TestValueTwo]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(0);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(1);
        fixture.Add(new(TestData.CliffordName, TestData.TestValueFive));
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
    }

    /// <summary>Determines whether this instance [can replace all items with complex items and edit and remove and add and clear].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanReplaceAllItemsWithComplexItemsAndEditAndRemoveAndAddAndClear()
    {
        ReactiveList<TestData> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.AddRange([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture.Items[0].Name).IsEqualTo(TestData.CelineName);
        fixture.ReplaceAll([new(TestData.CelineName, TestData.TestValueFive), new(TestData.ClarenceName, TestData.TestValueFive), new(TestData.CliffordName, TestData.TestValueFive)]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.Items[0].Name).IsEqualTo(TestData.CelineName);
        fixture.Update(fixture.Items[TestData.TestValueTwo], new(TestData.CliffordName, TestData.TestValueFive));
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.Items[TestData.TestValueTwo].Name).IsEqualTo(TestData.CliffordName);
        _ = fixture.Remove(fixture.Items[TestData.TestValueTwo]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(0);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(1);
        fixture.Add(new(TestData.CliffordName, TestData.TestValueFive));
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(0);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(TestData.TestValueThree);
    }

    /// <summary>Determines whether this instance [can add items and insert items].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanAddItemsAndInsertItems()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.AddRange(["one", "two"]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture.Items[0]).IsEqualTo("one");
        fixture.Insert(1, TestData.ThreeText);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture.Items[1]).IsEqualTo(TestData.ThreeText);
    }

    /// <summary>Determines whether this instance [can add items and insert items and remove at index].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanAddItemsAndInsertItemsAndRemoveAtIndex()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        await Assert.That(fixture.Count).IsEqualTo(0);
        fixture.AddRange(["one", "two"]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture.Items[0]).IsEqualTo("one");
        fixture.Insert(1, TestData.ThreeText);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture.Items[1]).IsEqualTo(TestData.ThreeText);
        fixture.RemoveAt(1);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ItemsAdded.Count).IsEqualTo(0);
        await Assert.That(fixture.ItemsChanged.Count).IsEqualTo(1);
        await Assert.That(fixture.ItemsRemoved.Count).IsEqualTo(1);
    }

    /// <summary>Determines whether this instance can enumerate.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanEnumerate()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.AddRange(["one", "two"]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        foreach (var item in fixture)
        {
            await Assert.That(item).IsNotNullOrEmpty();
        }
    }

    /// <summary>Determines whether this instance [can get an element at the index or return default].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanGetElementAtOrDefault()
    {
        ReactiveList<string> fixture = [];
        fixture.Clear();
        fixture.AddRange(["one", "two"]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.ElementAtOrDefault(0)).IsEqualTo("one");
        await Assert.That(fixture.ElementAtOrDefault(1)).IsEqualTo("two");
        await Assert.That(fixture.ElementAtOrDefault(TestData.TestValueTwo)).IsNull();
    }

    /// <summary>Determines whether this instance [can add items to a list then add to fixture].</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanAddItemsToAListThenAddToFixture()
    {
        List<string> fixture = [];
        fixture.Clear();
        fixture.AddRange(["one", "two"]);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture[0]).IsEqualTo("one");
        await Assert.That(fixture[1]).IsEqualTo("two");
        ReactiveList<string> fixture2 = [];
        fixture2.AddRange(fixture);
        await Assert.That(fixture2.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture2.ItemsAdded.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture2.ItemsChanged.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture2.ItemsRemoved.Count).IsEqualTo(0);
        await Assert.That(fixture2.Items[0]).IsEqualTo("one");
        await Assert.That(fixture2.Items[1]).IsEqualTo("two");
    }
}
