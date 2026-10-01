// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Collections;
#else
using CP.Primitives.Collections;
#endif
using TUnit.Assertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>ReactiveList Edit Tests.</summary>
public class ReactiveListEditTests
{
    /// <summary>Edit should allow batch add operations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldAllowBatchAddOperations()
    {
        ReactiveList<string> fixture = [];

        fixture.Edit(static list =>
        {
            list.Add("one");
            list.Add("two");
            list.Add(TestData.ThreeText);
        });

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture[0]).IsEqualTo("one");
        await Assert.That(fixture[1]).IsEqualTo("two");
        await Assert.That(fixture[TestData.TestValueTwo]).IsEqualTo(TestData.ThreeText);
    }

    /// <summary>Edit should allow batch remove operations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldAllowBatchRemoveOperations()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText, "four"];

        fixture.Edit(static list =>
        {
            _ = list.Remove("two");
            _ = list.Remove("four");
        });

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture[0]).IsEqualTo("one");
        await Assert.That(fixture[1]).IsEqualTo(TestData.ThreeText);
    }

    /// <summary>Edit should allow mixed operations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldAllowMixedOperations()
    {
        ReactiveList<string> fixture = ["one", "two"];

        fixture.Edit(static list =>
        {
            list.Add(TestData.ThreeText);
            _ = list.Remove("one");
            list.Add("four");
        });

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture).Contains("two");
        await Assert.That(fixture).Contains(TestData.ThreeText);
        await Assert.That(fixture).Contains("four");
        await Assert.That(fixture).DoesNotContain("one");
    }

    /// <summary>Edit should allow clear and repopulate.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldAllowClearAndRepopulate()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Edit(static list =>
        {
            list.Clear();
            list.Add("alpha");
            list.Add("beta");
        });

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture[0]).IsEqualTo("alpha");
        await Assert.That(fixture[1]).IsEqualTo("beta");
    }

    /// <summary>Edit should throw when action is null.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldThrowWhenActionIsNull()
    {
        ReactiveList<string> fixture = [];

        var action = () => fixture.Edit(null!);

        await Assert.That(action).Throws<ArgumentNullException>().WithParameterName("editAction");
    }

    /// <summary>Edit should raise property changed once for count.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldRaisePropertyChanged()
    {
        ReactiveList<string> fixture = [];
        var countChanges = 0;
        var itemArrayChanges = 0;
        fixture.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == "Count")
            {
                countChanges++;
            }

            if (args.PropertyName != "Item[]")
            {
                return;
            }

            itemArrayChanges++;
        };

        fixture.Edit(static list =>
        {
            list.Add("one");
            list.Add("two");
            list.Add(TestData.ThreeText);
        });

        await Assert.That(countChanges).IsEqualTo(1);
        await Assert.That(itemArrayChanges).IsEqualTo(1);
    }

    /// <summary>Edit should allow insert at index.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldAllowInsertAtIndex()
    {
        ReactiveList<string> fixture = ["one", TestData.ThreeText];

        fixture.Edit(static list => list.Insert(1, "two"));

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture[0]).IsEqualTo("one");
        await Assert.That(fixture[1]).IsEqualTo("two");
        await Assert.That(fixture[TestData.TestValueTwo]).IsEqualTo(TestData.ThreeText);
    }

    /// <summary>Edit should allow remove at index.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldAllowRemoveAtIndex()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Edit(static list => list.RemoveAt(1));

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture[0]).IsEqualTo("one");
        await Assert.That(fixture[1]).IsEqualTo(TestData.ThreeText);
    }

    /// <summary>Edit should allow add range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldAllowAddRange()
    {
        ReactiveList<string> fixture = ["one"];

        fixture.Edit(static list => list.AddRange(["two", TestData.ThreeText, "four"]));

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueFour);
        await Assert.That(fixture[0]).IsEqualTo("one");
        await Assert.That(fixture[1]).IsEqualTo("two");
        await Assert.That(fixture[TestData.TestValueTwo]).IsEqualTo(TestData.ThreeText);
        await Assert.That(fixture[TestData.TestValueThree]).IsEqualTo("four");
    }

    /// <summary>Edit should allow replace operation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldAllowReplaceOperation()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Edit(static list =>
        {
            var index = list.IndexOf("two");
            list.RemoveAt(index);
            list.Insert(index, "TWO");
        });

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture[0]).IsEqualTo("one");
        await Assert.That(fixture[1]).IsEqualTo("TWO");
        await Assert.That(fixture[TestData.TestValueTwo]).IsEqualTo(TestData.ThreeText);
    }

    /// <summary>Edit should work with complex types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldWorkWithComplexTypes()
    {
        ReactiveList<TestData> fixture = [];

        fixture.Edit(static list =>
        {
            list.Add(new("Alice", TestData.TestValueTwentyFive));
            list.Add(new("Bob", TestData.TestValueThirty));
        });

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture[0].Name).IsEqualTo("Alice");
        await Assert.That(fixture[1].Name).IsEqualTo("Bob");
    }

    /// <summary>Edit should handle empty action gracefully.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldHandleEmptyActionGracefully()
    {
        ReactiveList<string> fixture = ["one", "two"];

        fixture.Edit(static _ => { });

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture[0]).IsEqualTo("one");
        await Assert.That(fixture[1]).IsEqualTo("two");
    }

    /// <summary>Edit should allow move operation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldAllowMoveOperation()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Edit(static list => list.Move(0, TestData.TestValueTwo));

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture[0]).IsEqualTo("two");
        await Assert.That(fixture[1]).IsEqualTo(TestData.ThreeText);
        await Assert.That(fixture[TestData.TestValueTwo]).IsEqualTo("one");
    }

    /// <summary>Edit should allow multiple operations in sequence.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Edit_ShouldAllowMultipleOperationsInSequence()
    {
        ReactiveList<int> fixture = [];

        fixture.Edit(static list =>
        {
            for (var i = 1; i <= TestData.TestValueFive; i++)
            {
                list.Add(i);
            }

            list.RemoveAt(TestData.TestValueTwo); // Remove 3
            list.Insert(0, 0); // Add 0 at beginning
            list.Move(TestData.TestValueFour, 1); // Move 5 to position 1
        });

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueFive);
        await Assert.That(TestSequences.ContainsInOrder(fixture, [0, TestData.TestValueFive, 1, TestData.TestValueTwo, TestData.TestValueFour])).IsTrue();
    }
}
