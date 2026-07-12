// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveListTestApp.Models;
using ReactiveListTestApp.Services;
using TUnit.Core;

namespace ReactiveListTestApp.Tests;

/// <summary>Verifies deterministic high-rate generation and pooled hot-path behavior.</summary>
public sealed class LiveDataEngineTests
{
    private const int InstrumentCount = 40;

    private const int RawEventCount = 1_000;

    private const int SampleCount = 12;

    private const int MinimumRate = 1_000;

    private const int MaximumRate = 100_000;

    private const int PriceStabilityFrameCount = 20;

    private const int PostResetEventCount = 250;

    private const int ContinuousTargetRate = 8_000;

    private const int ExpectedContinuousFrameEvents = 1_000;

    private const double MinimumExpectedPrice = 50d;

    private const double MaximumExpectedPrice = 250d;

    private static readonly TimeSpan ProducerTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Confirms one frame processes the requested raw count and updates both pooled structures.</summary>
    /// <returns>A task that completes after assertions.</returns>
    [Test]
    public async Task GenerateFrameProcessesExactBatch()
    {
        using var engine = new LiveDataEngine();

        var frame = engine.GenerateFrame(RawEventCount);

        await Assert.That(frame.EventCount).IsEqualTo(RawEventCount);
        await Assert.That(frame.TotalEvents).IsEqualTo(RawEventCount);
        await Assert.That(frame.Snapshots).Count().IsEqualTo(InstrumentCount);
        await Assert.That(frame.Samples).Count().IsEqualTo(SampleCount);
        await Assert.That(engine.HotTickCapacityCount).IsEqualTo(RawEventCount);
        await Assert.That(engine.HotDictionaryCount).IsEqualTo(InstrumentCount);
        await Assert.That(frame.GenerationTime).IsGreaterThan(TimeSpan.Zero);
    }

    /// <summary>Guards the signed price movement path against unsigned arithmetic overflow.</summary>
    /// <returns>A task that completes after assertions.</returns>
    [Test]
    public async Task GeneratedPricesRemainWithinRealisticBounds()
    {
        using var engine = new LiveDataEngine();
        var frame = engine.GenerateFrame(RawEventCount);
        for (var i = 1; i < PriceStabilityFrameCount; i++)
        {
            frame = engine.GenerateFrame(RawEventCount);
        }

        await Assert.That(frame.Snapshots.All(snapshot => snapshot.Price is > MinimumExpectedPrice and < MaximumExpectedPrice)).IsTrue();
        await Assert.That(frame.Snapshots.Any(snapshot => snapshot.IsAlert)).IsTrue();
        await Assert.That(frame.Snapshots.Any(snapshot => !snapshot.IsAlert)).IsTrue();
    }

    /// <summary>Confirms cumulative counters and pooled state restart cleanly.</summary>
    /// <returns>A task that completes after assertions.</returns>
    [Test]
    public async Task ResetRestartsCountersAndState()
    {
        using var engine = new LiveDataEngine();
        _ = engine.GenerateFrame(RawEventCount);
        _ = engine.GenerateFrame(RawEventCount);

        engine.Reset();
        var frame = engine.GenerateFrame(PostResetEventCount);

        await Assert.That(frame.TotalEvents).IsEqualTo(PostResetEventCount);
        await Assert.That(frame.Snapshots[0].Symbol).IsEqualTo("RL01");
        await Assert.That(engine.HotDictionaryCount).IsEqualTo(InstrumentCount);
    }

    /// <summary>Confirms target rates are clamped to supported showcase presets.</summary>
    /// <returns>A task that completes after assertions.</returns>
    [Test]
    public async Task TargetRateIsClampedToSupportedRange()
    {
        using var engine = new LiveDataEngine
        {
            TargetEventsPerSecond = 1
        };

        await Assert.That(engine.TargetEventsPerSecond).IsEqualTo(MinimumRate);

        engine.TargetEventsPerSecond = int.MaxValue;

        await Assert.That(engine.TargetEventsPerSecond).IsEqualTo(MaximumRate);
    }

    /// <summary>Confirms the periodic producer publishes full-rate batches and supports pause and resume.</summary>
    /// <returns>A task that completes after the first periodic frame and assertions.</returns>
    [Test]
    public async Task ContinuousProducerPublishesAndCanPause()
    {
        using var engine = new LiveDataEngine
        {
            TargetEventsPerSecond = ContinuousTargetRate
        };
        var completion = new TaskCompletionSource<MarketFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<MarketFrame> handler = (_, frame) => completion.TrySetResult(frame);
        engine.FrameProduced += handler;

        engine.Start();
        var publishedFrame = await completion.Task.WaitAsync(ProducerTimeout);
        engine.TogglePause();

        await Assert.That(publishedFrame.EventCount).IsEqualTo(ExpectedContinuousFrameEvents);
        await Assert.That(engine.IsPaused).IsTrue();

        engine.Start();

        await Assert.That(engine.IsPaused).IsFalse();
        engine.FrameProduced -= handler;
    }
}
