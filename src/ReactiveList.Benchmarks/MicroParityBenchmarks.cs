// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using CP.Primitives;
using CP.Primitives.Collections;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

namespace ReactiveList.Benchmarks;

/// <summary>Provides MicroParityBenchmarks.</summary>
[MemoryDiagnoser]
[CategoriesColumn]
[RankColumn]
public class MicroParityBenchmarks : IDisposable
{
    private const string GroupIndexName = "Group";

    private int[] _numbers = [];

    private int[] _evens = [];

    private MicroItem[] _items = [];

    private KeyValuePair<int, MicroItem>[] _pairs = [];

    private QuaternaryList<MicroItem>? _indexedList;

    private SourceList<MicroItem>? _sourceItemList;

    private QuaternaryDictionary<int, MicroItem>? _indexedDictionary;

    private SourceCache<MicroItem, int>? _sourceCache;

    private bool _disposed;

    /// <summary>Gets or sets the item count.</summary>
    [Params(1, 8, 32, 128)]
    public int Count { get; set; }

    /// <summary>Provides Setup.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _numbers = Enumerable.Range(0, Count).ToArray();
        _evens = _numbers.Where(static item => (item & 1) == 0).ToArray();
        _items = Enumerable.Range(0, Count)
            .Select(static item => new MicroItem(item, item & 3, item * 17))
            .ToArray();
        _pairs = _items.Select(static item => KeyValuePair.Create(item.Id, item)).ToArray();

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

    /// <summary>Provides Cleanup.</summary>
    [GlobalCleanup]
    public void Cleanup() => Dispose(disposing: true);

    /// <summary>Provides Dispose.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Provides ReactiveList_AddRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("List", "AddRange")]
    public int ReactiveList_AddRange()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(_numbers);
        return list.Count;
    }

    /// <summary>Provides SourceList_AddRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "AddRange")]
    public int SourceList_AddRange()
    {
        using var list = new SourceList<int>();
        list.AddRange(_numbers);
        return list.Count;
    }

    /// <summary>Provides ReactiveList_RemoveRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "RemoveRange")]
    public int ReactiveList_RemoveRange()
    {
        using var list = new ReactiveList<int>(_numbers);
        list.RemoveRange(0, Count / 2);
        return list.Count;
    }

    /// <summary>Provides SourceList_RemoveRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "RemoveRange")]
    public int SourceList_RemoveRange()
    {
        using var list = new SourceList<int>();
        list.AddRange(_numbers);
        list.Edit(innerList => innerList.RemoveRange(0, Count / 2));
        return list.Count;
    }

    /// <summary>Provides ReactiveList_RemoveMany.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "RemoveMany")]
    public int ReactiveList_RemoveMany()
    {
        using var list = new ReactiveList<int>(_numbers);
        list.Remove(_evens);
        return list.Count;
    }

    /// <summary>Provides SourceList_RemoveMany.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "RemoveMany")]
    public int SourceList_RemoveMany()
    {
        using var list = new SourceList<int>();
        list.AddRange(_numbers);
        list.RemoveMany(_evens);
        return list.Count;
    }

    /// <summary>Provides ReactiveList_ConnectAddRange.</summary>
    /// <returns>The result.</returns>
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

    /// <summary>Provides SourceList_ConnectAddRange.</summary>
    /// <returns>The result.</returns>
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

    /// <summary>Provides ReactiveList_FilteredView.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "View")]
    public int ReactiveList_FilteredView()
    {
        using var list = new ReactiveList<int>(_numbers);
        using var view = list.CreateView(static item => (item & 1) == 0, Sequencer.Immediate, throttleMs: 0);
        return view.Count;
    }

    /// <summary>Provides SourceList_FilteredBind.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("List", "View")]
    public int SourceList_FilteredBind()
    {
        using var list = new SourceList<int>();
        list.AddRange(_numbers);
        using var subscription = list.Connect()
            .Filter(static item => (item & 1) == 0)
            .Bind(out ReadOnlyObservableCollection<int> view)
            .SubscribeObserver(_ => { });
        return view.Count;
    }

    /// <summary>Provides ReactiveList_DynamicFilteredView.</summary>
    /// <returns>The result.</returns>
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

    /// <summary>Provides SourceList_DynamicFilteredBind.</summary>
    /// <returns>The result.</returns>
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
            .SubscribeObserver(_ => { });
        filter.OnNext(static item => (item & 1) == 0);
        return view.Count;
    }

    /// <summary>Provides QuaternaryList_AddRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("QuaternaryList", "AddRange")]
    public int QuaternaryList_AddRange()
    {
        using var list = new QuaternaryList<MicroItem>();
        list.AddRange(_items);
        return list.Count;
    }

    /// <summary>Provides SourceList_ItemAddRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("QuaternaryList", "AddRange")]
    public int SourceList_ItemAddRange()
    {
        using var list = new SourceList<MicroItem>();
        list.AddRange(_items);
        return list.Count;
    }

    /// <summary>Provides QuaternaryList_SecondaryLookup.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("QuaternaryList", "Lookup")]
    public int QuaternaryList_SecondaryLookup()
    {
        return _indexedList!.GetItemsBySecondaryIndex(GroupIndexName, 1).Count();
    }

    /// <summary>Provides SourceList_SecondaryScan.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("QuaternaryList", "Lookup")]
    public int SourceList_SecondaryScan()
    {
        return _sourceItemList!.Items.Count(static item => item.Group == 1);
    }

    /// <summary>Provides QuaternaryDictionary_AddRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("Dictionary", "AddRange")]
    public int QuaternaryDictionary_AddRange()
    {
        using var dictionary = new QuaternaryDictionary<int, MicroItem>();
        dictionary.AddRange(_pairs);
        return dictionary.Count;
    }

    /// <summary>Provides SourceCache_AddOrUpdateRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("Dictionary", "AddRange")]
    public int SourceCache_AddOrUpdateRange()
    {
        using var cache = new SourceCache<MicroItem, int>(static item => item.Id);
        cache.AddOrUpdate(_items);
        return cache.Count;
    }

    /// <summary>Provides QuaternaryDictionary_TryGetValue.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("Dictionary", "Lookup")]
    public bool QuaternaryDictionary_TryGetValue()
    {
        return _indexedDictionary!.TryGetValue(Count - 1, out _);
    }

    /// <summary>Provides SourceCache_Lookup.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("Dictionary", "Lookup")]
    public bool SourceCache_Lookup()
    {
        return _sourceCache!.Lookup(Count - 1).HasValue;
    }

    /// <summary>Provides QuaternaryDictionary_SecondaryLookup.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("Dictionary", "SecondaryLookup")]
    public int QuaternaryDictionary_SecondaryLookup()
    {
        return _indexedDictionary!.GetValuesBySecondaryIndex(GroupIndexName, 1).Count();
    }

    /// <summary>Provides SourceCache_SecondaryScan.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    [BenchmarkCategory("Dictionary", "SecondaryLookup")]
    public int SourceCache_SecondaryScan()
    {
        return _sourceCache!.Items.Count(static item => item.Group == 1);
    }

    /// <summary>Provides Dispose.</summary>
    /// <param name="disposing">The disposing value.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (!disposing)
        {
            _disposed = true;
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

    /// <summary>Provides MicroItem.</summary>
    /// <param name="Id">The Id value.</param>
    /// <param name="Group">The Group value.</param>
    /// <param name="Value">The Value value.</param>
    private readonly record struct MicroItem(int Id, int Group, int Value);
}
