// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;
#else
namespace CP.Primitives.Internal;
#endif
/// <summary>Represents a rented array buffer with tracking of the used length.</summary>
/// <typeparam name="T">The type of item stored in the buffer.</typeparam>
/// <param name="buffer">The rented array used as the underlying buffer.</param>
/// <param name="length">The number of valid elements in <paramref name="buffer"/>.</param>
internal sealed class PooledBuffer<T>(T[] buffer, int length) : IDisposable
{
    private readonly int _length = length;

    private T[]? _buffer = buffer;

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
        return new(arr, source.Count);
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

        ArrayPool<T>.Shared.Return(_buffer, clearArray: ArrayPoolClearHelper.IsReferenceOrContainsReferences<T>());
        _buffer = null;
    }
}
