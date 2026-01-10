// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET6_0_OR_GREATER

using System.Diagnostics.CodeAnalysis;

namespace CP.Reactive;

/// <summary>
/// Represents a dictionary collection that supports efficient add, update, removal, and eviction operations for
/// key/value pairs.
/// </summary>
/// <remarks>In addition to standard dictionary operations, this interface provides methods for atomic
/// add-or-update, removal with value retrieval, and entry eviction. Implementations may use eviction to manage capacity
/// or support cache-like scenarios. The interface does not specify the eviction policy; consult the implementation
/// documentation for details.</remarks>
/// <typeparam name="TKey">The type of keys in the dictionary. Must be non-nullable.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
public interface IQuaternaryDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// Adds a key/value pair to the collection or updates the value for an existing key.
    /// </summary>
    /// <param name="key">The key of the element to add or update.</param>
    /// <param name="value">The value to set for the specified key.</param>
    /// <param name="oldValue">When this method returns, contains the previous value associated with the key, if the key existed;
    /// otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns>true if the key was already present and the value was updated; otherwise, false if a new key/value pair was
    /// added.</returns>
    bool Set(TKey key, TValue value, out TValue? oldValue);

    /// <summary>
    /// Removes the value with the specified key from the collection, if it exists.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <param name="value">When this method returns, contains the value that was removed, if the key was found; otherwise, the default
    /// value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns>true if the element is successfully found and removed; otherwise, false.</returns>
    bool Remove(TKey key, [MaybeNullWhen(false)] out TValue? value);

    /// <summary>
    /// Attempts to evict an entry from the collection and retrieve its key and value.
    /// </summary>
    /// <param name="evictedKey">When this method returns, contains the key of the evicted entry if an entry was evicted; otherwise, the
    /// default value for the type of the key.</param>
    /// <param name="evictedValue">When this method returns, contains the value of the evicted entry if an entry was evicted; otherwise, the
    /// default value for the type of the value.</param>
    /// <returns>true if an entry was successfully evicted; otherwise, false.</returns>
    bool TryEvict([MaybeNullWhen(false)] out TKey evictedKey, [MaybeNullWhen(false)] out TValue? evictedValue);
}
#endif
