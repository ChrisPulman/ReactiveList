// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using CP.Reactive;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Reactive2DListTests.
/// </summary>
public class Reactive2DListTests
{
    /// <summary>
    /// Constructors the should initialize empty list.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeEmptyList()
    {
        var list = new Reactive2DList<int>();
        Assert.Empty(list);
        list.Dispose();
    }

    /// <summary>
    /// Constructors the should initialize with items.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithItems()
    {
        var items = new List<List<int>> { new() { 1, 2 }, new() { 3, 4 } };
        var list = new Reactive2DList<int>(items);
        Assert.Equal(2, list.Count);
        Assert.Equal(2, list[0].Count);
        Assert.Equal(2, list[1].Count);
        list.Dispose();
    }

    /// <summary>
    /// Constructors the should initialize with reactive lists.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithReactiveLists()
    {
        var items = new List<ReactiveList<int>> { new() { 1, 2 }, new() { 3, 4 } };
        var list = new Reactive2DList<int>(items);
        Assert.Equal(2, list.Count);
        Assert.Equal(2, list[0].Count);
        Assert.Equal(2, list[1].Count);
        list.Dispose();
    }

    /// <summary>
    /// Constructors the should initialize with single item.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithSingleItem()
    {
        var list = new Reactive2DList<int>(5);
        Assert.Single(list);
        Assert.Single(list[0]);
        Assert.Equal(5, list[0][0]);
        list.Dispose();
    }

    /// <summary>
    /// Constructors the should initialize with items.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithReactiveList()
    {
        var items = new ReactiveList<int> { 1, 2, 3, 4 };
        var list = new Reactive2DList<int>(items);
        Assert.Single(list);
        Assert.Equal(4, list[0].Count);
        list.Dispose();
    }

    /// <summary>
    /// Adds the range should add items.
    /// </summary>
    [Fact]
    public void AddRange_ShouldAddItems()
    {
        var list = new Reactive2DList<int>();
        var items = new List<List<int>> { new() { 1, 2 }, new() { 3, 4 } };
        list.AddRange(items);
        Assert.Equal(2, list.Count);
        list.Dispose();
    }

    /// <summary>
    /// Adds the range should add single items.
    /// </summary>
    [Fact]
    public void AddRange_ShouldAddSingleItems()
    {
        var list = new Reactive2DList<int>();
        var items = new List<int> { 1, 2, 3, 4 };
        list.AddRange(items);
        Assert.Equal(4, list.Count);
        list.Dispose();
    }

    /// <summary>
    /// Adds the index of the range should insert items at.
    /// </summary>
    [Fact]
    public void AddRange_ShouldInsertItemsAtIndex()
    {
        var list = new Reactive2DList<int>(new List<int> { 5 });
        var items = new List<List<int>> { new() { 1, 2 }, new() { 3, 4 } };
        list.AddRange(items);
        Assert.Equal(3, list.Count);
        Assert.Equal(5, list[0][0]);
        Assert.Equal(2, list[1].Count);
        Assert.Equal(2, list[1][1]);
        Assert.Equal(2, list[2].Count);
        Assert.Equal(4, list[2][1]);
        list.Dispose();
    }

    /// <summary>
    /// Inserts the index of the should insert items at.
    /// </summary>
    [Fact]
    public void Insert_ShouldInsertItemsAtIndex()
    {
        var list = new Reactive2DList<int>(new List<int> { 5 });
        var items = new List<int> { 1, 2, 3, 4 };
        list.Insert(0, items);
        Assert.Equal(2, list.Count);
        Assert.Equal(1, list[0][0]);
        Assert.Equal(4, list[0][3]);
        Assert.Equal(5, list[1][0]);
        list.Dispose();
    }

    /// <summary>
    /// Inserts the index of the should insert single item at.
    /// </summary>
    [Fact]
    public void Insert_ShouldInsertSingleItemAtIndex()
    {
        var list = new Reactive2DList<int>(new List<int> { 5 });
        list.Insert(0, 10);
        Assert.Equal(2, list.Count);
        Assert.Equal(10, list[0][0]);
        list.Dispose();
    }

    /// <summary>
    /// Inserts the index of the should insert reactive list at.
    /// </summary>
    [Fact]
    public void Insert_ShouldInsertReactiveListAtIndex()
    {
        var list = new Reactive2DList<int>(new List<int> { 5 });
        var reactiveList = new ReactiveList<int> { 1, 2, 3, 4 };
        list.Insert(0, reactiveList);
        Assert.Equal(2, list.Count);
        Assert.Equal(1, list[0][0]);
        list.Dispose();
    }

    /// <summary>
    /// Inserts the index of the should insert items in reactive list at.
    /// </summary>
    [Fact]
    public void Insert_ShouldInsertItemsInReactiveListAtIndex()
    {
        var list = new Reactive2DList<int>(new List<int> { 5 });
        var items = new List<int> { 1, 2, 3, 4 };
        list.Insert(0, items, 0);
        Assert.Equal(5, list[0].Count);
        Assert.Equal(1, list[0][0]);
        Assert.Equal(4, list[0][3]);
        Assert.Equal(5, list[0][4]);
        list.Dispose();
    }
}
