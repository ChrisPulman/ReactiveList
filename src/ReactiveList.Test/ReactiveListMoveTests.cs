// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using CP.Reactive.Collections;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// ReactiveList Move Tests.
/// </summary>
public class ReactiveListMoveTests
{
    /// <summary>
    /// Move should reorder item forward in list.
    /// </summary>
    [Fact]
    public void Move_ShouldReorderItemForwardInList()
    {
        ReactiveList<string> fixture = ["one", "two", "three", "four"];

        fixture.Move(0, 2);

        fixture.Count.Should().Be(4);
        fixture[0].Should().Be("two");
        fixture[1].Should().Be("three");
        fixture[2].Should().Be("one");
        fixture[3].Should().Be("four");
    }

    /// <summary>
    /// Move should reorder item backward in list.
    /// </summary>
    [Fact]
    public void Move_ShouldReorderItemBackwardInList()
    {
        ReactiveList<string> fixture = ["one", "two", "three", "four"];

        fixture.Move(3, 1);

        fixture.Count.Should().Be(4);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("four");
        fixture[2].Should().Be("two");
        fixture[3].Should().Be("three");
    }

    /// <summary>
    /// Move should handle moving to first position.
    /// </summary>
    [Fact]
    public void Move_ShouldHandleMovingToFirstPosition()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        fixture.Move(2, 0);

        fixture.Count.Should().Be(3);
        fixture[0].Should().Be("three");
        fixture[1].Should().Be("one");
        fixture[2].Should().Be("two");
    }

    /// <summary>
    /// Move should handle moving to last position.
    /// </summary>
    [Fact]
    public void Move_ShouldHandleMovingToLastPosition()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        fixture.Move(0, 2);

        fixture.Count.Should().Be(3);
        fixture[0].Should().Be("two");
        fixture[1].Should().Be("three");
        fixture[2].Should().Be("one");
    }

    /// <summary>
    /// Move should do nothing when same index.
    /// </summary>
    [Fact]
    public void Move_ShouldDoNothingWhenSameIndex()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        fixture.Move(1, 1);

        fixture.Count.Should().Be(3);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("two");
        fixture[2].Should().Be("three");
    }

    /// <summary>
    /// Move should throw when old index is negative.
    /// </summary>
    [Fact]
    public void Move_ShouldThrowWhenOldIndexIsNegative()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        var action = () => fixture.Move(-1, 1);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("oldIndex");
    }

    /// <summary>
    /// Move should throw when old index exceeds count.
    /// </summary>
    [Fact]
    public void Move_ShouldThrowWhenOldIndexExceedsCount()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        var action = () => fixture.Move(3, 1);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("oldIndex");
    }

    /// <summary>
    /// Move should throw when new index is negative.
    /// </summary>
    [Fact]
    public void Move_ShouldThrowWhenNewIndexIsNegative()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        var action = () => fixture.Move(1, -1);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("newIndex");
    }

    /// <summary>
    /// Move should throw when new index exceeds count.
    /// </summary>
    [Fact]
    public void Move_ShouldThrowWhenNewIndexExceedsCount()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        var action = () => fixture.Move(1, 3);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("newIndex");
    }

    /// <summary>
    /// Move should raise property changed for item array.
    /// </summary>
    [Fact]
    public void Move_ShouldRaisePropertyChangedForItemArray()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];
        var propertyNames = string.Empty;
        fixture.PropertyChanged += (sender, args) => propertyNames += args.PropertyName;

        fixture.Move(0, 2);

        propertyNames.Should().Contain("Item[]");
    }

    /// <summary>
    /// Move should work with complex types.
    /// </summary>
    [Fact]
    public void Move_ShouldWorkWithComplexTypes()
    {
        ReactiveList<TestData> fixture =
        [
            new("Alice", 25),
            new("Bob", 30),
            new("Charlie", 35)
        ];

        fixture.Move(2, 0);

        fixture.Count.Should().Be(3);
        fixture[0].Name.Should().Be("Charlie");
        fixture[1].Name.Should().Be("Alice");
        fixture[2].Name.Should().Be("Bob");
    }

    /// <summary>
    /// Move should handle adjacent positions forward.
    /// </summary>
    [Fact]
    public void Move_ShouldHandleAdjacentPositionsForward()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        fixture.Move(0, 1);

        fixture.Count.Should().Be(3);
        fixture[0].Should().Be("two");
        fixture[1].Should().Be("one");
        fixture[2].Should().Be("three");
    }

    /// <summary>
    /// Move should handle adjacent positions backward.
    /// </summary>
    [Fact]
    public void Move_ShouldHandleAdjacentPositionsBackward()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        fixture.Move(1, 0);

        fixture.Count.Should().Be(3);
        fixture[0].Should().Be("two");
        fixture[1].Should().Be("one");
        fixture[2].Should().Be("three");
    }
}
