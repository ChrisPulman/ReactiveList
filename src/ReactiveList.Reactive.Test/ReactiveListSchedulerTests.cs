// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using CP.Reactive.Internal;

namespace ReactiveList.Reactive.Test;

/// <summary>Verifies the System.Reactive scheduler compatibility branch.</summary>
public sealed class ReactiveListSchedulerTests
{
    /// <summary>The relative delay assigned to queued test work.</summary>
    private const int DelaySeconds = 10;

    /// <summary>The advance that stops immediately before the due time.</summary>
    private const int BeforeDueSeconds = DelaySeconds - 1;

    /// <summary>The advance that exceeds the cancelled work's due time.</summary>
    private const int AfterCancelledMinutes = 2;

    /// <summary>Exposes the native Rx scheduler singletons rather than Primitives sequencers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SchedulerAccessors_ReturnNativeRxSchedulers()
    {
        await Assert.That(ReactiveListScheduler.CurrentThread).IsSameReferenceAs(CurrentThreadScheduler.Instance);
        await Assert.That(ReactiveListScheduler.Default).IsSameReferenceAs(Scheduler.Default);
    }

    /// <summary>Defers work until the exact relative due time on a virtual Rx scheduler.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Schedule_RelativeDelay_RunsAtDueTime()
    {
        var scheduler = new HistoricalScheduler();
        var calls = 0;
        using var work = ReactiveListScheduler.Schedule(scheduler, TimeSpan.FromSeconds(DelaySeconds), () => calls++);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(BeforeDueSeconds));
        await Assert.That(calls).IsEqualTo(0);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
        await Assert.That(calls).IsEqualTo(1);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(DelaySeconds));
        await Assert.That(calls).IsEqualTo(1);
    }

    /// <summary>Cancels queued Rx work when the returned subscription is disposed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Schedule_DisposedWork_DoesNotRun()
    {
        var scheduler = new HistoricalScheduler();
        var calls = 0;
        var work = ReactiveListScheduler.Schedule(scheduler, TimeSpan.FromMinutes(1), () => calls++);
        work.Dispose();
        scheduler.AdvanceBy(TimeSpan.FromMinutes(AfterCancelledMinutes));
        await Assert.That(calls).IsEqualTo(0);
    }

    /// <summary>Rejects missing schedulers and callbacks at the compatibility boundary.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Schedule_NullArguments_Throws()
    {
        await Assert.That(static () => ReactiveListScheduler.Schedule(null!, TimeSpan.Zero, static () => { }))
            .Throws<ArgumentNullException>();
        await Assert.That(static () => ReactiveListScheduler.Schedule(ImmediateScheduler.Instance, TimeSpan.Zero, null!))
            .Throws<ArgumentNullException>();
    }
}
