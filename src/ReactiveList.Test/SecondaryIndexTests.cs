// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER || NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CP.Primitives.Core;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for SecondaryIndex.</summary>
public class SecondaryIndexTests
{
    /// <summary>OnAdded should add item to index.</summary>
    [Test]
    public void OnAdded_ShouldAddItemToIndex()
    {
        var index = new SecondaryIndex<Person, string>(p => p.Department);
        var person = new Person(1, "John", "Engineering");

        index.OnAdded(person);

        index.Lookup("Engineering").Should().Contain(person);
    }

    /// <summary>OnAdded should add multiple items with same key.</summary>
    [Test]
    public void OnAdded_WithSameKey_ShouldAddMultipleItems()
    {
        var index = new SecondaryIndex<Person, string>(p => p.Department);
        var person1 = new Person(1, "John", "Engineering");
        var person2 = new Person(2, "Jane", "Engineering");

        index.OnAdded(person1);
        index.OnAdded(person2);

        var result = index.Lookup("Engineering").ToList();
        result.Should().HaveCount(2);
        result.Should().Contain(person1);
        result.Should().Contain(person2);
    }

    /// <summary>OnAdded should handle items with different keys.</summary>
    [Test]
    public void OnAdded_WithDifferentKeys_ShouldIndexSeparately()
    {
        var index = new SecondaryIndex<Person, string>(p => p.Department);
        var person1 = new Person(1, "John", "Engineering");
        var person2 = new Person(2, "Jane", "Sales");

        index.OnAdded(person1);
        index.OnAdded(person2);

        index.Lookup("Engineering").Should().ContainSingle().Which.Should().Be(person1);
        index.Lookup("Sales").Should().ContainSingle().Which.Should().Be(person2);
    }

    /// <summary>OnRemoved should remove item from index.</summary>
    [Test]
    public void OnRemoved_ShouldRemoveItemFromIndex()
    {
        var index = new SecondaryIndex<Person, string>(p => p.Department);
        var person = new Person(1, "John", "Engineering");
        index.OnAdded(person);

        index.OnRemoved(person);

        index.Lookup("Engineering").Should().BeEmpty();
    }

    /// <summary>OnRemoved should only remove specified item.</summary>
    [Test]
    public void OnRemoved_ShouldOnlyRemoveSpecifiedItem()
    {
        var index = new SecondaryIndex<Person, string>(p => p.Department);
        var person1 = new Person(1, "John", "Engineering");
        var person2 = new Person(2, "Jane", "Engineering");
        index.OnAdded(person1);
        index.OnAdded(person2);

        index.OnRemoved(person1);

        var result = index.Lookup("Engineering").ToList();
        result.Should().ContainSingle().Which.Should().Be(person2);
    }

    /// <summary>OnRemoved should handle non-existing item gracefully.</summary>
    [Test]
    public void OnRemoved_WithNonExistingItem_ShouldNotThrow()
    {
        var index = new SecondaryIndex<Person, string>(p => p.Department);
        var person = new Person(1, "John", "Engineering");

        var act = () => index.OnRemoved(person);

        act.Should().NotThrow();
    }

    /// <summary>OnUpdated should update index with new key.</summary>
    [Test]
    public void OnUpdated_ShouldUpdateIndex()
    {
        var index = new SecondaryIndex<Person, string>(p => p.Department);
        var oldPerson = new Person(1, "John", "Engineering");
        var newPerson = new Person(1, "John", "Sales");
        index.OnAdded(oldPerson);

        index.OnUpdated(oldPerson, newPerson);

        index.Lookup("Engineering").Should().BeEmpty();
        index.Lookup("Sales").Should().ContainSingle().Which.Should().Be(newPerson);
    }

    /// <summary>Lookup should return empty for non-existing key.</summary>
    [Test]
    public void Lookup_WithNonExistingKey_ShouldReturnEmpty()
    {
        var index = new SecondaryIndex<Person, string>(p => p.Department);

        var result = index.Lookup("NonExisting");

        result.Should().BeEmpty();
    }

    /// <summary>Clear should remove all items.</summary>
    [Test]
    public void Clear_ShouldRemoveAllItems()
    {
        var index = new SecondaryIndex<Person, string>(p => p.Department);
        index.OnAdded(new Person(1, "John", "Engineering"));
        index.OnAdded(new Person(2, "Jane", "Sales"));
        index.OnAdded(new Person(3, "Bob", "Marketing"));

        index.Clear();

        index.Lookup("Engineering").Should().BeEmpty();
        index.Lookup("Sales").Should().BeEmpty();
        index.Lookup("Marketing").Should().BeEmpty();
    }

    /// <summary>Index should handle integer keys.</summary>
    [Test]
    public void Index_WithIntegerKey_ShouldWork()
    {
        var index = new SecondaryIndex<Person, int>(p => p.Id);
        var person = new Person(42, "John", "Engineering");

        index.OnAdded(person);

        index.Lookup(42).Should().ContainSingle().Which.Should().Be(person);
    }

    /// <summary>Index should distribute items across shards.</summary>
    [Test]
    public void Index_ShouldDistributeAcrossShards()
    {
        var index = new SecondaryIndex<Person, string>(p => p.Department);

        // Add many items with different keys to ensure shard distribution
        for (var i = 0; i < 100; i++)
        {
            index.OnAdded(new Person(i, $"Person{i}", $"Dept{i}"));
        }

        // Verify all items can be looked up
        for (var i = 0; i < 100; i++)
        {
            index.Lookup($"Dept{i}").Should().ContainSingle();
        }
    }

    /// <summary>Index should be thread-safe for concurrent adds.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Index_ShouldBeThreadSafeForConcurrentAdds()
    {
        var index = new SecondaryIndex<Person, string>(p => p.Department);
        var tasks = new List<Task>();

        for (var i = 0; i < 100; i++)
        {
            var id = i;
            tasks.Add(Task.Run(() => index.OnAdded(new Person(id, $"Person{id}", "Engineering"))));
        }

        await Task.WhenAll(tasks.ToArray());

        index.Lookup("Engineering").Should().HaveCount(100);
    }

    /// <summary>Index should be thread-safe for concurrent removes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Index_ShouldBeThreadSafeForConcurrentRemoves()
    {
        var index = new SecondaryIndex<Person, string>(p => p.Department);
        var persons = Enumerable.Range(0, 100)
            .Select(i => new Person(i, $"Person{i}", "Engineering"))
            .ToList();

        foreach (var person in persons)
        {
            index.OnAdded(person);
        }

        var tasks = persons.Select(p => Task.Run(() => index.OnRemoved(p))).ToArray();
        await Task.WhenAll(tasks);

        index.Lookup("Engineering").Should().BeEmpty();
    }

    /// <summary>Provides Person.</summary>
    /// <param name="Id">The Id value.</param>
    /// <param name="Name">The Name value.</param>
    /// <param name="Department">The Department value.</param>
    private sealed record Person(int Id, string Name, string Department);
}
#endif
