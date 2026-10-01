// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
#if REACTIVELIST_REACTIVE
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
#else
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
#endif
using TUnit.Assertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>ReactiveList Remove Tests.</summary>
public class ReactiveListRemoveTests
{
    /// <summary>Remove should remove existing item for string type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_ShouldRemoveExistingItem_String()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        var result = fixture.Remove("two");

        await Assert.That(result).IsTrue();
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture).Contains("one");
        await Assert.That(fixture).Contains(TestData.ThreeText);
        await Assert.That(fixture).DoesNotContain("two");
    }

    /// <summary>Remove should return false for non-existing item for string type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_ShouldReturnFalseForNonExistingItem_String()
    {
        ReactiveList<string> fixture = ["one", "two"];

        var result = fixture.Remove(TestData.ThreeText);

        await Assert.That(result).IsFalse();
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
    }

    /// <summary>Remove should raise property changed for string type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_ShouldRaisePropertyChanged_String()
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

        await Assert.That(countChanges).IsEqualTo(1);
        await Assert.That(itemArrayChanges).IsEqualTo(1);
    }

    /// <summary>Remove should remove existing item for int type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_ShouldRemoveExistingItem_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo, TestData.TestValueThree];

        var result = fixture.Remove(TestData.TestValueTwo);

        await Assert.That(result).IsTrue();
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture).Contains(1);
        await Assert.That(fixture).Contains(TestData.TestValueThree);
        await Assert.That(fixture).DoesNotContain(TestData.TestValueTwo);
    }

    /// <summary>Remove should return false for non-existing item for int type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_ShouldReturnFalseForNonExistingItem_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo];

        var result = fixture.Remove(TestData.TestValueThree);

        await Assert.That(result).IsFalse();
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
    }

    /// <summary>Remove should raise property changed for int type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_ShouldRaisePropertyChanged_Int()
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

        await Assert.That(countChanges).IsEqualTo(1);
        await Assert.That(itemArrayChanges).IsEqualTo(1);
    }

    /// <summary>Remove should remove existing item for TestData type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_ShouldRemoveExistingItem_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty), new(TestData.CharlieName, TestData.TestValueThirtyFive)];
        var itemToRemove = fixture[1];
        var result = fixture.Remove(itemToRemove);

        await Assert.That(result).IsTrue();
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture).Contains(static d => d.Name == TestData.AliceName);
        await Assert.That(fixture).Contains(static d => d.Name == TestData.CharlieName);
        await Assert.That(fixture).DoesNotContain(static d => d.Name == "Bob");
    }

    /// <summary>Remove should return false for non-existing item for TestData type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_ShouldReturnFalseForNonExistingItem_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty)];

        var result = fixture.Remove(new TestData(TestData.CharlieName, TestData.TestValueThirtyFive));

        await Assert.That(result).IsFalse();
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
    }

    /// <summary>Remove should raise property changed for TestData type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Remove_ShouldRaisePropertyChanged_TestData()
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

        await Assert.That(countChanges).IsEqualTo(1);
        await Assert.That(itemArrayChanges).IsEqualTo(1);
    }

    /// <summary>RemoveAt should remove item at index for string type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveAt_ShouldRemoveItemAtIndex_String()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        fixture.RemoveAt(1);

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture[0]).IsEqualTo("one");
        await Assert.That(fixture[1]).IsEqualTo(TestData.ThreeText);
    }

    /// <summary>RemoveAt should throw for invalid index for string type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveAt_ShouldThrowForInvalidIndex_String()
    {
        ReactiveList<string> fixture = ["one", "two"];

        var action = () => fixture.RemoveAt(TestData.TestValueFive);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>RemoveAt should raise property changed for string type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveAt_ShouldRaisePropertyChanged_String()
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

        await Assert.That(countChanges).IsEqualTo(1);
        await Assert.That(itemArrayChanges).IsEqualTo(1);
    }

    /// <summary>RemoveAt should remove item at index for int type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveAt_ShouldRemoveItemAtIndex_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo, TestData.TestValueThree];

        fixture.RemoveAt(1);

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture[0]).IsEqualTo(1);
        await Assert.That(fixture[1]).IsEqualTo(TestData.TestValueThree);
    }

    /// <summary>RemoveAt should throw for invalid index for int type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveAt_ShouldThrowForInvalidIndex_Int()
    {
        ReactiveList<int> fixture = [1, TestData.TestValueTwo];

        var action = () => fixture.RemoveAt(TestData.TestValueFive);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>RemoveAt should raise property changed for int type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveAt_ShouldRaisePropertyChanged_Int()
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

        await Assert.That(countChanges).IsEqualTo(1);
        await Assert.That(itemArrayChanges).IsEqualTo(1);
    }

    /// <summary>RemoveAt should remove item at index for TestData type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveAt_ShouldRemoveItemAtIndex_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty), new(TestData.CharlieName, TestData.TestValueThirtyFive)];

        fixture.RemoveAt(1);

        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture[0].Name).IsEqualTo(TestData.AliceName);
        await Assert.That(fixture[1].Name).IsEqualTo(TestData.CharlieName);
    }

    /// <summary>RemoveAt should throw for invalid index for TestData type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveAt_ShouldThrowForInvalidIndex_TestData()
    {
        ReactiveList<TestData> fixture = [new(TestData.AliceName, TestData.TestValueTwentyFive), new("Bob", TestData.TestValueThirty)];

        var action = () => fixture.RemoveAt(TestData.TestValueFive);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>RemoveAt should raise property changed for TestData type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveAt_ShouldRaisePropertyChanged_TestData()
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

        await Assert.That(countChanges).IsEqualTo(1);
        await Assert.That(itemArrayChanges).IsEqualTo(1);
    }

    /// <summary>RemoveMany should remove items matching predicate for string type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveMany_ShouldRemoveMatchingItems_String()
    {
        ReactiveList<string> fixture = ["apple", "banana", "apricot", "cherry", "avocado"];

        var removed = fixture.RemoveMany(static s => s.Length > 0 && s[0] == 'a');

        await Assert.That(removed).IsEqualTo(TestData.TestValueThree);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture).Contains("banana");
        await Assert.That(fixture).Contains("cherry");
        await Assert.That(fixture).DoesNotContain("apple");
        await Assert.That(fixture).DoesNotContain("apricot");
        await Assert.That(fixture).DoesNotContain("avocado");
    }

    /// <summary>RemoveMany should return zero when no items match predicate.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveMany_ShouldReturnZeroWhenNoMatch()
    {
        ReactiveList<string> fixture = ["one", "two", TestData.ThreeText];

        var removed = fixture.RemoveMany(static s => s.Length > 0 && s[0] == 'z');

        await Assert.That(removed).IsEqualTo(0);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueThree);
    }

    /// <summary>RemoveMany should throw ArgumentNullException for null predicate.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveMany_ShouldThrowForNullPredicate()
    {
        ReactiveList<string> fixture = ["one", "two"];

        var action = () => fixture.RemoveMany(null!);

        await Assert.That(action).Throws<ArgumentNullException>();
    }

    /// <summary>RemoveMany should raise property changed events.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveMany_ShouldRaisePropertyChanged()
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

        await Assert.That(removed).IsEqualTo(TestData.TestValueFive);
        await Assert.That(countChanges).IsEqualTo(1);
        await Assert.That(fixture).IsEquivalentTo([1, TestData.TestValueThree, TestData.TestValueFive, TestData.TestValueSeven, TestData.TestValueNine]);
    }

    /// <summary>RemoveMany should emit change notification via Connect.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveMany_ShouldEmitChangeNotification()
    {
        using var fixture = new ReactiveList<int>([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
        var receivedChanges = new List<ChangeSet<int>>();
        using var subscription = fixture.Connect().Subscribe(receivedChanges.Add);
        receivedChanges.Clear();

        var removed = fixture.RemoveMany(static x => x > TestData.TestValueThree);

        await Assert.That(removed).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(receivedChanges).Count().IsEqualTo(1);
        await Assert.That(receivedChanges[0].Removes).IsEqualTo(TestData.TestValueTwo);
    }

    /// <summary>RemoveMany should work with complex types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveMany_ShouldWorkWithComplexTypes()
    {
        ReactiveList<TestData> fixture =
        [
            new(TestData.AliceName, TestData.TestValueTwentyFive),
            new("Bob", TestData.TestValueThirty),
            new(TestData.CharlieName, TestData.TestValueThirtyFive),
            new("Diana", TestData.TestValueForty)
        ];

        var removed = fixture.RemoveMany(static p => p.Age >= TestData.TestValueThirtyFive);

        await Assert.That(removed).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture.Count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(fixture).Contains(static p => p.Name == TestData.AliceName);
        await Assert.That(fixture).Contains(static p => p.Name == "Bob");
    }
}
