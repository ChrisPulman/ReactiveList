// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CP.Reactive.Core;

/// <summary>
/// Represents a single change in a reactive collection.
/// </summary>
/// <remarks>
/// This is a struct-based change notification to minimize heap allocations.
/// Compatible with DynamicData's change patterns.
/// </remarks>
/// <typeparam name="T">The type of item that changed.</typeparam>
public readonly record struct Change<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Change{T}"/> struct.
    /// </summary>
    /// <param name="reason">The reason for the change.</param>
    /// <param name="current">The current/new item value.</param>
    /// <param name="previous">The previous item value (for updates).</param>
    /// <param name="currentIndex">The current index of the item, or -1 if not applicable.</param>
    /// <param name="previousIndex">The previous index of the item (for moves), or -1 if not applicable.</param>
    public Change(ChangeReason reason, T current, T? previous = default, int currentIndex = -1, int previousIndex = -1)
    {
        Reason = reason;
        Current = current;
        Previous = previous;
        CurrentIndex = currentIndex;
        PreviousIndex = previousIndex;
    }

    /// <summary>
    /// Gets the reason for the change.
    /// </summary>
    public ChangeReason Reason { get; }

    /// <summary>
    /// Gets the current/new item value.
    /// </summary>
    public T Current { get; }

    /// <summary>
    /// Gets the previous item value. Only populated for Update operations.
    /// </summary>
    public T? Previous { get; }

    /// <summary>
    /// Gets the current index of the item in the collection, or -1 if not applicable.
    /// </summary>
    public int CurrentIndex { get; }

    /// <summary>
    /// Gets the previous index of the item. Only populated for Move operations.
    /// </summary>
    public int PreviousIndex { get; }

    /// <summary>
    /// Creates an Add change.
    /// </summary>
    /// <param name="item">The item being added.</param>
    /// <param name="index">The index where the item was added.</param>
    /// <returns>A new Change representing an add operation.</returns>
    public static Change<T> CreateAdd(T item, int index = -1) =>
        new(ChangeReason.Add, item, currentIndex: index);

    /// <summary>
    /// Creates a Remove change.
    /// </summary>
    /// <param name="item">The item being removed.</param>
    /// <param name="index">The index from which the item was removed.</param>
    /// <returns>A new Change representing a remove operation.</returns>
    public static Change<T> CreateRemove(T item, int index = -1) =>
        new(ChangeReason.Remove, item, previousIndex: index);

    /// <summary>
    /// Creates an Update change.
    /// </summary>
    /// <param name="current">The new item value.</param>
    /// <param name="previous">The previous item value.</param>
    /// <param name="index">The index of the updated item.</param>
    /// <returns>A new Change representing an update operation.</returns>
    public static Change<T> CreateUpdate(T current, T previous, int index = -1) =>
        new(ChangeReason.Update, current, previous, index, index);

    /// <summary>
    /// Creates a Move change.
    /// </summary>
    /// <param name="item">The item being moved.</param>
    /// <param name="currentIndex">The new index of the item.</param>
    /// <param name="previousIndex">The previous index of the item.</param>
    /// <returns>A new Change representing a move operation.</returns>
    public static Change<T> CreateMove(T item, int currentIndex, int previousIndex) =>
        new(ChangeReason.Move, item, currentIndex: currentIndex, previousIndex: previousIndex);

    /// <summary>
    /// Creates a Refresh change.
    /// </summary>
    /// <param name="item">The item being refreshed.</param>
    /// <param name="index">The index of the item.</param>
    /// <returns>A new Change representing a refresh operation.</returns>
    public static Change<T> CreateRefresh(T item, int index = -1) =>
        new(ChangeReason.Refresh, item, currentIndex: index);
}
