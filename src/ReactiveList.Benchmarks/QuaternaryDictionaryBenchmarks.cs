// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using CP.Primitives.Collections;
using DynamicData;

namespace ReactiveList.Benchmarks;

/// <summary>Provides QuaternaryDictionaryBenchmarks.</summary>
[MemoryDiagnoser]
public class QuaternaryDictionaryBenchmarks
{
    /// <summary>Divides a count in half for partial-removal benchmarks.</summary>
    private const int HalfCountDivisor = 2;

    /// <summary>Identifies the value used to probe a secondary index.</summary>
    private const int IndexedProbeValue = 4;

    /// <summary>Specifies the minimum size needed to exercise the parallel add path.</summary>
    private const int MinimumLargeDatasetCount = 500;

    /// <summary>Provides the divisor used by modulo-four operations.</summary>
    private const int ModuloFourDivisor = 4;

    /// <summary>Provides the divisor used by modulo-three operations.</summary>
    private const int ModuloThreeDivisor = 3;

    /// <summary>Provides the divisor used by modulo-two operations.</summary>
    private const int ModuloTwoDivisor = 2;

    /// <summary>Provides the divisor used by modulo-five operations.</summary>
    private const int ModuloFiveDivisor = 5;

    /// <summary>Controls how frequently items are removed in mixed-operation benchmarks.</summary>
    private const int PeriodicRemovalDivisor = 10;

    /// <summary>Provides the multiplier used when benchmark values are updated.</summary>
    private const int ValueMultiplier = 2;

    /// <summary>Stores the key/value input shared by dictionary benchmarks.</summary>
    private KeyValuePair<int, int>[] _kvps = [];

    /// <summary>Stores the keyed items shared by source-cache benchmarks.</summary>
    private Item[] _items = [];

    /// <summary>Gets or sets the item count.</summary>
    [Params(100, 1_000, 10_000)]
    public int Count { get; set; }

    /// <summary>Provides Setup.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _kvps = new KeyValuePair<int, int>[Count];
        _items = new Item[Count];
        for (var i = 0; i < Count; i++)
        {
            _kvps[i] = new(i, i);
            _items[i] = new(i, i);
        }
    }

    /// <summary>Provides Dictionary_AddRange.</summary>
    /// <returns>The result.</returns>
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

    /// <summary>Provides QuaternaryDictionary_AddRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_AddRange()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        return dict.Count;
    }

    /// <summary>Provides SourceCache_AddRange.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_AddRange()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        cache.AddOrUpdate(_items);
        return cache.Count;
    }

    /// <summary>Provides Dictionary_Remove.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int Dictionary_Remove()
    {
        var dict = new Dictionary<int, int>(_kvps);
        for (var i = 0; i < Count / HalfCountDivisor; i++)
        {
            _ = dict.Remove(i);
        }

        return dict.Count;
    }

    /// <summary>Provides QuaternaryDictionary_Remove.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_Remove()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        for (var i = 0; i < Count / HalfCountDivisor; i++)
        {
            _ = dict.Remove(i);
        }

        return dict.Count;
    }

    /// <summary>Provides SourceCache_Remove.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_Remove()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        cache.AddOrUpdate(_items);
        cache.RemoveKeys(Enumerable.Range(0, Count / HalfCountDivisor));
        return cache.Count;
    }

    /// <summary>Provides QuaternaryDictionary_RemoveKeys.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_RemoveKeys()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        dict.RemoveKeys(Enumerable.Range(0, Count / HalfCountDivisor));
        return dict.Count;
    }

    /// <summary>Provides Dictionary_Clear.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int Dictionary_Clear()
    {
        var dict = new Dictionary<int, int>(_kvps);
        dict.Clear();
        return dict.Count;
    }

    /// <summary>Provides QuaternaryDictionary_Clear.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_Clear()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        dict.Clear();
        return dict.Count;
    }

    /// <summary>Provides SourceCache_Clear.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_Clear()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        cache.AddOrUpdate(_items);
        cache.Clear();
        return cache.Count;
    }

    /// <summary>Provides Dictionary_TryGetValue.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool Dictionary_TryGetValue()
    {
        var dict = new Dictionary<int, int>(_kvps);
        return dict.TryGetValue(Count - 1, out _);
    }

    /// <summary>Provides QuaternaryDictionary_TryGetValue.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool QuaternaryDictionary_TryGetValue()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        return dict.TryGetValue(Count - 1, out _);
    }

    /// <summary>Provides QuaternaryDictionary_Lookup.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool QuaternaryDictionary_Lookup()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        return dict.Lookup(Count - 1).HasValue;
    }

    /// <summary>Provides SourceCache_Lookup.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool SourceCache_Lookup()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        cache.AddOrUpdate(_items);
        return cache.Lookup(Count - 1).HasValue;
    }

    /// <summary>Provides QuaternaryDictionary_Stream_Add.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_Stream_Add()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        var events = 0;
        using var sub = dict.Stream.SubscribeObserver(_ => events++);
        dict.AddRange(_kvps);
        return events;
    }

    /// <summary>Provides SourceCache_Stream_Add.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_Stream_Add()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        var events = 0;
        using var sub = cache.Connect().SubscribeObserver(_ => events++);
        cache.AddOrUpdate(_items);
        return events;
    }

    /// <summary>Provides QuaternaryDictionary_Edit.</summary>
    /// <returns>The result.</returns>
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
                innerDict.Add(i, i * ValueMultiplier);
            }
        });
        return dict.Count;
    }

    /// <summary>Provides SourceCache_Edit.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_Edit()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        cache.AddOrUpdate(_items);
        cache.Edit(innerCache =>
        {
            innerCache.Clear();
            for (var i = 0; i < Count; i++)
            {
                innerCache.AddOrUpdate(new Item(i, i * ValueMultiplier));
            }
        });
        return cache.Count;
    }

    /// <summary>Provides QuaternaryDictionary_RemoveMany.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_RemoveMany()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        _ = dict.RemoveMany(static kvp => kvp.Key % ModuloTwoDivisor == 0);
        return dict.Count;
    }

    /// <summary>Provides QuaternaryDictionary_VersionTracking.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public long QuaternaryDictionary_VersionTracking()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        var initialVersion = dict.Version;
        dict.AddRange(_kvps);
        _ = dict.RemoveMany(static kvp => kvp.Key % ModuloTwoDivisor == 0);
        dict.Clear();
        return dict.Version - initialVersion;
    }

    /// <summary>Provides QuaternaryDictionary_ValueIndex.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_ValueIndex()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddValueIndex("Mod2", static value => value % ModuloTwoDivisor);
        dict.AddRange(_kvps);
        return CountItems(dict.GetValuesBySecondaryIndex("Mod2", 0));
    }

    /// <summary>Provides QuaternaryDictionary_ParallelAdd.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_ParallelAdd()
    {
        using var dict = new QuaternaryDictionary<int, int>();

        // Large dataset to trigger parallel processing (threshold is 256)
        var largeKvps = new KeyValuePair<int, int>[Math.Max(Count, MinimumLargeDatasetCount)];
        for (var i = 0; i < largeKvps.Length; i++)
        {
            largeKvps[i] = new(i, i);
        }

        dict.AddRange(largeKvps);
        return dict.Count;
    }

    /// <summary>Provides QuaternaryDictionary_IterateAll.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_IterateAll()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        var sum = 0;
        foreach (var kvp in dict)
        {
            sum += kvp.Value;
        }

        return sum;
    }

    /// <summary>Provides Dictionary_IterateAll.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int Dictionary_IterateAll()
    {
        var dict = new Dictionary<int, int>(_kvps);
        var sum = 0;
        foreach (var kvp in dict)
        {
            sum += kvp.Value;
        }

        return sum;
    }

    /// <summary>Provides QuaternaryDictionary_AddOrUpdate.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_AddOrUpdate()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        for (var i = 0; i < Count; i++)
        {
            dict.AddOrUpdate(i, i);
        }

        // Update existing
        for (var i = 0; i < Count / HalfCountDivisor; i++)
        {
            dict.AddOrUpdate(i, i * ValueMultiplier);
        }

        return dict.Count;
    }

    /// <summary>Provides QuaternaryDictionary_Keys.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_Keys()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        return dict.Keys.Count;
    }

    /// <summary>Provides QuaternaryDictionary_Values.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_Values()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        return dict.Values.Count;
    }

    /// <summary>Provides QuaternaryDictionary_Enumerate.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_Enumerate()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        var count = 0;
        foreach (var kvp in dict)
        {
            count += kvp.Key >= 0 ? 1 : 0;
        }

        return count;
    }

    /// <summary>Provides Dictionary_ContainsKey.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool Dictionary_ContainsKey()
    {
        var dict = new Dictionary<int, int>(_kvps);
        return dict.ContainsKey(Count - 1);
    }

    /// <summary>Provides QuaternaryDictionary_Contains.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool QuaternaryDictionary_Contains()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        return dict.Contains(new(Count - 1, Count - 1));
    }

    /// <summary>Provides Dictionary_IndexerGet.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int Dictionary_IndexerGet()
    {
        var dict = new Dictionary<int, int>(_kvps);
        var sum = 0;
        for (var i = 0; i < Count; i++)
        {
            sum += dict[i];
        }

        return sum;
    }

    /// <summary>Provides QuaternaryDictionary_IndexerGet.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_IndexerGet()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        var sum = 0;
        for (var i = 0; i < Count; i++)
        {
            sum += dict[i];
        }

        return sum;
    }

    /// <summary>Provides Dictionary_IndexerSet.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int Dictionary_IndexerSet()
    {
        var dict = new Dictionary<int, int>();
        for (var i = 0; i < Count; i++)
        {
            dict[i] = i;
        }

        return dict.Count;
    }

    /// <summary>Provides QuaternaryDictionary_IndexerSet.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_IndexerSet()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        for (var i = 0; i < Count; i++)
        {
            dict[i] = i;
        }

        return dict.Count;
    }

    /// <summary>Provides Dictionary_Keys.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int Dictionary_Keys()
    {
        var dict = new Dictionary<int, int>(_kvps);
        return dict.Keys.Count;
    }

    /// <summary>Provides SourceCache_Keys.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_Keys()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        cache.AddOrUpdate(_items);
        return CountItems(cache.Keys);
    }

    /// <summary>Provides Dictionary_Values.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int Dictionary_Values()
    {
        var dict = new Dictionary<int, int>(_kvps);
        return dict.Values.Count;
    }

    /// <summary>Provides SourceCache_Values.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_Values()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        cache.AddOrUpdate(_items);
        return CountItems(cache.Items);
    }

    /// <summary>Provides SourceCache_IterateAll.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_IterateAll()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        cache.AddOrUpdate(_items);
        var sum = 0;
        foreach (var item in cache.Items)
        {
            sum += item.Value;
        }

        return sum;
    }

    /// <summary>Provides QuaternaryDictionary_Stream_Remove.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_Stream_Remove()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        var events = 0;
        using var sub = dict.Stream.SubscribeObserver(_ => events++);
        dict.RemoveKeys(Enumerable.Range(0, Count / HalfCountDivisor));
        return events;
    }

    /// <summary>Provides SourceCache_Stream_Remove.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_Stream_Remove()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        cache.AddOrUpdate(_items);
        var events = 0;
        using var sub = cache.Connect().SubscribeObserver(_ => events++);
        cache.RemoveKeys(Enumerable.Range(0, Count / HalfCountDivisor));
        return events;
    }

    /// <summary>Provides QuaternaryDictionary_AddValueIndex.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_AddValueIndex()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        dict.AddValueIndex("Mod2", static value => value % ModuloTwoDivisor);
        return dict.Count;
    }

    /// <summary>Provides QuaternaryDictionary_QueryValueIndex.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_QueryValueIndex() => QuaternaryDictionary_ValueIndex();

    /// <summary>Provides QuaternaryDictionary_ValueMatchesSecondaryIndex.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public bool QuaternaryDictionary_ValueMatchesSecondaryIndex()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddValueIndex("Mod2", static value => value % ModuloTwoDivisor);
        dict.AddRange(_kvps);
        return dict.ValueMatchesSecondaryIndex("Mod2", IndexedProbeValue, 0);
    }

    /// <summary>Provides QuaternaryDictionary_MultipleValueIndices.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_MultipleValueIndices()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddValueIndex("Mod2", static value => value % ModuloTwoDivisor);
        dict.AddValueIndex("Mod3", static value => value % ModuloThreeDivisor);
        dict.AddValueIndex("Mod5", static value => value % ModuloFiveDivisor);
        dict.AddRange(_kvps);
        return CountItems(dict.GetValuesBySecondaryIndex("Mod2", 0))
            + CountItems(dict.GetValuesBySecondaryIndex("Mod3", 0))
            + CountItems(dict.GetValuesBySecondaryIndex("Mod5", 0));
    }

    /// <summary>Provides QuaternaryDictionary_IndexWithAddRemove.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_IndexWithAddRemove()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddValueIndex("Mod2", static value => value % ModuloTwoDivisor);
        dict.AddRange(_kvps);
        _ = dict.RemoveMany(static kvp => kvp.Key % ModuloFourDivisor == 0);
        return CountItems(dict.GetValuesBySecondaryIndex("Mod2", 0));
    }

    /// <summary>Provides Dictionary_Count.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int Dictionary_Count()
    {
        var dict = new Dictionary<int, int>(_kvps);
        return dict.Count;
    }

    /// <summary>Provides QuaternaryDictionary_Count.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_Count() => QuaternaryDictionary_AddRange();

    /// <summary>Provides SourceCache_Count.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_Count() => SourceCache_AddRange();

    /// <summary>Provides QuaternaryDictionary_MixedOperations.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_MixedOperations()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        dict.AddRange(_kvps);
        dict.AddOrUpdate(Count, Count);
        _ = dict.Remove(0);
        _ = dict.RemoveMany(static kvp => kvp.Key % PeriodicRemovalDivisor == 0);
        return dict.Count;
    }

    /// <summary>Provides SourceCache_MixedOperations.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_MixedOperations()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        cache.AddOrUpdate(_items);
        cache.AddOrUpdate(new Item(Count, Count));
        cache.Remove(0);
        List<int> keysToRemove = [];
        foreach (var key in cache.Keys)
        {
            if (key % PeriodicRemovalDivisor == 0)
            {
                keysToRemove.Add(key);
            }
        }

        cache.RemoveKeys(keysToRemove);
        return cache.Count;
    }

    /// <summary>Provides Dictionary_MixedOperations.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int Dictionary_MixedOperations()
    {
        var dict = new Dictionary<int, int>(_kvps);
        dict[Count] = Count;
        _ = dict.Remove(0);
        List<int> keysToRemove = [];
        foreach (var key in dict.Keys)
        {
            if (key % PeriodicRemovalDivisor == 0)
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _ = dict.Remove(key);
        }

        return dict.Count;
    }

    /// <summary>Provides Dictionary_Add.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int Dictionary_Add()
    {
        var dict = new Dictionary<int, int>();
        for (var i = 0; i < Count; i++)
        {
            dict.Add(i, i);
        }

        return dict.Count;
    }

    /// <summary>Provides QuaternaryDictionary_Add.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_Add()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        for (var i = 0; i < Count; i++)
        {
            dict.Add(i, i);
        }

        return dict.Count;
    }

    /// <summary>Provides SourceCache_Add.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_Add()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        for (var i = 0; i < Count; i++)
        {
            cache.AddOrUpdate(new Item(i, i));
        }

        return cache.Count;
    }

    /// <summary>Provides Dictionary_TryAdd.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int Dictionary_TryAdd()
    {
        var dict = new Dictionary<int, int>();
        for (var i = 0; i < Count; i++)
        {
            _ = dict.TryAdd(i, i);
        }

        return dict.Count;
    }

    /// <summary>Provides QuaternaryDictionary_TryAdd.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryDictionary_TryAdd()
    {
        using var dict = new QuaternaryDictionary<int, int>();
        for (var i = 0; i < Count; i++)
        {
            _ = dict.TryAdd(i, i);
        }

        return dict.Count;
    }

    /// <summary>Provides Dictionary_AddOrUpdate.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int Dictionary_AddOrUpdate()
    {
        var dict = new Dictionary<int, int>();
        for (var i = 0; i < Count; i++)
        {
            dict[i] = i;
        }

        // Update existing
        for (var i = 0; i < Count / HalfCountDivisor; i++)
        {
            dict[i] = i * ValueMultiplier;
        }

        return dict.Count;
    }

    /// <summary>Provides SourceCache_AddOrUpdate.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_AddOrUpdate()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        for (var i = 0; i < Count; i++)
        {
            cache.AddOrUpdate(new Item(i, i));
        }

        // Update existing
        for (var i = 0; i < Count / HalfCountDivisor; i++)
        {
            cache.AddOrUpdate(new Item(i, i * ValueMultiplier));
        }

        return cache.Count;
    }

    /// <summary>Provides SourceCache_RemoveKeys.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_RemoveKeys() => SourceCache_Remove();

    /// <summary>Provides SourceCache_RemoveMany.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceCache_RemoveMany()
    {
        using var cache = new SourceCache<Item, int>(static x => x.Id);
        cache.AddOrUpdate(_items);
        List<int> keysToRemove = [];
        foreach (var key in cache.Keys)
        {
            if (key % ModuloTwoDivisor == 0)
            {
                keysToRemove.Add(key);
            }
        }

        cache.RemoveKeys(keysToRemove);
        return cache.Count;
    }

    /// <summary>Counts an explicitly enumerated sequence without adding LINQ overhead to a benchmark.</summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="items">The items to count.</param>
    /// <returns>The number of enumerated items.</returns>
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

    /// <summary>Provides Item.</summary>
    /// <param name="Id">The Id value.</param>
    /// <param name="Value">The Value.</param>
    private sealed record Item(int Id, int Value);
}
