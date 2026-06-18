// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using CP.Primitives;
using CP.Primitives.Collections;
using DynamicData;

namespace ReactiveList.Benchmarks;

/// <summary>Provides ListBenchmarks.</summary>
[MemoryDiagnoser]
public class ListBenchmarks
{
    private int[] _data = [];

    /// <summary>Gets or sets the item count.</summary>
    [Params(100, 1_000, 10_000)]
    public int Count { get; set; }

    /// <summary>Provides Setup.</summary>
    [GlobalSetup]
    public void Setup() => _data = Enumerable.Range(0, Count).ToArray();

    /// <summary>Provides List_AddRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_AddRange()
    {
        var list = new List<int>();
        list.AddRange(_data);
        return list.Count;
    }

    /// <summary>Provides ReactiveList_AddRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int ReactiveList_AddRange()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(_data);
        return list.Count;
    }

    /// <summary>Provides SourceList_AddRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_AddRange()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        return list.Count;
    }

    /// <summary>Provides List_RemoveRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_RemoveRange()
    {
        var list = new List<int>(_data);
        list.RemoveRange(0, Count / 2);
        return list.Count;
    }

    /// <summary>Provides ReactiveList_RemoveRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int ReactiveList_RemoveRange()
    {
        using var list = new ReactiveList<int>(_data);
        list.RemoveRange(0, Count / 2);
        return list.Count;
    }

    /// <summary>Provides SourceList_RemoveRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_RemoveRange()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        list.Edit(l => l.RemoveRange(0, Count / 2));
        return list.Count;
    }

    /// <summary>Provides List_Clear.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_Clear()
    {
        var list = new List<int>(_data);
        list.Clear();
        return list.Count;
    }

    /// <summary>Provides ReactiveList_Clear.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int ReactiveList_Clear()
    {
        using var list = new ReactiveList<int>(_data);
        list.Clear();
        return list.Count;
    }

    /// <summary>Provides SourceList_Clear.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Clear()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        list.Clear();
        return list.Count;
    }

    /// <summary>Provides List_Search.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool List_Search()
    {
        var list = new List<int>(_data);
        return list.Contains(Count - 1);
    }

    /// <summary>Provides ReactiveList_Search.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool ReactiveList_Search()
    {
        using var list = new ReactiveList<int>(_data);
        return list.Contains(Count - 1);
    }

    /// <summary>Provides SourceList_Search.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool SourceList_Search()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        return list.Items.Contains(Count - 1);
    }

    /// <summary>Provides ReactiveList_Add_WithObserver.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int ReactiveList_Add_WithObserver()
    {
        using var list = new ReactiveList<int>();
        var total = 0;
        using var sub = list.Added.SubscribeObserver(items => total += items.Count());
        list.AddRange(_data);
        return total;
    }

    /// <summary>Provides SourceList_Add_WithObserver.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Add_WithObserver()
    {
        using var list = new SourceList<int>();
        var total = 0;
        using var sub = list.Connect().SubscribeObserver(changes => total += changes.TotalChanges);
        list.AddRange(_data);
        return total;
    }

    /// <summary>Provides List_Filter.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_Filter()
    {
        var list = new List<int>(_data);
        return list.Where(x => x % 2 == 0).Count();
    }

    /// <summary>Provides ReactiveList_Filter.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int ReactiveList_Filter()
    {
        using var list = new ReactiveList<int>(_data);
        return list.Where(x => x % 2 == 0).Count();
    }

    /// <summary>Provides SourceList_Filter.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Filter()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        return list.Items.Where(x => x % 2 == 0).Count();
    }

    /// <summary>Provides ReactiveList_Connect.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int ReactiveList_Connect()
    {
        using var list = new ReactiveList<int>();
        var total = 0;
        using var sub = list.Connect().SubscribeObserver(changes => total += changes.Count);
        list.AddRange(_data);
        return total;
    }

    /// <summary>Provides SourceList_Connect.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Connect()
    {
        using var list = new SourceList<int>();
        var total = 0;
        using var sub = list.Connect().SubscribeObserver(changes => total += changes.TotalChanges);
        list.AddRange(_data);
        return total;
    }

    /// <summary>Provides ReactiveList_Connect_Preloaded.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int ReactiveList_Connect_Preloaded()
    {
        using var list = new ReactiveList<int>(_data);
        var total = 0;
        using var sub = list.Connect().SubscribeObserver(changes => total += changes.Count);
        return total;
    }

    /// <summary>Provides SourceList_Connect_Preloaded.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Connect_Preloaded()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        var total = 0;
        using var sub = list.Connect().SubscribeObserver(changes => total += changes.TotalChanges);
        return total;
    }

    /// <summary>Provides ReactiveList_ReplaceAll.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int ReactiveList_ReplaceAll()
    {
        using var list = new ReactiveList<int>(_data);
        var newData = Enumerable.Range(Count, Count).ToArray();
        list.ReplaceAll(newData);
        return list.Count;
    }

    /// <summary>Provides SourceList_ReplaceAll.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_ReplaceAll()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        var newData = Enumerable.Range(Count, Count).ToArray();
        list.Edit(innerList =>
        {
            innerList.Clear();
            innerList.AddRange(newData);
        });
        return list.Count;
    }

    /// <summary>Provides ReactiveList_Move.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int ReactiveList_Move()
    {
        using var list = new ReactiveList<int>(_data);
        list.Move(0, Count / 2);
        return list.Count;
    }

    /// <summary>Provides SourceList_Move.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Move()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        list.Move(0, Count / 2);
        return list.Count;
    }

    /// <summary>Provides ReactiveList_RemoveMany.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int ReactiveList_RemoveMany()
    {
        using var list = new ReactiveList<int>(_data);
        list.RemoveMany(x => x % 2 == 0);
        return list.Count;
    }

    /// <summary>Provides SourceList_RemoveMany.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_RemoveMany()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        list.RemoveMany(list.Items.Where(x => x % 2 == 0));
        return list.Count;
    }
}
