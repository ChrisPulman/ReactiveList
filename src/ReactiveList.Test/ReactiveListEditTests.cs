// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using CP.Primitives.Collections;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>ReactiveList Edit Tests.</summary>
public class ReactiveListEditTests
{
    /// <summary>Edit should allow batch add operations.</summary>
    [Test]
    public void Edit_ShouldAllowBatchAddOperations()
    {
        ReactiveList<string> fixture = [];

        fixture.Edit(list =>
        {
            list.Add("one");
            list.Add("two");
            list.Add(TestData.ThreeText);
        });

        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("two");
        fixture[TestData.TestValueTwo].Should().Be(TestData.ThreeText);
    }

    /// <summary>Edit should allow batch remove operations.</summary>
    [Test]
    public void Edit_ShouldAllowBatchRemoveOperations()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText, "four"];

        fixture.Edit(list =>
        {
            list.Remove("two");
            list.Remove("four");
        });

        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be(TestData.ThreeText);
    }

    /// <summary>Edit should allow mixed operations.</summary>
    [Test]
    public void Edit_ShouldAllowMixedOperations()
    {
        ReactiveList<string> fixture = ["one", "two"];

        fixture.Edit(list =>
        {
            list.Add(TestData.ThreeText);
            list.Remove("one");
            list.Add("four");
        });

        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture.Should().Contain("two");
        fixture.Should().Contain(TestData.ThreeText);
        fixture.Should().Contain("four");
        fixture.Should().NotContain("one");
    }

    /// <summary>Edit should allow clear and repopulate.</summary>
    [Test]
    public void Edit_ShouldAllowClearAndRepopulate()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Edit(list =>
        {
            list.Clear();
            list.Add("alpha");
            list.Add("beta");
        });

        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture[0].Should().Be("alpha");
        fixture[1].Should().Be("beta");
    }

    /// <summary>Edit should throw when action is null.</summary>
    [Test]
    public void Edit_ShouldThrowWhenActionIsNull()
    {
        ReactiveList<string> fixture = [];

        var action = () => fixture.Edit(null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("editAction");
    }

    /// <summary>Edit should raise property changed once for count.</summary>
    [Test]
    public void Edit_ShouldRaisePropertyChanged()
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

        fixture.Edit(list =>
        {
            list.Add("one");
            list.Add("two");
            list.Add(TestData.ThreeText);
        });

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>Edit should allow insert at index.</summary>
    [Test]
    public void Edit_ShouldAllowInsertAtIndex()
    {
        ReactiveList<string> fixture = ["one", TestData.ThreeText];

        fixture.Edit(list => list.Insert(1, "two"));

        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("two");
        fixture[TestData.TestValueTwo].Should().Be(TestData.ThreeText);
    }

    /// <summary>Edit should allow remove at index.</summary>
    [Test]
    public void Edit_ShouldAllowRemoveAtIndex()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Edit(list => list.RemoveAt(1));

        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be(TestData.ThreeText);
    }

    /// <summary>Edit should allow add range.</summary>
    [Test]
    public void Edit_ShouldAllowAddRange()
    {
        ReactiveList<string> fixture = ["one"];

        fixture.Edit(list => list.AddRange(["two", TestData.ThreeText, "four"]));

        fixture.Count.Should().Be(TestData.TestValueFour);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("two");
        fixture[TestData.TestValueTwo].Should().Be(TestData.ThreeText);
        fixture[TestData.TestValueThree].Should().Be("four");
    }

    /// <summary>Edit should allow replace operation.</summary>
    [Test]
    public void Edit_ShouldAllowReplaceOperation()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Edit(list =>
        {
            var index = list.IndexOf("two");
            list.RemoveAt(index);
            list.Insert(index, "TWO");
        });

        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("TWO");
        fixture[TestData.TestValueTwo].Should().Be(TestData.ThreeText);
    }

    /// <summary>Edit should work with complex types.</summary>
    [Test]
    public void Edit_ShouldWorkWithComplexTypes()
    {
        ReactiveList<TestData> fixture = [];

        fixture.Edit(list =>
        {
            list.Add(new TestData("Alice", TestData.TestValueTwentyFive));
            list.Add(new TestData("Bob", TestData.TestValueThirty));
        });

        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture[0].Name.Should().Be("Alice");
        fixture[1].Name.Should().Be("Bob");
    }

    /// <summary>Edit should handle empty action gracefully.</summary>
    [Test]
    public void Edit_ShouldHandleEmptyActionGracefully()
    {
        ReactiveList<string> fixture = ["one", "two"];

        fixture.Edit(_ => { });

        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("two");
    }

    /// <summary>Edit should allow move operation.</summary>
    [Test]
    public void Edit_ShouldAllowMoveOperation()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Edit(list => list.Move(0, TestData.TestValueTwo));

        fixture.Count.Should().Be(TestData.TestValueThree);
        fixture[0].Should().Be("two");
        fixture[1].Should().Be(TestData.ThreeText);
        fixture[TestData.TestValueTwo].Should().Be("one");
    }

    /// <summary>Edit should allow multiple operations in sequence.</summary>
    [Test]
    public void Edit_ShouldAllowMultipleOperationsInSequence()
    {
        ReactiveList<int> fixture = [];

        fixture.Edit(list =>
        {
            for (var i = 1; i <= TestData.TestValueFive; i++)
            {
                list.Add(i);
            }

            list.RemoveAt(TestData.TestValueTwo); // Remove 3
            list.Insert(0, 0); // Add 0 at beginning
            list.Move(TestData.TestValueFour, 1); // Move 5 to position 1
        });

        fixture.Count.Should().Be(TestData.TestValueFive);
        fixture.Should().ContainInOrder(0, TestData.TestValueFive, 1, TestData.TestValueTwo, TestData.TestValueFour);
    }
}
