// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveListTestApp.Tests;

/// <summary>Creates a manually triggered timer for deterministic producer lifecycle tests.</summary>
internal sealed class ControlledTimeProvider : TimeProvider
{
    /// <summary>Signals when the producer has created its first timer.</summary>
    private readonly TaskCompletionSource<ControlledTimer> _created = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Signals when frame generation requests a timestamp.</summary>
    private readonly TaskCompletionSource<bool> _timestampRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Counts timer creation across repeated startup calls.</summary>
    private int _timerCount;

    /// <summary>Gets the first timer once producer initialization reaches the clock.</summary>
    internal Task<ControlledTimer> CreatedTimer => _created.Task;

    /// <summary>Gets the number of timers requested by the producer.</summary>
    internal int TimerCount => Volatile.Read(ref _timerCount);

    /// <summary>Gets a task that completes when frame generation reaches the injected clock.</summary>
    internal Task TimestampRequested => _timestampRequested.Task;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        _ = _timestampRequested.TrySetResult(true);
        return DateTimeOffset.UnixEpoch;
    }

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ControlledTimer(callback, state, dueTime, period);
        _ = Interlocked.Increment(ref _timerCount);
        _ = _created.TrySetResult(timer);
        return timer;
    }

    /// <summary>Invokes periodic callbacks only when explicitly ticked by a test.</summary>
    internal sealed class ControlledTimer : ITimer
    {
        /// <summary>The callback owned by the periodic producer.</summary>
        private readonly TimerCallback _callback;

        /// <summary>The callback's opaque state.</summary>
        private readonly object? _state;

        /// <summary>Signals when the worker releases its timer.</summary>
        private readonly TaskCompletionSource<bool> _disposalCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Tracks idempotent timer shutdown.</summary>
        private int _disposed;

        /// <summary>Initializes a new instance of the <see cref="ControlledTimer"/> class.</summary>
        /// <param name="callback">The periodic timer callback.</param>
        /// <param name="state">The callback's opaque state.</param>
        /// <param name="dueTime">The requested first tick delay.</param>
        /// <param name="period">The requested recurring delay.</param>
        internal ControlledTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _callback = callback;
            _state = state;
            DueTime = dueTime;
            Period = period;
        }

        /// <summary>Gets the requested initial tick delay.</summary>
        internal TimeSpan DueTime { get; private set; }

        /// <summary>Gets the requested recurring tick delay.</summary>
        internal TimeSpan Period { get; private set; }

        /// <summary>Gets a value indicating whether the producer released the timer.</summary>
        internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        /// <summary>Gets a task that completes when the worker releases the timer.</summary>
        internal Task DisposalCompleted => _disposalCompleted.Task;

        /// <inheritdoc/>
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (IsDisposed)
            {
                return false;
            }

            DueTime = dueTime;
            Period = period;
            return true;
        }

        /// <summary>Delivers a tick unless the producer has released the timer.</summary>
        internal void Tick()
        {
            if (IsDisposed)
            {
                return;
            }

            _callback(_state);
        }

        /// <summary>Delivers a callback queued before disposal, even if the timer has been released.</summary>
        internal void DeliverQueuedTick() => _callback(_state);

        /// <inheritdoc/>
        void IDisposable.Dispose() => DisposeCore();

        /// <inheritdoc/>
        ValueTask IAsyncDisposable.DisposeAsync()
        {
            DisposeCore();
            return default;
        }

        /// <summary>Releases the timer exactly once.</summary>
        private void DisposeCore()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _ = _disposalCompleted.TrySetResult(true);
        }
    }
}
