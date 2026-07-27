// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using CP.Primitives;
using CP.Primitives.Collections;
using DynamicData;

namespace ReactiveList.Benchmarks;

/// <summary>Benchmarks observable delivery and secondary-index isolation.</summary>
[MemoryDiagnoser]
[CategoriesColumn]
[RankColumn]
public sealed class ObservableIsolationBenchmarks : IDisposable
{
    /// <summary>Provides a bit mask that partitions items into eight groups.</summary>
    private const int GroupMask = 7;

    /// <summary>Names the secondary index used by indexed lookup benchmarks.</summary>
    private const string GroupIndexName = "Group";

    /// <summary>Identifies the group queried by indexed lookup and scan benchmarks.</summary>
    private const int IndexedGroup = 3;

    /// <summary>Scales the payload stored in generated benchmark items.</summary>
    private const int ValueMultiplier = 17;

    /// <summary>Stores generated items used by the observable benchmarks.</summary>
    private BenchItem[] _items = [];

    /// <summary>Stores the keyed form of the generated benchmark items.</summary>
    private KeyValuePair<int, BenchItem>[] _pairs = [];

    /// <summary>Holds the prepared indexed quaternary list.</summary>
    private QuaternaryList<BenchItem>? _indexedList;

    /// <summary>Holds the prepared indexed quaternary dictionary.</summary>
    private QuaternaryDictionary<int, BenchItem>? _indexedDictionary;

    /// <summary>Holds the prepared DynamicData source cache.</summary>
    private SourceCache<BenchItem, int>? _sourceCache;

    /// <summary>Tracks whether the prepared benchmark collections have been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the item count.</summary>
    [Params(1024)]
    public int Count { get; set; }

    /// <summary>Initializes the data and indexed collections used by the benchmarks.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _disposed = false;
        _items = new BenchItem[Count];
        _pairs = new KeyValuePair<int, BenchItem>[Count];
        for (var index = 0; index < Count; index++)
        {
            BenchItem item = new(index, index & GroupMask, index * ValueMultiplier);
            _items[index] = item;
            _pairs[index] = KeyValuePair.Create(item.Id, item);
        }

        _indexedList = new();
        _indexedList.AddRange(_items);
        _indexedList.AddIndex(GroupIndexName, static item => item.Group);

        _indexedDictionary = new();
        _indexedDictionary.AddRange(_pairs);
        _indexedDictionary.AddValueIndex(GroupIndexName, static item => item.Group);

        _sourceCache = new(static item => item.Id);
        _sourceCache.AddOrUpdate(_items);
    }

    /// <summary>Releases the indexed collections created for the benchmarks.</summary>
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

    /// <summary>Releases the indexed collections created for the benchmarks.</summary>
    public void Dispose() => Cleanup();

    /// <summary>Measures adding all items to a ReactiveList with no subscriber.</summary>
    /// <returns>The resulting list count.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StreamIsolation")]
    public int ReactiveList_AddRange_NoSubscriber()
    {
        using var list = new ReactiveList<BenchItem>();
        list.AddRange(_items);
        return list.Count;
    }

    /// <summary>Measures adding all items to a ReactiveList with a Connect subscriber.</summary>
    /// <returns>The combined list and observed change counts.</returns>
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

    /// <summary>Measures adding all items to a QuaternaryList with no subscriber.</summary>
    /// <returns>The resulting list count.</returns>
    [Benchmark]
    [BenchmarkCategory("StreamIsolation")]
    public int QuaternaryList_AddRange_NoSubscriber()
    {
        using var list = new QuaternaryList<BenchItem>();
        list.AddRange(_items);
        return list.Count;
    }

    /// <summary>Measures adding all pairs to a QuaternaryDictionary with no subscriber.</summary>
    /// <returns>The resulting dictionary count.</returns>
    [Benchmark]
    [BenchmarkCategory("StreamIsolation")]
    public int QuaternaryDictionary_AddRange_NoSubscriber()
    {
        using var dictionary = new QuaternaryDictionary<int, BenchItem>();
        dictionary.AddRange(_pairs);
        return dictionary.Count;
    }

    /// <summary>Measures adding or updating all items in a SourceCache with a Connect subscriber.</summary>
    /// <returns>The combined cache and observed change counts.</returns>
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

    /// <summary>Measures looking up indexed items in a QuaternaryList.</summary>
    /// <returns>The number of matching items.</returns>
    [Benchmark]
    [BenchmarkCategory("IndexedLookup")]
    public int QuaternaryList_SecondaryIndexLookup()
    {
        var matches = 0;
        foreach (var _ in _indexedList!.GetItemsBySecondaryIndex(GroupIndexName, IndexedGroup))
        {
            matches++;
        }

        return matches;
    }

    /// <summary>Measures looking up indexed values in a QuaternaryDictionary.</summary>
    /// <returns>The number of matching values.</returns>
    [Benchmark]
    [BenchmarkCategory("IndexedLookup")]
    public int QuaternaryDictionary_SecondaryIndexLookup()
    {
        var matches = 0;
        foreach (var _ in _indexedDictionary!.GetValuesBySecondaryIndex(GroupIndexName, IndexedGroup))
        {
            matches++;
        }

        return matches;
    }

    /// <summary>Measures scanning a SourceCache for indexed values.</summary>
    /// <returns>The number of matching values.</returns>
    [Benchmark]
    [BenchmarkCategory("IndexedLookup")]
    public int SourceCache_SecondaryScan()
    {
        var matches = 0;
        foreach (var item in _sourceCache!.Items)
        {
            if (item.Group == IndexedGroup)
            {
                matches++;
            }
        }

        return matches;
    }

    /// <summary>Represents an item used by the isolation benchmarks.</summary>
    /// <param name="Id">The item's identifier.</param>
    /// <param name="Group">The item's secondary-index group.</param>
    /// <param name="Value">The item's payload value.</param>
    private readonly record struct BenchItem(int Id, int Group, int Value);
}
