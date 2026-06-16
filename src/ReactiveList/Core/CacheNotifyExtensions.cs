// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using CP.Reactive.Collections;
using CP.Reactive.Internal;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;

namespace CP.Reactive.Core;

/// <summary>Provides extension methods for working with <see cref="CacheNotify{T}"/> streams across all reactive collection types.</summary>
/// <remarks>
/// These extension methods provide a unified API for observing and transforming change notifications
/// from <see cref="ReactiveList{T}"/>, <see cref="Reactive2DList{T}"/>, and Quaternary collections.
/// </remarks>
public static class CacheNotifyExtensions
{
    /// <summary>Converts single cache notifications into change-set values.</summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="notification">The cache notification to convert.</param>
    extension<T>(CacheNotify<T> notification)
    {
        /// <summary>Converts a single <see cref="CacheNotify{T}"/> to a <see cref="Change{T}"/>.</summary>
        /// <returns>A <see cref="Change{T}"/> representing the notification, or null for batch/clear operations without an item.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Change<T>? ToChange()
        {
            if (notification is null)
            {
                return null;
            }

            return notification.Action switch
            {
                CacheAction.Added when notification.Item is not null =>
                    Change<T>.CreateAdd(notification.Item, notification.CurrentIndex),
                CacheAction.Removed when notification.Item is not null =>
                    Change<T>.CreateRemove(notification.Item, notification.CurrentIndex),
                CacheAction.Updated when notification.Item is not null =>
                    Change<T>.CreateUpdate(notification.Item, notification.Previous!, notification.CurrentIndex),
                CacheAction.Moved when notification.Item is not null =>
                    Change<T>.CreateMove(notification.Item, notification.CurrentIndex, notification.PreviousIndex),
                CacheAction.Refreshed when notification.Item is not null =>
                    Change<T>.CreateRefresh(notification.Item, notification.CurrentIndex),
                CacheAction.Cleared =>
                    new Change<T>(ChangeReason.Clear, default!),
                _ => null
            };
        }
    }

    /// <summary>Helpers for observables of cache notifications.</summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source observable.</param>
    extension<T>(IObservable<CacheNotify<T>> source)
    {
        /// <summary>Filters the stream to only include notifications of a specific action type.</summary>
        /// <param name="action">The cache action to filter for.</param>
        /// <returns>An observable containing only notifications with the specified action.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<CacheNotify<T>> WhereAction(CacheAction action)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Keep(n => n.Action == action);
        }

        /// <summary>Filters the stream to only include add notifications (single and batch).</summary>
        /// <returns>An observable containing only add notifications.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<CacheNotify<T>> WhereAdded()
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Keep(n => n.Action == CacheAction.Added || n.Action == CacheAction.BatchAdded);
        }

        /// <summary>Filters the stream to only include remove notifications (single and batch).</summary>
        /// <returns>An observable containing only remove notifications.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<CacheNotify<T>> WhereRemoved()
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Keep(n => n.Action == CacheAction.Removed || n.Action == CacheAction.BatchRemoved);
        }

        /// <summary>Projects single item notifications to their items.</summary>
        /// <returns>An observable of items from single-item notifications.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<T> SelectItems()
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source
                .Keep(n => n.Item is not null)
                .Map(n => n.Item!);
        }

        /// <summary>Projects all items from notifications (both single and batch) to a flat sequence.</summary>
        /// <returns>An observable of all items from both single-item and batch notifications.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<T> SelectAllItems()
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.FlatMap(n =>
            {
                if (n.Batch is not null)
                {
                    var items = new T[n.Batch.Count];
                    Array.Copy(n.Batch.Items, items, n.Batch.Count);
                    return items;
                }

                if (n.Item is not null)
                {
                    return [n.Item];
                }

                return [];
            });
        }

        /// <summary>Subscribes to added items only from the stream.</summary>
        /// <returns>An observable of added items.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<T> OnItemAdded() => source.WhereAdded().SelectAllItems();

        /// <summary>Subscribes to removed items only from the stream.</summary>
        /// <returns>An observable of removed items.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<T> OnItemRemoved() => source.WhereRemoved().SelectAllItems();

        /// <summary>Subscribes to updated items only from the stream.</summary>
        /// <returns>An observable of updated items.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<T> OnItemUpdated() => source.WhereAction(CacheAction.Updated).SelectItems();

        /// <summary>Subscribes to moved items only from the stream.</summary>
        /// <returns>An observable of move information tuples (Item, OldIndex, NewIndex).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<(T Item, int OldIndex, int NewIndex)> OnItemMoved()
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source
                .WhereAction(CacheAction.Moved)
                .Keep(n => n.Item is not null)
                .Map(n => (n.Item!, n.PreviousIndex, n.CurrentIndex));
        }

        /// <summary>Subscribes to clear notifications from the stream.</summary>
        /// <returns>An observable that emits when the collection is cleared.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<CacheNotify<T>> OnCleared() => source.WhereAction(CacheAction.Cleared);

        /// <summary>Buffers notifications over a time period and flattens batch items for efficient processing.</summary>
        /// <param name="bufferTime">The time span over which to buffer notifications.</param>
        /// <returns>An observable of buffered notification lists.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<IList<CacheNotify<T>>> BufferNotifications(TimeSpan bufferTime)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source
                .Buffer(bufferTime)
                .Keep(b => b.Count > 0);
        }

        /// <summary>Throttles the stream to reduce notification frequency.</summary>
        /// <param name="throttleTime">The minimum time interval between notifications.</param>
        /// <returns>An observable with throttled notifications.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<CacheNotify<T>> ThrottleNotifications(TimeSpan throttleTime)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Throttle(throttleTime);
        }

        /// <summary>Observes notifications on the specified scheduler.</summary>
        /// <param name="scheduler">The scheduler to observe on.</param>
        /// <returns>An observable that dispatches notifications on the specified scheduler.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<CacheNotify<T>> ObserveOnScheduler(ISequencer scheduler)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (scheduler is null)
            {
                throw new ArgumentNullException(nameof(scheduler));
            }

            return source.ObserveOn(scheduler);
        }

        /// <summary>Transforms notifications using a selector function.</summary>
        /// <typeparam name="TResult">The type of elements in the result.</typeparam>
        /// <param name="selector">A transform function to apply to each item.</param>
        /// <returns>An observable of transformed items.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<TResult> TransformItems<TResult>(Func<T, TResult> selector)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (selector is null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            return source.SelectAllItems().Map(selector);
        }

        /// <summary>Filters items in notifications using a predicate.</summary>
        /// <param name="predicate">A function to test each item for a condition.</param>
        /// <returns>An observable of items that satisfy the predicate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<T> FilterItems(Func<T, bool> predicate)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicate is null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return source.SelectAllItems().Keep(predicate);
        }

        /// <summary>Disposes batch resources after processing.</summary>
        /// <returns>An observable that automatically disposes batch resources.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<CacheNotify<T>> AutoDisposeBatches()
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Tap(n => n.Batch?.Dispose());
        }

        /// <summary>Counts items by action type in a stream.</summary>
        /// <returns>An observable of tuples containing (Action, Count).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<(CacheAction Action, int Count)> CountByAction()
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Map(n =>
            {
                var count = n.Batch?.Count ?? (n.Item is not null ? 1 : 0);
                return (n.Action, count);
            });
        }

        /// <summary>Converts a stream of <see cref="CacheNotify{T}"/> to a stream of <see cref="ChangeSet{T}"/>.</summary>
        /// <returns>An observable of change sets.</returns>
        /// <remarks>
        /// This method bridges the gap between the Stream-based notifications and ChangeSet-based processing.
        /// Batch operations are expanded into individual changes within the change set.
        /// Clear operations emit a change set with individual Remove changes for each cleared item (consistent with DynamicData).
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<ChangeSet<T>> ToChangeSets()
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Map(notification =>
            {
                if (notification.Batch is { Count: > 0 })
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

                        if (reason == ChangeReason.Add)
                        {
                            changes[i] = Change<T>.CreateAdd(item, startIndex + i);
                        }
                        else if (reason == ChangeReason.Remove)
                        {
                            changes[i] = Change<T>.CreateRemove(item, -1);
                        }
                        else
                        {
                            changes[i] = Change<T>.CreateRefresh(item, startIndex + i);
                        }
                    }

                    return new ChangeSet<T>(changes);
                }

                if (notification.Action == CacheAction.Cleared)
                {
                    return ChangeSet<T>.Empty;
                }

                var change = notification.ToChange();
                if (change.HasValue)
                {
                    return new ChangeSet<T>(change.Value);
                }

                return ChangeSet<T>.Empty;
            }).Where(cs => cs.Count > 0);
        }
    }
}
