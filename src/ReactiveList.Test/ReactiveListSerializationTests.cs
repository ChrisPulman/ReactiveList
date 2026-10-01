// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.Serialization;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Collections;
#else
using CP.Primitives.Collections;
#endif
using TUnit.Assertions;
using TUnit.Core;
using static ReactiveList.Test.TestData;

namespace ReactiveList.Test;

/// <summary>Tests for ReactiveList serialization.</summary>
public class ReactiveListSerializationTests
{
    /// <summary>ReactiveList should be serializable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReactiveList_ShouldBeSerializable()
    {
        var list = new ReactiveList<string>();
        list.AddRange(["one", "two", "three"]);

        var deserialized = RoundTrip(list);

        await Assert.That(deserialized.Count).IsEqualTo(TestValueThree);
        await Assert.That(deserialized[0]).IsEqualTo("one");
        await Assert.That(deserialized[1]).IsEqualTo("two");
        await Assert.That(deserialized[TestValueTwo]).IsEqualTo("three");
    }

    /// <summary>Deserialized ReactiveList should work normally.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeserializedReactiveList_ShouldWorkNormally()
    {
        var list = new ReactiveList<int>();
        list.AddRange([1, TestValueTwo, TestValueThree]);

        var deserialized = RoundTrip(list);

        // Test that we can add items
        deserialized.Add(TestValueFour);
        await Assert.That(deserialized.Count).IsEqualTo(TestValueFour);

        // Test that Items property works
        await Assert.That(deserialized.Items).IsEquivalentTo([1, TestValueTwo, TestValueThree, TestValueFour]);

        // Test that observables work
        var addedItems = Array.Empty<int>();
        using var subscription = deserialized.Added.Subscribe(items => addedItems = [.. items]);

        deserialized.Add(TestValueFive);
        await Assert.That(addedItems).IsEquivalentTo([TestValueFive]);
    }

    /// <summary>Deserialized ReactiveList should support remove operations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeserializedReactiveList_ShouldSupportRemoveOperations()
    {
        var list = new ReactiveList<string>();
        list.AddRange(["a", "b", "c"]);

        var deserialized = RoundTrip(list);

        _ = deserialized.Remove("b");
        await Assert.That(deserialized.Count).IsEqualTo(TestValueTwo);
        await Assert.That(deserialized.Items).IsEquivalentTo(["a", "c"]);
    }

    /// <summary>Deserialized ReactiveList should support clear operations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeserializedReactiveList_ShouldSupportClearOperations()
    {
        var list = new ReactiveList<int>();
        list.AddRange([1, TestValueTwo, TestValueThree, TestValueFour, TestValueFive]);

        var deserialized = RoundTrip(list);

        deserialized.Clear();
        await Assert.That(deserialized.Count).IsEqualTo(0);
        await Assert.That(deserialized.Items).IsEmpty();
    }

    /// <summary>Empty ReactiveList should be serializable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyReactiveList_ShouldBeSerializable()
    {
        var list = new ReactiveList<string>();

        var deserialized = RoundTrip(list);

        await Assert.That(deserialized.Count).IsEqualTo(0);
        await Assert.That(deserialized.Items).IsEmpty();
    }

    /// <summary>ReactiveList with complex types should be serializable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReactiveListWithComplexTypes_ShouldBeSerializable()
    {
        var list = new ReactiveList<TestData> { new("Alice", TestValueThirty), new("Bob", TestValueTwentyFive) };

        var deserialized = RoundTrip(list);

        await Assert.That(deserialized.Count).IsEqualTo(TestValueTwo);
        await Assert.That(deserialized[0].Name).IsEqualTo("Alice");
        await Assert.That(deserialized[0].Age).IsEqualTo(TestValueThirty);
        await Assert.That(deserialized[1].Name).IsEqualTo("Bob");
        await Assert.That(deserialized[1].Age).IsEqualTo(TestValueTwentyFive);
    }

    /// <summary>Round-trips a value through the .NET Framework serializer.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to round-trip.</param>
    /// <returns>The deserialized value.</returns>
    private static T RoundTrip<T>(T value)
    {
        var serializer = new DataContractSerializer(typeof(T));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, value);
        stream.Position = 0;

        var deserialized = serializer.ReadObject(stream);
        if (deserialized is T typed)
        {
            return typed;
        }

        throw new InvalidOperationException($"Expected a deserialized {typeof(T).FullName} instance.");
    }
}
