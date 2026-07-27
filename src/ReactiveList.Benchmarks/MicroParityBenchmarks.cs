// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using BenchmarkDotNet.Attributes;
using CP.Primitives;
using CP.Primitives.Collections;
using DynamicData;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

namespace ReactiveList.Benchmarks;

/// <summary>Benchmarks equivalent ReactiveList and DynamicData operations on small data sets.</summary>
[MemoryDiagnoser]
[CategoriesColumn]
[RankColumn]
public sealed class MicroParityBenchmarks : IDisposable
{
    /// <summary>Provides a bit mask that partitions items into four groups.</summary>
    private const int GroupMask = 3;

    /// <summary>Names the secondary index used by indexed lookup benchmarks.</summary>
    private const string GroupIndexName = "Group";

    /// <summary>Divides a count in half for partial-removal benchmarks.</summary>
    private const int HalfCountDivisor = 2;

    /// <summary>Scales the payload stored in generated benchmark items.</summary>
    private const int ValueMultiplier = 17;

    /// <summary>Stores all sequential numbers used by list benchmarks.</summary>
    private int[] _numbers = [];

    /// <summary>Stores the even-number subset used by removal benchmarks.</summary>
    private int[] _evens = [];

    /// <summary>Stores generated items used by indexed collection benchmarks.</summary>
    private MicroItem[] _items = [];

    /// <summary>Stores the keyed form of the generated benchmark items.</summary>
    private KeyValuePair<int, MicroItem>[] _pairs = [];

    /// <summary>Holds the prepared indexed quaternary list.</summary>
    private QuaternaryList<MicroItem>? _indexedList;

    /// <summary>Holds the prepared DynamicData source list.</summary>
    private SourceList<MicroItem>? _sourceItemList;

    /// <summary>Holds the prepared indexed quaternary dictionary.</summary>
    private QuaternaryDictionary<int, MicroItem>? _indexedDictionary;

    /// <summary>Holds the prepared DynamicData source cache.</summary>
    private SourceCache<MicroItem, int>? _sourceCache;

    /// <summary>Tracks whether the prepared benchmark collections have been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the item count.</summary>
    [Params(1, 8, 32, 128)]
    public int Count { get; set; }

    /// <summary>Initializes the data and indexed collections used by the benchmarks.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _numbers = new int[Count];
        _items = new MicroItem[Count];
        var evenCount = 0;
        for (var index = 0; index < Count; index++)
        {
            _numbers[index] = index;
            _items[index] = new(index, index & GroupMask, index * ValueMultiplier);
            if ((index & 1) == 0)
            {
                evenCount++;
            }
        }

        _evens = new int[evenCount];
        var evenIndex = 0;
        foreach (var number in _numbers)
        {
            if ((number & 1) == 0)
            {
                _evens[evenIndex] = number;
                evenIndex++;
            }
        }

        _pairs = new KeyValuePair<int, MicroItem>[_items.Length];
        for (var index = 0; index < _items.Length; index++)
        {
            var item = _items[index];
            _pairs[index] = KeyValuePair.Create(item.Id, item);
        }

        _indexedList = new();
        _indexedList.AddRange(_items);
        _indexedList.AddIndex(GroupIndexName, static item => item.Group);

        _sourceItemList = new();
        _sourceItemList.AddRange(_items);

        _indexedDictionary = new();
        _indexedDictionary.AddRange(_pairs);
        _indexedDictionary.AddValueIndex(GroupIndexName, static item => item.Group);

        _sourceCache = new(static item => item.Id);
        _sourceCache.AddOrUpdate(_items);
        _disposed = false;
    }

    /// <summary>Releases the indexed collections created for the benchmarks.</summary>
    [GlobalCleanup]
    public void Cleanup() => Dispose();

    /// <summary>Releases the indexed collections created for the benchmarks.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _indexedList?.Dispose();
        _sourceItemList?.Dispose();
        _indexedDictionary?.Dispose();
        _sourceCache?.Dispose();
        _indexedList = null;
        _sourceItemList = null;
        _indexedDictionary = null;
        _sourceCache = null;
        _disposed = true;
    }

    /// <summary>Measures adding all numbers to a ReactiveList.</summary>
    /// <returns>The resulting list count.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("List", "AddRange")]
    public int ReactiveList_AddRange()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(_numbers);
        return list.Count;
    }

    /// <summary>Measures adding all numbers to a DynamicData SourceList.</summary>
    /// <returns>The resulting list count.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "AddRange")]
    public int SourceList_AddRange()
    {
        using var list = new SourceList<int>();
        list.AddRange(_numbers);
        return list.Count;
    }

    /// <summary>Measures removing the first half of a ReactiveList.</summary>
    /// <returns>The resulting list count.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "RemoveRange")]
    public int ReactiveList_RemoveRange()
    {
        using var list = new ReactiveList<int>(_numbers);
        list.RemoveRange(0, Count / HalfCountDivisor);
        return list.Count;
    }

    /// <summary>Measures removing the first half of a DynamicData SourceList.</summary>
    /// <returns>The resulting list count.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "RemoveRange")]
    public int SourceList_RemoveRange()
    {
        using var list = new SourceList<int>();
        list.AddRange(_numbers);
        list.Edit(innerList => innerList.RemoveRange(0, Count / HalfCountDivisor));
        return list.Count;
    }

    /// <summary>Measures removing all even numbers from a ReactiveList.</summary>
    /// <returns>The resulting list count.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "RemoveMany")]
    public int ReactiveList_RemoveMany()
    {
        using var list = new ReactiveList<int>(_numbers);
        list.Remove(_evens);
        return list.Count;
    }

    /// <summary>Measures removing all even numbers from a DynamicData SourceList.</summary>
    /// <returns>The resulting list count.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "RemoveMany")]
    public int SourceList_RemoveMany()
    {
        using var list = new SourceList<int>();
        list.AddRange(_numbers);
        list.RemoveMany(_evens);
        return list.Count;
    }

    /// <summary>Measures ReactiveList change delivery while adding all numbers.</summary>
    /// <returns>The number of delivered changes.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "Connect")]
    public int ReactiveList_ConnectAddRange()
    {
        using var list = new ReactiveList<int>();
        var total = 0;
        using var subscription = list.Connect().SubscribeObserver(changes => total += changes.Count);
        list.AddRange(_numbers);
        return total;
    }

    /// <summary>Measures SourceList change delivery while adding all numbers.</summary>
    /// <returns>The number of delivered changes.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "Connect")]
    public int SourceList_ConnectAddRange()
    {
        using var list = new SourceList<int>();
        var total = 0;
        using var subscription = list.Connect().SubscribeObserver(changes => total += changes.TotalChanges);
        list.AddRange(_numbers);
        return total;
    }

    /// <summary>Measures creating a ReactiveList view that contains even numbers.</summary>
    /// <returns>The filtered view count.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "View")]
    public int ReactiveList_FilteredView()
    {
        using var list = new ReactiveList<int>(_numbers);
        using var view = list.CreateView(static item => (item & 1) == 0, Sequencer.Immediate, throttleMs: 0);
        return view.Count;
    }

    /// <summary>Measures binding the even numbers from a SourceList.</summary>
    /// <returns>The filtered view count.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "View")]
    public int SourceList_FilteredBind()
    {
        using var list = new SourceList<int>();
        list.AddRange(_numbers);
        using var subscription = list.Connect()
            .Filter(static item => (item & 1) == 0)
            .Bind(out ReadOnlyObservableCollection<int> view)
            .SubscribeObserver(static _ => { });
        return view.Count;
    }

    /// <summary>Measures dynamically filtering a ReactiveList view to even numbers.</summary>
    /// <returns>The filtered view count.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "DynamicView")]
    public int ReactiveList_DynamicFilteredView()
    {
        using var list = new ReactiveList<int>(_numbers);
        using var filter = new BehaviorSignal<Func<int, bool>>(static item => item >= 0);
        using var view = list.CreateView(filter, Sequencer.Immediate, throttleMs: 0);
        filter.OnNext(static item => (item & 1) == 0);
        return view.Count;
    }

    /// <summary>Measures dynamically filtering a SourceList binding to even numbers.</summary>
    /// <returns>The filtered view count.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "DynamicView")]
    public int SourceList_DynamicFilteredBind()
    {
        using var list = new SourceList<int>();
        list.AddRange(_numbers);
        using var filter = new BehaviorSignal<Func<int, bool>>(static item => item >= 0);
        using var subscription = list.Connect()
            .Filter(filter)
            .Bind(out ReadOnlyObservableCollection<int> view)
            .SubscribeObserver(static _ => { });
        filter.OnNext(static item => (item & 1) == 0);
        return view.Count;
    }

    /// <summary>Measures adding all items to a QuaternaryList.</summary>
    /// <returns>The resulting list count.</returns>
    [Benchmark]
    [BenchmarkCategory("QuaternaryList", "AddRange")]
    public int QuaternaryList_AddRange()
    {
        using var list = new QuaternaryList<MicroItem>();
        list.AddRange(_items);
        return list.Count;
    }

    /// <summary>Measures adding all items to a DynamicData SourceList.</summary>
    /// <returns>The resulting list count.</returns>
    [Benchmark]
    [BenchmarkCategory("QuaternaryList", "AddRange")]
    public int SourceList_ItemAddRange()
    {
        using var list = new SourceList<MicroItem>();
        list.AddRange(_items);
        return list.Count;
    }

    /// <summary>Measures looking up the group-one items through a QuaternaryList secondary index.</summary>
    /// <returns>The number of matching items.</returns>
    [Benchmark]
    [BenchmarkCategory("QuaternaryList", "Lookup")]
    public int QuaternaryList_SecondaryLookup()
    {
        var matches = 0;
        foreach (var _ in _indexedList!.GetItemsBySecondaryIndex(GroupIndexName, 1))
        {
            matches++;
        }

        return matches;
    }

    /// <summary>Measures scanning a SourceList for group-one items.</summary>
    /// <returns>The number of matching items.</returns>
    [Benchmark]
    [BenchmarkCategory("QuaternaryList", "Lookup")]
    public int SourceList_SecondaryScan()
    {
        var matches = 0;
        foreach (var item in _sourceItemList!.Items)
        {
            if (item.Group == 1)
            {
                matches++;
            }
        }

        return matches;
    }

    /// <summary>Measures adding all pairs to a QuaternaryDictionary.</summary>
    /// <returns>The resulting dictionary count.</returns>
    [Benchmark]
    [BenchmarkCategory("Dictionary", "AddRange")]
    public int QuaternaryDictionary_AddRange()
    {
        using var dictionary = new QuaternaryDictionary<int, MicroItem>();
        dictionary.AddRange(_pairs);
        return dictionary.Count;
    }

    /// <summary>Measures adding or updating all items in a DynamicData SourceCache.</summary>
    /// <returns>The resulting cache count.</returns>
    [Benchmark]
    [BenchmarkCategory("Dictionary", "AddRange")]
    public int SourceCache_AddOrUpdateRange()
    {
        using var cache = new SourceCache<MicroItem, int>(static item => item.Id);
        cache.AddOrUpdate(_items);
        return cache.Count;
    }

    /// <summary>Measures looking up the last item in a QuaternaryDictionary.</summary>
    /// <returns><see langword="true"/> when the item is found; otherwise, <see langword="false"/>.</returns>
    [Benchmark]
    [BenchmarkCategory("Dictionary", "Lookup")]
    public bool QuaternaryDictionary_TryGetValue() => _indexedDictionary!.TryGetValue(Count - 1, out _);

    /// <summary>Measures looking up the last item in a DynamicData SourceCache.</summary>
    /// <returns><see langword="true"/> when the item is found; otherwise, <see langword="false"/>.</returns>
    [Benchmark]
    [BenchmarkCategory("Dictionary", "Lookup")]
    public bool SourceCache_Lookup() => _sourceCache!.Lookup(Count - 1).HasValue;

    /// <summary>Measures looking up group-one values through a QuaternaryDictionary secondary index.</summary>
    /// <returns>The number of matching values.</returns>
    [Benchmark]
    [BenchmarkCategory("Dictionary", "SecondaryLookup")]
    public int QuaternaryDictionary_SecondaryLookup()
    {
        var matches = 0;
        foreach (var _ in _indexedDictionary!.GetValuesBySecondaryIndex(GroupIndexName, 1))
        {
            matches++;
        }

        return matches;
    }

    /// <summary>Measures scanning a SourceCache for group-one items.</summary>
    /// <returns>The number of matching items.</returns>
    [Benchmark]
    [BenchmarkCategory("Dictionary", "SecondaryLookup")]
    public int SourceCache_SecondaryScan()
    {
        var matches = 0;
        foreach (var item in _sourceCache!.Items)
        {
            if (item.Group == 1)
            {
                matches++;
            }
        }

        return matches;
    }

    /// <summary>Represents an item used by the micro benchmarks.</summary>
    /// <param name="Id">The item's identifier.</param>
    /// <param name="Group">The item's secondary-index group.</param>
    /// <param name="Value">The item's payload value.</param>
    private readonly record struct MicroItem(int Id, int Group, int Value);
}
