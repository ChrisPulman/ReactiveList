// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
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

        result.Should().BeTrue();
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.Should().Contain("one");
        fixture.Should().Contain(TestData.ThreeText);
        fixture.Should().NotContain("two");
    }

    /// <summary>Remove should return false for non-existing item for string type.</summary>
    [Test]
    public void Remove_ShouldReturnFalseForNonExistingItem_String()
    {
        ReactiveList<string> fixture = ["one", "two"];

        var result = fixture.Remove(TestData.ThreeText);

        result.Should().BeFalse();
        fixture.Count.Should().Be(TestData.TestValueTwo);
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

        fixture.Remove("two");

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>Remove should remove existing item for int type.</summary>
    [Test]
    public void Remove_ShouldRemoveExistingItem_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo, TestData.TestValueThree];

        var result = fixture.Remove(TestData.TestValueTwo);

        result.Should().BeTrue();
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.Should().Contain(1);
        fixture.Should().Contain(TestData.TestValueThree);
        fixture.Should().NotContain(TestData.TestValueTwo);
    }

    /// <summary>Remove should return false for non-existing item for int type.</summary>
    [Test]
    public void Remove_ShouldReturnFalseForNonExistingItem_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo];

        var result = fixture.Remove(TestData.TestValueThree);

        result.Should().BeFalse();
        fixture.Count.Should().Be(TestData.TestValueTwo);
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

        fixture.Remove(TestData.TestValueTwo);

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>Remove should remove existing item for TestData type.</summary>
    [Test]
    public void Remove_ShouldRemoveExistingItem_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty), new(TestData.CharlieName, TestData.TestValueThirtyFive)];
        var itemToRemove = fixture[1];
        var result = fixture.Remove(itemToRemove);

        result.Should().BeTrue();
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.Should().Contain(d => d.Name == TestData.AliceName);
        fixture.Should().Contain(d => d.Name == TestData.CharlieName);
        fixture.Should().NotContain(d => d.Name == "Bob");
    }

    /// <summary>Remove should return false for non-existing item for TestData type.</summary>
    [Test]
    public void Remove_ShouldReturnFalseForNonExistingItem_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty)];

        var result = fixture.Remove(new TestData(TestData.CharlieName, TestData.TestValueThirtyFive));

        result.Should().BeFalse();
        fixture.Count.Should().Be(TestData.TestValueTwo);
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
        fixture.Remove(itemToRemove);

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>RemoveAt should remove item at index for string type.</summary>
    [Test]
    public void RemoveAt_ShouldRemoveItemAtIndex_String()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.RemoveAt(1);

        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be(TestData.ThreeText);
    }

    /// <summary>RemoveAt should throw for invalid index for string type.</summary>
    [Test]
    public void RemoveAt_ShouldThrowForInvalidIndex_String()
    {
        ReactiveList<string> fixture = ["one", "two"];

        var action = () => fixture.RemoveAt(TestData.TestValueFive);

        action.Should().Throw<ArgumentOutOfRangeException>();
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

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>RemoveAt should remove item at index for int type.</summary>
    [Test]
    public void RemoveAt_ShouldRemoveItemAtIndex_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo, TestData.TestValueThree];

        fixture.RemoveAt(1);

        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture[0].Should().Be(1);
        fixture[1].Should().Be(TestData.TestValueThree);
    }

    /// <summary>RemoveAt should throw for invalid index for int type.</summary>
    [Test]
    public void RemoveAt_ShouldThrowForInvalidIndex_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo];

        var action = () => fixture.RemoveAt(TestData.TestValueFive);

        action.Should().Throw<ArgumentOutOfRangeException>();
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

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>RemoveAt should remove item at index for TestData type.</summary>
    [Test]
    public void RemoveAt_ShouldRemoveItemAtIndex_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty), new(TestData.CharlieName, TestData.TestValueThirtyFive)];

        fixture.RemoveAt(1);

        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture[0].Name.Should().Be(TestData.AliceName);
        fixture[1].Name.Should().Be(TestData.CharlieName);
    }

    /// <summary>RemoveAt should throw for invalid index for TestData type.</summary>
    [Test]
    public void RemoveAt_ShouldThrowForInvalidIndex_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty)];

        var action = () => fixture.RemoveAt(TestData.TestValueFive);

        action.Should().Throw<ArgumentOutOfRangeException>();
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

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>RemoveMany should remove items matching predicate for string type.</summary>
    [Test]
    public void RemoveMany_ShouldRemoveMatchingItems_String()
    {
        ReactiveList<string> fixture = ["apple", "banana", "apricot", "cherry", "avocado"];

        var removed = fixture.RemoveMany(s => s.StartsWith("a"));

        removed.Should().Be(TestData.TestValueThree);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.Should().Contain("banana");
        fixture.Should().Contain("cherry");
        fixture.Should().NotContain("apple");
        fixture.Should().NotContain("apricot");
        fixture.Should().NotContain("avocado");
    }

    /// <summary>RemoveMany should return zero when no items match predicate.</summary>
    [Test]
    public void RemoveMany_ShouldReturnZeroWhenNoMatch()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        var removed = fixture.RemoveMany(s => s.StartsWith("z"));

        removed.Should().Be(0);
        fixture.Count.Should().Be(TestData.TestValueThree);
    }

    /// <summary>RemoveMany should throw ArgumentNullException for null predicate.</summary>
    [Test]
    public void RemoveMany_ShouldThrowForNullPredicate()
    {
        ReactiveList<string> fixture = ["one", "two"];

        var action = () => fixture.RemoveMany(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    /// <summary>RemoveMany should raise property changed events.</summary>
    [Test]
    public void RemoveMany_ShouldRaisePropertyChanged()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive, TestData.TestValueSix, TestData.TestValueSeven, TestData.TestValueEight, TestData.TestValueNine, TestData.TestValueTen];
        var countChanges = 0;
        fixture.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != TestData.CountPropertyName)
            {
                return;
            }

            countChanges++;
        };

        var removed = fixture.RemoveMany(x => x % TestData.TestValueTwo == 0);

        removed.Should().Be(TestData.TestValueFive);
        countChanges.Should().Be(1);
        fixture.Should().BeEquivalentTo([1, TestData.TestValueThree, TestData.TestValueFive, TestData.TestValueSeven, TestData.TestValueNine]);
    }

    /// <summary>RemoveMany should emit change notification via Connect.</summary>
    [Test]
    public void RemoveMany_ShouldEmitChangeNotification()
    {
        using var fixture = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
        var receivedChanges = new System.Collections.Generic.List<ChangeSet<int>>();
        using var subscription = fixture.Connect().Subscribe(receivedChanges.Add);
        receivedChanges.Clear();

        var removed = fixture.RemoveMany(x => x > TestData.TestValueThree);

        removed.Should().Be(TestData.TestValueTwo);
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Removes.Should().Be(TestData.TestValueTwo);
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

        var removed = fixture.RemoveMany(p => p.Age >= TestData.TestValueThirtyFive);

        removed.Should().Be(TestData.TestValueTwo);
        fixture.Count.Should().Be(TestData.TestValueTwo);
        fixture.Should().Contain(p => p.Name == TestData.AliceName);
        fixture.Should().Contain(p => p.Name == "Bob");
    }
}
