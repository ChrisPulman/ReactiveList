// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using BenchmarkDotNet.Attributes;
using CP.Reactive;
using CP.Reactive.Collections;
using DynamicData;
using DynamicData.Binding;

namespace ReactiveList.Benchmarks;

/// <summary>Provides DynamicDataParityBenchmarks.</summary>
[MemoryDiagnoser]
public class DynamicDataParityBenchmarks
{
    private int[] _data = [];

    /// <summary>Gets or sets the item count.</summary>
    [Params(1_000, 10_000)]
    public int Count { get; set; }

    /// <summary>Provides Setup.</summary>
    [GlobalSetup]
    public void Setup() => _data = Enumerable.Range(0, Count).ToArray();

    /// <summary>Provides ReactiveList_Connect_Preloaded_InitialSnapshot.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int ReactiveList_Connect_Preloaded_InitialSnapshot()
    {
        using var list = new ReactiveList<int>(_data);
        var total = 0;
        using var subscription = list.Connect().SubscribeObserver(changes => total += changes.Count);
        return total;
    }

    /// <summary>Provides SourceList_Connect_Preloaded_InitialSnapshot.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Connect_Preloaded_InitialSnapshot()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        var total = 0;
        using var subscription = list.Connect().SubscribeObserver(changes => total += changes.TotalChanges);
        return total;
    }

    /// <summary>Provides ReactiveList_FilterTransformSort.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int ReactiveList_FilterTransformSort()
    {
        using var list = new ReactiveList<int>();
        var total = 0;
        var pipeline = CP.Reactive.ReactiveListExtensions.SortBy<int, int>(
            list.Connect()
                .WhereChanges(static change => change.Current % 2 == 0)
                .SelectChanges(static (int item) => item * 2),
            static item => item);
        using var subscription = pipeline.SubscribeObserver(changes => total += changes.Count);

        list.AddRange(_data);
        return total;
    }

    /// <summary>Provides SourceList_FilterTransformSortBind.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_FilterTransformSortBind()
    {
        using var list = new SourceList<int>();
        using var subscription = list.Connect()
            .Filter(static item => item % 2 == 0)
            .Transform(static item => item * 2)
            .Sort(SortExpressionComparer<int>.Ascending(static item => item))
            .Bind(out ReadOnlyObservableCollection<int> bound)
            .SubscribeObserver(_ => { });

        list.AddRange(_data);
        return bound.Count;
    }

    /// <summary>Provides ReactiveList_INCC_AddRange_WithItemsSubscriber.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int ReactiveList_INCC_AddRange_WithItemsSubscriber()
    {
        using var list = new ReactiveList<int>();
        var events = 0;
        ((INotifyCollectionChanged)list.Items).CollectionChanged += (_, _) => events++;

        list.AddRange(_data);
        return events;
    }

    /// <summary>Provides SourceList_INCC_AddRange_WithBoundSubscriber.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_INCC_AddRange_WithBoundSubscriber()
    {
        using var list = new SourceList<int>();
        using var subscription = list.Connect()
            .Bind(out ReadOnlyObservableCollection<int> bound)
            .SubscribeObserver(_ => { });
        var events = 0;
        ((INotifyCollectionChanged)bound).CollectionChanged += (_, _) => events++;

        list.AddRange(_data);
        return events;
    }

    /// <summary>Provides QuaternaryList_Stream_AddRange_DeliveryWait.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int QuaternaryList_Stream_AddRange_DeliveryWait()
    {
        using var list = new QuaternaryList<int>();
        using var delivered = new ManualResetEventSlim();
        var events = 0;
        using var subscription = list.Stream.SubscribeObserver(notification =>
        {
            events++;
            notification.Batch?.Dispose();
            delivered.Set();
        });

        list.AddRange(_data);
        delivered.Wait(TimeSpan.FromSeconds(1));
        return events;
    }

    /// <summary>Provides SourceList_Stream_AddRange_Delivery.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public int SourceList_Stream_AddRange_Delivery()
    {
        using var list = new SourceList<int>();
        var events = 0;
        using var subscription = list.Connect().SubscribeObserver(_ => events++);

        list.AddRange(_data);
        return events;
    }
}
