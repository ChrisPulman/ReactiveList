using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using CP.Reactive;
using DynamicData;

namespace ReactiveList.Benchmarks;

[MemoryDiagnoser]
public class ListBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int Count { get; set; }

    private int[] _data = [];

    [GlobalSetup]
    public void Setup() => _data = Enumerable.Range(0, Count).ToArray();

    [Benchmark]
    public int List_AddRange()
    {
        var list = new List<int>();
        list.AddRange(_data);
        return list.Count;
    }

    [Benchmark]
    public int ReactiveList_AddRange()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(_data);
        return list.Count;
    }

    [Benchmark]
    public int SourceList_AddRange()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        return list.Count;
    }

    [Benchmark]
    public int List_RemoveRange()
    {
        var list = new List<int>(_data);
        list.RemoveRange(0, Count / 2);
        return list.Count;
    }

    [Benchmark]
    public int ReactiveList_RemoveRange()
    {
        using var list = new ReactiveList<int>(_data);
        list.RemoveRange(0, Count / 2);
        return list.Count;
    }

    [Benchmark]
    public int SourceList_RemoveRange()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        list.Edit(l => l.RemoveRange(0, Count / 2));
        return list.Count;
    }

    [Benchmark]
    public int List_Clear()
    {
        var list = new List<int>(_data);
        list.Clear();
        return list.Count;
    }

    [Benchmark]
    public int ReactiveList_Clear()
    {
        using var list = new ReactiveList<int>(_data);
        list.Clear();
        return list.Count;
    }

    [Benchmark]
    public int SourceList_Clear()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        list.Clear();
        return list.Count;
    }

    [Benchmark]
    public bool List_Search()
    {
        var list = new List<int>(_data);
        return list.Contains(Count - 1);
    }

    [Benchmark]
    public bool ReactiveList_Search()
    {
        using var list = new ReactiveList<int>(_data);
        return list.Contains(Count - 1);
    }

    [Benchmark]
    public bool SourceList_Search()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        return list.Items.Contains(Count - 1);
    }

    [Benchmark]
    public int ReactiveList_Add_WithObserver()
    {
        using var list = new ReactiveList<int>();
        var total = 0;
        using var sub = list.Added.Subscribe(items => total += items.Count());
        list.AddRange(_data);
        return total;
    }

    [Benchmark]
    public int SourceList_Add_WithObserver()
    {
        using var list = new SourceList<int>();
        var total = 0;
        using var sub = list.Connect().Subscribe(_ => total++);
        list.AddRange(_data);
        return total;
    }

    [Benchmark]
    public int List_Filter()
    {
        var list = new List<int>(_data);
        return list.Where(x => x % 2 == 0).Count();
    }

    [Benchmark]
    public int ReactiveList_Filter()
    {
        using var list = new ReactiveList<int>(_data);
        return list.Where(x => x % 2 == 0).Count();
    }

    [Benchmark]
    public int SourceList_Filter()
    {
        using var list = new SourceList<int>();
        list.Edit(l => l.AddRange(_data));
        return list.Items.Where(x => x % 2 == 0).Count();
    }
}
