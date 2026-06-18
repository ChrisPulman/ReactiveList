// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;
#else
namespace CP.Primitives.Internal;
#endif

/// <summary>Provides scheduler access across the Primitives and System.Reactive package variants.</summary>
internal static class ReactiveListScheduler
{
    /// <summary>Gets the current-thread scheduler.</summary>
    public static ISequencer CurrentThread
#if REACTIVELIST_REACTIVE
        => System.Reactive.Concurrency.CurrentThreadScheduler.Instance;
#else
        => Sequencer.CurrentThread;
#endif

    /// <summary>Gets the default scheduler.</summary>
    public static ISequencer Default
#if REACTIVELIST_REACTIVE
        => System.Reactive.Concurrency.Scheduler.Default;
#else
        => Sequencer.Default;
#endif

    /// <summary>Schedules an action after a relative delay.</summary>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="dueTime">The relative delay.</param>
    /// <param name="action">The action to run.</param>
    /// <returns>The scheduled action disposable.</returns>
    public static IDisposable Schedule(ISequencer scheduler, TimeSpan dueTime, Action action)
    {
        ThrowHelper.ThrowIfNull(scheduler);
        ThrowHelper.ThrowIfNull(action);

#if REACTIVELIST_REACTIVE
        return System.Reactive.Concurrency.Scheduler.Schedule(scheduler, dueTime, action);
#else
        return scheduler.Schedule(dueTime, action);
#endif
    }
}
