// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

#if NET6_0_OR_GREATER
using System.Buffers;
using System.Runtime.CompilerServices;
#endif

namespace CP.Reactive.Core;

/// <summary>
/// Represents a set of changes to a collection, compatible with DynamicData patterns.
/// </summary>
/// <remarks>
/// This struct uses array pooling on .NET 6+ for optimal memory efficiency.
/// The caller should dispose of the ChangeSet when done to return the array to the pool.
/// </remarks>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public readonly record struct ChangeSet<T> : IReadOnlyList<Change<T>>, IDisposable, IEquatable<ChangeSet<T>>
{
    private readonly Change<T>[]? _changes;
    private readonly int _count;
#if NET6_0_OR_GREATER
    private readonly bool _isPooled;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSet{T}"/> struct with a single change.
    /// </summary>
    /// <param name="change">The single change.</param>
    #if NET6_0_OR_GREATER
    public ChangeSet(in Change<T> change)
#else
    public ChangeSet(Change<T> change)
#endif
    {
#if NET6_0_OR_GREATER
        _changes = ArrayPool<Change<T>>.Shared.Rent(1);
        _isPooled = true;
#else
        _changes = new Change<T>[1];
#endif
        _changes[0] = change;
        _count = 1;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSet{T}"/> struct from an array.
    /// </summary>
    /// <param name="changes">The changes array. This array is not copied on .NET Framework.</param>
    public ChangeSet(Change<T>[] changes)
    {
        if (changes == null)
        {
            throw new ArgumentNullException(nameof(changes));
        }

#if NET6_0_OR_GREATER
        _changes = ArrayPool<Change<T>>.Shared.Rent(changes.Length);
        changes.AsSpan().CopyTo(_changes);
        _isPooled = true;
#else
        _changes = changes;
#endif
        _count = changes.Length;
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSet{T}"/> struct from a span.
    /// </summary>
    /// <param name="changes">The changes span.</param>
    public ChangeSet(ReadOnlySpan<Change<T>> changes)
    {
        _changes = ArrayPool<Change<T>>.Shared.Rent(changes.Length);
        changes.CopyTo(_changes);
        _count = changes.Length;
        _isPooled = true;
    }
#endif

    /// <summary>
    /// Gets an empty change set.
    /// </summary>
    public static ChangeSet<T> Empty => new(Array.Empty<Change<T>>());

    /// <summary>
    /// Gets the number of changes in the set.
    /// </summary>
#pragma warning disable RCS1085 // Use auto-implemented property - backing field needed for struct initialization
    public int Count => _count;
#pragma warning restore RCS1085

    /// <summary>
    /// Gets the number of Add changes in this set.
    /// </summary>
    public int Adds => CountByReason(ChangeReason.Add);

    /// <summary>
    /// Gets the number of Remove changes in this set.
    /// </summary>
    public int Removes => CountByReason(ChangeReason.Remove);

    /// <summary>
    /// Gets the number of Update changes in this set.
    /// </summary>
    public int Updates => CountByReason(ChangeReason.Update);

    /// <summary>
    /// Gets the number of Move changes in this set.
    /// </summary>
    public int Moves => CountByReason(ChangeReason.Move);

    /// <summary>
    /// Gets the change at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the change to get.</param>
    /// <returns>The change at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">index is less than 0 or greater than or equal to Count.</exception>
    public Change<T> this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _changes![index];
        }
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Gets a span over the changes.
    /// </summary>
    /// <returns>A read-only span of the changes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<Change<T>> AsSpan() => _changes.AsSpan(0, _count);
#endif

    /// <summary>
    /// Determines whether the specified ChangeSet is equal to the current ChangeSet.
    /// </summary>
    /// <param name="other">The ChangeSet to compare with the current ChangeSet.</param>
    /// <returns>true if the specified ChangeSet is equal to the current ChangeSet; otherwise, false.</returns>
    public bool Equals(ChangeSet<T> other) => _count == other._count && ReferenceEquals(_changes, other._changes);

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>A hash code for this instance.</returns>
#if NET6_0_OR_GREATER
    public override int GetHashCode() => HashCode.Combine(_changes, _count);
#else
    public override int GetHashCode() => (_changes?.GetHashCode() ?? 0) ^ _count;
#endif

    /// <summary>
    /// Returns an enumerator that iterates through the changes.
    /// </summary>
    /// <returns>An enumerator for the change set.</returns>
    public Enumerator GetEnumerator() => new(_changes!, _count);

    /// <inheritdoc/>
    IEnumerator<Change<T>> IEnumerable<Change<T>>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns the pooled array to the pool.
    /// </summary>
    public void Dispose()
    {
#if NET6_0_OR_GREATER
        if (_isPooled && _changes != null)
        {
            ArrayPool<Change<T>>.Shared.Return(_changes, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<Change<T>>());
        }
#endif
    }

    private int CountByReason(ChangeReason reason)
    {
        if (_changes == null)
        {
            return 0;
        }

        var count = 0;
        for (var i = 0; i < _count; i++)
        {
            if (_changes[i].Reason == reason)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Enumerates the elements of a <see cref="ChangeSet{T}"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<Change<T>>
    {
        private readonly Change<T>[] _changes;
        private readonly int _count;
        private int _index;

        internal Enumerator(Change<T>[] changes, int count)
        {
            _changes = changes;
            _count = count;
            _index = -1;
        }

        /// <summary>
        /// Gets the element at the current position of the enumerator.
        /// </summary>
        public readonly Change<T> Current => _changes[_index];

        /// <inheritdoc/>
        readonly object IEnumerator.Current => Current;

        /// <summary>
        /// Advances the enumerator to the next element.
        /// </summary>
        /// <returns>true if the enumerator was successfully advanced; false if it has passed the end.</returns>
        public bool MoveNext()
        {
            var index = _index + 1;
            if (index < _count)
            {
                _index = index;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the enumerator to its initial position.
        /// </summary>
        public void Reset() => _index = -1;

        /// <summary>
        /// Releases resources used by the enumerator.
        /// </summary>
        public readonly void Dispose()
        {
        }
    }
}
