using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BenchmarkDotNet.Attributes;
using CP.Reactive;
using DynamicData;

namespace ReactiveList.Benchmarks;

[MemoryDiagnoser]
public class QuaternaryDictionaryBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int Count { get; set; }

    private KeyValuePair<int, int>[] _kvps = [];

    [GlobalSetup]
    public void Setup()
    {
        _kvps = Enumerable.Range(0, Count)
            .Select(i => new KeyValuePair<int, int>(i, i))
            .ToArray();
    }

    [Benchmark]
    public int Dictionary_AddRange()
    {
        var dict = new Dictionary<int, int>();
        foreach (var kvp in _kvps)
        {
            dict[kvp.Key] = kvp.Value;
        }

        return dict.Count;
    }

    [Benchmark]
    public int QuaternaryDictionary_AddRange()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        return dict.Count;
    }

    [Benchmark]
    public int SourceCache_AddRange()
    {
        using var cache = new SourceCache<Item, int>(x => x.Id);
        cache.AddOrUpdate(_kvps.Select(k => new Item(k.Key, k.Value)));
        return cache.Count;
    }

    [Benchmark]
    public int Dictionary_Remove()
    {
        var dict = _kvps.ToDictionary(k => k.Key, k => k.Value);
        for (var i = 0; i < Count / 2; i++)
        {
            dict.Remove(i);
        }

        return dict.Count;
    }

    [Benchmark]
    public int QuaternaryDictionary_Remove()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        for (var i = 0; i < Count / 2; i++)
        {
            dict.Remove(i);
        }

        return dict.Count;
    }

    [Benchmark]
    public int SourceCache_Remove()
    {
        using var cache = new SourceCache<Item, int>(x => x.Id);
        cache.AddOrUpdate(_kvps.Select(k => new Item(k.Key, k.Value)));
        cache.RemoveKeys(Enumerable.Range(0, Count / 2));
        return cache.Count;
    }

    [Benchmark]
    public int QuaternaryDictionary_RemoveKeys()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        dict.RemoveKeys(Enumerable.Range(0, Count / 2));
        return dict.Count;
    }

    [Benchmark]
    public int Dictionary_Clear()
    {
        var dict = _kvps.ToDictionary(k => k.Key, k => k.Value);
        dict.Clear();
        return dict.Count;
    }

    [Benchmark]
    public int QuaternaryDictionary_Clear()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        dict.Clear();
        return dict.Count;
    }

    [Benchmark]
    public int SourceCache_Clear()
    {
        using var cache = new SourceCache<Item, int>(x => x.Id);
        cache.AddOrUpdate(_kvps.Select(k => new Item(k.Key, k.Value)));
        cache.Clear();
        return cache.Count;
    }

    [Benchmark]
    public bool Dictionary_TryGetValue()
    {
        var dict = _kvps.ToDictionary(k => k.Key, k => k.Value);
        return dict.TryGetValue(Count - 1, out _);
    }

    [Benchmark]
    public bool QuaternaryDictionary_TryGetValue()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        return dict.TryGetValue(Count - 1, out _);
    }

    [Benchmark]
    public bool QuaternaryDictionary_Lookup()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        return dict.Lookup(Count - 1).HasValue;
    }

    [Benchmark]
    public bool SourceCache_Lookup()
    {
        using var cache = new SourceCache<Item, int>(x => x.Id);
        cache.AddOrUpdate(_kvps.Select(k => new Item(k.Key, k.Value)));
        return cache.Lookup(Count - 1).HasValue;
    }

    [Benchmark]
    public int QuaternaryDictionary_Stream_Add()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        var events = 0;
        using var sub = dict.Stream.Subscribe(_ => events++);
        dict.AddRange(_kvps);
        return events;
    }

    [Benchmark]
    public int SourceCache_Stream_Add()
    {
        using var cache = new SourceCache<Item, int>(x => x.Id);
        var events = 0;
        using var sub = cache.Connect().Subscribe(_ => events++);
        cache.AddOrUpdate(_kvps.Select(k => new Item(k.Key, k.Value)));
        return events;
    }

    [Benchmark]
    public int QuaternaryDictionary_Edit()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        dict.Edit(innerDict =>
        {
            innerDict.Clear();
            for (var i = 0; i < Count; i++)
            {
                innerDict.Add(i, i * 2);
            }
        });
        return dict.Count;
    }

    [Benchmark]
    public int SourceCache_Edit()
    {
        using var cache = new SourceCache<Item, int>(x => x.Id);
        cache.AddOrUpdate(_kvps.Select(k => new Item(k.Key, k.Value)));
        cache.Edit(innerCache =>
        {
            innerCache.Clear();
            for (var i = 0; i < Count; i++)
            {
                innerCache.AddOrUpdate(new Item(i, i * 2));
            }
        });
        return cache.Count;
    }

    [Benchmark]
    public int QuaternaryDictionary_RemoveMany()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        return dict.RemoveMany(kvp => kvp.Key % 2 == 0);
    }

    private record Item(int Id, int Value);
}
