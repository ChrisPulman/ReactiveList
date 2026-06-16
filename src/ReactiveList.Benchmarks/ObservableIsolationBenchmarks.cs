// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using CP.Reactive;
using CP.Reactive.Collections;
using DynamicData;

namespace ReactiveList.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[RankColumn]
public class ObservableIsolationBenchmarks
{
    private const string GroupIndexName = "Group";

    private BenchItem[] _items = [];
    private KeyValuePair<int, BenchItem>[] _pairs = [];
    private QuaternaryList<BenchItem>? _indexedList;
    private QuaternaryDictionary<int, BenchItem>? _indexedDictionary;
    private SourceCache<BenchItem, int>? _sourceCache;

    [Params(1024)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _items = Enumerable.Range(0, Count)
            .Select(static index => new BenchItem(index, index & 7, index * 17))
            .ToArray();
        _pairs = _items.Select(static item => KeyValuePair.Create(item.Id, item)).ToArray();

        _indexedList = new QuaternaryList<BenchItem>();
        _indexedList.AddRange(_items);
        _indexedList.AddIndex(GroupIndexName, static item => item.Group);

        _indexedDictionary = new QuaternaryDictionary<int, BenchItem>();
        _indexedDictionary.AddRange(_pairs);
        _indexedDictionary.AddValueIndex(GroupIndexName, static item => item.Group);

        _sourceCache = new SourceCache<BenchItem, int>(static item => item.Id);
        _sourceCache.AddOrUpdate(_items);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _indexedList?.Dispose();
        _indexedDictionary?.Dispose();
        _sourceCache?.Dispose();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StreamIsolation")]
    public int ReactiveList_AddRange_NoSubscriber()
    {
        using var list = new ReactiveList<BenchItem>();
        list.AddRange(_items);
        return list.Count;
    }

    [Benchmark]
    [BenchmarkCategory("StreamIsolation")]
    public int ReactiveList_AddRange_WithConnectSubscriber()
    {
        using var list = new ReactiveList<BenchItem>();
        var observed = 0;
        using var subscription = list.Connect().SubscribeObserver(changes => observed += changes.Count);
        list.AddRange(_items);
        return list.Count + observed;
    }

    [Benchmark]
    [BenchmarkCategory("StreamIsolation")]
    public int QuaternaryList_AddRange_NoSubscriber()
    {
        using var list = new QuaternaryList<BenchItem>();
        list.AddRange(_items);
        return list.Count;
    }

    [Benchmark]
    [BenchmarkCategory("StreamIsolation")]
    public int QuaternaryDictionary_AddRange_NoSubscriber()
    {
        using var dictionary = new QuaternaryDictionary<int, BenchItem>();
        dictionary.AddRange(_pairs);
        return dictionary.Count;
    }

    [Benchmark]
    [BenchmarkCategory("StreamIsolation")]
    public int SourceCache_AddOrUpdate_WithConnectSubscriber()
    {
        using var cache = new SourceCache<BenchItem, int>(static item => item.Id);
        var observed = 0;
        using var subscription = cache.Connect().SubscribeObserver(changes => observed += changes.Count);
        cache.AddOrUpdate(_items);
        return cache.Count + observed;
    }

    [Benchmark]
    [BenchmarkCategory("IndexedLookup")]
    public int QuaternaryList_SecondaryIndexLookup()
    {
        return _indexedList!.GetItemsBySecondaryIndex(GroupIndexName, 3).Count();
    }

    [Benchmark]
    [BenchmarkCategory("IndexedLookup")]
    public int QuaternaryDictionary_SecondaryIndexLookup()
    {
        return _indexedDictionary!.GetValuesBySecondaryIndex(GroupIndexName, 3).Count();
    }

    [Benchmark]
    [BenchmarkCategory("IndexedLookup")]
    public int SourceCache_SecondaryScan()
    {
        return _sourceCache!.Items.Count(static item => item.Group == 3);
    }

    private readonly record struct BenchItem(int Id, int Group, int Value);
}
