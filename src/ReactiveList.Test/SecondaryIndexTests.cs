// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER || NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Core;
#else
using CP.Primitives.Core;
#endif
using TUnit.Assertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for SecondaryIndex.</summary>
public class SecondaryIndexTests
{
    /// <summary>OnAdded should add item to index.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnAdded_ShouldAddItemToIndex()
    {
        var index = new SecondaryIndex<Person, string>(static p => p.Department);
        var person = new Person(1, "John", TestData.EngineeringDepartment);

        index.OnAdded(person);

        await Assert.That(index.Lookup(TestData.EngineeringDepartment)).Contains(person);
    }

    /// <summary>OnAdded should add multiple items with same key.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnAdded_WithSameKey_ShouldAddMultipleItems()
    {
        var index = new SecondaryIndex<Person, string>(static p => p.Department);
        var person1 = new Person(1, "John", TestData.EngineeringDepartment);
        var person2 = new Person(TestData.TestValueTwo, "Jane", TestData.EngineeringDepartment);

        index.OnAdded(person1);
        index.OnAdded(person2);

        var count = 0;
        var containsFirst = false;
        var containsSecond = false;
        foreach (var person in index.Lookup(TestData.EngineeringDepartment))
        {
            count++;
            containsFirst |= person == person1;
            containsSecond |= person == person2;
        }

        await Assert.That(count).IsEqualTo(TestData.TestValueTwo);
        await Assert.That(containsFirst).IsTrue();
        await Assert.That(containsSecond).IsTrue();
    }

    /// <summary>OnAdded should handle items with different keys.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnAdded_WithDifferentKeys_ShouldIndexSeparately()
    {
        var index = new SecondaryIndex<Person, string>(static p => p.Department);
        var person1 = new Person(1, "John", TestData.EngineeringDepartment);
        var person2 = new Person(TestData.TestValueTwo, "Jane", TestData.SalesDepartment);

        index.OnAdded(person1);
        index.OnAdded(person2);

        await Assert.That(await Assert.That(index.Lookup(TestData.EngineeringDepartment)).HasSingleItem()).IsEqualTo(person1);
        await Assert.That(await Assert.That(index.Lookup(TestData.SalesDepartment)).HasSingleItem()).IsEqualTo(person2);
    }

    /// <summary>OnRemoved should remove item from index.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnRemoved_ShouldRemoveItemFromIndex()
    {
        var index = new SecondaryIndex<Person, string>(static p => p.Department);
        var person = new Person(1, "John", TestData.EngineeringDepartment);
        index.OnAdded(person);

        index.OnRemoved(person);

        await Assert.That(index.Lookup(TestData.EngineeringDepartment)).IsEmpty();
    }

    /// <summary>OnRemoved should only remove specified item.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnRemoved_ShouldOnlyRemoveSpecifiedItem()
    {
        var index = new SecondaryIndex<Person, string>(static p => p.Department);
        var person1 = new Person(1, "John", TestData.EngineeringDepartment);
        var person2 = new Person(TestData.TestValueTwo, "Jane", TestData.EngineeringDepartment);
        index.OnAdded(person1);
        index.OnAdded(person2);

        index.OnRemoved(person1);

        var count = 0;
        Person? remainingPerson = null;
        foreach (var person in index.Lookup(TestData.EngineeringDepartment))
        {
            count++;
            remainingPerson = person;
        }

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(remainingPerson).IsEqualTo(person2);
    }

    /// <summary>OnRemoved should handle non-existing item gracefully.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnRemoved_WithNonExistingItem_ShouldNotThrow()
    {
        var index = new SecondaryIndex<Person, string>(static p => p.Department);
        var person = new Person(1, "John", TestData.EngineeringDepartment);

        var act = () => index.OnRemoved(person);

        await Assert.That(act).ThrowsNothing();
    }

    /// <summary>OnUpdated should update index with new key.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnUpdated_ShouldUpdateIndex()
    {
        var index = new SecondaryIndex<Person, string>(static p => p.Department);
        var oldPerson = new Person(1, "John", TestData.EngineeringDepartment);
        var newPerson = new Person(1, "John", TestData.SalesDepartment);
        index.OnAdded(oldPerson);

        index.OnUpdated(oldPerson, newPerson);

        await Assert.That(index.Lookup(TestData.EngineeringDepartment)).IsEmpty();
        await Assert.That(await Assert.That(index.Lookup(TestData.SalesDepartment)).HasSingleItem()).IsEqualTo(newPerson);
    }

    /// <summary>Lookup should return empty for non-existing key.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Lookup_WithNonExistingKey_ShouldReturnEmpty()
    {
        var index = new SecondaryIndex<Person, string>(static p => p.Department);

        var result = index.Lookup("NonExisting");

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Clear should remove all items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Clear_ShouldRemoveAllItems()
    {
        var index = new SecondaryIndex<Person, string>(static p => p.Department);
        index.OnAdded(new(1, "John", TestData.EngineeringDepartment));
        index.OnAdded(new(TestData.TestValueTwo, "Jane", TestData.SalesDepartment));
        index.OnAdded(new(TestData.TestValueThree, "Bob", "Marketing"));

        index.Clear();

        await Assert.That(index.Lookup(TestData.EngineeringDepartment)).IsEmpty();
        await Assert.That(index.Lookup(TestData.SalesDepartment)).IsEmpty();
        await Assert.That(index.Lookup("Marketing")).IsEmpty();
    }

    /// <summary>Index should handle integer keys.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Index_WithIntegerKey_ShouldWork()
    {
        var index = new SecondaryIndex<Person, int>(static p => p.Id);
        var person = new Person(TestData.TestValueFortyTwo, "John", TestData.EngineeringDepartment);

        index.OnAdded(person);

        await Assert.That(await Assert.That(index.Lookup(TestData.TestValueFortyTwo)).HasSingleItem()).IsEqualTo(person);
    }

    /// <summary>Index should distribute items across shards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Index_ShouldDistributeAcrossShards()
    {
        var index = new SecondaryIndex<Person, string>(static p => p.Department);

        // Add many items with different keys to ensure shard distribution
        for (var i = 0; i < TestData.TestValueOneHundred; i++)
        {
            index.OnAdded(new(i, $"Person{i}", $"Dept{i}"));
        }

        // Verify all items can be looked up
        for (var i = 0; i < TestData.TestValueOneHundred; i++)
        {
            await Assert.That(index.Lookup($"Dept{i}")).HasSingleItem();
        }
    }

    /// <summary>Index should be thread-safe for concurrent adds.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Index_ShouldBeThreadSafeForConcurrentAdds()
    {
        var index = new SecondaryIndex<Person, string>(static p => p.Department);
        var tasks = new List<Task>();

        for (var i = 0; i < TestData.TestValueOneHundred; i++)
        {
            var id = i;
            tasks.Add(Task.Run(() => index.OnAdded(new(id, $"Person{id}", TestData.EngineeringDepartment))));
        }

        await Task.WhenAll([.. tasks]);

        await Assert.That(index.Lookup(TestData.EngineeringDepartment)).Count().IsEqualTo(TestData.TestValueOneHundred);
    }

    /// <summary>Index should be thread-safe for concurrent removes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Index_ShouldBeThreadSafeForConcurrentRemoves()
    {
        var index = new SecondaryIndex<Person, string>(static p => p.Department);
        var persons = new List<Person>(TestData.TestValueOneHundred);
        for (var i = 0; i < TestData.TestValueOneHundred; i++)
        {
            persons.Add(new(i, $"Person{i}", TestData.EngineeringDepartment));
        }

        foreach (var person in persons)
        {
            index.OnAdded(person);
        }

        var tasks = new Task[persons.Count];
        for (var i = 0; i < persons.Count; i++)
        {
            var person = persons[i];
            tasks[i] = Task.Run(() => index.OnRemoved(person));
        }

        await Task.WhenAll(tasks);

        await Assert.That(index.Lookup(TestData.EngineeringDepartment)).IsEmpty();
    }

    /// <summary>Provides Person.</summary>
    /// <param name="Id">The Id value.</param>
    /// <param name="Name">The Name value.</param>
    /// <param name="Department">The Department value.</param>
    private sealed record Person(int Id, string Name, string Department);
}
#endif
