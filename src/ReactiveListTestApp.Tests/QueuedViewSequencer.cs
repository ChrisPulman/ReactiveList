// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives.Concurrency;

namespace ReactiveListTestApp.Tests;

/// <summary>Holds projection callbacks until a test explicitly processes the dispatch queue.</summary>
internal sealed class QueuedViewSequencer : ISequencer
{
    /// <summary>Serializes work received from timer callbacks.</summary>
    private readonly Lock _gate = new();

    /// <summary>The projection callbacks awaiting execution.</summary>
    private readonly Queue<IWorkItem> _items = new();

    /// <summary>Signals that delayed work reached the dispatch queue.</summary>
    private readonly TaskCompletionSource<bool> _workAvailable = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public DateTimeOffset Now => Sequencer.Immediate.Now;

    /// <inheritdoc/>
    public long Timestamp => Sequencer.Immediate.Timestamp;

    /// <summary>Gets a task that completes when a callback reaches the dispatch queue.</summary>
    internal Task WorkAvailable => _workAvailable.Task;

    /// <inheritdoc/>
    public void Schedule(IWorkItem item)
    {
        lock (_gate)
        {
            _items.Enqueue(item);
        }

        _ = _workAvailable.TrySetResult(true);
    }

    /// <inheritdoc/>
    public void Schedule(IWorkItem item, long dueTimestamp) => Schedule(item);

    /// <summary>Executes callbacks in their dispatch order, including callbacks they enqueue.</summary>
    internal void RunAll()
    {
        while (true)
        {
            IWorkItem item;
            lock (_gate)
            {
                if (_items.Count == 0)
                {
                    return;
                }

                item = _items.Dequeue();
            }

            item.Execute();
        }
    }
}
