// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using CP.Reactive.Collections;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// ReactiveList Edit Tests.
/// </summary>
public class ReactiveListEditTests
{
    /// <summary>
    /// Edit should allow batch add operations.
    /// </summary>
    [Fact]
    public void Edit_ShouldAllowBatchAddOperations()
    {
        ReactiveList<string> fixture = [];

        fixture.Edit(list =>
        {
            list.Add("one");
            list.Add("two");
            list.Add("three");
        });

        fixture.Count.Should().Be(3);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("two");
        fixture[2].Should().Be("three");
    }

    /// <summary>
    /// Edit should allow batch remove operations.
    /// </summary>
    [Fact]
    public void Edit_ShouldAllowBatchRemoveOperations()
    {
        ReactiveList<string> fixture = ["one", "two", "three", "four"];

        fixture.Edit(list =>
        {
            list.Remove("two");
            list.Remove("four");
        });

        fixture.Count.Should().Be(2);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("three");
    }

    /// <summary>
    /// Edit should allow mixed operations.
    /// </summary>
    [Fact]
    public void Edit_ShouldAllowMixedOperations()
    {
        ReactiveList<string> fixture = ["one", "two"];

        fixture.Edit(list =>
        {
            list.Add("three");
            list.Remove("one");
            list.Add("four");
        });

        fixture.Count.Should().Be(3);
        fixture.Should().Contain("two");
        fixture.Should().Contain("three");
        fixture.Should().Contain("four");
        fixture.Should().NotContain("one");
    }

    /// <summary>
    /// Edit should allow clear and repopulate.
    /// </summary>
    [Fact]
    public void Edit_ShouldAllowClearAndRepopulate()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        fixture.Edit(list =>
        {
            list.Clear();
            list.Add("alpha");
            list.Add("beta");
        });

        fixture.Count.Should().Be(2);
        fixture[0].Should().Be("alpha");
        fixture[1].Should().Be("beta");
    }

    /// <summary>
    /// Edit should throw when action is null.
    /// </summary>
    [Fact]
    public void Edit_ShouldThrowWhenActionIsNull()
    {
        ReactiveList<string> fixture = [];

        var action = () => fixture.Edit(null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("editAction");
    }

    /// <summary>
    /// Edit should raise property changed once for count.
    /// </summary>
    [Fact]
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

            if (args.PropertyName == "Item[]")
            {
                itemArrayChanges++;
            }
        };

        fixture.Edit(list =>
        {
            list.Add("one");
            list.Add("two");
            list.Add("three");
        });

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>
    /// Edit should allow insert at index.
    /// </summary>
    [Fact]
    public void Edit_ShouldAllowInsertAtIndex()
    {
        ReactiveList<string> fixture = ["one", "three"];

        fixture.Edit(list => list.Insert(1, "two"));

        fixture.Count.Should().Be(3);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("two");
        fixture[2].Should().Be("three");
    }

    /// <summary>
    /// Edit should allow remove at index.
    /// </summary>
    [Fact]
    public void Edit_ShouldAllowRemoveAtIndex()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        fixture.Edit(list => list.RemoveAt(1));

        fixture.Count.Should().Be(2);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("three");
    }

    /// <summary>
    /// Edit should allow add range.
    /// </summary>
    [Fact]
    public void Edit_ShouldAllowAddRange()
    {
        ReactiveList<string> fixture = ["one"];

        fixture.Edit(list => list.AddRange(["two", "three", "four"]));

        fixture.Count.Should().Be(4);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("two");
        fixture[2].Should().Be("three");
        fixture[3].Should().Be("four");
    }

    /// <summary>
    /// Edit should allow replace operation.
    /// </summary>
    [Fact]
    public void Edit_ShouldAllowReplaceOperation()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        fixture.Edit(list =>
        {
            var index = list.IndexOf("two");
            list.RemoveAt(index);
            list.Insert(index, "TWO");
        });

        fixture.Count.Should().Be(3);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("TWO");
        fixture[2].Should().Be("three");
    }

    /// <summary>
    /// Edit should work with complex types.
    /// </summary>
    [Fact]
    public void Edit_ShouldWorkWithComplexTypes()
    {
        ReactiveList<TestData> fixture = [];

        fixture.Edit(list =>
        {
            list.Add(new TestData("Alice", 25));
            list.Add(new TestData("Bob", 30));
        });

        fixture.Count.Should().Be(2);
        fixture[0].Name.Should().Be("Alice");
        fixture[1].Name.Should().Be("Bob");
    }

    /// <summary>
    /// Edit should handle empty action gracefully.
    /// </summary>
    [Fact]
    public void Edit_ShouldHandleEmptyActionGracefully()
    {
        ReactiveList<string> fixture = ["one", "two"];

        fixture.Edit(_ => { });

        fixture.Count.Should().Be(2);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("two");
    }

    /// <summary>
    /// Edit should allow move operation.
    /// </summary>
    [Fact]
    public void Edit_ShouldAllowMoveOperation()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        fixture.Edit(list => list.Move(0, 2));

        fixture.Count.Should().Be(3);
        fixture[0].Should().Be("two");
        fixture[1].Should().Be("three");
        fixture[2].Should().Be("one");
    }

    /// <summary>
    /// Edit should allow multiple operations in sequence.
    /// </summary>
    [Fact]
    public void Edit_ShouldAllowMultipleOperationsInSequence()
    {
        ReactiveList<int> fixture = [];

        fixture.Edit(list =>
        {
            for (var i = 1; i <= 5; i++)
            {
                list.Add(i);
            }

            list.RemoveAt(2); // Remove 3
            list.Insert(0, 0); // Add 0 at beginning
            list.Move(4, 1); // Move 5 to position 1
        });

        fixture.Count.Should().Be(5);
        fixture.Should().ContainInOrder(0, 5, 1, 2, 4);
    }
}
