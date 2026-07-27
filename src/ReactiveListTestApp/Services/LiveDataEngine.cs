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
    /// <summary>The number of instruments projected into each frame.</summary>
    private const int InstrumentCount = 40;

    /// <summary>The maximum number of raw ticks sampled into each frame.</summary>
    private const int SampleCount = 12;

    /// <summary>The number of frames generated each second.</summary>
    private const int FramesPerSecond = 8;

    /// <summary>The minimum accepted target event rate.</summary>
    private const int MinimumRate = 1_000;

    /// <summary>The maximum accepted target event rate.</summary>
    private const int MaximumRate = 100_000;

    /// <summary>The midpoint subtracted from generated movement units.</summary>
    private const int MovementCentre = 1_024;

    /// <summary>The exclusive upper range used for generated volume.</summary>
    private const int VolumeRange = 500;

    /// <summary>The opening price of the first instrument.</summary>
    private const double BaseOpeningPrice = 70D;

    /// <summary>The opening-price increment between consecutive instruments.</summary>
    private const double OpeningPriceStep = 2.75D;

    /// <summary>The scale applied to generated price movement units.</summary>
    private const double MovementScale = 0.00000015D;

    /// <summary>The lower bound applied to generated prices.</summary>
    private const double MinimumPrice = 0.01D;

    /// <summary>The multiplier used to express a ratio as a percentage.</summary>
    private const double PercentageMultiplier = 100D;

    /// <summary>The baseline simulated latency.</summary>
    private const double BaseLatencyMilliseconds = 0.04D;

    /// <summary>The divisor used to scale simulated latency.</summary>
    private const double LatencyDivisor = 34D;

    /// <summary>The absolute percentage change that raises an alert.</summary>
    private const double ChangeAlertThreshold = 0.65D;

    /// <summary>The simulated latency that raises an alert.</summary>
    private const double LatencyAlertThreshold = 6D;

    /// <summary>The delay between continuous producer frames.</summary>
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(1_000D / FramesPerSecond);

    /// <summary>The stable symbols used by generated instruments.</summary>
    private static readonly string[] Symbols = CreateSymbols();

    /// <summary>The sectors distributed across generated instruments.</summary>
    private static readonly string[] Sectors = ["Energy", "Finance", "Health", "Industrials", "Technology"];

    /// <summary>The venues distributed across generated instruments.</summary>
    private static readonly string[] Venues = ["LSE", "XNAS", "XNYS", "XEUR"];

    /// <summary>The producer-owned mutable aggregation states.</summary>
    private readonly InstrumentState[] _states = CreateStates();

    /// <summary>The pooled raw tick scratch collection.</summary>
    private readonly QuadList<MarketTick> _hotTicks = [];

    /// <summary>The pooled latest-snapshot aggregation dictionary.</summary>
    private readonly QuadDictionary<int, InstrumentSnapshot> _latestByInstrument = [];

    /// <summary>Coordinates producer startup, cancellation, and disposal.</summary>
    private readonly System.Threading.Lock _lifecycleGate = new();

    /// <summary>Serializes frame generation and reset operations.</summary>
    private readonly System.Threading.Lock _generationGate = new();

    /// <summary>The clock used to timestamp generated data.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Cancels the continuous producer.</summary>
    private CancellationTokenSource? _cancellation;

    /// <summary>The active continuous producer task.</summary>
    private Task? _runTask;

    /// <summary>The latest generated raw-event sequence.</summary>
    private long _sequence;

    /// <summary>The cumulative number of generated raw events.</summary>
    private long _totalEvents;

    /// <summary>The current state of the local xorshift generator.</summary>
    private uint _randomState = 0xA341316CU;

    /// <summary>Indicates whether continuous frame publication is paused.</summary>
    private bool _paused;

    /// <summary>Tracks whether owned resources have been released.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="LiveDataEngine"/> class.</summary>
    internal LiveDataEngine()
        : this(TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LiveDataEngine"/> class.</summary>
    /// <param name="timeProvider">The clock used to timestamp generated data.</param>
    internal LiveDataEngine(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Occurs after the worker has generated and aggregated one projection frame.</summary>
    internal event EventHandler<MarketFrame>? FrameProduced;

    /// <summary>Gets or sets the requested raw event throughput.</summary>
    internal int TargetEventsPerSecond
    {
        get => Volatile.Read(ref field);
        set => Volatile.Write(ref field, Math.Clamp(value, MinimumRate, MaximumRate));
    } = 10_000;

    /// <summary>Gets a value indicating whether continuous publication is paused.</summary>
    internal bool IsPaused => Volatile.Read(ref _paused);

    /// <summary>Gets the number of raw ticks in the current pooled scratch collection.</summary>
    internal int HotTickCapacityCount => _hotTicks.Count;

    /// <summary>Gets the number of current entries in the pooled aggregation dictionary.</summary>
    internal int HotDictionaryCount => _latestByInstrument.Count;

    /// <summary>Starts or resumes the continuous producer.</summary>
    internal void Start()
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
    internal void TogglePause() => _paused = !_paused;

    /// <summary>Generates and aggregates one deterministic-size raw event batch.</summary>
    /// <param name="eventCount">The number of raw events to process.</param>
    /// <returns>The immutable projection frame.</returns>
    internal MarketFrame GenerateFrame(int eventCount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventCount);
        lock (_generationGate)
        {
            return GenerateFrameCore(eventCount);
        }
    }

    /// <summary>Resets generated state while retaining pooled collection storage.</summary>
    internal void Reset()
    {
        lock (_generationGate)
        {
            var initialStates = CreateStates();
            initialStates.CopyTo(_states, 0);
            _hotTicks.Clear();
            _latestByInstrument.Clear();
            _ = Interlocked.Exchange(ref _sequence, 0);
            _ = Interlocked.Exchange(ref _totalEvents, 0);
            _randomState = 0xA341316CU;
        }
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

    /// <summary>Stops production and releases all owned resources.</summary>
    private void DisposeCore()
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

    /// <inheritdoc/>
    void IDisposable.Dispose() => DisposeCore();

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
            return new(
                sequence,
                eventCount,
                total,
                elapsed,
                allocated,
                snapshots,
                samples,
                _timeProvider.GetLocalNow());
        }
        finally
        {
            ArrayPool<MarketTick>.Shared.Return(rented);
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
            var price = Math.Max(MinimumPrice, state.LastPrice * (1D + movement));
            var volume = 1 + (int)((random >> 20) % VolumeRange);
            target[i] = new(
                Interlocked.Increment(ref _sequence),
                instrumentId,
                price,
                volume,
                Stopwatch.GetTimestamp(),
                (random & 1U) == 0);
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
        var now = _timeProvider.GetLocalNow();
        var snapshots = new InstrumentSnapshot[InstrumentCount];
        for (var i = 0; i < snapshots.Length; i++)
        {
            ref var state = ref _states[i];
            var change = ((state.LastPrice / state.OpeningPrice) - 1D) * PercentageMultiplier;
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
