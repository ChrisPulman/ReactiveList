// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using CP.Primitives;
using CP.Primitives.Collections;
using DynamicData;

namespace ReactiveList.Benchmarks;

/// <summary>Provides ObservableIsolationBenchmarks.</summary>
[MemoryDiagnoser]
[CategoriesColumn]
[RankColumn]
public sealed class ObservableIsolationBenchmarks : IDisposable
{
    private const int GroupMask = 7;

    private const string GroupIndexName = "Group";

    private const int IndexedGroup = 3;

    private const int ValueMultiplier = 17;

    private BenchItem[] _items = [];

    private KeyValuePair<int, BenchItem>[] _pairs = [];

    private QuaternaryList<BenchItem>? _indexedList;

    private QuaternaryDictionary<int, BenchItem>? _indexedDictionary;

    private SourceCache<BenchItem, int>? _sourceCache;

    private bool _disposed;

    /// <summary>Gets or sets the item count.</summary>
    [Params(1024)]
    public int Count { get; set; }

    /// <summary>Provides Setup.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _disposed = false;
        _items = Enumerable.Range(0, Count)
            .Select(static index => new BenchItem(index, index & GroupMask, index * ValueMultiplier))
            .ToArray();
        _pairs = _items.Select(static item => KeyValuePair.Create(item.Id, item)).ToArray();

        _indexedList = new();
        _indexedList.AddRange(_items);
        _indexedList.AddIndex(GroupIndexName, static item => item.Group);

        _indexedDictionary = new();
        _indexedDictionary.AddRange(_pairs);
        _indexedDictionary.AddValueIndex(GroupIndexName, static item => item.Group);

        _sourceCache = new(static item => item.Id);
        _sourceCache.AddOrUpdate(_items);
    }

    /// <summary>Provides Cleanup.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        if (_disposed)
        {
            return;
        }

        _indexedList?.Dispose();
        _indexedDictionary?.Dispose();
        _sourceCache?.Dispose();
        _indexedList = null;
        _indexedDictionary = null;
        _sourceCache = null;
        _disposed = true;
    }

    /// <summary>Disposes benchmark resources.</summary>
    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    /// <summary>Provides ReactiveList_AddRange_NoSubscriber.</summary>
    /// <returns>The result.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StreamIsolation")]
    public int ReactiveList_AddRange_NoSubscriber()
    {
        using var list = new ReactiveList<BenchItem>();
        list.AddRange(_items);
        return list.Count;
    }

    /// <summary>Provides ReactiveList_AddRange_WithConnectSubscriber.</summary>
    /// <returns>The result.</returns>
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

    /// <summary>Provides QuaternaryList_AddRange_NoSubscriber.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("StreamIsolation")]
    public int QuaternaryList_AddRange_NoSubscriber()
    {
        using var list = new QuaternaryList<BenchItem>();
        list.AddRange(_items);
        return list.Count;
    }

    /// <summary>Provides QuaternaryDictionary_AddRange_NoSubscriber.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("StreamIsolation")]
    public int QuaternaryDictionary_AddRange_NoSubscriber()
    {
        using var dictionary = new QuaternaryDictionary<int, BenchItem>();
        dictionary.AddRange(_pairs);
        return dictionary.Count;
    }

    /// <summary>Provides SourceCache_AddOrUpdate_WithConnectSubscriber.</summary>
    /// <returns>The result.</returns>
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

    /// <summary>Provides QuaternaryList_SecondaryIndexLookup.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("IndexedLookup")]
    public int QuaternaryList_SecondaryIndexLookup()
    {
        return _indexedList!.GetItemsBySecondaryIndex(GroupIndexName, IndexedGroup).Count();
    }

    /// <summary>Provides QuaternaryDictionary_SecondaryIndexLookup.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("IndexedLookup")]
    public int QuaternaryDictionary_SecondaryIndexLookup()
    {
        return _indexedDictionary!.GetValuesBySecondaryIndex(GroupIndexName, IndexedGroup).Count();
    }

    /// <summary>Provides SourceCache_SecondaryScan.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("IndexedLookup")]
    public int SourceCache_SecondaryScan()
    {
        return _sourceCache!.Items.Count(static item => item.Group == IndexedGroup);
    }

    /// <summary>Provides BenchItem.</summary>
    /// <param name="Id">The Id value.</param>
    /// <param name="Group">The Group value.</param>
    /// <param name="Value">The Value.</param>
    private readonly record struct BenchItem(int Id, int Group, int Value);
}
