// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using CP.Primitives;
using CP.Primitives.Collections;
using DynamicData;

namespace ReactiveList.Benchmarks;

/// <summary>Benchmarks common list operations across List, ReactiveList, and SourceList.</summary>
[MemoryDiagnoser]
public class ListBenchmarks
{
    /// <summary>Divides the configured item count when an operation affects half the data.</summary>
    private const int HalfCountDivisor = 2;

    /// <summary>Identifies the even values used by parity-based benchmarks.</summary>
    private const int ParityDivisor = 2;

    /// <summary>Stores the sequential values supplied to each benchmark invocation.</summary>
    private int[] _data = [];

    /// <summary>Gets or sets the number of sequential items supplied to each benchmark.</summary>
    [Params(100, 1_000, 10_000)]
    public int Count { get; set; }

    /// <summary>Initializes the sequential source data for the configured item count.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var data = new int[Count];
        for (var index = 0; index < data.Length; index++)
        {
            data[index] = index;
        }

        _data = data;
    }

    /// <summary>Benchmarks appending the prepared values to a standard list.</summary>
    /// <returns>The number of items appended.</returns>
    [Benchmark]
    public int List_AddRange()
    {
        var list = new List<int>(Count);
        list.AddRange(_data);
        return list.Count;
    }

    /// <summary>Benchmarks appending the prepared values to a ReactiveList.</summary>
    /// <returns>The number of items appended.</returns>
    [Benchmark]
    public int ReactiveList_AddRange()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(_data);
        return list.Count;
    }

    /// <summary>Benchmarks appending the prepared values to a DynamicData SourceList edit.</summary>
    /// <returns>The number of items appended.</returns>
    [Benchmark]
    public int SourceList_AddRange()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        return list.Count;
    }

    /// <summary>Benchmarks removing half the prepared values from a standard list.</summary>
    /// <returns>The number of items remaining.</returns>
    [Benchmark]
    public int List_RemoveRange()
    {
        var list = new List<int>(_data);
        list.RemoveRange(0, Count / HalfCountDivisor);
        return list.Count;
    }

    /// <summary>Benchmarks removing half the prepared values from a ReactiveList.</summary>
    /// <returns>The number of items remaining.</returns>
    [Benchmark]
    public int ReactiveList_RemoveRange()
    {
        using var list = new ReactiveList<int>(_data);
        list.RemoveRange(0, Count / HalfCountDivisor);
        return list.Count;
    }

    /// <summary>Benchmarks removing half the prepared values from a SourceList edit.</summary>
    /// <returns>The number of items remaining.</returns>
    [Benchmark]
    public int SourceList_RemoveRange()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        list.Edit(l => l.RemoveRange(0, Count / HalfCountDivisor));
        return list.Count;
    }

    /// <summary>Benchmarks clearing a populated standard list.</summary>
    /// <returns>The number of items remaining.</returns>
    [Benchmark]
    public int List_Clear()
    {
        var list = new List<int>(_data);
        list.Clear();
        return list.Count;
    }

    /// <summary>Benchmarks clearing a populated ReactiveList.</summary>
    /// <returns>The number of items remaining.</returns>
    [Benchmark]
    public int ReactiveList_Clear()
    {
        using var list = new ReactiveList<int>(_data);
        list.Clear();
        return list.Count;
    }

    /// <summary>Benchmarks clearing a populated SourceList.</summary>
    /// <returns>The number of items remaining.</returns>
    [Benchmark]
    public int SourceList_Clear()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        list.Clear();
        return list.Count;
    }

    /// <summary>Benchmarks searching a standard list for its final prepared value.</summary>
    /// <returns>A value indicating whether the final value was found.</returns>
    [Benchmark]
    public bool List_Search()
    {
        var list = new List<int>(_data);
        return list.Contains(Count - 1);
    }

    /// <summary>Benchmarks searching a ReactiveList for its final prepared value.</summary>
    /// <returns>A value indicating whether the final value was found.</returns>
    [Benchmark]
    public bool ReactiveList_Search()
    {
        using var list = new ReactiveList<int>(_data);
        return list.Contains(Count - 1);
    }

    /// <summary>Benchmarks searching a SourceList for its final prepared value.</summary>
    /// <returns>A value indicating whether the final value was found.</returns>
    [Benchmark]
    public bool SourceList_Search()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        return ContainsValue(list.Items, Count - 1);
    }

    /// <summary>Benchmarks delivery of an add-range notification from a ReactiveList.</summary>
    /// <returns>The number of items reported by the observer.</returns>
    [Benchmark]
    public int ReactiveList_Add_WithObserver()
    {
        using var list = new ReactiveList<int>();
        var total = 0;
        using var sub = list.Added.SubscribeObserver(items => total += CountItems(items));
        list.AddRange(_data);
        return total;
    }

    /// <summary>Benchmarks delivery of an add-range change set from a SourceList.</summary>
    /// <returns>The number of changes reported by the observer.</returns>
    [Benchmark]
    public int SourceList_Add_WithObserver()
    {
        using var list = new SourceList<int>();
        var total = 0;
        using var sub = list.Connect().SubscribeObserver(changes => total += changes.TotalChanges);
        list.AddRange(_data);
        return total;
    }

    /// <summary>Benchmarks counting even values in a standard list.</summary>
    /// <returns>The number of even values.</returns>
    [Benchmark]
    public int List_Filter()
    {
        var list = new List<int>(_data);
        return CountEvenValues(list);
    }

    /// <summary>Benchmarks counting even values in a ReactiveList.</summary>
    /// <returns>The number of even values.</returns>
    [Benchmark]
    public int ReactiveList_Filter()
    {
        using var list = new ReactiveList<int>(_data);
        return CountEvenValues(list);
    }

    /// <summary>Benchmarks counting even values in a SourceList.</summary>
    /// <returns>The number of even values.</returns>
    [Benchmark]
    public int SourceList_Filter()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        return CountEvenValues(list.Items);
    }

    /// <summary>Benchmarks delivery of change notifications from an initially empty ReactiveList.</summary>
    /// <returns>The number of changes reported by the observer.</returns>
    [Benchmark]
    public int ReactiveList_Connect()
    {
        using var list = new ReactiveList<int>();
        var total = 0;
        using var sub = list.Connect().SubscribeObserver(changes => total += changes.Count);
        list.AddRange(_data);
        return total;
    }

    /// <summary>Benchmarks delivery of change notifications from an initially empty SourceList.</summary>
    /// <returns>The number of changes reported by the observer.</returns>
    [Benchmark]
    public int SourceList_Connect() => SourceList_Add_WithObserver();

    /// <summary>Benchmarks the initial snapshot delivered when connecting to a preloaded ReactiveList.</summary>
    /// <returns>The number of changes reported by the observer.</returns>
    [Benchmark]
    public int ReactiveList_Connect_Preloaded()
    {
        using var list = new ReactiveList<int>(_data);
        var total = 0;
        using var sub = list.Connect().SubscribeObserver(changes => total += changes.Count);
        return total;
    }

    /// <summary>Benchmarks the initial snapshot delivered when connecting to a preloaded SourceList.</summary>
    /// <returns>The number of changes reported by the observer.</returns>
    [Benchmark]
    public int SourceList_Connect_Preloaded()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        var total = 0;
        using var sub = list.Connect().SubscribeObserver(changes => total += changes.TotalChanges);
        return total;
    }

    /// <summary>Benchmarks replacing every value in a ReactiveList.</summary>
    /// <returns>The number of items after replacement.</returns>
    [Benchmark]
    public int ReactiveList_ReplaceAll()
    {
        using var list = new ReactiveList<int>(_data);
        var newData = CreateReplacementData(Count);
        list.ReplaceAll(newData);
        return list.Count;
    }

    /// <summary>Benchmarks replacing every value in a SourceList edit.</summary>
    /// <returns>The number of items after replacement.</returns>
    [Benchmark]
    public int SourceList_ReplaceAll()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        var newData = CreateReplacementData(Count);
        list.Edit(innerList =>
        {
            innerList.Clear();
            innerList.AddRange(newData);
        });
        return list.Count;
    }

    /// <summary>Benchmarks moving the first item halfway through a ReactiveList.</summary>
    /// <returns>The number of items after the move.</returns>
    [Benchmark]
    public int ReactiveList_Move()
    {
        using var list = new ReactiveList<int>(_data);
        list.Move(0, Count / HalfCountDivisor);
        return list.Count;
    }

    /// <summary>Benchmarks moving the first item halfway through a SourceList.</summary>
    /// <returns>The number of items after the move.</returns>
    [Benchmark]
    public int SourceList_Move()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        list.Move(0, Count / HalfCountDivisor);
        return list.Count;
    }

    /// <summary>Benchmarks removing even values from a ReactiveList.</summary>
    /// <returns>The number of items remaining.</returns>
    [Benchmark]
    public int ReactiveList_RemoveMany()
    {
        using var list = new ReactiveList<int>(_data);
        _ = list.RemoveMany(static x => x % ParityDivisor == 0);
        return list.Count;
    }

    /// <summary>Benchmarks removing even values from a SourceList.</summary>
    /// <returns>The number of items remaining.</returns>
    [Benchmark]
    public int SourceList_RemoveMany()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        var itemsToRemove = new List<int>(Count / HalfCountDivisor);
        foreach (var item in list.Items)
        {
            if (item % ParityDivisor == 0)
            {
                itemsToRemove.Add(item);
            }
        }

        list.RemoveMany(itemsToRemove);
        return list.Count;
    }

    /// <summary>Counts the even values in the supplied sequence without allocating a LINQ iterator.</summary>
    /// <param name="items">The values to inspect.</param>
    /// <returns>The number of even values in <paramref name="items"/>.</returns>
    private static int CountEvenValues(IEnumerable<int> items)
    {
        var count = 0;
        foreach (var item in items)
        {
            if (item % ParityDivisor == 0)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Counts all values in a sequence without allocating a LINQ iterator.</summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="items">The values to enumerate.</param>
    /// <returns>The number of enumerated values.</returns>
    private static int CountItems<T>(IEnumerable<T> items)
    {
        var count = 0;
        foreach (var item in items)
        {
            _ = item;
            count++;
        }

        return count;
    }

    /// <summary>Searches an explicitly enumerated sequence for a value.</summary>
    /// <param name="items">The values to search.</param>
    /// <param name="value">The value to find.</param>
    /// <returns><see langword="true"/> when the value is present; otherwise, <see langword="false"/>.</returns>
    private static bool ContainsValue(IEnumerable<int> items, int value)
    {
        foreach (var item in items)
        {
            if (item == value)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Creates the sequence used to replace all initially prepared values.</summary>
    /// <param name="count">The number of replacement values to create.</param>
    /// <returns>Sequential values starting at <paramref name="count"/>.</returns>
    private static int[] CreateReplacementData(int count)
    {
        var data = new int[count];
        for (var index = 0; index < data.Length; index++)
        {
            data[index] = count + index;
        }

        return data;
    }
}
