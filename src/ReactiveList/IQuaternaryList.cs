// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET6_0_OR_GREATER

namespace CP.Reactive;

/// <summary>
/// Represents a collection that supports quaternary eviction policies and provides methods for managing item usage and
/// storage capacity.
/// </summary>
/// <remarks>In addition to standard collection operations, this interface defines methods for eviction management
/// and usage tracking, making it suitable for advanced caching or resource management scenarios. Implementations may
/// use custom eviction strategies, such as least recently used (LRU) or other policies, to determine which items to
/// remove when capacity constraints are reached.</remarks>
/// <typeparam name="T">The type of elements contained in the list.</typeparam>
public interface IQuaternaryList<T> : ICollection<T>
{
    /// <summary>
    /// Determines whether the current value should be evicted based on the eviction policy.
    /// </summary>
    /// <returns>The value to be evicted if eviction is required; otherwise, null if no eviction is necessary.</returns>
    T? CheckEviction();

    /// <summary>
    /// Marks the specified item as recently used or accessed.
    /// </summary>
    /// <param name="item">The item to mark as recently used. Cannot be null.</param>
    void Touch(T item);

    /// <summary>
    /// Ensures that the underlying storage can accommodate at least the specified number of elements without
    /// further resizing.
    /// </summary>
    /// <remarks>If the current capacity is less than the specified value, the capacity is increased.
    /// If the current capacity is already greater than or equal to the specified value, no action is
    /// taken.</remarks>
    /// <param name="newcapacity">The minimum number of elements that the storage must be able to hold. Must be non-negative.</param>
    void EnsureCapacity(int newcapacity);
}
#endif
