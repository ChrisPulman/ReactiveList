// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using CP.Primitives.Collections;
using ReactiveListTestApp.Models;

namespace ReactiveListTestApp.Services;

/// <summary>Generates high-rate value events and publishes bounded immutable frames.</summary>
internal sealed class LiveDataEngine : IDisposable
{
    private const int InstrumentCount = 40;

    private const int SampleCount = 12;

    private const int FramesPerSecond = 8;

    private const int MinimumRate = 1_000;

    private const int MaximumRate = 100_000;

    private const int MovementCentre = 1_024;

    private const int VolumeRange = 500;

    private const double BaseOpeningPrice = 70d;

    private const double OpeningPriceStep = 2.75d;

    private const double MovementScale = 0.00000015d;

    private const double MinimumPrice = 0.01d;

    private const double PercentageMultiplier = 100d;

    private const double BaseLatencyMilliseconds = 0.04d;

    private const double LatencyDivisor = 34d;

    private const double ChangeAlertThreshold = 0.65d;

    private const double LatencyAlertThreshold = 6d;

    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(1_000d / FramesPerSecond);

    private static readonly string[] Symbols = CreateSymbols();

    private static readonly string[] Sectors = ["Energy", "Finance", "Health", "Industrials", "Technology"];

    private static readonly string[] Venues = ["LSE", "XNAS", "XNYS", "XEUR"];

    private readonly InstrumentState[] _states = CreateStates();

    private readonly QuadList<MarketTick> _hotTicks = [];

    private readonly QuadDictionary<int, InstrumentSnapshot> _latestByInstrument = [];

    private readonly object _lifecycleGate = new();

    private readonly object _generationGate = new();

    private CancellationTokenSource? _cancellation;

    private Task? _runTask;

    private long _sequence;

    private long _totalEvents;

    private uint _randomState = 0xA341316Cu;

    private bool _paused;

    private bool _disposed;

    /// <summary>Occurs after the worker has generated and aggregated one projection frame.</summary>
    public event EventHandler<MarketFrame>? FrameProduced;

    /// <summary>Gets or sets the requested raw event throughput.</summary>
    public int TargetEventsPerSecond
    {
        get => Volatile.Read(ref field);
        set => Volatile.Write(ref field, Math.Clamp(value, MinimumRate, MaximumRate));
    } = 10_000;

    /// <summary>Gets a value indicating whether continuous publication is paused.</summary>
    public bool IsPaused => Volatile.Read(ref _paused);

    /// <summary>Gets the number of raw ticks in the current pooled scratch collection.</summary>
    public int HotTickCapacityCount => _hotTicks.Count;

    /// <summary>Gets the number of current entries in the pooled aggregation dictionary.</summary>
    public int HotDictionaryCount => _latestByInstrument.Count;

    /// <summary>Starts or resumes the continuous producer.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lifecycleGate)
        {
            if (_runTask is not null)
            {
                _paused = false;
                return;
            }

            _cancellation = new();
            _paused = false;
            _runTask = Task.Run(() => RunAsync(_cancellation.Token));
        }
    }

    /// <summary>Switches between continuous generation and a paused state.</summary>
    public void TogglePause() => _paused = !_paused;

    /// <summary>Generates and aggregates one deterministic-size raw event batch.</summary>
    /// <param name="eventCount">The number of raw events to process.</param>
    /// <returns>The immutable projection frame.</returns>
    public MarketFrame GenerateFrame(int eventCount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventCount);
        lock (_generationGate)
        {
            return GenerateFrameCore(eventCount);
        }
    }

    /// <summary>Resets generated state while retaining pooled collection storage.</summary>
    public void Reset()
    {
        lock (_generationGate)
        {
            var initialStates = CreateStates();
            initialStates.CopyTo(_states, 0);
            _hotTicks.Clear();
            _latestByInstrument.Clear();
            Interlocked.Exchange(ref _sequence, 0);
            Interlocked.Exchange(ref _totalEvents, 0);
            _randomState = 0xA341316Cu;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_lifecycleGate)
        {
            _cancellation?.Cancel();
        }

        try
        {
            _runTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _cancellation?.Dispose();
        _hotTicks.Dispose();
        _latestByInstrument.Dispose();
    }

    /// <summary>Creates stable display symbols once for the lifetime of the process.</summary>
    /// <returns>The symbol table.</returns>
    private static string[] CreateSymbols()
    {
        var symbols = new string[InstrumentCount];
        for (var i = 0; i < symbols.Length; i++)
        {
            symbols[i] = $"RL{i + 1:00}";
        }

        return symbols;
    }

    /// <summary>Creates deterministic initial aggregation states.</summary>
    /// <returns>The initial state array.</returns>
    private static InstrumentState[] CreateStates()
    {
        var states = new InstrumentState[InstrumentCount];
        for (var i = 0; i < states.Length; i++)
        {
            var openingPrice = BaseOpeningPrice + (i * OpeningPriceStep);
            states[i] = new(openingPrice, openingPrice, 0);
        }

        return states;
    }

    /// <summary>Runs the ten-frame-per-second projection clock.</summary>
    /// <param name="cancellationToken">Stops the producer.</param>
    /// <returns>A task that completes when cancellation is requested.</returns>
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(FrameInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_paused)
            {
                continue;
            }

            var eventCount = Math.Max(1, TargetEventsPerSecond / FramesPerSecond);
            FrameProduced?.Invoke(this, GenerateFrame(eventCount));
        }
    }

    /// <summary>Generates a frame while the caller holds the generation lock.</summary>
    /// <param name="eventCount">The number of raw events.</param>
    /// <returns>The generated frame.</returns>
    private MarketFrame GenerateFrameCore(int eventCount)
    {
        var started = Stopwatch.GetTimestamp();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var rented = ArrayPool<MarketTick>.Shared.Rent(eventCount);
        try
        {
            var ticks = rented.AsSpan(0, eventCount);
            GenerateTicks(ticks);
            _hotTicks.Clear();
            ReadOnlySpan<MarketTick> readOnlyTicks = ticks;
            _hotTicks.AddRange(in readOnlyTicks);
            Aggregate(readOnlyTicks);

            var snapshots = CreateSnapshots();
            var samples = new MarketTick[Math.Min(SampleCount, eventCount)];
            ticks[..samples.Length].CopyTo(samples);
            var total = Interlocked.Add(ref _totalEvents, eventCount);
            var sequence = Interlocked.Increment(ref _sequence);
            var elapsed = Stopwatch.GetElapsedTime(started);
            var allocated = Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
            return new MarketFrame(sequence, eventCount, total, elapsed, allocated, snapshots, samples, DateTimeOffset.Now);
        }
        finally
        {
            ArrayPool<MarketTick>.Shared.Return(rented, clearArray: false);
        }
    }

    /// <summary>Fills a caller-provided span with raw value events.</summary>
    /// <param name="target">The target span.</param>
    private void GenerateTicks(Span<MarketTick> target)
    {
        for (var i = 0; i < target.Length; i++)
        {
            var random = NextRandom();
            var instrumentId = (int)(random % InstrumentCount);
            ref var state = ref _states[instrumentId];
            var movementUnits = (int)((random >> 8) & 0x7FF) - MovementCentre;
            var movement = movementUnits * MovementScale;
            var price = Math.Max(MinimumPrice, state.LastPrice * (1d + movement));
            var volume = 1 + (int)((random >> 20) % VolumeRange);
            target[i] = new(
                Interlocked.Increment(ref _sequence),
                instrumentId,
                price,
                volume,
                Stopwatch.GetTimestamp(),
                (random & 1u) == 0);
        }
    }

    /// <summary>Aggregates a raw span without allocating per event.</summary>
    /// <param name="ticks">The raw events.</param>
    private void Aggregate(ReadOnlySpan<MarketTick> ticks)
    {
        foreach (ref readonly var tick in ticks)
        {
            ref var state = ref _states[tick.InstrumentId];
            state.LastPrice = tick.Price;
            state.Volume += tick.Volume;
        }
    }

    /// <summary>Projects mutable aggregation state into immutable UI snapshots.</summary>
    /// <returns>The fixed-size snapshot array.</returns>
    private InstrumentSnapshot[] CreateSnapshots()
    {
        var now = DateTimeOffset.Now;
        var snapshots = new InstrumentSnapshot[InstrumentCount];
        for (var i = 0; i < snapshots.Length; i++)
        {
            ref var state = ref _states[i];
            var change = ((state.LastPrice / state.OpeningPrice) - 1d) * PercentageMultiplier;
            var latency = BaseLatencyMilliseconds + ((NextRandom() & 0xFF) / LatencyDivisor);
            var snapshot = new InstrumentSnapshot(
                Volatile.Read(ref _sequence),
                i,
                Symbols[i],
                Sectors[i % Sectors.Length],
                Venues[i % Venues.Length],
                state.LastPrice,
                change,
                state.Volume,
                latency,
                Math.Abs(change) >= ChangeAlertThreshold || latency >= LatencyAlertThreshold,
                now);
            snapshots[i] = snapshot;
            _latestByInstrument[i] = snapshot;
        }

        return snapshots;
    }

    /// <summary>Advances the local xorshift random generator.</summary>
    /// <returns>The next pseudo-random value.</returns>
    private uint NextRandom()
    {
        var value = _randomState;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        _randomState = value;
        return value;
    }

    /// <summary>Stores mutable producer-only aggregation state.</summary>
    /// <param name="openingPrice">The opening reference price.</param>
    /// <param name="lastPrice">The latest price.</param>
    /// <param name="volume">The accumulated volume.</param>
    private struct InstrumentState(double openingPrice, double lastPrice, long volume)
    {
        /// <summary>Gets the immutable opening reference price.</summary>
        public readonly double OpeningPrice { get; } = openingPrice;

        /// <summary>Gets or sets the most recent generated price.</summary>
        public double LastPrice { readonly get; set; } = lastPrice;

        /// <summary>Gets or sets accumulated volume.</summary>
        public long Volume { readonly get; set; } = volume;
    }
}
