using System.Reactive.Linq;
using BenchmarkDotNet.Attributes;
using CP.Reactive.Collections;
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
    public int List_Add()
    {
        var list = new List<int>();
        for (var i = 0; i < Count; i++)
        {
            list.Add(i);
        }

        return list.Count;
    }

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
    public bool SourceList_Contains()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        return list.Items.Contains(Count - 1);
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

    [Benchmark]
    public int QuaternaryList_ParallelAdd()
    {
        using var list = new QuaternaryList<int>();

        // Large dataset to trigger parallel processing (threshold is 256)
        var largeData = Enumerable.Range(0, Math.Max(Count, 500)).ToArray();
        list.AddRange(largeData);
        return list.Count;
    }

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

    [Benchmark]
    public int QuaternaryList_CopyTo()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        var buffer = new int[Count];
        list.CopyTo(buffer, 0);
        return buffer.Length;
    }

    [Benchmark]
    public int QuaternaryList_ReplaceAll()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        var newData = Enumerable.Range(Count, Count).ToArray();
        list.ReplaceAll(newData);
        return list.Count;
    }

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

    [Benchmark]
    public int List_RemoveAll()
    {
        var list = new List<int>(_data);
        list.RemoveAll(x => x % 2 == 0);
        return list.Count;
    }

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

    [Benchmark]
    public int List_ReplaceAll()
    {
        var list = new List<int>(_data);
        var newData = Enumerable.Range(Count, Count).ToArray();
        list.Clear();
        list.AddRange(newData);
        return list.Count;
    }

    [Benchmark]
    public int QuaternaryList_Stream_Remove()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        var events = 0;
        using var sub = list.Stream.Subscribe(_ => events++);
        list.RemoveRange(_data.Take(Count / 2));
        return events;
    }

    [Benchmark]
    public int SourceList_Stream_Remove()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        var events = 0;
        using var sub = list.Connect().Subscribe(_ => events++);
        list.RemoveMany(_data.Take(Count / 2));
        return events;
    }

    [Benchmark]
    public int QuaternaryList_AddIndex()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        list.AddIndex("Mod2", x => x % 2);
        return list.Count;
    }

    [Benchmark]
    public bool QuaternaryList_ItemMatchesSecondaryIndex()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", x => x % 2);
        list.AddRange(_data);
        return list.ItemMatchesSecondaryIndex("Mod2", 4, 0);
    }

    [Benchmark]
    public int QuaternaryList_IndexWithAddRemove()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod2", x => x % 2);
        list.AddRange(_data);
        list.RemoveMany(x => x % 4 == 0);
        return list.GetItemsBySecondaryIndex("Mod2", 0).Count();
    }

    [Benchmark]
    public int List_CopyTo()
    {
        var list = new List<int>(_data);
        var buffer = new int[Count];
        list.CopyTo(buffer, 0);
        return buffer.Length;
    }

    [Benchmark]
    public int List_Count()
    {
        var list = new List<int>(_data);
        return list.Count;
    }

    [Benchmark]
    public int QuaternaryList_Count()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(_data);
        return list.Count;
    }

    [Benchmark]
    public int SourceList_Count()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        return list.Count;
    }

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
