// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if NET48
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using CP.Primitives.Collections;
using FluentAssertions;
using TUnit.Core;
using static ReactiveList.Test.TestData;

namespace ReactiveList.Test;

/// <summary>Tests for ReactiveList serialization.</summary>
public class ReactiveListSerializationTests
{
    /// <summary>ReactiveList should be serializable.</summary>
    [Test]
    public void ReactiveList_ShouldBeSerializable()
    {
        var list = new ReactiveList<string>();
        list.AddRange(["one", "two", "three"]);

        var deserialized = RoundTrip(list);

        deserialized.Count.Should().Be(TestValueThree);
        deserialized[0].Should().Be("one");
        deserialized[1].Should().Be("two");
        deserialized[TestValueTwo].Should().Be("three");
    }

    /// <summary>Deserialized ReactiveList should work normally.</summary>
    [Test]
    public void DeserializedReactiveList_ShouldWorkNormally()
    {
        var list = new ReactiveList<int>();
        list.AddRange([1, TestValueTwo, TestValueThree]);

        var deserialized = RoundTrip(list);

        // Test that we can add items
        deserialized.Add(TestValueFour);
        deserialized.Count.Should().Be(TestValueFour);

        // Test that Items property works
        deserialized.Items.Should().BeEquivalentTo([1, TestValueTwo, TestValueThree, TestValueFour]);

        // Test that observables work
        var addedItems = Array.Empty<int>();
        deserialized.Added.Subscribe(items => addedItems = items.ToArray());

        deserialized.Add(TestValueFive);
        addedItems.Should().BeEquivalentTo([TestValueFive]);
    }

    /// <summary>Deserialized ReactiveList should support remove operations.</summary>
    [Test]
    public void DeserializedReactiveList_ShouldSupportRemoveOperations()
    {
        var list = new ReactiveList<string>();
        list.AddRange(["a", "b", "c"]);

        var deserialized = RoundTrip(list);

        deserialized.Remove("b");
        deserialized.Count.Should().Be(TestValueTwo);
        deserialized.Items.Should().BeEquivalentTo(["a", "c"]);
    }

    /// <summary>Deserialized ReactiveList should support clear operations.</summary>
    [Test]
    public void DeserializedReactiveList_ShouldSupportClearOperations()
    {
        var list = new ReactiveList<int>();
        list.AddRange([1, TestValueTwo, TestValueThree, TestValueFour, TestValueFive]);

        var deserialized = RoundTrip(list);

        deserialized.Clear();
        deserialized.Count.Should().Be(0);
        deserialized.Items.Should().BeEmpty();
    }

    /// <summary>Empty ReactiveList should be serializable.</summary>
    [Test]
    public void EmptyReactiveList_ShouldBeSerializable()
    {
        var list = new ReactiveList<string>();

        var deserialized = RoundTrip(list);

        deserialized.Count.Should().Be(0);
        deserialized.Items.Should().BeEmpty();
    }

    /// <summary>ReactiveList with complex types should be serializable.</summary>
    [Test]
    public void ReactiveListWithComplexTypes_ShouldBeSerializable()
    {
        var list = new ReactiveList<TestData>
        {
            new("Alice", TestValueThirty),
            new("Bob", TestValueTwentyFive)
        };

        var deserialized = RoundTrip(list);

        deserialized.Count.Should().Be(TestValueTwo);
        deserialized[0].Name.Should().Be("Alice");
        deserialized[0].Age.Should().Be(TestValueThirty);
        deserialized[1].Name.Should().Be("Bob");
        deserialized[1].Age.Should().Be(TestValueTwentyFive);
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
#endif
