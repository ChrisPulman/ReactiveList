// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

namespace CP.Reactive.Quaternary;

/// <summary>
/// Represents a collection of quaternary changes, supporting batch operations for efficient change tracking.
/// </summary>
/// <remarks>QuaternaryChangeSet{T} extends List{QuaternaryChange{T}} to provide additional functionality for
/// grouping and batching changes. This class is typically used in scenarios where multiple changes need to be tracked
/// and processed together, such as in data synchronization or state management workflows.</remarks>
/// <typeparam name="T">The type of the items associated with each quaternary change in the set.</typeparam>
public sealed class QuaternaryChangeSet<T> : List<QuaternaryChange<T>>
{
    /// <summary>
    /// Creates a new quaternary change set that represents a batch operation containing the specified changes.
    /// </summary>
    /// <remarks>The returned change set begins with a batch marker to indicate that the contained changes
    /// should be processed as a single batch operation. The order of the changes in the input collection is
    /// preserved.</remarks>
    /// <param name="changes">The collection of changes to include in the batch. Cannot be null.</param>
    /// <returns>A <see cref="QuaternaryChangeSet{T}"/> containing a batch marker followed by the specified changes.</returns>
    public static QuaternaryChangeSet<T> Batch(IEnumerable<QuaternaryChange<T>> changes)
    {
        var set = new QuaternaryChangeSet<T>
        {
            new QuaternaryChange<T>(QuaternaryChangeReason.Batch, default!)
        };
        set.AddRange(changes);
        return set;
    }

    /// <summary>
    /// Creates a new change set containing a single specified change.
    /// </summary>
    /// <param name="change">The change to include in the resulting change set. Cannot be null.</param>
    /// <returns>A new <see cref="QuaternaryChangeSet{T}"/> containing only the specified change.</returns>
    public static QuaternaryChangeSet<T> Single(in QuaternaryChange<T> change) => new QuaternaryChangeSet<T> { change };
}
#endif
