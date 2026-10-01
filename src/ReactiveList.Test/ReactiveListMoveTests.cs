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

/// <summary>ReactiveList Move Tests.</summary>
public class ReactiveListMoveTests
{
    /// <summary>Move should reorder item forward in list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldReorderItemForwardInList()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText, "four"];

        fixture.Move(0, TestData.TestValueTwo);

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueFour);
        await Assert.That(fixture[0]).IsEqualTo("two");
        await Assert.That(fixture[1]).IsEqualTo(TestData.ThreeText);
        await Assert.That(fixture[TestData.TestValueTwo]).IsEqualTo("one");
        await Assert.That(fixture[TestData.TestValueThree]).IsEqualTo("four");
    }

    /// <summary>Move should reorder item backward in list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldReorderItemBackwardInList()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText, "four"];

        fixture.Move(TestData.TestValueThree, 1);

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueFour);
        await Assert.That(fixture[0]).IsEqualTo("one");
        await Assert.That(fixture[1]).IsEqualTo("four");
        await Assert.That(fixture[TestData.TestValueTwo]).IsEqualTo("two");
        await Assert.That(fixture[TestData.TestValueThree]).IsEqualTo(TestData.ThreeText);
    }

    /// <summary>Move should handle moving to first position.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldHandleMovingToFirstPosition()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Move(TestData.TestValueTwo, 0);

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture[0]).IsEqualTo(TestData.ThreeText);
        await Assert.That(fixture[1]).IsEqualTo("one");
        await Assert.That(fixture[TestData.TestValueTwo]).IsEqualTo("two");
    }

    /// <summary>Move should handle moving to last position.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldHandleMovingToLastPosition()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Move(0, TestData.TestValueTwo);

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture[0]).IsEqualTo("two");
        await Assert.That(fixture[1]).IsEqualTo(TestData.ThreeText);
        await Assert.That(fixture[TestData.TestValueTwo]).IsEqualTo("one");
    }

    /// <summary>Move should do nothing when same index.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldDoNothingWhenSameIndex()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Move(1, 1);

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture[0]).IsEqualTo("one");
        await Assert.That(fixture[1]).IsEqualTo("two");
        await Assert.That(fixture[TestData.TestValueTwo]).IsEqualTo(TestData.ThreeText);
    }

    /// <summary>Move should throw when old index is negative.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldThrowWhenOldIndexIsNegative()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        var action = () => fixture.Move(-1, 1);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>().WithParameterName("oldIndex");
    }

    /// <summary>Move should throw when old index exceeds count.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldThrowWhenOldIndexExceedsCount()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        var action = () => fixture.Move(TestData.TestValueThree, 1);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>().WithParameterName("oldIndex");
    }

    /// <summary>Move should throw when new index is negative.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldThrowWhenNewIndexIsNegative()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        var action = () => fixture.Move(1, -1);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>().WithParameterName("newIndex");
    }

    /// <summary>Move should throw when new index exceeds count.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldThrowWhenNewIndexExceedsCount()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        var action = () => fixture.Move(1, TestData.TestValueThree);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>().WithParameterName("newIndex");
    }

    /// <summary>Move should raise property changed for item array.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldRaisePropertyChangedForItemArray()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];
        var propertyNames = string.Empty;
        fixture.PropertyChanged += (sender, args) => propertyNames += args.PropertyName;

        fixture.Move(0, TestData.TestValueTwo);

        await Assert.That(propertyNames).Contains("Item[]");
    }

    /// <summary>Move should work with complex types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldWorkWithComplexTypes()
    {
        ReactiveList<TestData> fixture =
        [
            new("Alice", TestData.TestValueTwentyFive),
            new("Bob", TestData.TestValueThirty),
            new("Charlie", TestData.TestValueThirtyFive)
        ];

        fixture.Move(TestData.TestValueTwo, 0);

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture[0].Name).IsEqualTo("Charlie");
        await Assert.That(fixture[1].Name).IsEqualTo("Alice");
        await Assert.That(fixture[TestData.TestValueTwo].Name).IsEqualTo("Bob");
    }

    /// <summary>Move should handle adjacent positions forward.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldHandleAdjacentPositionsForward()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Move(0, 1);

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture[0]).IsEqualTo("two");
        await Assert.That(fixture[1]).IsEqualTo("one");
        await Assert.That(fixture[TestData.TestValueTwo]).IsEqualTo(TestData.ThreeText);
    }

    /// <summary>Move should handle adjacent positions backward.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ShouldHandleAdjacentPositionsBackward()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.Move(1, 0);

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture[0]).IsEqualTo("two");
        await Assert.That(fixture[1]).IsEqualTo("one");
        await Assert.That(fixture[TestData.TestValueTwo]).IsEqualTo(TestData.ThreeText);
    }
}
