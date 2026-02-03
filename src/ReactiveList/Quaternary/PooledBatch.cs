// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Buffers;

namespace CP.Reactive.Quaternary;

/// <summary>
/// Represents a batch of items obtained from an array pool, along with the number of valid items in the batch.
/// </summary>
/// <remarks>The underlying array is returned to the shared array pool when the batch is disposed. After calling
/// <see cref="Dispose"/>, the contents of <paramref name="Items"/> should not be accessed. This type is sealed and not
/// intended for inheritance.</remarks>
/// <typeparam name="T">The type of elements contained in the batch.</typeparam>
/// <param name="Items">The array of items provided by the array pool. Must not be null.</param>
/// <param name="Count">The number of valid items in the batch. Must be between 0 and the length of the <paramref name="Items"/> array,
/// inclusive.</param>
public sealed record PooledBatch<T>(T[] Items, int Count) : IDisposable
{
    private int _isDisposed;

    /// <summary>
    /// Releases all resources used by the current instance.
    /// </summary>
    /// <remarks>Call this method when the instance is no longer needed to return any pooled resources and
    /// allow for proper cleanup. After calling this method, the instance should not be used.</remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            ArrayPool<T>.Shared.Return(Items);
        }
    }
}
#endif
