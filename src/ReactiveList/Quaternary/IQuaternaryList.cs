// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Collections.Specialized;

namespace CP.Reactive;

/// <summary>
/// Represents a list of elements that supports secondary named indexes for efficient querying in addition to standard
/// list operations.
/// </summary>
/// <remarks>The IQuaternaryList of T interface extends IList of T by allowing the creation of named indexes on
/// element properties. These indexes enable fast retrieval of elements based on indexed keys. Indexes must be
/// explicitly added before they can be used for querying. This interface is suitable for scenarios where frequent
/// lookups by specific keys are required in addition to sequential access.</remarks>
/// <typeparam name="T">The type of elements contained in the list.</typeparam>
public interface IQuaternaryList<T> : ICollection<T>, INotifyCollectionChanged, IDisposable
{
    /// <summary>
    /// Adds an index to the collection using the specified name and key selector.
    /// </summary>
    /// <remarks>If an index with the specified name already exists, this method may throw an exception or
    /// overwrite the existing index, depending on the implementation. Indexes can improve lookup performance for
    /// queries using the specified key.</remarks>
    /// <typeparam name="TKey">The type of the key used for the index. Must be non-nullable.</typeparam>
    /// <param name="name">The unique name of the index to add. Cannot be null or empty.</param>
    /// <param name="keySelector">A function that extracts the key from each element in the collection. Cannot be null.</param>
    void AddIndex<TKey>(string name, Func<T, TKey> keySelector)
        where TKey : notnull;

    /// <summary>
    /// Adds the elements of the specified collection to the end of the list.
    /// </summary>
    /// <remarks>The order of the elements in the new list will match the order in the specified collection.
    /// If the collection is modified while the operation is in progress, the behavior is undefined.</remarks>
    /// <param name="collection">The collection whose elements should be added to the end of the list. The collection itself cannot be null, but
    /// it may contain elements that are null if the list type permits null values.</param>
    void AddRange(IEnumerable<T> collection);

    /// <summary>
    /// Retrieves all entities of type T that match the specified key in the given index.
    /// </summary>
    /// <typeparam name="TKey">The type of the key used to query the index. Must be non-nullable.</typeparam>
    /// <param name="indexName">The name of the index to query. Specifies which index to use for the lookup.</param>
    /// <param name="key">The key value to search for within the specified index. Must not be null.</param>
    /// <returns>An enumerable collection of entities of type T that match the specified key. The collection is empty if no
    /// matching entities are found.</returns>
    IEnumerable<T> GetItemsBySecondaryIndex<TKey>(string indexName, TKey key)
        where TKey : notnull;

    /// <summary>
    /// Removes all elements in the specified collection from the current collection.
    /// </summary>
    /// <remarks>If an element in the specified collection does not exist in the current collection, it is
    /// ignored. The method removes all occurrences of each element found in the collection.</remarks>
    /// <param name="collection">The collection of elements to remove from the current collection. All matching elements will be removed. Cannot
    /// be null.</param>
    void RemoveRange(IEnumerable<T> collection);

    /// <summary>
    /// Removes all elements that match the specified predicate from the collection.
    /// </summary>
    /// <remarks>This method provides an efficient way to remove multiple items based on a condition
    /// without having to enumerate the collection separately.</remarks>
    /// <param name="predicate">A function that returns true for elements that should be removed.</param>
    /// <returns>The number of elements removed from the collection.</returns>
    int RemoveMany(Func<T, bool> predicate);

    /// <summary>
    /// Performs a batch edit operation on the collection, ensuring only a single change notification is emitted.
    /// </summary>
    /// <remarks>Use this method when making multiple modifications to the collection to improve efficiency
    /// by reducing the number of change notifications. All operations within the edit action are applied atomically.</remarks>
    /// <param name="editAction">An action that receives an editable list interface to perform modifications.</param>
    void Edit(Action<ICollection<T>> editAction);
}
#endif
