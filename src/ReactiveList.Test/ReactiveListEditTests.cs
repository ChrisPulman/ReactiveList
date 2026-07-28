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

        fixture.Edit(static list =>
        {
            list.Add("one");
            list.Add("two");
            list.Add(TestData.ThreeText);
        });

        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture[0].Should().Be("one");
        _ = fixture[1].Should().Be("two");
        _ = fixture[TestData.TestValueTwo].Should().Be(TestData.ThreeText);
    }

    /// <summary>Edit should allow batch remove operations.</summary>
    [Test]
    public void Edit_ShouldAllowBatchRemoveOperations()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText, "four"];

        fixture.Edit(static list =>
        {
            _ = list.Remove("two");
            _ = list.Remove("four");
        });

        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture[0].Should().Be("one");
        _ = fixture[1].Should().Be(TestData.ThreeText);
    }

    /// <summary>Edit should allow mixed operations.</summary>
    [Test]
    public void Edit_ShouldAllowMixedOperations()
    {
        ReactiveList<string> fixture = ["one", "two"];

        fixture.Edit(static list =>
        {
            list.Add(TestData.ThreeText);
            _ = list.Remove("one");
            list.Add("four");
        });

        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture.Should().Contain("two");
        _ = fixture.Should().Contain(TestData.ThreeText);
        _ = fixture.Should().Contain("four");
        _ = fixture.Should().NotContain("one");
    }

    /// <summary>Edit should allow clear and repopulate.</summary>
    [Test]
    public void Edit_ShouldAllowClearAndRepopulate()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Edit(static list =>
        {
            list.Clear();
            list.Add("alpha");
            list.Add("beta");
        });

        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture[0].Should().Be("alpha");
        _ = fixture[1].Should().Be("beta");
    }

    /// <summary>Edit should throw when action is null.</summary>
    [Test]
    public void Edit_ShouldThrowWhenActionIsNull()
    {
        ReactiveList<string> fixture = [];

        var action = () => fixture.Edit(null!);

        _ = action.Should().Throw<ArgumentNullException>()
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

        fixture.Edit(static list =>
        {
            list.Add("one");
            list.Add("two");
            list.Add(TestData.ThreeText);
        });

        _ = countChanges.Should().Be(1);
        _ = itemArrayChanges.Should().Be(1);
    }

    /// <summary>Edit should allow insert at index.</summary>
    [Test]
    public void Edit_ShouldAllowInsertAtIndex()
    {
        ReactiveList<string> fixture = ["one", TestData.ThreeText];

        fixture.Edit(static list => list.Insert(1, "two"));

        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture[0].Should().Be("one");
        _ = fixture[1].Should().Be("two");
        _ = fixture[TestData.TestValueTwo].Should().Be(TestData.ThreeText);
    }

    /// <summary>Edit should allow remove at index.</summary>
    [Test]
    public void Edit_ShouldAllowRemoveAtIndex()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Edit(static list => list.RemoveAt(1));

        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture[0].Should().Be("one");
        _ = fixture[1].Should().Be(TestData.ThreeText);
    }

    /// <summary>Edit should allow add range.</summary>
    [Test]
    public void Edit_ShouldAllowAddRange()
    {
        ReactiveList<string> fixture = ["one"];

        fixture.Edit(static list => list.AddRange(["two", TestData.ThreeText, "four"]));

        _ = fixture.Count.Should().Be(TestData.TestValueFour);
        _ = fixture[0].Should().Be("one");
        _ = fixture[1].Should().Be("two");
        _ = fixture[TestData.TestValueTwo].Should().Be(TestData.ThreeText);
        _ = fixture[TestData.TestValueThree].Should().Be("four");
    }

    /// <summary>Edit should allow replace operation.</summary>
    [Test]
    public void Edit_ShouldAllowReplaceOperation()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Edit(static list =>
        {
            var index = list.IndexOf("two");
            list.RemoveAt(index);
            list.Insert(index, "TWO");
        });

        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture[0].Should().Be("one");
        _ = fixture[1].Should().Be("TWO");
        _ = fixture[TestData.TestValueTwo].Should().Be(TestData.ThreeText);
    }

    /// <summary>Edit should work with complex types.</summary>
    [Test]
    public void Edit_ShouldWorkWithComplexTypes()
    {
        ReactiveList<TestData> fixture = [];

        fixture.Edit(static list =>
        {
            list.Add(new("Alice", TestData.TestValueTwentyFive));
            list.Add(new("Bob", TestData.TestValueThirty));
        });

        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture[0].Name.Should().Be("Alice");
        _ = fixture[1].Name.Should().Be("Bob");
    }

    /// <summary>Edit should handle empty action gracefully.</summary>
    [Test]
    public void Edit_ShouldHandleEmptyActionGracefully()
    {
        ReactiveList<string> fixture = ["one", "two"];

        fixture.Edit(static _ => { });

        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture[0].Should().Be("one");
        _ = fixture[1].Should().Be("two");
    }

    /// <summary>Edit should allow move operation.</summary>
    [Test]
    public void Edit_ShouldAllowMoveOperation()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Edit(static list => list.Move(0, TestData.TestValueTwo));

        _ = fixture.Count.Should().Be(TestData.TestValueThree);
        _ = fixture[0].Should().Be("two");
        _ = fixture[1].Should().Be(TestData.ThreeText);
        _ = fixture[TestData.TestValueTwo].Should().Be("one");
    }

    /// <summary>Edit should allow multiple operations in sequence.</summary>
    [Test]
    public void Edit_ShouldAllowMultipleOperationsInSequence()
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

        _ = fixture.Count.Should().Be(TestData.TestValueFive);
        _ = fixture.Should().ContainInOrder(0, TestData.TestValueFive, 1, TestData.TestValueTwo, TestData.TestValueFour);
    }
}
