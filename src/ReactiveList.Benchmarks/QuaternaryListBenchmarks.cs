// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using CP.Reactive.Collections;
using DynamicData;

namespace ReactiveList.Benchmarks;

/// <summary>Provides QuaternaryListBenchmarks.</summary>
[MemoryDiagnoser]
public class QuaternaryListBenchmarks
{
    private int[] _data = [];

    /// <summary>Gets or sets the item count.</summary>
    [Params(100, 1_000, 10_000)]
    public int Count { get; set; }

    /// <summary>Provides Setup.</summary>
    [GlobalSetup]
    public void Setup() => _data = Enumerable.Range(0, Count).ToArray();

    /// <summary>Provides List_Add.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_Add()
    {
        var list = new List<int>();
        for (var i = 0; i < Count; i++)
        {
            list.Add(i);
        }

        return list.Count;
    }

    /// <summary>Provides QuaternaryList_Add.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_Add()
    {
        using var list = new QuaternaryList<int>();
        for (var i = 0; i < Count; i++)
        {
            list.Add(i);
        }

        return list.Count;
    }

    /// <summary>Provides SourceList_Add.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Add()
    {
        using var list = new SourceList<int>();
        for (var i = 0; i < Count; i++)
        {
            list.Add(i);
        }

        return list.Count;
    }

    /// <summary>Provides List_AddRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_AddRange()
    {
        var list = new List<int>();
        list.AddRange(_data);
        return list.Count;
    }

    /// <summary>Provides QuaternaryList_AddRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_AddRange()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        return list.Count;
    }

    /// <summary>Provides SourceList_AddRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_AddRange()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
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

    /// <summary>Provides QuaternaryList_RemoveRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_RemoveRange()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.RemoveRange(_data.Take(Count / 2));
        return list.Count;
    }

    /// <summary>Provides SourceList_RemoveRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_RemoveRange()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        list.RemoveMany(_data.Take(Count / 2));
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

    /// <summary>Provides QuaternaryList_Clear.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_Clear()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.Clear();
        return list.Count;
    }

    /// <summary>Provides SourceList_Clear.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Clear()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        list.Clear();
        return list.Count;
    }

    /// <summary>Provides List_Contains.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool List_Contains()
    {
        var list = new List<int>(_data);
        return list.Contains(Count - 1);
    }

    /// <summary>Provides QuaternaryList_Contains.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool QuaternaryList_Contains()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        return list.Contains(Count - 1);
    }

    /// <summary>Provides SourceList_Contains.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool SourceList_Contains()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        return list.Items.Contains(Count - 1);
    }

    /// <summary>Provides QuaternaryList_QueryIndex.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_QueryIndex()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", x => x % 2);
        list.AddRange(_data);
        return list.GetItemsBySecondaryIndex("Mod2", 0).Count();
    }

    /// <summary>Provides QuaternaryList_Stream_Add.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_Stream_Add()
    {
        using var list = new QuaternaryList<int>();
        var events = 0;
        using var sub = list.Stream.SubscribeObserver(_ => events++);
        list.AddRange(_data);
        return events;
    }

    /// <summary>Provides SourceList_Stream_Add.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Stream_Add()
    {
        using var list = new SourceList<int>();
        var events = 0;
        using var sub = list.Connect().SubscribeObserver(_ => events++);
        list.AddRange(_data);
        return events;
    }

    /// <summary>Provides QuaternaryList_Edit.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_Edit()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.Edit(innerList =>
        {
            innerList.Clear();
            for (var i = 0; i < Count; i++)
            {
                innerList.Add(i * 2);
            }
        });
        return list.Count;
    }

    /// <summary>Provides SourceList_Edit.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Edit()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        list.Edit(innerList =>
        {
            innerList.Clear();
            for (var i = 0; i < Count; i++)
            {
                innerList.Add(i * 2);
            }
        });
        return list.Count;
    }

    /// <summary>Provides QuaternaryList_RemoveMany.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_RemoveMany()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.RemoveMany(x => x % 2 == 0);
        return list.Count;
    }

    /// <summary>Provides SourceList_RemoveMany.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_RemoveMany()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        list.RemoveMany(list.Items.Where(x => x % 2 == 0));
        return list.Count;
    }

    /// <summary>Provides QuaternaryList_VersionTracking.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public long QuaternaryList_VersionTracking()
    {
        using var list = new QuaternaryList<int>();
        var initialVersion = list.Version;
        list.AddRange(_data);
        list.RemoveMany(x => x % 2 == 0);
        list.Clear();
        return list.Version - initialVersion;
    }

    /// <summary>Provides QuaternaryList_MultipleIndices.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_MultipleIndices()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", x => x % 2);
        list.AddIndex("Mod3", x => x % 3);
        list.AddIndex("Mod5", x => x % 5);
        list.AddRange(_data);
        return list.GetItemsBySecondaryIndex("Mod2", 0).Count() +
               list.GetItemsBySecondaryIndex("Mod3", 0).Count() +
               list.GetItemsBySecondaryIndex("Mod5", 0).Count();
    }

    /// <summary>Provides QuaternaryList_ParallelAdd.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_ParallelAdd()
    {
        using var list = new QuaternaryList<int>();

        // Large dataset to trigger parallel processing (threshold is 256)
        var largeData = Enumerable.Range(0, Math.Max(Count, 500)).ToArray();
        list.AddRange(largeData);
        return list.Count;
    }

    /// <summary>Provides QuaternaryList_IterateAll.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_IterateAll()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        var sum = 0;
        foreach (var item in list)
        {
            sum += item;
        }

        return sum;
    }

    /// <summary>Provides List_IterateAll.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_IterateAll()
    {
        var list = new List<int>(_data);
        var sum = 0;
        foreach (var item in list)
        {
            sum += item;
        }

        return sum;
    }

    /// <summary>Provides SourceList_IterateAll.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_IterateAll()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        var sum = 0;
        foreach (var item in list.Items)
        {
            sum += item;
        }

        return sum;
    }

    /// <summary>Provides QuaternaryList_CopyTo.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_CopyTo()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        var buffer = new int[Count];
        list.CopyTo(buffer, 0);
        return buffer.Length;
    }

    /// <summary>Provides QuaternaryList_ReplaceAll.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_ReplaceAll()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
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
        list.AddRange(_data);
        var newData = Enumerable.Range(Count, Count).ToArray();
        list.Edit(innerList =>
        {
            innerList.Clear();
            innerList.AddRange(newData);
        });
        return list.Count;
    }

    /// <summary>Provides List_Remove.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_Remove()
    {
        var list = new List<int>(_data);
        for (var i = 0; i < Count / 2; i++)
        {
            list.Remove(i);
        }

        return list.Count;
    }

    /// <summary>Provides QuaternaryList_Remove.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_Remove()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        for (var i = 0; i < Count / 2; i++)
        {
            list.Remove(i);
        }

        return list.Count;
    }

    /// <summary>Provides SourceList_Remove.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Remove()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        for (var i = 0; i < Count / 2; i++)
        {
            list.Remove(i);
        }

        return list.Count;
    }

    /// <summary>Provides List_RemoveAll.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_RemoveAll()
    {
        var list = new List<int>(_data);
        list.RemoveAll(x => x % 2 == 0);
        return list.Count;
    }

    /// <summary>Provides List_IndexerAccess.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_IndexerAccess()
    {
        var list = new List<int>(_data);
        var sum = 0;
        for (var i = 0; i < list.Count; i++)
        {
            sum += list[i];
        }

        return sum;
    }

    /// <summary>Provides QuaternaryList_IndexerAccess.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_IndexerAccess()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        var sum = 0;
        for (var i = 0; i < list.Count; i++)
        {
            sum += list[i];
        }

        return sum;
    }

    /// <summary>Provides List_ReplaceAll.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_ReplaceAll()
    {
        var list = new List<int>(_data);
        var newData = Enumerable.Range(Count, Count).ToArray();
        list.Clear();
        list.AddRange(newData);
        return list.Count;
    }

    /// <summary>Provides QuaternaryList_Stream_Remove.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_Stream_Remove()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        var events = 0;
        using var sub = list.Stream.SubscribeObserver(_ => events++);
        list.RemoveRange(_data.Take(Count / 2));
        return events;
    }

    /// <summary>Provides SourceList_Stream_Remove.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Stream_Remove()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        var events = 0;
        using var sub = list.Connect().SubscribeObserver(_ => events++);
        list.RemoveMany(_data.Take(Count / 2));
        return events;
    }

    /// <summary>Provides QuaternaryList_AddIndex.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_AddIndex()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.AddIndex("Mod2", x => x % 2);
        return list.Count;
    }

    /// <summary>Provides QuaternaryList_ItemMatchesSecondaryIndex.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool QuaternaryList_ItemMatchesSecondaryIndex()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", x => x % 2);
        list.AddRange(_data);
        return list.ItemMatchesSecondaryIndex("Mod2", 4, 0);
    }

    /// <summary>Provides QuaternaryList_IndexWithAddRemove.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_IndexWithAddRemove()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", x => x % 2);
        list.AddRange(_data);
        list.RemoveMany(x => x % 4 == 0);
        return list.GetItemsBySecondaryIndex("Mod2", 0).Count();
    }

    /// <summary>Provides List_CopyTo.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_CopyTo()
    {
        var list = new List<int>(_data);
        var buffer = new int[Count];
        list.CopyTo(buffer, 0);
        return buffer.Length;
    }

    /// <summary>Provides List_Count.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_Count()
    {
        var list = new List<int>(_data);
        return list.Count;
    }

    /// <summary>Provides QuaternaryList_Count.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_Count()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        return list.Count;
    }

    /// <summary>Provides SourceList_Count.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Count()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        return list.Count;
    }

    /// <summary>Provides QuaternaryList_MixedOperations.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_MixedOperations()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.Add(Count);
        list.Remove(0);
        list.RemoveMany(x => x % 10 == 0);
        return list.Count;
    }

    /// <summary>Provides SourceList_MixedOperations.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_MixedOperations()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        list.Add(Count);
        list.Remove(0);
        list.RemoveMany(list.Items.Where(x => x % 10 == 0));
        return list.Count;
    }

    /// <summary>Provides List_MixedOperations.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int List_MixedOperations()
    {
        var list = new List<int>(_data);
        list.Add(Count);
        list.Remove(0);
        list.RemoveAll(x => x % 10 == 0);
        return list.Count;
    }
}
