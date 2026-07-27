// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>ReactiveList Remove Tests.</summary>
public class ReactiveListRemoveTests
{
    /// <summary>Remove should remove existing item for string type.</summary>
    [Test]
    public void Remove_ShouldRemoveExistingItem_String()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        var result = fixture.Remove("two");

        _ = result.Should().BeTrue();
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.Should().Contain("one");
        _ = fixture.Should().Contain(TestData.ThreeText);
        _ = fixture.Should().NotContain("two");
    }

    /// <summary>Remove should return false for non-existing item for string type.</summary>
    [Test]
    public void Remove_ShouldReturnFalseForNonExistingItem_String()
    {
        ReactiveList<string> fixture = ["one", "two"];

        var result = fixture.Remove(TestData.ThreeText);

        _ = result.Should().BeFalse();
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
    }

    /// <summary>Remove should raise property changed for string type.</summary>
    [Test]
    public void Remove_ShouldRaisePropertyChanged_String()
    {
        ReactiveList<string> fixture = ["one", "two"];
        var countChanges = 0;
        var itemArrayChanges = 0;
        fixture.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == TestData.CountPropertyName)
            {
                countChanges++;
            }

            if (args.PropertyName != TestData.IndexerPropertyName)
            {
                return;
            }

            itemArrayChanges++;
        };

        _ = fixture.Remove("two");

        _ = countChanges.Should().Be(1);
        _ = itemArrayChanges.Should().Be(1);
    }

    /// <summary>Remove should remove existing item for int type.</summary>
    [Test]
    public void Remove_ShouldRemoveExistingItem_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo, TestData.TestValueThree];

        var result = fixture.Remove(TestData.TestValueTwo);

        _ = result.Should().BeTrue();
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.Should().Contain(1);
        _ = fixture.Should().Contain(TestData.TestValueThree);
        _ = fixture.Should().NotContain(TestData.TestValueTwo);
    }

    /// <summary>Remove should return false for non-existing item for int type.</summary>
    [Test]
    public void Remove_ShouldReturnFalseForNonExistingItem_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo];

        var result = fixture.Remove(TestData.TestValueThree);

        _ = result.Should().BeFalse();
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
    }

    /// <summary>Remove should raise property changed for int type.</summary>
    [Test]
    public void Remove_ShouldRaisePropertyChanged_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo];
        var countChanges = 0;
        var itemArrayChanges = 0;
        fixture.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == TestData.CountPropertyName)
            {
                countChanges++;
            }

            if (args.PropertyName != TestData.IndexerPropertyName)
            {
                return;
            }

            itemArrayChanges++;
        };

        _ = fixture.Remove(TestData.TestValueTwo);

        _ = countChanges.Should().Be(1);
        _ = itemArrayChanges.Should().Be(1);
    }

    /// <summary>Remove should remove existing item for TestData type.</summary>
    [Test]
    public void Remove_ShouldRemoveExistingItem_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty), new(TestData.CharlieName, TestData.TestValueThirtyFive)];
        var itemToRemove = fixture[1];
        var result = fixture.Remove(itemToRemove);

        _ = result.Should().BeTrue();
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.Should().Contain(static d => d.Name == TestData.AliceName);
        _ = fixture.Should().Contain(static d => d.Name == TestData.CharlieName);
        _ = fixture.Should().NotContain(static d => d.Name == "Bob");
    }

    /// <summary>Remove should return false for non-existing item for TestData type.</summary>
    [Test]
    public void Remove_ShouldReturnFalseForNonExistingItem_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty)];

        var result = fixture.Remove(new TestData(TestData.CharlieName, TestData.TestValueThirtyFive));

        _ = result.Should().BeFalse();
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
    }

    /// <summary>Remove should raise property changed for TestData type.</summary>
    [Test]
    public void Remove_ShouldRaisePropertyChanged_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty)];
        var countChanges = 0;
        var itemArrayChanges = 0;
        fixture.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == TestData.CountPropertyName)
            {
                countChanges++;
            }

            if (args.PropertyName != TestData.IndexerPropertyName)
            {
                return;
            }

            itemArrayChanges++;
        };

        var itemToRemove = fixture[1];
        _ = fixture.Remove(itemToRemove);

        _ = countChanges.Should().Be(1);
        _ = itemArrayChanges.Should().Be(1);
    }

    /// <summary>RemoveAt should remove item at index for string type.</summary>
    [Test]
    public void RemoveAt_ShouldRemoveItemAtIndex_String()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.RemoveAt(1);

        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture[0].Should().Be("one");
        _ = fixture[1].Should().Be(TestData.ThreeText);
    }

    /// <summary>RemoveAt should throw for invalid index for string type.</summary>
    [Test]
    public void RemoveAt_ShouldThrowForInvalidIndex_String()
    {
        ReactiveList<string> fixture = ["one", "two"];

        var action = () => fixture.RemoveAt(TestData.TestValueFive);

        _ = action.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>RemoveAt should raise property changed for string type.</summary>
    [Test]
    public void RemoveAt_ShouldRaisePropertyChanged_String()
    {
        ReactiveList<string> fixture = ["one", "two"];
        var countChanges = 0;
        var itemArrayChanges = 0;
        fixture.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == TestData.CountPropertyName)
            {
                countChanges++;
            }

            if (args.PropertyName != TestData.IndexerPropertyName)
            {
                return;
            }

            itemArrayChanges++;
        };

        fixture.RemoveAt(1);

        _ = countChanges.Should().Be(1);
        _ = itemArrayChanges.Should().Be(1);
    }

    /// <summary>RemoveAt should remove item at index for int type.</summary>
    [Test]
    public void RemoveAt_ShouldRemoveItemAtIndex_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo, TestData.TestValueThree];

        fixture.RemoveAt(1);

        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture[0].Should().Be(1);
        _ = fixture[1].Should().Be(TestData.TestValueThree);
    }

    /// <summary>RemoveAt should throw for invalid index for int type.</summary>
    [Test]
    public void RemoveAt_ShouldThrowForInvalidIndex_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo];

        var action = () => fixture.RemoveAt(TestData.TestValueFive);

        _ = action.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>RemoveAt should raise property changed for int type.</summary>
    [Test]
    public void RemoveAt_ShouldRaisePropertyChanged_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo];
        var countChanges = 0;
        var itemArrayChanges = 0;
        fixture.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == TestData.CountPropertyName)
            {
                countChanges++;
            }

            if (args.PropertyName != TestData.IndexerPropertyName)
            {
                return;
            }

            itemArrayChanges++;
        };

        fixture.RemoveAt(1);

        _ = countChanges.Should().Be(1);
        _ = itemArrayChanges.Should().Be(1);
    }

    /// <summary>RemoveAt should remove item at index for TestData type.</summary>
    [Test]
    public void RemoveAt_ShouldRemoveItemAtIndex_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty), new(TestData.CharlieName, TestData.TestValueThirtyFive)];

        fixture.RemoveAt(1);

        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture[0].Name.Should().Be(TestData.AliceName);
        _ = fixture[1].Name.Should().Be(TestData.CharlieName);
    }

    /// <summary>RemoveAt should throw for invalid index for TestData type.</summary>
    [Test]
    public void RemoveAt_ShouldThrowForInvalidIndex_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty)];

        var action = () => fixture.RemoveAt(TestData.TestValueFive);

        _ = action.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>RemoveAt should raise property changed for TestData type.</summary>
    [Test]
    public void RemoveAt_ShouldRaisePropertyChanged_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty)];
        var countChanges = 0;
        var itemArrayChanges = 0;
        fixture.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == TestData.CountPropertyName)
            {
                countChanges++;
            }

            if (args.PropertyName != TestData.IndexerPropertyName)
            {
                return;
            }

            itemArrayChanges++;
        };

        fixture.RemoveAt(1);

        _ = countChanges.Should().Be(1);
        _ = itemArrayChanges.Should().Be(1);
    }

    /// <summary>RemoveMany should remove items matching predicate for string type.</summary>
    [Test]
    public void RemoveMany_ShouldRemoveMatchingItems_String()
    {
        ReactiveList<string> fixture = ["apple", "banana", "apricot", "cherry", "avocado"];

        var removed = fixture.RemoveMany(static s => s.Length > 0 && s[0] == 'a');

        _ = removed.Should().Be(TestData.TestValueThree);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.Should().Contain("banana");
        _ = fixture.Should().Contain("cherry");
        _ = fixture.Should().NotContain("apple");
        _ = fixture.Should().NotContain("apricot");
        _ = fixture.Should().NotContain("avocado");
    }

    /// <summary>RemoveMany should return zero when no items match predicate.</summary>
    [Test]
    public void RemoveMany_ShouldReturnZeroWhenNoMatch()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        var removed = fixture.RemoveMany(static s => s.Length > 0 && s[0] == 'z');

        _ = removed.Should().Be(0);
        _ = fixture.Count.Should().Be(TestData.TestValueThree);
    }

    /// <summary>RemoveMany should throw ArgumentNullException for null predicate.</summary>
    [Test]
    public void RemoveMany_ShouldThrowForNullPredicate()
    {
        ReactiveList<string> fixture = ["one", "two"];

        var action = () => fixture.RemoveMany(null!);

        _ = action.Should().Throw<ArgumentNullException>();
    }

    /// <summary>RemoveMany should raise property changed events.</summary>
    [Test]
    public void RemoveMany_ShouldRaisePropertyChanged()
    {
        ReactiveList<int> fixture =
        [
            1,
            TestData.TestValueTwo,
            TestData.TestValueThree,
            TestData.TestValueFour,
            TestData.TestValueFive,
            TestData.TestValueSix,
            TestData.TestValueSeven,
            TestData.TestValueEight,
            TestData.TestValueNine,
            TestData.TestValueTen
        ];
        var countChanges = 0;
        fixture.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != TestData.CountPropertyName)
            {
                return;
            }

            countChanges++;
        };

        var removed = fixture.RemoveMany(static x => x % TestData.TestValueTwo == 0);

        _ = removed.Should().Be(TestData.TestValueFive);
        _ = countChanges.Should().Be(1);
        _ = fixture.Should().BeEquivalentTo([1, TestData.TestValueThree, TestData.TestValueFive, TestData.TestValueSeven, TestData.TestValueNine]);
    }

    /// <summary>RemoveMany should emit change notification via Connect.</summary>
    [Test]
    public void RemoveMany_ShouldEmitChangeNotification()
    {
        using var fixture = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = fixture.Connect().Subscribe(receivedChanges.Add);
        receivedChanges.Clear();

        var removed = fixture.RemoveMany(static x => x > TestData.TestValueThree);

        _ = removed.Should().Be(TestData.TestValueTwo);
        _ = receivedChanges.Should().HaveCount(1);
        _ = receivedChanges[0].Removes.Should().Be(TestData.TestValueTwo);
    }

    /// <summary>RemoveMany should work with complex types.</summary>
    [Test]
    public void RemoveMany_ShouldWorkWithComplexTypes()
    {
        ReactiveList<TestData> fixture =
        [
            new(TestData.AliceName, TestData.TestValueTwentyFive),
            new("Bob", TestData.TestValueThirty),
            new(TestData.CharlieName, TestData.TestValueThirtyFive),
            new("Diana", TestData.TestValueForty)
        ];

        var removed = fixture.RemoveMany(static p => p.Age >= TestData.TestValueThirtyFive);

        _ = removed.Should().Be(TestData.TestValueTwo);
        _ = fixture.Count.Should().Be(TestData.TestValueTwo);
        _ = fixture.Should().Contain(static p => p.Name == TestData.AliceName);
        _ = fixture.Should().Contain(static p => p.Name == "Bob");
    }
}
