// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET6_0_OR_GREATER

using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace CP.Reactive;

/// <summary>
/// Provides a base class for managing a quaternary (four-part) data structure with thread-safe access and asynchronous
/// persistence operations.
/// </summary>
/// <remarks>This class implements thread safety using four internal locks, allowing concurrent access to
/// different parts of the data structure. It supports asynchronous flushing to and recovery from a file, enabling
/// efficient persistence of the data. Derived classes must implement the serialization and deserialization logic by
/// overriding the abstract methods. The class is intended for scenarios where high-performance, multi-threaded access
/// and reliable persistence are required.</remarks>
/// <typeparam name="TItem">The type of items stored in the data structure. Must not be null.</typeparam>
public abstract class QuaternaryBase<TItem> : IDisposable
{
    /// <summary>
    /// Provides an array of four <see cref="ReaderWriterLockSlim"/> instances used to synchronize access to shared
    /// resources.
    /// </summary>
    /// <remarks>Each element in the array can be used to protect a distinct partition of data, enabling
    /// concurrent read and write operations with reduced contention. The array is initialized with four separate lock
    /// instances.</remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Intended for use in derived classes.")]
    protected readonly ReaderWriterLockSlim[] Locks = [.. Enumerable.Range(0, 4).Select(_ => new ReaderWriterLockSlim())];

    /// <summary>
    /// Provides an unbounded channel for publishing cache notification events to consumers.
    /// </summary>
    /// <remarks>The channel is configured for a single reader and may have multiple writers. This allows
    /// multiple producers to post cache notifications, while a single consumer processes them in order. The channel is
    /// intended for internal event propagation within the cache implementation.</remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Intended for use in derived classes.")]
    protected readonly Channel<CacheNotify<TItem>> EventChannel = Channel.CreateUnbounded<CacheNotify<TItem>>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly Subject<CacheNotify<TItem>> _subject = new();
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="QuaternaryBase{TItem}"/> class and starts asynchronous event processing.
    /// </summary>
    /// <remarks>This constructor begins processing events in the background by invoking the
    /// ProcessEventsAsync method. Event processing starts immediately upon instantiation. Derived classes should ensure
    /// that any required initialization is complete before events are processed.</remarks>
    protected QuaternaryBase() => Task.Factory.StartNew(ProcessEventsAsync, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>
    /// Gets an observable sequence that provides notifications about changes to the cache.
    /// </summary>
    /// <remarks>Subscribers receive a stream of cache change events as they occur. The sequence completes
    /// when the cache is disposed or no longer available.</remarks>
    public IObservable<CacheNotify<TItem>> Stream => _subject.AsObservable();

    /// <summary>
    /// Gets a value indicating whether the object has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the generic type parameter is blittable and can be safely copied to unmanaged
    /// memory.
    /// </summary>
    /// <remarks>A type is considered blittable if it does not contain any reference types or fields that
    /// require special marshaling. This property is useful when working with interop scenarios or low-level memory
    /// operations that require types to be directly transferable between managed and unmanaged code.</remarks>
    protected bool IsBlittable => field = !RuntimeHelpers.IsReferenceOrContainsReferences<TItem>();

    /// <summary>
    /// Asynchronously writes the current in-memory data to a file at the specified path, ensuring a consistent snapshot
    /// is saved.
    /// </summary>
    /// <remarks>The method acquires internal locks to guarantee that the data written reflects a stable
    /// state. The file is created or replaced with the latest data. This operation is thread-safe and can be awaited.
    /// Ensure that the provided path is valid and accessible for writing.</remarks>
    /// <param name="path">The file system path where the data will be written. If the file exists, it will be overwritten.</param>
    /// <returns>A ValueTask that represents the asynchronous flush operation.</returns>
    public async ValueTask FlushToFileAsync(string path)
    {
        // Acquire all locks in order to ensure a stable snapshot
        for (var i = 0; i < 4; i++)
        {
            Locks[i].EnterReadLock();
        }

        try
        {
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
            await using var writer = new BinaryWriter(fs);

            writer.Write(0x41544552); // Magic 'QUAT'
            writer.Write(IsBlittable); // Header flag: Is data raw memory?

            await WriteDataAsync(writer);
        }
        finally
        {
            for (var i = 3; i >= 0; i--)
            {
                Locks[i].ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Recovers the cache state from the specified file asynchronously.
    /// </summary>
    /// <remarks>This method acquires write locks during the recovery process to ensure thread safety. The
    /// operation reads and validates the file format before restoring the cache state.</remarks>
    /// <param name="path">The path to the file containing the cache data to recover. The file must exist and be accessible for reading.</param>
    /// <returns>A ValueTask that represents the asynchronous recovery operation.</returns>
    /// <exception cref="InvalidDataException">Thrown if the specified file does not contain valid cache data.</exception>
    public async ValueTask RecoverFromFileAsync(string path)
    {
        for (var i = 0; i < 4; i++)
        {
            Locks[i].EnterWriteLock();
        }

        try
        {
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
            using var reader = new BinaryReader(fs);

            if (reader.ReadInt32() != 0x41544552)
            {
                throw new InvalidDataException("Invalid Cache File");
            }

            var wasBlittable = reader.ReadBoolean();

            await ReadDataAsync(reader, wasBlittable);
        }
        finally
        {
            for (var i = 3; i >= 0; i--)
            {
                Locks[i].ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// Releases all resources used by the current instance of the class.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Attempts to enqueue a cache event with the specified action, item, and optional batch to the event channel.
    /// </summary>
    /// <param name="action">The cache action to be performed. Determines the type of event to emit.</param>
    /// <param name="item">The item associated with the cache event. May be null if the action does not require an item.</param>
    /// <param name="batch">An optional batch of items related to the cache event. Specify null if the event does not involve a batch
    /// operation.</param>
    internal void Emit(CacheAction action, TItem? item, PooledBatch<TItem>? batch = null)
        => EventChannel.Writer.TryWrite(new(action, item, batch));

    /// <summary>
    /// Serializes the specified item to the provided binary writer using the item's string representation as a
    /// fallback.
    /// </summary>
    /// <remarks>This method is intended as a fallback serialization mechanism when no specialized serializer
    /// is available for the item type. The resulting output depends on the implementation of the item's ToString()
    /// method.</remarks>
    /// <param name="w">The binary writer to which the item's string representation is written. Cannot be null.</param>
    /// <param name="item">The item to serialize. Its string representation, as returned by ToString(), is written to the binary writer.</param>
    protected static void SerializeFallback(BinaryWriter w, TItem item)
    {
        if (w == null)
        {
            throw new ArgumentNullException(nameof(w));
        }

        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        w.Write(item.ToString()!);
    }

    /// <summary>
    /// Deserializes a value of type T from the specified binary reader using a fallback mechanism that reads a string
    /// representation.
    /// </summary>
    /// <remarks>This method assumes that the serialized representation of T can be obtained from a string.
    /// Use this fallback only when no specialized deserialization logic is available for T.</remarks>
    /// <param name="r">The binary reader from which to read the serialized data. Must not be null.</param>
    /// <returns>A value of type T deserialized from the string read from the binary reader.</returns>
    protected static TItem DeserializeFallback(BinaryReader r)
    {
        if (r == null)
        {
            throw new ArgumentNullException(nameof(r));
        }

        return (TItem)(object)r.ReadString();
    }

    /// <summary>
    /// Releases the unmanaged resources used by the class and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                _cts.Cancel();
                _cts.Dispose();
                _subject.Dispose();
                if (Locks != null)
                {
                    foreach (var rwLock in Locks)
                    {
                        rwLock?.Dispose();
                    }
                }
            }

            IsDisposed = true;
        }
    }

    /// <summary>
    /// Asynchronously writes data to the specified binary stream using the provided writer.
    /// </summary>
    /// <param name="writer">The <see cref="BinaryWriter"/> instance used to write data to the underlying stream. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous write operation.</returns>
    protected abstract ValueTask WriteDataAsync(BinaryWriter writer);

    /// <summary>
    /// Asynchronously reads data from the specified binary stream, using the provided reader and format information.
    /// </summary>
    /// <param name="reader">The binary reader used to read data from the input stream. Must not be null.</param>
    /// <param name="wasBlittable">Indicates whether the data format is blittable. If <see langword="true"/>, the data is expected to be in a
    /// format that can be directly mapped to memory; otherwise, additional processing may be required.</param>
    /// <returns>A ValueTask that represents the asynchronous read operation.</returns>
    protected abstract ValueTask ReadDataAsync(BinaryReader reader, bool wasBlittable);

    private async Task ProcessEventsAsync()
    {
        try
        {
            while (await EventChannel.Reader.WaitToReadAsync(_cts.Token))
            {
                while (EventChannel.Reader.TryRead(out var evt))
                {
                    _subject.OnNext(evt);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
#endif
