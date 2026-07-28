// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using BenchmarkDotNet.Attributes;
using CP.Primitives;
using CP.Primitives.Collections;
using DynamicData;
using DynamicData.Binding;

namespace ReactiveList.Benchmarks;

/// <summary>Benchmarks ReactiveList and DynamicData pipelines with equivalent observable behavior.</summary>
[MemoryDiagnoser]
public class DynamicDataParityBenchmarks
{
    /// <summary>Identifies the even values used by parity-based pipelines.</summary>
    private const int ParityDivisor = 2;

    /// <summary>Scales values emitted by transform benchmarks.</summary>
    private const int TransformMultiplier = 2;

    /// <summary>Stores the sequential values supplied to each benchmark invocation.</summary>
    private int[] _data = [];

    /// <summary>Gets or sets the number of sequential items supplied to each benchmark.</summary>
    [Params(1_000, 10_000)]
    public int Count { get; set; }

    /// <summary>Initializes the sequential source data for the configured item count.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var data = new int[Count];
        for (var index = 0; index < data.Length; index++)
        {
            data[index] = index;
        }

        _data = data;
    }

    /// <summary>Benchmarks the initial snapshot delivered when connecting to a preloaded ReactiveList.</summary>
    /// <returns>The number of changes reported by the observer.</returns>
    [Benchmark]
    public int ReactiveList_Connect_Preloaded_InitialSnapshot()
    {
        using var list = new ReactiveList<int>(_data);
        var total = 0;
        using var subscription = list.Connect().SubscribeObserver(changes => total += changes.Count);
        return total;
    }

    /// <summary>Benchmarks the initial snapshot delivered when connecting to a preloaded SourceList.</summary>
    /// <returns>The number of changes reported by the observer.</returns>
    [Benchmark]
    public int SourceList_Connect_Preloaded_InitialSnapshot()
    {
        using var list = new SourceList<int>();
        list.AddRange(_data);
        var total = 0;
        using var subscription = list.Connect().SubscribeObserver(changes => total += changes.TotalChanges);
        return total;
    }

    /// <summary>Benchmarks filtering, transforming, and sorting a ReactiveList change stream.</summary>
    /// <returns>The number of changes delivered by the pipeline.</returns>
    [Benchmark]
    public int ReactiveList_FilterTransformSort()
    {
        using var list = new ReactiveList<int>();
        var total = 0;
        var pipeline = CP.Primitives.ReactiveListExtensions.SortBy(
            list.Connect()
                .WhereChanges(static change => change.Current % ParityDivisor == 0)
                .SelectChanges(static item => item * TransformMultiplier),
            static item => item);
        using var subscription = pipeline.SubscribeObserver(changes => total += changes.Count);

        list.AddRange(_data);
        return total;
    }

    /// <summary>Benchmarks filtering, transforming, sorting, and binding a SourceList change stream.</summary>
    /// <returns>The number of values in the bound collection.</returns>
    [Benchmark]
    public int SourceList_FilterTransformSortBind()
    {
        using var list = new SourceList<int>();
        using var subscription = list.Connect()
            .Filter(static item => item % ParityDivisor == 0)
            .Transform(static item => item * TransformMultiplier)
            .Sort(SortExpressionComparer<int>.Ascending(static item => item))
            .Bind(out ReadOnlyObservableCollection<int> bound)
            .SubscribeObserver(static _ => { });

        list.AddRange(_data);
        return bound.Count;
    }

    /// <summary>Benchmarks item collection-change events raised by a ReactiveList add range.</summary>
    /// <returns>The number of collection-change events raised.</returns>
    [Benchmark]
    public int ReactiveList_INCC_AddRange_WithItemsSubscriber()
    {
        using var list = new ReactiveList<int>();
        var events = 0;
        ((INotifyCollectionChanged)list.Items).CollectionChanged += (_, _) => events++;

        list.AddRange(_data);
        return events;
    }

    /// <summary>Benchmarks bound collection-change events raised by a SourceList add range.</summary>
    /// <returns>The number of collection-change events raised.</returns>
    [Benchmark]
    public int SourceList_INCC_AddRange_WithBoundSubscriber()
    {
        using var list = new SourceList<int>();
        using var subscription = list.Connect()
            .Bind(out ReadOnlyObservableCollection<int> bound)
            .SubscribeObserver(static _ => { });
        var events = 0;
        ((INotifyCollectionChanged)bound).CollectionChanged += (_, _) => events++;

        list.AddRange(_data);
        return events;
    }

    /// <summary>Benchmarks delivery of a QuaternaryList stream notification for an add range.</summary>
    /// <returns>The number of stream notifications received.</returns>
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
        _ = delivered.Wait(TimeSpan.FromSeconds(1));
        return events;
    }

    /// <summary>Benchmarks delivery of a SourceList change stream notification for an add range.</summary>
    /// <returns>The number of change stream notifications received.</returns>
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
