// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveListTestApp.Models;
using ReactiveListTestApp.Services;

namespace ReactiveListTestApp.Tests;

/// <summary>Verifies producer cancellation, injected timers and callback failures.</summary>
public sealed partial class LiveDataEngineTests
{
    /// <summary>The expected interval for eight producer frames per second.</summary>
    private static readonly TimeSpan ExpectedTimerInterval = TimeSpan.FromMilliseconds(125);

    /// <summary>Exposes a successfully completed task before continuous production starts.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Completion_BeforeStart_IsCompleted()
    {
        using var engine = new LiveDataEngine();
        await Assert.That(engine.Completion.IsCompletedSuccessfully).IsTrue();
    }

    /// <summary>Avoids generating unpublished market data when no subscribers are attached.</summary>
    /// <param name="cancellationToken">Cancels bounded lifecycle waits.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Start_WithoutSubscribers_DoesNotGenerateMarketData(CancellationToken cancellationToken)
    {
        var clock = new ControlledTimeProvider();
        var engine = new LiveDataEngine(clock);
        try
        {
            engine.Start();
            var timer = await clock.CreatedTimer.WaitAsync(ProducerTimeout, cancellationToken);
            timer.Tick();
            ((IDisposable)engine).Dispose();
            await timer.DisposalCompleted.WaitAsync(ProducerTimeout, cancellationToken);
            await engine.Completion.WaitAsync(ProducerTimeout, cancellationToken);

            await Assert.That(clock.TimerCount).IsEqualTo(1);
            await Assert.That(timer.IsDisposed).IsTrue();
            await Assert.That(clock.TimestampRequested.IsCompleted).IsFalse();
        }
        finally
        {
            ((IDisposable)engine).Dispose();
        }
    }

    /// <summary>Cancels an active worker and prevents queued ticks from publishing after shutdown.</summary>
    /// <param name="cancellationToken">Cancels bounded lifecycle waits.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Dispose_ActiveProducer_CompletesWithoutFurtherEvents(CancellationToken cancellationToken)
    {
        var clock = new ControlledTimeProvider();
        var engine = new LiveDataEngine(clock);
        try
        {
            var firstFrame = new TaskCompletionSource<MarketFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            var publishedCount = 0;
            engine.FrameProduced += (_, frame) =>
            {
                _ = Interlocked.Increment(ref publishedCount);
                _ = firstFrame.TrySetResult(frame);
            };
            engine.Start();
            var timer = await clock.CreatedTimer.WaitAsync(ProducerTimeout, cancellationToken);
            timer.Tick();
            _ = await firstFrame.Task.WaitAsync(ProducerTimeout, cancellationToken);

            ((IDisposable)engine).Dispose();
            await timer.DisposalCompleted.WaitAsync(ProducerTimeout, cancellationToken);
            await engine.Completion.WaitAsync(ProducerTimeout, cancellationToken);
            timer.DeliverQueuedTick();

            await Assert.That(engine.Completion.IsCompletedSuccessfully).IsTrue();
            await Assert.That(timer.IsDisposed).IsTrue();
            await Assert.That(Volatile.Read(ref publishedCount)).IsEqualTo(1);
            await Assert.That(engine.Reset).Throws<ObjectDisposedException>();
            await Assert.That(engine.TogglePause).Throws<ObjectDisposedException>();
        }
        finally
        {
            ((IDisposable)engine).Dispose();
        }
    }

    /// <summary>Allows a frame subscriber to shut down its own worker without deadlock.</summary>
    /// <param name="cancellationToken">Cancels bounded lifecycle waits.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Dispose_InsideFrameCallback_CompletesWorker(CancellationToken cancellationToken)
    {
        var clock = new ControlledTimeProvider();
        using var callbackEngine = new LiveDataEngine(clock);
        var publishedCount = 0;
        callbackEngine.FrameProduced += (_, _) =>
        {
            _ = Interlocked.Increment(ref publishedCount);
            ((IDisposable)callbackEngine).Dispose();
        };
        callbackEngine.Start();
        var timer = await clock.CreatedTimer.WaitAsync(ProducerTimeout, cancellationToken);

        timer.Tick();
        await timer.DisposalCompleted.WaitAsync(ProducerTimeout, cancellationToken);
        await callbackEngine.Completion.WaitAsync(ProducerTimeout, cancellationToken);
        timer.DeliverQueuedTick();

        await Assert.That(callbackEngine.Completion.IsCompletedSuccessfully).IsTrue();
        await Assert.That(timer.IsDisposed).IsTrue();
        await Assert.That(Volatile.Read(ref publishedCount)).IsEqualTo(1);
    }

    /// <summary>Resumes paused publication and reuses the injected periodic timer.</summary>
    /// <param name="cancellationToken">Cancels bounded frame waits.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Start_PausedProducer_ReusesTimerAndResumesFrames(CancellationToken cancellationToken)
    {
        var clock = new ControlledTimeProvider();
        using var engine = new LiveDataEngine(clock) { TargetEventsPerSecond = ContinuousTargetRate };
        var firstFrame = new TaskCompletionSource<MarketFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumedFrame = new TaskCompletionSource<MarketFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.FrameProduced += (_, frame) =>
        {
            if (firstFrame.TrySetResult(frame))
            {
                return;
            }

            _ = resumedFrame.TrySetResult(frame);
        };
        engine.Start();
        var completion = engine.Completion;
        var timer = await clock.CreatedTimer.WaitAsync(ProducerTimeout, cancellationToken);
        timer.Tick();
        var first = await firstFrame.Task.WaitAsync(ProducerTimeout, cancellationToken);

        engine.TogglePause();
        await Assert.That(engine.IsPaused).IsTrue();
        engine.Start();
        await Assert.That(engine.IsPaused).IsFalse();
        timer.Tick();
        var resumed = await resumedFrame.Task.WaitAsync(ProducerTimeout, cancellationToken);

        await Assert.That(resumed.EventCount).IsEqualTo(ExpectedContinuousFrameEvents);
        await Assert.That(resumed.TotalEvents).IsEqualTo(first.TotalEvents + ExpectedContinuousFrameEvents);
        await Assert.That(clock.TimerCount).IsEqualTo(1);
        await Assert.That(timer.DueTime).IsEqualTo(ExpectedTimerInterval);
        await Assert.That(timer.Period).IsEqualTo(ExpectedTimerInterval);
        await Assert.That(engine.Completion).IsSameReferenceAs(completion);
    }

    /// <summary>Propagates subscriber failures through completion and releases the periodic timer.</summary>
    /// <param name="cancellationToken">Cancels bounded fault waits.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FrameProduced_SubscriberThrows_FaultsCompletionAndReleasesTimer(CancellationToken cancellationToken)
    {
        var clock = new ControlledTimeProvider();
        using var engine = new LiveDataEngine(clock);
        var expected = new InvalidOperationException("subscriber failure");
        var publishedCount = 0;
        engine.FrameProduced += (_, _) =>
        {
            _ = Interlocked.Increment(ref publishedCount);
            throw expected;
        };
        engine.Start();
        var timer = await clock.CreatedTimer.WaitAsync(ProducerTimeout, cancellationToken);

        timer.Tick();
        await timer.DisposalCompleted.WaitAsync(ProducerTimeout, cancellationToken);
        var thrown = await Assert.That(() => engine.Completion.WaitAsync(ProducerTimeout, cancellationToken)).Throws<InvalidOperationException>();
        timer.DeliverQueuedTick();

        await Assert.That(thrown).IsSameReferenceAs(expected);
        await Assert.That(engine.Completion.IsFaulted).IsTrue();
        await Assert.That(Volatile.Read(ref publishedCount)).IsEqualTo(1);
        await Assert.That(timer.IsDisposed).IsTrue();
    }

    /// <summary>Does not mistake subscriber cancellation for the engine's own shutdown token.</summary>
    /// <param name="cancellationToken">Cancels bounded completion waits.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FrameProduced_SubscriberCancels_PropagatesUnrelatedCancellation(CancellationToken cancellationToken)
    {
        var clock = new ControlledTimeProvider();
        using var engine = new LiveDataEngine(clock);
        var expected = new OperationCanceledException(new CancellationToken(true));
        engine.FrameProduced += (_, _) => throw expected;
        engine.Start();
        var timer = await clock.CreatedTimer.WaitAsync(ProducerTimeout, cancellationToken);

        timer.Tick();
        var thrown = await Assert.That(() => engine.Completion.WaitAsync(ProducerTimeout, cancellationToken)).Throws<OperationCanceledException>();

        await Assert.That(thrown).IsSameReferenceAs(expected);
        await Assert.That(engine.Completion.IsCanceled).IsTrue();
        await Assert.That(timer.IsDisposed).IsTrue();
    }
}
