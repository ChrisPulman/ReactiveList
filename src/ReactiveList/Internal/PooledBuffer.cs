// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Buffers;
using System.Runtime.CompilerServices;

namespace CP.Reactive.Internal;

/// <summary>
/// Initializes a new instance of the <see cref="PooledBuffer{T}"/> class with the specified buffer and length.
/// </summary>
/// <param name="buffer">The array to be used as the underlying buffer. Cannot be null.</param>
/// <param name="length">The number of elements in the buffer that are considered valid. Must be non-negative and less than or equal to
/// the length of the buffer.</param>
internal sealed class PooledBuffer<T>(T[] buffer, int length) : IDisposable
{
    /// <summary>
    /// Gets a read-only span over the valid elements in the buffer.
    /// </summary>
    public ReadOnlySpan<T> Span => buffer.AsSpan(0, length);

    /// <summary>
    /// Creates a new pooled buffer containing the elements of the specified list.
    /// </summary>
    /// <remarks>The returned buffer uses a pooled array for storage. The caller is responsible for disposing
    /// the buffer when it is no longer needed to return the underlying array to the pool.</remarks>
    /// <param name="source">The list whose elements are copied into the new pooled buffer. Cannot be null.</param>
    /// <returns>A new <see cref="PooledBuffer{T}"/> containing the elements of <paramref name="source"/>.</returns>
    public static PooledBuffer<T> FromList(List<T> source)
    {
        var arr = ArrayPool<T>.Shared.Rent(source.Count);
        source.CopyTo(arr);
        return new PooledBuffer<T>(arr, source.Count);
    }

    /// <summary>
    /// Releases all resources used by the current instance of the object.
    /// </summary>
    /// <remarks>Call this method when you are finished using the object to return any pooled resources and
    /// allow for proper cleanup. After calling Dispose, the object should not be used.</remarks>
    public void Dispose()
    {
        if (buffer != null)
        {
            ArrayPool<T>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            buffer = null!;
        }
    }
}
#endif
