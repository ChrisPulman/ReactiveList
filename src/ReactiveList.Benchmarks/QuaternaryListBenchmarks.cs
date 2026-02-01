using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using CP.Reactive;
using DynamicData;

namespace ReactiveList.Benchmarks;

[MemoryDiagnoser]
public class QuaternaryListBenchmarks
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
    public int QuaternaryList_AddRange()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        return list.Count;
    }

    [Benchmark]
    public int SourceList_AddRange()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
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
    public int QuaternaryList_RemoveRange()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.RemoveRange(_data.Take(Count / 2));
        return list.Count;
    }

    [Benchmark]
    public int SourceList_RemoveRange()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        list.RemoveMany(_data.Take(Count / 2));
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
    public int QuaternaryList_Clear()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.Clear();
        return list.Count;
    }

    [Benchmark]
    public int SourceList_Clear()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        list.Clear();
        return list.Count;
    }

    [Benchmark]
    public bool List_Contains()
    {
        var list = new List<int>(_data);
        return list.Contains(Count - 1);
    }

    [Benchmark]
    public bool QuaternaryList_Contains()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        return list.Contains(Count - 1);
    }

    [Benchmark]
    public int QuaternaryList_QueryIndex()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", x => x % 2);
        list.AddRange(_data);
        return list.GetItemsBySecondaryIndex("Mod2", 0).Count();
    }

    [Benchmark]
    public int QuaternaryList_Stream_Add()
    {
        using var list = new QuaternaryList<int>();
        var events = 0;
        using var sub = list.Stream.Subscribe(_ => events++);
        list.AddRange(_data);
        return events;
    }

    [Benchmark]
    public int SourceList_Stream_Add()
    {
        using var list = new SourceList<int>();
        var events = 0;
        using var sub = list.Connect().Subscribe(_ => events++);
        list.AddRange(_data);
        return events;
    }

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

    [Benchmark]
    public int QuaternaryList_RemoveMany()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        return list.RemoveMany(x => x % 2 == 0);
    }

    [Benchmark]
    public int SourceList_RemoveMany()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        list.RemoveMany(list.Items.Where(x => x % 2 == 0));
        return list.Count;
    }
}
