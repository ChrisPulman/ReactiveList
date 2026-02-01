// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Collections.Specialized;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace CP.Reactive;

/// <summary>
/// Provides a base class for cache collections that support change notifications, concurrent access, and event
/// streaming for cache operations.
/// </summary>
/// <typeparam name="TItem">The type of items stored in the cache collection.</typeparam>
public abstract class QuaternaryBase<TItem> : IDisposable, INotifyCollectionChanged
{
    /// <summary>
    /// The number of shards used for partitioning.
    /// </summary>
    protected const int ShardCount = 4;

    /// <summary>
    /// Provides an array of ReaderWriterLockSlim instances used to synchronize access to shared resources.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Intended for use in derived classes.")]
    protected readonly ReaderWriterLockSlim[] Locks =
    [
        new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion),
        new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion),
        new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion),
        new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion)
    ];

    private readonly Channel<CacheNotify<TItem>> _eventChannel = Channel.CreateUnbounded<CacheNotify<TItem>>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });

    private readonly Subject<CacheNotify<TItem>> _pipeline = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SynchronizationContext? _syncContext;
    private volatile bool _hasSubscribers;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuaternaryBase{TItem}"/> class.
    /// </summary>
    protected QuaternaryBase()
    {
        // Capture the current synchronization context (UI thread context in WPF/WinForms)
        _syncContext = SynchronizationContext.Current;
        Task.Factory.StartNew(ProcessEventsAsync, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    /// <summary>
    /// Occurs when the collection changes.
    /// </summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>
    /// Gets an observable sequence that emits cache change notifications as they occur.
    /// </summary>
    public IObservable<CacheNotify<TItem>> Stream
    {
        get
        {
            _hasSubscribers = true;
            return _pipeline.AsObservable();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the object has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Releases all resources used by the current instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Attempts to enqueue a cache event for processing.
    /// </summary>
    /// <param name="action">The cache action type.</param>
    /// <param name="item">The item associated with the action.</param>
    /// <param name="batch">An optional batch of items.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Emit(CacheAction action, TItem? item, PooledBatch<TItem>? batch = null)
    {
        // Fast path: skip channel write if no subscribers and no INCC
        if (!_hasSubscribers && CollectionChanged == null)
        {
            batch?.Dispose();
            return;
        }

        _eventChannel.Writer.TryWrite(new(action, item, batch));
    }

    /// <summary>
    /// Releases the unmanaged resources and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                _cts.Cancel();
                _cts.Dispose();
                foreach (var l in Locks)
                {
                    l.Dispose();
                }

                _pipeline.OnCompleted();
                _pipeline.Dispose();
            }

            IsDisposed = true;
        }
    }

    /// <summary>
    /// Asynchronously processes events from the event channel until cancellation is requested.
    /// </summary>
    /// <remarks>This method reads events from the internal event channel and processes them through the
    /// pipeline. The operation continues until the associated cancellation token is triggered. If a legacy collection
    /// changed handler is registered, it is invoked for each event.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ProcessEventsAsync()
    {
        var reader = _eventChannel.Reader;
        try
        {
            while (await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var evt))
                {
                    _pipeline.OnNext(evt);

                    if (CollectionChanged != null)
                    {
                        InvokeLegacyINCC(evt);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Raises the CollectionChanged event to notify subscribers of changes to the collection, using legacy
    /// INotifyCollectionChanged semantics.
    /// </summary>
    /// <remarks>This method adapts cache change events to the INotifyCollectionChanged pattern, using the
    /// Reset action for batch or ambiguous operations to ensure correct UI updates, especially in sharded or
    /// partitioned collections. The event is dispatched on the captured synchronization context if available, which is
    /// typically required for UI thread updates.</remarks>
    /// <param name="evt">The cache notification event containing information about the collection change to be propagated.</param>
    private void InvokeLegacyINCC(CacheNotify<TItem> evt)
    {
        var handler = CollectionChanged;
        if (handler == null)
        {
            evt.Batch?.Dispose();
            return;
        }

        NotifyCollectionChangedEventArgs args;

        if (evt.Batch != null)
        {
            // Batch operations use Reset to avoid index issues with sharded collections
            args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
            evt.Batch.Dispose();
        }
        else
        {
            var action = evt.Action switch
            {
                CacheAction.Added => NotifyCollectionChangedAction.Add,
                CacheAction.Removed => NotifyCollectionChangedAction.Remove,
                CacheAction.Cleared => NotifyCollectionChangedAction.Reset,
                CacheAction.Updated => NotifyCollectionChangedAction.Replace,
                _ => NotifyCollectionChangedAction.Reset
            };

            // For Add/Remove with single items, we can't provide index in sharded collection
            // Use Reset for safety to ensure UI updates correctly
            if (action == NotifyCollectionChangedAction.Add || action == NotifyCollectionChangedAction.Remove)
            {
                args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
            }
            else
            {
                args = new NotifyCollectionChangedEventArgs(action);
            }
        }

        // Dispatch to the captured synchronization context (UI thread)
        if (_syncContext != null)
        {
            _syncContext.Post(_ => handler.Invoke(this, args), null);
        }
        else
        {
            // Fallback: invoke directly if no sync context was captured
            handler.Invoke(this, args);
        }
    }
}
#endif
