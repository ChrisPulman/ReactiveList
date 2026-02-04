// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CP.Reactive.Core;

namespace CP.Reactive.Collections;

/// <summary>
/// Provides a base class for partitioned, observable collections that support quaternary (four-way) sharding and cache
/// change notifications.
/// </summary>
/// <remarks>This class implements partitioning logic using four shards to improve concurrency and scalability for
/// collections that require frequent updates and notifications. It provides observable change tracking via both
/// INotifyCollectionChanged and IObservable patterns, making it suitable for use in UI-bound or reactive scenarios.
/// Derived classes should implement enumeration and may extend synchronization or notification behavior as needed.
/// Thread safety is managed internally using per-shard locks and a background event processing pipeline.</remarks>
/// <typeparam name="TItem">The type of items stored in the collection. Must be non-nullable.</typeparam>
/// <typeparam name="TQuad">The type representing a quad (shard) within the collection. Must implement <see cref="IQuad{TItem}"/>.</typeparam>
/// <typeparam name="TValue">The type used for secondary indexing within the collection.</typeparam>
public abstract class QuaternaryBase<TItem, TQuad, TValue> : IQuaternarySource<TItem>
    where TItem : notnull
    where TQuad : IQuad<TItem>, new()
{
    /// <summary>
    /// The number of shards used for partitioning.
    /// </summary>
    protected const int ShardCount = 4;

    /// <summary>
    /// Provides thread-safe access to the collection of secondary indices associated with the current instance.
    /// </summary>
    /// <remarks>Each entry maps a unique index name to its corresponding secondary index. This dictionary
    /// enables efficient retrieval and management of secondary indices in concurrent scenarios.</remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Intended for use in derived classes.")]
    protected readonly ConcurrentDictionary<string, ISecondaryIndex<TValue>> Indices = new();

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

    /// <summary>
    /// Represents the collection of four quadrants used by the containing type.
    /// </summary>
    /// <remarks>Each element in the array corresponds to a quadrant and is initialized to a new instance of
    /// <typeparamref name="TQuad"/>. The array is intended for use by derived types to manage or access
    /// quadrant-specific data.</remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Intended for use in derived classes.")]
    protected readonly TQuad[] Quads =
    [
        new TQuad(),
        new TQuad(),
        new TQuad(),
        new TQuad()
    ];

    private readonly Channel<CacheNotify<TItem>> _eventChannel = Channel.CreateUnbounded<CacheNotify<TItem>>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });

    private readonly Subject<CacheNotify<TItem>> _pipeline = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SynchronizationContext? _syncContext;
    private int _hasSubscribers;
    private long _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuaternaryBase{TItem, TQuad, TValue}"/> class.
    /// </summary>
    /// <remarks>This constructor captures the current synchronization context, which is typically associated
    /// with the UI thread in WPF or Windows Forms applications. It also initiates a background task to process events
    /// asynchronously. Derived classes can rely on the synchronization context being set for thread-safe operations
    /// that require marshaling to the original context.</remarks>
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
    /// Gets the total number of items contained in all quads.
    /// </summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var count = 0;
            for (var i = 0; i < ShardCount; i++)
            {
                Locks[i].EnterReadLock();
                try
                {
                    count += Quads[i].Count;
                }
                finally
                {
                    Locks[i].ExitReadLock();
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the collection is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets an observable sequence that emits cache change notifications as they occur.
    /// </summary>
    /// <remarks>
    /// This is the primary observable for change notifications. It provides all change information
    /// including single item changes and batch operations. The Stream uses a channel-based pipeline
    /// for efficient, low-allocation event delivery.
    /// </remarks>
    public IObservable<CacheNotify<TItem>> Stream
    {
        get
        {
            Volatile.Write(ref _hasSubscribers, 1);
            return _pipeline.AsObservable();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the object has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets the current version number of the collection, which is incremented on each modification.
    /// </summary>
    /// <remarks>This property can be used for efficient change detection without acquiring locks.
    /// The version is incremented atomically using <see cref="Interlocked.Increment(ref long)"/>.</remarks>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>
    /// Removes all items from the cache.
    /// </summary>
    public void Clear()
    {
        // Acquire all locks first to ensure consistency
        for (var i = 0; i < ShardCount; i++)
        {
            Locks[i].EnterWriteLock();
        }

        try
        {
            for (var i = 0; i < ShardCount; i++)
            {
                Quads[i].Clear();
            }
        }
        finally
        {
            for (var i = ShardCount - 1; i >= 0; i--)
            {
                Locks[i].ExitWriteLock();
            }
        }

        // Clear indices outside of locks
        if (!Indices.IsEmpty)
        {
            foreach (var idx in Indices.Values)
            {
                idx.Clear();
            }
        }

        Emit(CacheAction.Cleared, default);
    }

    /// <summary>
    /// Releases all resources used by the current instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public abstract IEnumerator<TItem> GetEnumerator();

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>An <see cref="System.Collections.IEnumerator"/> object that can be used to iterate through the collection.</returns>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Attempts to enqueue a cache event for processing and increments the version counter.
    /// </summary>
    /// <param name="action">The cache action type.</param>
    /// <param name="item">The item associated with the action.</param>
    /// <param name="batch">An optional batch of items.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Emit(CacheAction action, TItem? item, PooledBatch<TItem>? batch = null)
    {
        // Increment version atomically for change tracking
        Interlocked.Increment(ref _version);

        // Fast path: skip channel write if no subscribers and no INCC
        if (Interlocked.CompareExchange(ref _hasSubscribers, 0, 0) == 0 && CollectionChanged == null)
        {
            batch?.Dispose();
            return;
        }

        _eventChannel.Writer.TryWrite(new(action, item, batch));
    }

    /// <summary>
    /// Emits a batch operation using the specified items and count without additional validation or copying.
    /// </summary>
    /// <remarks>This method is intended for scenarios where the caller can guarantee the validity of the
    /// input parameters. No parameter validation is performed. The method may rent temporary arrays from the shared
    /// pool for performance reasons.</remarks>
    /// <param name="items">The array of items to include in the batch operation. Must contain at least <paramref name="count"/> elements.</param>
    /// <param name="count">The number of items from <paramref name="items"/> to include in the batch operation. Must be greater than or
    /// equal to zero and less than or equal to the length of <paramref name="items"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void EmitBatchDirect(TItem[] items, int count)
    {
        var pool = ArrayPool<TItem>.Shared.Rent(count);
        Array.Copy(items, pool, count);
        Emit(CacheAction.BatchOperation, default, new PooledBatch<TItem>(pool, count));
    }

    /// <summary>
    /// Emits a batch added notification using the specified items and count without additional validation or copying.
    /// </summary>
    /// <remarks>This method is intended for scenarios where the caller can guarantee the validity of the
    /// input parameters. No parameter validation is performed. The method may rent temporary arrays from the shared
    /// pool for performance reasons.</remarks>
    /// <param name="items">The array of items to include in the batch operation. Must contain at least <paramref name="count"/> elements.</param>
    /// <param name="count">The number of items from <paramref name="items"/> to include in the batch operation. Must be greater than or
    /// equal to zero and less than or equal to the length of <paramref name="items"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void EmitBatchAddedDirect(TItem[] items, int count)
    {
        var pool = ArrayPool<TItem>.Shared.Rent(count);
        Array.Copy(items, pool, count);
        Emit(CacheAction.BatchAdded, default, new PooledBatch<TItem>(pool, count));
    }

    /// <summary>
    /// Emits a batch added notification using the specified number of items from the provided list.
    /// </summary>
    /// <remarks>The method copies the specified number of items from the list into a pooled array before
    /// emitting the batch added notification. The caller is responsible for ensuring that the list contains at least the
    /// specified number of items.</remarks>
    /// <param name="items">The list of items to include in the batch operation. Items are taken from the start of the list.</param>
    /// <param name="count">The number of items from the list to include in the batch. Must be less than or equal to the number of items in
    /// the list and greater than or equal to zero.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void EmitBatchAddedFromList(IList<TItem> items, int count)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var pool = ArrayPool<TItem>.Shared.Rent(count);
        for (var i = 0; i < count; i++)
        {
            pool[i] = items[i];
        }

        Emit(CacheAction.BatchAdded, default, new PooledBatch<TItem>(pool, count));
    }

    /// <summary>
    /// Raises a batch removed event for the specified items.
    /// </summary>
    /// <remarks>This method uses a pooled array to optimize memory usage when emitting the batch removed
    /// event. The caller should ensure that the <paramref name="items"/> array contains at least <paramref
    /// name="count"/> elements.</remarks>
    /// <param name="items">The array of items that have been removed. Only the first <paramref name="count"/> elements are considered.</param>
    /// <param name="count">The number of items to include from the <paramref name="items"/> array. Must be less than or equal to the length
    /// of <paramref name="items"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void EmitBatchRemoved(TItem[] items, int count)
    {
        var pool = ArrayPool<TItem>.Shared.Rent(count);
        Array.Copy(items, pool, count);
        Emit(CacheAction.BatchRemoved, default, new PooledBatch<TItem>(pool, count));
    }

    /// <summary>
    /// Emits a notification that a batch of items has been removed from the list.
    /// </summary>
    /// <param name="items">The list containing the items that were removed. The first <paramref name="count"/> elements are considered
    /// removed.</param>
    /// <param name="count">The number of items removed from the list. Must be greater than or equal to 0 and less than or equal to the
    /// number of items in <paramref name="items"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void EmitBatchRemovedFromList(IList<TItem> items, int count)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var pool = ArrayPool<TItem>.Shared.Rent(count);
        for (var i = 0; i < count; i++)
        {
            pool[i] = items[i];
        }

        Emit(CacheAction.BatchRemoved, default, new PooledBatch<TItem>(pool, count));
    }

    /// <summary>
    /// Notifies all registered indices that a new item has been added.
    /// </summary>
    /// <param name="item">The item that was added and should be communicated to all indices.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void NotifyIndicesAdded(TValue item)
    {
        if (Indices.IsEmpty)
        {
            return;
        }

        foreach (var kvp in Indices)
        {
            kvp.Value.OnAdded(item);
        }
    }

    /// <summary>
    /// Notifies all registered index listeners that the specified item has been removed.
    /// </summary>
    /// <param name="item">The item that was removed and for which index listeners should be notified.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void NotifyIndicesRemoved(TValue item)
    {
        if (Indices.IsEmpty)
        {
            return;
        }

        foreach (var kvp in Indices)
        {
            kvp.Value.OnRemoved(item);
        }
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
