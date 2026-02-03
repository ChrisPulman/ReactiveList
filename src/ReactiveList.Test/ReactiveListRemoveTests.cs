// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using CP.Reactive;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// ReactiveList Remove Tests.
/// </summary>
public class ReactiveListRemoveTests
{
    /// <summary>
    /// Remove should remove existing item for string type.
    /// </summary>
    [Fact]
    public void Remove_ShouldRemoveExistingItem_String()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        var result = fixture.Remove("two");

        result.Should().BeTrue();
        fixture.Count.Should().Be(2);
        fixture.Should().Contain("one");
        fixture.Should().Contain("three");
        fixture.Should().NotContain("two");
    }

    /// <summary>
    /// Remove should return false for non-existing item for string type.
    /// </summary>
    [Fact]
    public void Remove_ShouldReturnFalseForNonExistingItem_String()
    {
        ReactiveList<string> fixture = ["one", "two"];

        var result = fixture.Remove("three");

        result.Should().BeFalse();
        fixture.Count.Should().Be(2);
    }

    /// <summary>
    /// Remove should raise property changed for string type.
    /// </summary>
    [Fact]
    public void Remove_ShouldRaisePropertyChanged_String()
    {
        ReactiveList<string> fixture = ["one", "two"];
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

        fixture.Remove("two");

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>
    /// Remove should remove existing item for int type.
    /// </summary>
    [Fact]
    public void Remove_ShouldRemoveExistingItem_Int()
    {
        ReactiveList<int> fixture = [1, 2, 3];

        var result = fixture.Remove(2);

        result.Should().BeTrue();
        fixture.Count.Should().Be(2);
        fixture.Should().Contain(1);
        fixture.Should().Contain(3);
        fixture.Should().NotContain(2);
    }

    /// <summary>
    /// Remove should return false for non-existing item for int type.
    /// </summary>
    [Fact]
    public void Remove_ShouldReturnFalseForNonExistingItem_Int()
    {
        ReactiveList<int> fixture = [1, 2];

        var result = fixture.Remove(3);

        result.Should().BeFalse();
        fixture.Count.Should().Be(2);
    }

    /// <summary>
    /// Remove should raise property changed for int type.
    /// </summary>
    [Fact]
    public void Remove_ShouldRaisePropertyChanged_Int()
    {
        ReactiveList<int> fixture = [1, 2];
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

        fixture.Remove(2);

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>
    /// Remove should remove existing item for TestData type.
    /// </summary>
    [Fact]
    public void Remove_ShouldRemoveExistingItem_TestData()
    {
        ReactiveList<TestData> fixture = [new("Alice", 25), new("Bob", 30), new("Charlie", 35)];
        var itemToRemove = fixture[1];
        var result = fixture.Remove(itemToRemove);

        result.Should().BeTrue();
        fixture.Count.Should().Be(2);
        fixture.Should().Contain(d => d.Name == "Alice");
        fixture.Should().Contain(d => d.Name == "Charlie");
        fixture.Should().NotContain(d => d.Name == "Bob");
    }

    /// <summary>
    /// Remove should return false for non-existing item for TestData type.
    /// </summary>
    [Fact]
    public void Remove_ShouldReturnFalseForNonExistingItem_TestData()
    {
        ReactiveList<TestData> fixture = [new("Alice", 25), new("Bob", 30)];

        var result = fixture.Remove(new TestData("Charlie", 35));

        result.Should().BeFalse();
        fixture.Count.Should().Be(2);
    }

    /// <summary>
    /// Remove should raise property changed for TestData type.
    /// </summary>
    [Fact]
    public void Remove_ShouldRaisePropertyChanged_TestData()
    {
        ReactiveList<TestData> fixture = [new("Alice", 25), new("Bob", 30)];
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

        var itemToRemove = fixture[1];
        fixture.Remove(itemToRemove);

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>
    /// RemoveAt should remove item at index for string type.
    /// </summary>
    [Fact]
    public void RemoveAt_ShouldRemoveItemAtIndex_String()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        fixture.RemoveAt(1);

        fixture.Count.Should().Be(2);
        fixture[0].Should().Be("one");
        fixture[1].Should().Be("three");
    }

    /// <summary>
    /// RemoveAt should throw for invalid index for string type.
    /// </summary>
    [Fact]
    public void RemoveAt_ShouldThrowForInvalidIndex_String()
    {
        ReactiveList<string> fixture = ["one", "two"];

        var action = () => fixture.RemoveAt(5);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// RemoveAt should raise property changed for string type.
    /// </summary>
    [Fact]
    public void RemoveAt_ShouldRaisePropertyChanged_String()
    {
        ReactiveList<string> fixture = ["one", "two"];
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

        fixture.RemoveAt(1);

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>
    /// RemoveAt should remove item at index for int type.
    /// </summary>
    [Fact]
    public void RemoveAt_ShouldRemoveItemAtIndex_Int()
    {
        ReactiveList<int> fixture = [1, 2, 3];

        fixture.RemoveAt(1);

        fixture.Count.Should().Be(2);
        fixture[0].Should().Be(1);
        fixture[1].Should().Be(3);
    }

    /// <summary>
    /// RemoveAt should throw for invalid index for int type.
    /// </summary>
    [Fact]
    public void RemoveAt_ShouldThrowForInvalidIndex_Int()
    {
        ReactiveList<int> fixture = [1, 2];

        var action = () => fixture.RemoveAt(5);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// RemoveAt should raise property changed for int type.
    /// </summary>
    [Fact]
    public void RemoveAt_ShouldRaisePropertyChanged_Int()
    {
        ReactiveList<int> fixture = [1, 2];
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

        fixture.RemoveAt(1);

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>
    /// RemoveAt should remove item at index for TestData type.
    /// </summary>
    [Fact]
    public void RemoveAt_ShouldRemoveItemAtIndex_TestData()
    {
        ReactiveList<TestData> fixture = [new("Alice", 25), new("Bob", 30), new("Charlie", 35)];

        fixture.RemoveAt(1);

        fixture.Count.Should().Be(2);
        fixture[0].Name.Should().Be("Alice");
        fixture[1].Name.Should().Be("Charlie");
    }

    /// <summary>
    /// RemoveAt should throw for invalid index for TestData type.
    /// </summary>
    [Fact]
    public void RemoveAt_ShouldThrowForInvalidIndex_TestData()
    {
        ReactiveList<TestData> fixture = [new("Alice", 25), new("Bob", 30)];

        var action = () => fixture.RemoveAt(5);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// RemoveAt should raise property changed for TestData type.
    /// </summary>
    [Fact]
    public void RemoveAt_ShouldRaisePropertyChanged_TestData()
    {
        ReactiveList<TestData> fixture = [new("Alice", 25), new("Bob", 30)];
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

        fixture.RemoveAt(1);

        countChanges.Should().Be(1);
        itemArrayChanges.Should().Be(1);
    }

    /// <summary>
    /// RemoveMany should remove items matching predicate for string type.
    /// </summary>
    [Fact]
    public void RemoveMany_ShouldRemoveMatchingItems_String()
    {
        ReactiveList<string> fixture = ["apple", "banana", "apricot", "cherry", "avocado"];

        var removed = fixture.RemoveMany(s => s.StartsWith("a"));

        removed.Should().Be(3);
        fixture.Count.Should().Be(2);
        fixture.Should().Contain("banana");
        fixture.Should().Contain("cherry");
        fixture.Should().NotContain("apple");
        fixture.Should().NotContain("apricot");
        fixture.Should().NotContain("avocado");
    }

    /// <summary>
    /// RemoveMany should return zero when no items match predicate.
    /// </summary>
    [Fact]
    public void RemoveMany_ShouldReturnZeroWhenNoMatch()
    {
        ReactiveList<string> fixture = ["one", "two", "three"];

        var removed = fixture.RemoveMany(s => s.StartsWith("z"));

        removed.Should().Be(0);
        fixture.Count.Should().Be(3);
    }

    /// <summary>
    /// RemoveMany should throw ArgumentNullException for null predicate.
    /// </summary>
    [Fact]
    public void RemoveMany_ShouldThrowForNullPredicate()
    {
        ReactiveList<string> fixture = ["one", "two"];

        var action = () => fixture.RemoveMany(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// RemoveMany should raise property changed events.
    /// </summary>
    [Fact]
    public void RemoveMany_ShouldRaisePropertyChanged()
    {
        ReactiveList<int> fixture = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        var countChanges = 0;
        fixture.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "Count")
            {
                countChanges++;
            }
        };

        var removed = fixture.RemoveMany(x => x % 2 == 0);

        removed.Should().Be(5);
        countChanges.Should().Be(1);
        fixture.Should().BeEquivalentTo([1, 3, 5, 7, 9]);
    }

    /// <summary>
    /// RemoveMany should emit change notification via Connect.
    /// </summary>
    [Fact]
    public void RemoveMany_ShouldEmitChangeNotification()
    {
        using var fixture = new ReactiveList<int>([1, 2, 3, 4, 5]);
        var receivedChanges = new System.Collections.Generic.List<ChangeSet<int>>();
        using var subscription = fixture.Connect().Subscribe(cs => receivedChanges.Add(cs));

        var removed = fixture.RemoveMany(x => x > 3);

        removed.Should().Be(2);
        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Removes.Should().Be(2);
    }

    /// <summary>
    /// RemoveMany should work with complex types.
    /// </summary>
    [Fact]
    public void RemoveMany_ShouldWorkWithComplexTypes()
    {
        ReactiveList<TestData> fixture =
        [
            new("Alice", 25),
            new("Bob", 30),
            new("Charlie", 35),
            new("Diana", 40)
        ];

        var removed = fixture.RemoveMany(p => p.Age >= 35);

        removed.Should().Be(2);
        fixture.Count.Should().Be(2);
        fixture.Should().Contain(p => p.Name == "Alice");
        fixture.Should().Contain(p => p.Name == "Bob");
    }
}
