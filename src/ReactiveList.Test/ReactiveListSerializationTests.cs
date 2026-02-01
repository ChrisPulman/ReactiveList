// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// BinaryFormatter is only supported on .NET Framework
#if NET48
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using CP.Reactive;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Tests for ReactiveList serialization.
/// </summary>
public class ReactiveListSerializationTests
{
#pragma warning disable SYSLIB0011 // BinaryFormatter is obsolete - needed for testing serialization
    /// <summary>
    /// ReactiveList should be serializable.
    /// </summary>
    [Fact]
    public void ReactiveList_ShouldBeSerializable()
    {
        var list = new ReactiveList<string>();
        list.AddRange(["one", "two", "three"]);

        var formatter = new BinaryFormatter();
        using var stream = new MemoryStream();

        formatter.Serialize(stream, list);

        stream.Position = 0;
        var deserialized = (ReactiveList<string>)formatter.Deserialize(stream);

        deserialized.Count.Should().Be(3);
        deserialized[0].Should().Be("one");
        deserialized[1].Should().Be("two");
        deserialized[2].Should().Be("three");
    }

    /// <summary>
    /// Deserialized ReactiveList should work normally.
    /// </summary>
    [Fact]
    public void DeserializedReactiveList_ShouldWorkNormally()
    {
        var list = new ReactiveList<int>();
        list.AddRange([1, 2, 3]);

        var formatter = new BinaryFormatter();
        using var stream = new MemoryStream();
        formatter.Serialize(stream, list);
        stream.Position = 0;
        var deserialized = (ReactiveList<int>)formatter.Deserialize(stream);

        // Test that we can add items
        deserialized.Add(4);
        deserialized.Count.Should().Be(4);

        // Test that Items property works
        deserialized.Items.Should().BeEquivalentTo([1, 2, 3, 4]);

        // Test that observables work
        var addedItems = Array.Empty<int>();
        deserialized.Added.Subscribe(items => addedItems = items.ToArray());

        deserialized.Add(5);
        addedItems.Should().BeEquivalentTo([5]);
    }

    /// <summary>
    /// Deserialized ReactiveList should support remove operations.
    /// </summary>
    [Fact]
    public void DeserializedReactiveList_ShouldSupportRemoveOperations()
    {
        var list = new ReactiveList<string>();
        list.AddRange(["a", "b", "c"]);

        var formatter = new BinaryFormatter();
        using var stream = new MemoryStream();
        formatter.Serialize(stream, list);
        stream.Position = 0;
        var deserialized = (ReactiveList<string>)formatter.Deserialize(stream);

        deserialized.Remove("b");
        deserialized.Count.Should().Be(2);
        deserialized.Items.Should().BeEquivalentTo(["a", "c"]);
    }

    /// <summary>
    /// Deserialized ReactiveList should support clear operations.
    /// </summary>
    [Fact]
    public void DeserializedReactiveList_ShouldSupportClearOperations()
    {
        var list = new ReactiveList<int>();
        list.AddRange([1, 2, 3, 4, 5]);

        var formatter = new BinaryFormatter();
        using var stream = new MemoryStream();
        formatter.Serialize(stream, list);
        stream.Position = 0;
        var deserialized = (ReactiveList<int>)formatter.Deserialize(stream);

        deserialized.Clear();
        deserialized.Count.Should().Be(0);
        deserialized.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Empty ReactiveList should be serializable.
    /// </summary>
    [Fact]
    public void EmptyReactiveList_ShouldBeSerializable()
    {
        var list = new ReactiveList<string>();

        var formatter = new BinaryFormatter();
        using var stream = new MemoryStream();
        formatter.Serialize(stream, list);
        stream.Position = 0;
        var deserialized = (ReactiveList<string>)formatter.Deserialize(stream);

        deserialized.Count.Should().Be(0);
        deserialized.Items.Should().BeEmpty();
    }

    /// <summary>
    /// ReactiveList with complex types should be serializable.
    /// </summary>
    [Fact]
    public void ReactiveListWithComplexTypes_ShouldBeSerializable()
    {
        var list = new ReactiveList<TestData>();
        list.Add(new TestData("Alice", 30));
        list.Add(new TestData("Bob", 25));

        var formatter = new BinaryFormatter();
        using var stream = new MemoryStream();
        formatter.Serialize(stream, list);
        stream.Position = 0;
        var deserialized = (ReactiveList<TestData>)formatter.Deserialize(stream);

        deserialized.Count.Should().Be(2);
        deserialized[0].Name.Should().Be("Alice");
        deserialized[0].Age.Should().Be(30);
        deserialized[1].Name.Should().Be("Bob");
        deserialized[1].Age.Should().Be(25);
    }
#pragma warning restore SYSLIB0011
}
#endif
