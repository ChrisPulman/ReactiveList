// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveListTestApp.Services;

namespace ReactiveListTestApp.Tests;

/// <summary>Verifies frame aggregation, deterministic reset and lifecycle boundaries.</summary>
public sealed partial class LiveDataEngineTests
{
    /// <summary>The batch size replayed after resetting the random seed.</summary>
    private const int ReplayBatchCount = 100;

    /// <summary>The raw batch generated between replay snapshots.</summary>
    private const int IntermediateBatchCount = 200;

    /// <summary>The second small batch in the accumulation test.</summary>
    private const int SecondBatchCount = 5;

    /// <summary>The highest generated trade volume.</summary>
    private const int MaximumTradeVolume = 500;

    /// <summary>The price movement that raises an alert.</summary>
    private const double ChangeAlertThreshold = 0.65;

    /// <summary>The latency that raises an alert.</summary>
    private const double LatencyAlertThreshold = 6;

    /// <summary>Rejects an absent clock at construction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_NullClock_Throws() =>
        await Assert.That(static () => new LiveDataEngine(null!)).Throws<ArgumentNullException>();

    /// <summary>Rejects empty or negative batches without changing producer state.</summary>
    /// <param name="count">The invalid raw batch size.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task GenerateFrame_InvalidCount_Throws(int count)
    {
        using var engine = new LiveDataEngine();
        await Assert.That(() => engine.GenerateFrame(count)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(engine.HotTickCapacityCount).IsEqualTo(0);
        await Assert.That(engine.HotDictionaryCount).IsEqualTo(0);
    }

    /// <summary>Bounds diagnostic sampling while preserving the exact requested batch size.</summary>
    /// <param name="count">The requested batch size.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(11)]
    [Arguments(12)]
    [Arguments(13)]
    public async Task GenerateFrame_SmallBatch_BoundsSamples(int count)
    {
        using var engine = new LiveDataEngine();
        var frame = engine.GenerateFrame(count);
        await Assert.That(frame.Samples.Length).IsEqualTo(Math.Min(SampleCount, count));
        await Assert.That(frame.EventCount).IsEqualTo(count);
        await Assert.That(frame.Sequence).IsEqualTo(count + 1L);
        await Assert.That(Array.TrueForAll(frame.Snapshots, snapshot => snapshot.Sequence == count)).IsTrue();
        for (var i = 0; i < frame.Samples.Length; i++)
        {
            await Assert.That(frame.Samples[i].Sequence).IsEqualTo(i + 1L);
        }
    }

    /// <summary>Aggregates every sampled trade into its instrument's last price and volume.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GenerateFrame_FullySampledBatch_AggregatesTrades()
    {
        using var engine = new LiveDataEngine();
        var frame = engine.GenerateFrame(SampleCount);
        foreach (var snapshot in frame.Snapshots)
        {
            var volume = 0L;
            var lastPrice = 0D;
            foreach (var tick in frame.Samples)
            {
                if (tick.InstrumentId != snapshot.InstrumentId)
                {
                    continue;
                }

                volume += tick.Volume;
                lastPrice = tick.Price;
            }

            await Assert.That(snapshot.Volume).IsEqualTo(volume);
            if (volume > 0)
            {
                await Assert.That(snapshot.Price).IsEqualTo(lastPrice);
            }
        }

        await Assert.That(Array.TrueForAll(frame.Samples, static tick => tick.Volume is >= 1 and <= MaximumTradeVolume && tick.Price > 0)).IsTrue();
        await Assert.That(Array.TrueForAll(frame.Snapshots, static snapshot =>
            snapshot.IsAlert == (Math.Abs(snapshot.ChangePercent) >= ChangeAlertThreshold || snapshot.LatencyMilliseconds >= LatencyAlertThreshold))).IsTrue();
    }

    /// <summary>Publishes timestamps supplied by the injected clock to the frame and all snapshots.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GenerateFrame_InjectedClock_TimestampsAllProjections()
    {
        var clock = new FixedTimeProvider();
        using var engine = new LiveDataEngine(clock);
        var frame = engine.GenerateFrame(InstrumentCount);
        await Assert.That(frame.CreatedAt).IsEqualTo(clock.GetUtcNow());
        await Assert.That(Array.TrueForAll(frame.Snapshots, snapshot => snapshot.UpdatedAt == clock.GetUtcNow())).IsTrue();
        var symbols = new HashSet<string>();
        for (var i = 0; i < frame.Snapshots.Length; i++)
        {
            await Assert.That(frame.Snapshots[i].InstrumentId).IsEqualTo(i);
            _ = symbols.Add(frame.Snapshots[i].Symbol);
        }

        await Assert.That(symbols.Count).IsEqualTo(InstrumentCount);
    }

    /// <summary>Restarts the seeded generator and leaves earlier immutable frames unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Reset_ReplaysSeededData_WithoutMutatingEarlierFrame()
    {
        using var engine = new LiveDataEngine(new FixedTimeProvider());
        var first = engine.GenerateFrame(ReplayBatchCount);
        var snapshots = (ReactiveListTestApp.Models.InstrumentSnapshot[])first.Snapshots.Clone();
        _ = engine.GenerateFrame(IntermediateBatchCount);
        engine.Reset();
        await Assert.That(engine.HotTickCapacityCount).IsEqualTo(0);
        await Assert.That(engine.HotDictionaryCount).IsEqualTo(0);
        var replay = engine.GenerateFrame(ReplayBatchCount);
        await Assert.That(replay.Snapshots).IsEquivalentTo(snapshots);
        await Assert.That(first.Snapshots).IsEquivalentTo(snapshots);
        await Assert.That(replay.TotalEvents).IsEqualTo((long)ReplayBatchCount);
        for (var i = 0; i < replay.Samples.Length; i++)
        {
            var expected = first.Samples[i];
            var actual = replay.Samples[i];
            await Assert.That((actual.Sequence, actual.InstrumentId, actual.Price, actual.Volume, actual.IsBuy))
                .IsEqualTo((expected.Sequence, expected.InstrumentId, expected.Price, expected.Volume, expected.IsBuy));
        }
    }

    /// <summary>Accumulates volume and advances sequence across independently sized batches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GenerateFrame_ConsecutiveBatches_AccumulatesCounters()
    {
        using var engine = new LiveDataEngine();
        var first = engine.GenerateFrame(SampleCount);
        var second = engine.GenerateFrame(SecondBatchCount);
        var firstVolume = 0L;
        var secondVolume = 0L;
        var additionalVolume = 0L;
        foreach (var snapshot in first.Snapshots)
        {
            firstVolume += snapshot.Volume;
        }

        foreach (var snapshot in second.Snapshots)
        {
            secondVolume += snapshot.Volume;
        }

        foreach (var tick in second.Samples)
        {
            additionalVolume += tick.Volume;
        }

        await Assert.That(second.TotalEvents).IsEqualTo((long)SampleCount + SecondBatchCount);
        await Assert.That(second.Sequence).IsEqualTo((long)SampleCount + SecondBatchCount + 1 + 1);
        await Assert.That(second.Samples[0].Sequence).IsEqualTo((long)SampleCount + 1 + 1);
        await Assert.That(secondVolume).IsEqualTo(firstVolume + additionalVolume);
        await Assert.That(engine.HotTickCapacityCount).IsEqualTo(SecondBatchCount);
    }

    /// <summary>Allows repeat disposal and rejects subsequent generation or startup.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Dispose_RepeatedCall_RejectsFurtherWork()
    {
        using var engine = new LiveDataEngine();
        ((IDisposable)engine).Dispose();
        ((IDisposable)engine).Dispose();
        await Assert.That(() => engine.GenerateFrame(1)).Throws<ObjectDisposedException>();
        await Assert.That(engine.Start).Throws<ObjectDisposedException>();
    }

    /// <summary>Switches pause state independently of producer startup.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TogglePause_Twice_RestoresInitialState()
    {
        using var engine = new LiveDataEngine();
        await Assert.That(engine.IsPaused).IsFalse();
        engine.TogglePause();
        await Assert.That(engine.IsPaused).IsTrue();
        engine.TogglePause();
        await Assert.That(engine.IsPaused).IsFalse();
    }
}
