// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using CP.Reactive;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// ReactiveList Add Tests.
/// </summary>
public class ReactiveListAddTests
{
    /// <summary>
    /// Determines whether this instance [can add array item].
    /// </summary>
    [Fact]
    public void CanAddArrayItem()
    {
        ReactiveList<string> fixture = new();
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(new string[] { "one", "two" });
        fixture.Count.Should().Be(2);
    }

    /// <summary>
    /// Determines whether this instance [can add complex array item].
    /// </summary>
    [Fact]
    public void CanAddComplexArrayItem()
    {
        ReactiveList<TestData> fixture = new();
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(new TestData[] { new("Celine", 5), new("Clarence", 5), new("Clifford", 5) });
        fixture.Count.Should().Be(3);
    }

    /// <summary>
    /// Determines whether this instance [can add multiple single complex items].
    /// </summary>
    [Fact]
    public void CanAddMultipleSingleComplexItems()
    {
        ReactiveList<TestData> fixture = new();
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add(new TestData("Celine", 5));
        fixture.Count.Should().Be(1);
        fixture.Add(new TestData("Clarence", 5));
        fixture.Count.Should().Be(2);
        fixture.Add(new TestData("Clifford", 5));
        fixture.Count.Should().Be(3);
    }

    /// <summary>
    /// Determines whether this instance [can add multiple single complex items and edit].
    /// </summary>
    [Fact]
    public void CanAddMultipleSingleComplexItemsAndEdit()
    {
        ReactiveList<string> fixture = new();
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add("Celine");
        fixture.Count.Should().Be(1);
        fixture.Add("Clarence");
        fixture.Count.Should().Be(2);
        fixture.Add("Cliffordddd");
        fixture.Count.Should().Be(3);
        fixture.Update(fixture.Items[2], "Clifford");
        fixture.Count.Should().Be(3);
    }

    /// <summary>
    /// Determines whether this instance [can add multiple single items].
    /// </summary>
    [Fact]
    public void CanAddMultipleSingleItems()
    {
        ReactiveList<string> fixture = new();
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add("one");
        fixture.Count.Should().Be(1);
        fixture.Add("two");
        fixture.Count.Should().Be(2);
        fixture.Add("three");
        fixture.Count.Should().Be(3);
    }

    /// <summary>
    /// Determines whether this instance [can add single complex item].
    /// </summary>
    [Fact]
    public void CanAddSingleComplexItem()
    {
        ReactiveList<TestData> fixture = new();
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add(new TestData("Chris", 44));
        fixture.Count.Should().Be(1);
    }

    /// <summary>
    /// Determines whether this instance [can add single item].
    /// </summary>
    [Fact]
    public void CanAddSingleItem()
    {
        ReactiveList<string> fixture = new();
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add("one");
        fixture.Count.Should().Be(1);
    }

    /// <summary>
    /// Determines whether this instance [can clear and add item].
    /// </summary>
    [Fact]
    public void CanClearAndAddItem()
    {
        ReactiveList<string> fixture = new();
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.AddRange(new string[] { "one", "two" });
        fixture.Count.Should().Be(2);
        fixture.ItemsAdded.Count.Should().Be(2);
        fixture.ItemsChanged.Count.Should().Be(2);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be("one");
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(2);
        fixture.ItemsRemoved.Count.Should().Be(2);
        fixture.Add("three");
        fixture.Count.Should().Be(1);
        fixture.ItemsAdded.Count.Should().Be(1);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(0);
        fixture.Items[0].Should().Be("three");
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.ItemsAdded.Count.Should().Be(0);
        fixture.ItemsChanged.Count.Should().Be(1);
        fixture.ItemsRemoved.Count.Should().Be(1);
    }

    /// <summary>
    /// Determines whether this instance [can observe add array of item asynchronous].
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CanObserveAddArrayOfItemAsync()
    {
        ReactiveList<string> fixture = new();
        var a = false;
        fixture.Added.Subscribe(items =>
        {
            items.Count().Should().Be(2);
            a = true;
        });
        fixture.Count.Should().Be(0);
        fixture.AddRange(new string[] { "one", "two" });
        fixture.Count.Should().Be(2);
        while (!a)
        {
            await Task.Delay(1);
        }
    }

    /// <summary>
    /// Determines whether this instance [can observe add single item asynchronous].
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CanObserveAddSingleItemAsync()
    {
        ReactiveList<string> fixture = new();
        var a = false;
        fixture.Added.Subscribe(items =>
        {
            items.Count().Should().Be(1);
            a = true;
        });
        fixture.Clear();
        fixture.Count.Should().Be(0);
        fixture.Add("one");
        fixture.Count.Should().Be(1);
        while (!a)
        {
            await Task.Delay(1);
        }
    }
}
