// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using CP.Primitives.Collections;
using DynamicData;

namespace ReactiveList.Benchmarks;

/// <summary>Benchmarks <see cref="QuaternaryList{T}"/> operations against comparable list implementations.</summary>
[MemoryDiagnoser]
public sealed class QuaternaryListBenchmarks
{
    /// <summary>Divides a count in half for partial-removal benchmarks.</summary>
    private const int HalfCountDivisor = 2;

    /// <summary>Identifies the value used to probe a secondary index.</summary>
    private const int IndexedProbeItem = 4;

    /// <summary>Specifies the minimum size needed to exercise the parallel add path.</summary>
    private const int MinimumLargeDatasetCount = 500;

    /// <summary>Provides the divisor used by modulo-four operations.</summary>
    private const int ModuloFourDivisor = 4;

    /// <summary>Provides the divisor used by modulo-three operations.</summary>
    private const int ModuloThreeDivisor = 3;

    /// <summary>Provides the divisor used by modulo-two operations.</summary>
    private const int ModuloTwoDivisor = 2;

    /// <summary>Provides the divisor used by modulo-five operations.</summary>
    private const int ModuloFiveDivisor = 5;

    /// <summary>Controls how frequently items are removed in mixed-operation benchmarks.</summary>
    private const int PeriodicRemovalDivisor = 10;

    /// <summary>Provides the multiplier used when benchmark values are updated.</summary>
    private const int ValueMultiplier = 2;

    /// <summary>Stores the sequential input shared by list benchmarks.</summary>
    private int[] _data = [];

    /// <summary>Gets or sets the number of input items used by each benchmark.</summary>
    [Params(100, 1_000, 10_000)]
    public int Count { get; set; }

    /// <summary>Creates the benchmark input data.</summary>
    [GlobalSetup]
    public void Setup() => _data = CreateSequentialData(0, Count);

    /// <summary>Benchmarks adding items individually to <see cref="List{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
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

    /// <summary>Benchmarks adding items individually to <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
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

    /// <summary>Benchmarks adding items individually to <see cref="SourceList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
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

    /// <summary>Benchmarks adding the input data to <see cref="List{T}"/> in one operation.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int List_AddRange()
    {
        var list = new List<int>(Count);
        list.AddRange(_data);
        return list.Count;
    }

    /// <summary>Benchmarks adding the input data to <see cref="QuaternaryList{T}"/> in one operation.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_AddRange()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        return list.Count;
    }

    /// <summary>Benchmarks adding the input data to <see cref="SourceList{T}"/> in one operation.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int SourceList_AddRange()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        return list.Count;
    }

    /// <summary>Benchmarks removing the first half of a <see cref="List{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int List_RemoveRange()
    {
        var list = new List<int>(_data);
        list.RemoveRange(0, Count / HalfCountDivisor);
        return list.Count;
    }

    /// <summary>Benchmarks removing the first half of a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_RemoveRange()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.RemoveRange(CreateFirstHalfData());
        return list.Count;
    }

    /// <summary>Benchmarks removing the first half of a <see cref="SourceList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int SourceList_RemoveRange()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        list.RemoveMany(CreateFirstHalfData());
        return list.Count;
    }

    /// <summary>Benchmarks clearing a <see cref="List{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int List_Clear()
    {
        var list = new List<int>(_data);
        list.Clear();
        return list.Count;
    }

    /// <summary>Benchmarks clearing a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_Clear()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.Clear();
        return list.Count;
    }

    /// <summary>Benchmarks clearing a <see cref="SourceList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int SourceList_Clear()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        list.Clear();
        return list.Count;
    }

    /// <summary>Benchmarks finding the final input item in a <see cref="List{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public bool List_Contains()
    {
        var list = new List<int>(_data);
        return list.Contains(Count - 1);
    }

    /// <summary>Benchmarks finding the final input item in a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public bool QuaternaryList_Contains()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        return list.Contains(Count - 1);
    }

    /// <summary>Benchmarks finding the final input item in a <see cref="SourceList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public bool SourceList_Contains()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        foreach (var item in list.Items)
        {
            if (item == Count - 1)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Benchmarks querying a secondary index in a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_QueryIndex()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", static x => x % ModuloTwoDivisor);
        list.AddRange(_data);
        return CountItems(list.GetItemsBySecondaryIndex("Mod2", 0));
    }

    /// <summary>Benchmarks stream notifications generated by adding input data to a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_Stream_Add()
    {
        using var list = new QuaternaryList<int>();
        var events = 0;
        using var sub = list.Stream.SubscribeObserver(_ => events++);
        list.AddRange(_data);
        return events;
    }

    /// <summary>Benchmarks stream notifications generated by adding input data to a <see cref="SourceList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int SourceList_Stream_Add()
    {
        using var list = new SourceList<int>();
        var events = 0;
        using var sub = list.Connect().SubscribeObserver(_ => events++);
        list.AddRange(_data);
        return events;
    }

    /// <summary>Benchmarks replacing all items through <see cref="QuaternaryList{T}.Edit"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_Edit()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.Edit(ReplaceWithDoubledValues);
        return list.Count;
    }

    /// <summary>Benchmarks replacing all items through <see cref="SourceList{T}.Edit"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int SourceList_Edit()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        list.Edit(ReplaceWithDoubledValues);
        return list.Count;
    }

    /// <summary>Benchmarks removing even values from a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_RemoveMany()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        _ = list.RemoveMany(static x => x % ModuloTwoDivisor == 0);
        return list.Count;
    }

    /// <summary>Benchmarks removing even values from a <see cref="SourceList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int SourceList_RemoveMany()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        list.RemoveMany(FilterItems(list.Items, static x => x % ModuloTwoDivisor == 0));
        return list.Count;
    }

    /// <summary>Benchmarks version tracking across add, remove, and clear operations.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public long QuaternaryList_VersionTracking()
    {
        using var list = new QuaternaryList<int>();
        var initialVersion = list.Version;
        list.AddRange(_data);
        _ = list.RemoveMany(static x => x % ModuloTwoDivisor == 0);
        list.Clear();
        return list.Version - initialVersion;
    }

    /// <summary>Benchmarks querying several secondary indices in a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_MultipleIndices()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", static x => x % ModuloTwoDivisor);
        list.AddIndex("Mod3", static x => x % ModuloThreeDivisor);
        list.AddIndex("Mod5", static x => x % ModuloFiveDivisor);
        list.AddRange(_data);
        return CountItems(list.GetItemsBySecondaryIndex("Mod2", 0))
            + CountItems(list.GetItemsBySecondaryIndex("Mod3", 0))
            + CountItems(list.GetItemsBySecondaryIndex("Mod5", 0));
    }

    /// <summary>Benchmarks adding enough items to exercise parallel processing.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_ParallelAdd()
    {
        using var list = new QuaternaryList<int>();

        // Large dataset to trigger parallel processing (threshold is 256)
        var largeData = CreateSequentialData(0, Math.Max(Count, MinimumLargeDatasetCount));
        list.AddRange(largeData);
        return list.Count;
    }

    /// <summary>Benchmarks iterating every item in a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
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

    /// <summary>Benchmarks iterating every item in a <see cref="List{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
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

    /// <summary>Benchmarks iterating every item in a <see cref="SourceList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
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

    /// <summary>Benchmarks copying a <see cref="QuaternaryList{T}"/> to an array.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_CopyTo()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        var buffer = new int[Count];
        list.CopyTo(buffer, 0);
        return buffer.Length;
    }

    /// <summary>Benchmarks replacing all items in a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_ReplaceAll()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        var newData = CreateSequentialData(Count, Count);
        list.ReplaceAll(newData);
        return list.Count;
    }

    /// <summary>Benchmarks replacing all items in a <see cref="SourceList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int SourceList_ReplaceAll()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        var newData = CreateSequentialData(Count, Count);
        list.Edit(innerList =>
        {
            innerList.Clear();
            innerList.AddRange(newData);
        });
        return list.Count;
    }

    /// <summary>Benchmarks removing the first half of values individually from a <see cref="List{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int List_Remove()
    {
        var list = new List<int>(_data);
        for (var i = 0; i < Count / HalfCountDivisor; i++)
        {
            _ = list.Remove(i);
        }

        return list.Count;
    }

    /// <summary>Benchmarks removing the first half of values individually from a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_Remove()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        for (var i = 0; i < Count / HalfCountDivisor; i++)
        {
            _ = list.Remove(i);
        }

        return list.Count;
    }

    /// <summary>Benchmarks removing the first half of values individually from a <see cref="SourceList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int SourceList_Remove()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        for (var i = 0; i < Count / HalfCountDivisor; i++)
        {
            _ = list.Remove(i);
        }

        return list.Count;
    }

    /// <summary>Benchmarks removing even values from a <see cref="List{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int List_RemoveAll()
    {
        var list = new List<int>(_data);
        _ = list.RemoveAll(static x => x % ModuloTwoDivisor == 0);
        return list.Count;
    }

    /// <summary>Benchmarks indexed access to every item in a <see cref="List{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
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

    /// <summary>Benchmarks indexed access to every item in a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
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

    /// <summary>Benchmarks replacing all items in a <see cref="List{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int List_ReplaceAll()
    {
        var list = new List<int>(_data);
        var newData = CreateSequentialData(Count, Count);
        list.Clear();
        list.AddRange(newData);
        return list.Count;
    }

    /// <summary>Benchmarks stream notifications generated by removing half of a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_Stream_Remove()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        var events = 0;
        using var sub = list.Stream.SubscribeObserver(_ => events++);
        list.RemoveRange(CreateFirstHalfData());
        return events;
    }

    /// <summary>Benchmarks stream notifications generated by removing half of a <see cref="SourceList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int SourceList_Stream_Remove()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        var events = 0;
        using var sub = list.Connect().SubscribeObserver(_ => events++);
        list.RemoveMany(CreateFirstHalfData());
        return events;
    }

    /// <summary>Benchmarks adding a secondary index to a populated <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_AddIndex()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.AddIndex("Mod2", static x => x % ModuloTwoDivisor);
        return list.Count;
    }

    /// <summary>Benchmarks testing an item against a secondary index.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public bool QuaternaryList_ItemMatchesSecondaryIndex()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", static x => x % ModuloTwoDivisor);
        list.AddRange(_data);
        return list.ItemMatchesSecondaryIndex("Mod2", IndexedProbeItem, 0);
    }

    /// <summary>Benchmarks an indexed list after additions and removals.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_IndexWithAddRemove()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", static x => x % ModuloTwoDivisor);
        list.AddRange(_data);
        _ = list.RemoveMany(static x => x % ModuloFourDivisor == 0);
        return CountItems(list.GetItemsBySecondaryIndex("Mod2", 0));
    }

    /// <summary>Benchmarks copying a <see cref="List{T}"/> to an array.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int List_CopyTo()
    {
        var list = new List<int>(_data);
        var buffer = new int[Count];
        list.CopyTo(buffer, 0);
        return buffer.Length;
    }

    /// <summary>Benchmarks retrieving <see cref="List{T}.Count"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int List_Count()
    {
        var list = new List<int>(_data);
        return list.Count;
    }

    /// <summary>Benchmarks retrieving the count of a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_Count() => QuaternaryList_AddRange();

    /// <summary>Benchmarks retrieving <see cref="SourceList{T}.Count"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int SourceList_Count() => SourceList_AddRange();

    /// <summary>Benchmarks mixed add, remove, and predicate-removal operations on a <see cref="QuaternaryList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int QuaternaryList_MixedOperations()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.Add(Count);
        _ = list.Remove(0);
        _ = list.RemoveMany(static x => x % PeriodicRemovalDivisor == 0);
        return list.Count;
    }

    /// <summary>Benchmarks mixed add, remove, and predicate-removal operations on a <see cref="SourceList{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int SourceList_MixedOperations()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        list.Add(Count);
        _ = list.Remove(0);
        list.RemoveMany(FilterItems(list.Items, static x => x % PeriodicRemovalDivisor == 0));
        return list.Count;
    }

    /// <summary>Benchmarks mixed add, remove, and predicate-removal operations on a <see cref="List{T}"/>.</summary>
    /// <returns>The result produced by the benchmark operation.</returns>
    [Benchmark]
    public int List_MixedOperations()
    {
        var list = new List<int>(_data);
        list.Add(Count);
        _ = list.Remove(0);
        _ = list.RemoveAll(static x => x % PeriodicRemovalDivisor == 0);
        return list.Count;
    }

    /// <summary>Creates a sequential array without introducing LINQ overhead.</summary>
    /// <param name="start">The first value in the sequence.</param>
    /// <param name="count">The number of values to create.</param>
    /// <returns>The generated sequence.</returns>
    private static int[] CreateSequentialData(int start, int count)
    {
        var data = new int[count];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = start + i;
        }

        return data;
    }

    /// <summary>Materializes values that satisfy a predicate without introducing LINQ overhead.</summary>
    /// <param name="source">The values to inspect.</param>
    /// <param name="predicate">The predicate used to select values.</param>
    /// <returns>The selected values.</returns>
    private static List<int> FilterItems(IEnumerable<int> source, Func<int, bool> predicate)
    {
        var filteredItems = new List<int>();
        foreach (var item in source)
        {
            if (predicate(item))
            {
                filteredItems.Add(item);
            }
        }

        return filteredItems;
    }

    /// <summary>Counts an explicitly enumerated sequence without introducing LINQ overhead.</summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="items">The values to enumerate.</param>
    /// <returns>The number of enumerated values.</returns>
    private static int CountItems<T>(IEnumerable<T> items)
    {
        var count = 0;
        foreach (var _ in items)
        {
            count++;
        }

        return count;
    }

    /// <summary>Copies the first half of the configured input for partial-removal benchmarks.</summary>
    /// <returns>The copied first-half input.</returns>
    private int[] CreateFirstHalfData()
    {
        var count = Count / HalfCountDivisor;
        var data = new int[count];
        Array.Copy(_data, data, count);
        return data;
    }

    /// <summary>Replaces a collection with the configured number of doubled values.</summary>
    /// <param name="items">The collection to replace.</param>
    private void ReplaceWithDoubledValues(ICollection<int> items)
    {
        items.Clear();
        for (var i = 0; i < Count; i++)
        {
            items.Add(i * ValueMultiplier);
        }
    }
}
