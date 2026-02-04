// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using CP.Reactive.Collections;

namespace CP.Reactive.Core;

/// <summary>
/// Provides extension methods for working with <see cref="CacheNotify{T}"/> streams across all reactive collection types.
/// </summary>
/// <remarks>
/// These extension methods provide a unified API for observing and transforming change notifications
/// from <see cref="ReactiveList{T}"/>, <see cref="Reactive2DList{T}"/>, and Quaternary collections.
/// </remarks>
public static class CacheNotifyExtensions
{
    /// <summary>
    /// Filters the stream to only include notifications of a specific action type.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <param name="action">The cache action to filter for.</param>
    /// <returns>An observable containing only notifications with the specified action.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<CacheNotify<T>> WhereAction<T>(
        this IObservable<CacheNotify<T>> source,
        CacheAction action)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Where(n => n.Action == action);
    }

    /// <summary>
    /// Filters the stream to only include add notifications (single and batch).
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <returns>An observable containing only add notifications.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<CacheNotify<T>> WhereAdded<T>(this IObservable<CacheNotify<T>> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Where(n => n.Action == CacheAction.Added || n.Action == CacheAction.BatchAdded);
    }

    /// <summary>
    /// Filters the stream to only include remove notifications (single and batch).
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <returns>An observable containing only remove notifications.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<CacheNotify<T>> WhereRemoved<T>(this IObservable<CacheNotify<T>> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Where(n => n.Action == CacheAction.Removed || n.Action == CacheAction.BatchRemoved);
    }

    /// <summary>
    /// Projects single item notifications to their items.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <returns>An observable of items from single-item notifications.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<T> SelectItems<T>(this IObservable<CacheNotify<T>> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source
            .Where(n => n.Item != null)
            .Select(n => n.Item!);
    }

    /// <summary>
    /// Projects all items from notifications (both single and batch) to a flat sequence.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <returns>An observable of all items from both single-item and batch notifications.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<T> SelectAllItems<T>(this IObservable<CacheNotify<T>> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.SelectMany(n =>
        {
            if (n.Batch != null)
            {
                var items = new T[n.Batch.Count];
                Array.Copy(n.Batch.Items, items, n.Batch.Count);
                return items;
            }

            if (n.Item != null)
            {
                return new[] { n.Item };
            }

            return Array.Empty<T>();
        });
    }

    /// <summary>
    /// Subscribes to added items only from the stream.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <returns>An observable of added items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<T> OnItemAdded<T>(this IObservable<CacheNotify<T>> source) => source.WhereAdded().SelectAllItems();

    /// <summary>
    /// Subscribes to removed items only from the stream.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <returns>An observable of removed items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<T> OnItemRemoved<T>(this IObservable<CacheNotify<T>> source) => source.WhereRemoved().SelectAllItems();

    /// <summary>
    /// Subscribes to updated items only from the stream.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <returns>An observable of updated items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<T> OnItemUpdated<T>(this IObservable<CacheNotify<T>> source) => source.WhereAction(CacheAction.Updated).SelectItems();

    /// <summary>
    /// Subscribes to moved items only from the stream.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <returns>An observable of move information tuples (Item, OldIndex, NewIndex).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<(T Item, int OldIndex, int NewIndex)> OnItemMoved<T>(this IObservable<CacheNotify<T>> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source
            .WhereAction(CacheAction.Moved)
            .Where(n => n.Item != null)
            .Select(n => (n.Item!, n.PreviousIndex, n.CurrentIndex));
    }

    /// <summary>
    /// Subscribes to clear notifications from the stream.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <returns>An observable that emits when the collection is cleared.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<CacheNotify<T>> OnCleared<T>(this IObservable<CacheNotify<T>> source) => source.WhereAction(CacheAction.Cleared);

    /// <summary>
    /// Buffers notifications over a time period and flattens batch items for efficient processing.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <param name="bufferTime">The time span over which to buffer notifications.</param>
    /// <returns>An observable of buffered notification lists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<IList<CacheNotify<T>>> BufferNotifications<T>(
        this IObservable<CacheNotify<T>> source,
#if NET8_0_OR_GREATER
        in TimeSpan bufferTime)
#else
        TimeSpan bufferTime)
#endif
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source
            .Buffer(bufferTime)
            .Where(b => b.Count > 0);
    }

    /// <summary>
    /// Throttles the stream to reduce notification frequency.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <param name="throttleTime">The minimum time interval between notifications.</param>
    /// <returns>An observable with throttled notifications.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<CacheNotify<T>> ThrottleNotifications<T>(
        this IObservable<CacheNotify<T>> source,
#if NET8_0_OR_GREATER
        in TimeSpan throttleTime)
#else
        TimeSpan throttleTime)
#endif
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Throttle(throttleTime);
    }

    /// <summary>
    /// Observes notifications on the specified scheduler.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <param name="scheduler">The scheduler to observe on.</param>
    /// <returns>An observable that dispatches notifications on the specified scheduler.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<CacheNotify<T>> ObserveOnScheduler<T>(
        this IObservable<CacheNotify<T>> source,
        IScheduler scheduler)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (scheduler == null)
        {
            throw new ArgumentNullException(nameof(scheduler));
        }

        return source.ObserveOn(scheduler);
    }

    /// <summary>
    /// Transforms notifications using a selector function.
    /// </summary>
    /// <typeparam name="T">The type of elements in the source collection.</typeparam>
    /// <typeparam name="TResult">The type of elements in the result.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <param name="selector">A transform function to apply to each item.</param>
    /// <returns>An observable of transformed items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<TResult> TransformItems<T, TResult>(
        this IObservable<CacheNotify<T>> source,
        Func<T, TResult> selector)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return source.SelectAllItems().Select(selector);
    }

    /// <summary>
    /// Filters items in notifications using a predicate.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <param name="predicate">A function to test each item for a condition.</param>
    /// <returns>An observable of items that satisfy the predicate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<T> FilterItems<T>(
        this IObservable<CacheNotify<T>> source,
        Func<T, bool> predicate)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return source.SelectAllItems().Where(predicate);
    }

    /// <summary>
    /// Disposes batch resources after processing.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <returns>An observable that automatically disposes batch resources.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<CacheNotify<T>> AutoDisposeBatches<T>(this IObservable<CacheNotify<T>> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Do(n => n.Batch?.Dispose());
    }

    /// <summary>
    /// Counts items by action type in a stream.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <returns>An observable of tuples containing (Action, Count).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<(CacheAction Action, int Count)> CountByAction<T>(this IObservable<CacheNotify<T>> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Select(n =>
        {
            var count = n.Batch?.Count ?? (n.Item != null ? 1 : 0);
            return (n.Action, count);
        });
    }

    /// <summary>
    /// Converts a single <see cref="CacheNotify{T}"/> to a <see cref="Change{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="notification">The cache notification to convert.</param>
    /// <returns>A Change representing the notification, or null for batch/clear operations without an item.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Change<T>? ToChange<T>(this CacheNotify<T> notification)
    {
        if (notification == null)
        {
            return null;
        }

        return notification.Action switch
        {
            CacheAction.Added when notification.Item != null =>
                Change<T>.CreateAdd(notification.Item, notification.CurrentIndex),
            CacheAction.Removed when notification.Item != null =>
                Change<T>.CreateRemove(notification.Item, notification.CurrentIndex),
            CacheAction.Updated when notification.Item != null =>
                Change<T>.CreateUpdate(notification.Item, notification.Previous!, notification.CurrentIndex),
            CacheAction.Moved when notification.Item != null =>
                Change<T>.CreateMove(notification.Item, notification.CurrentIndex, notification.PreviousIndex),
            CacheAction.Refreshed when notification.Item != null =>
                Change<T>.CreateRefresh(notification.Item, notification.CurrentIndex),
            CacheAction.Cleared =>
                new Change<T>(ChangeReason.Clear, default!),
            _ => null
        };
    }

    /// <summary>
    /// Converts a stream of <see cref="CacheNotify{T}"/> to a stream of <see cref="ChangeSet{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable of cache notifications.</param>
    /// <returns>An observable of change sets.</returns>
    /// <remarks>
    /// This method bridges the gap between the Stream-based notifications and ChangeSet-based processing.
    /// Batch operations are expanded into individual changes within the change set.
    /// Clear operations emit a change set with individual Remove changes for each cleared item (consistent with DynamicData).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<ChangeSet<T>> ToChangeSets<T>(this IObservable<CacheNotify<T>> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Select(notification =>
        {
            // Handle batch operations (including Cleared which contains the removed items)
            if (notification.Batch != null && notification.Batch.Count > 0)
            {
                var changes = new Change<T>[notification.Batch.Count];
                var reason = notification.Action switch
                {
                    CacheAction.BatchAdded => ChangeReason.Add,
                    CacheAction.BatchRemoved => ChangeReason.Remove,
                    CacheAction.Cleared => ChangeReason.Remove, // Clear is semantically a bulk remove
                    _ => ChangeReason.Refresh
                };

                var startIndex = notification.CurrentIndex >= 0 ? notification.CurrentIndex : 0;
                for (var i = 0; i < notification.Batch.Count; i++)
                {
                    var item = notification.Batch.Items[i];
                    changes[i] = reason == ChangeReason.Add
                        ? Change<T>.CreateAdd(item, startIndex + i)
                        : reason == ChangeReason.Remove
                            ? Change<T>.CreateRemove(item, -1)
                            : Change<T>.CreateRefresh(item, startIndex + i);
                }

                return new ChangeSet<T>(changes);
            }

            // Handle Cleared action without batch (empty collection was cleared)
            if (notification.Action == CacheAction.Cleared)
            {
                return ChangeSet<T>.Empty;
            }

            // Handle single item operations
            var change = notification.ToChange();
            if (change.HasValue)
            {
                return new ChangeSet<T>(change.Value);
            }

            // Return empty for operations without items
            return ChangeSet<T>.Empty;
        }).Where(cs => cs.Count > 0);
    }
}
