// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK

using System.Buffers;
using System.Runtime.CompilerServices;

namespace CP.Reactive.Internal;

/// <summary>Represents a rented array buffer with tracking of the used length.</summary>
/// <typeparam name="T">The type of item stored in the buffer.</typeparam>
internal sealed class PooledBuffer<T> : IDisposable
{
    private readonly int _length;

    private T[] _buffer;

    /// <summary>Initializes a new instance of the <see cref="PooledBuffer{T}"/> class with the provided buffer and length.</summary>
    /// <param name="buffer">The array to be used as the underlying buffer. Cannot be null.</param>
    /// <param name="length">The number of elements in the buffer that are considered valid. Must be non-negative and less than or equal to
    /// the length of the buffer.</param>
    public PooledBuffer(T[] buffer, int length)
    {
        _buffer = buffer;
        _length = length;
    }

    /// <summary>Gets a read-only span over the valid elements in the buffer.</summary>
    public ReadOnlySpan<T> Span => _buffer.AsSpan(0, _length);

    /// <summary>Creates a new pooled buffer containing the elements of the specified list.</summary>
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

    /// <summary>Releases all resources used by the current instance of the object.</summary>
    /// <remarks>Call this method when you are finished using the object to return any pooled resources and
    /// allow for proper cleanup. After calling Dispose, the object should not be used.</remarks>
    public void Dispose()
    {
        if (_buffer is null)
        {
            return;
        }

        ArrayPool<T>.Shared.Return(_buffer, clearArray: CP.Reactive.Internal.ArrayPoolClearHelper.IsReferenceOrContainsReferences<T>());
        _buffer = null!;
    }
}
#endif
