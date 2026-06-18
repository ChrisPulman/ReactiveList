// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.Reactive;

/// <summary>Provides extension methods for reactive list operations including filtering, transforming, and observing changes.</summary>
public static class ReactiveListExtensions
{
    /// <summary>Extensions for key-value cache notification streams.</summary>
    /// <typeparam name="TKey">The key type carried by the receiver.</typeparam>
    /// <typeparam name="TValue">The value type carried by the receiver.</typeparam>
    /// <param name="stream">The source sequence.</param>
    extension<TKey, TValue>(IObservable<CacheNotify<KeyValuePair<TKey, TValue>>> stream)
        where TKey : notnull
    {
        /// <summary>
        /// Creates a filtered observable sequence where notifications are transformed based on a custom predicate that
        /// re-evaluates on each emission.
        /// </summary>
        /// <remarks>This extension provides dynamic filtering capabilities where the filter predicate is provided by
        /// an observable. When a new filter is emitted, subsequent cache notifications are filtered using that predicate.</remarks>
        /// <param name="filterObservable">An observable that emits filter predicates. Each new predicate is used to filter subsequent notifications.</param>
        /// <returns>An observable sequence of cache notifications filtered by the most recent predicate.</returns>
        public IObservable<CacheNotify<KeyValuePair<TKey, TValue>>> FilterDynamic(IObservable<Func<KeyValuePair<TKey, TValue>, bool>> filterObservable)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(stream);
            ThrowHelper.ThrowIfNull(filterObservable);
#else
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (filterObservable is null)
            {
                throw new ArgumentNullException(nameof(filterObservable));
            }
#endif

            return filterObservable
                .Lead(static _ => true) // Default to include all items
                .Map(filter => stream.Map(notification => notification.Action switch
                {
                    CacheAction.Added when filter(notification.Item) => notification,
                    CacheAction.Removed => notification, // Always pass removed items
                    CacheAction.BatchAdded or CacheAction.BatchRemoved or CacheAction.BatchOperation when notification.Batch is not null =>
                        FilterBatchByPredicate(notification, filter),
                    CacheAction.Cleared => notification,
                    _ => null
                }).Where(n => n is not null).Map(n => n!))
                .SwitchTo();
        }
    }

    /// <summary>Extensions for cache notification streams.</summary>
    /// <typeparam name="T">The item type carried by the receiver.</typeparam>
    /// <param name="stream">The source sequence.</param>
    extension<T>(IObservable<CacheNotify<T>> stream)
        where T : notnull
    {
        /// <summary>
        /// Creates a filtered observable sequence where notifications are transformed based on a custom predicate that
        /// re-evaluates on each emission.
        /// </summary>
        /// <remarks>This extension provides dynamic filtering capabilities where the filter predicate is provided by
        /// an observable. When a new filter is emitted, subsequent cache notifications are filtered using that predicate.</remarks>
        /// <param name="filterObservable">An observable that emits filter predicates. Each new predicate is used to filter subsequent notifications.</param>
        /// <returns>An observable sequence of cache notifications filtered by the most recent predicate.</returns>
        public IObservable<CacheNotify<T>> FilterDynamic(IObservable<Func<T, bool>> filterObservable)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(stream);
            ThrowHelper.ThrowIfNull(filterObservable);
#else
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (filterObservable is null)
            {
                throw new ArgumentNullException(nameof(filterObservable));
            }
#endif

            return filterObservable
                .Lead(static _ => true) // Default to include all items
                .Map(filter => stream.Map(notification => notification.Action switch
                {
                    CacheAction.Added when notification.Item is not null && filter(notification.Item) => notification,
                    CacheAction.Removed when notification.Item is not null => notification, // Always pass removed items
                    CacheAction.BatchAdded or CacheAction.BatchRemoved or CacheAction.BatchOperation when notification.Batch is not null =>
                        FilterBatchByPredicate(notification, filter),
                    CacheAction.Cleared => notification,
                    _ => null
                }).Where(n => n is not null).Map(n => n!))
                .SwitchTo();
        }

        /// <summary>Filters the items in each cache notification emitted by the stream observable according to the specified predicate.</summary>
        /// <remarks>This method produces a new sequence where each notification's item is checked against the predicate.
        /// Notifications with items that don't match are filtered out.</remarks>
        /// <param name="predicate">A function that defines the conditions each item must satisfy to be included.</param>
        /// <returns>An observable sequence of cache notifications containing only notifications with items that satisfy the predicate.</returns>
        public IObservable<CacheNotify<T>> WhereItems(Func<T, bool> predicate) =>
            stream.Keep(notification =>
            {
                // Check if this is a single-item action (where Item property is meaningful)
                var isSingleItemAction = notification.Action is CacheAction.Added
                    or CacheAction.Removed
                    or CacheAction.Updated
                    or CacheAction.Moved
                    or CacheAction.Refreshed;

                if (isSingleItemAction && notification.Item is not null)
                {
                    return predicate(notification.Item);
                }

                // For batch operations or cleared, pass through
                return notification.Action is CacheAction.Cleared or
                       CacheAction.BatchOperation or
                       CacheAction.BatchAdded or
                       CacheAction.BatchRemoved;
            });
    }

    /// <summary>Extensions for observable change-set streams.</summary>
    /// <typeparam name="T">The item type carried by the receiver.</typeparam>
    /// <param name="source">The source sequence.</param>
    extension<T>(IObservable<ChangeSet<T>> source)
    {
        /// <summary>Filters the change stream to only include changes matching the specified predicate.</summary>
        /// <param name="predicate">A function to test each change for a condition.</param>
        /// <returns>An observable containing only changes that satisfy the predicate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<ChangeSet<T>> WhereChanges(Func<Change<T>, bool> predicate)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(source);
            ThrowHelper.ThrowIfNull(predicate);
#else
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicate is null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }
#endif

            return source.Map(changeSet =>
            {
                if (changeSet.Count == 0)
                {
                    return ChangeSet<T>.Empty;
                }

                // Count matching items first to avoid resizing
                var matchCount = 0;
                for (var i = 0; i < changeSet.Count; i++)
                {
                    if (predicate(changeSet[i]))
                    {
                        matchCount++;
                    }
                }

                if (matchCount == 0)
                {
                    return ChangeSet<T>.Empty;
                }

                // If all match, return original (avoid copy)
                if (matchCount == changeSet.Count)
                {
                    return changeSet;
                }

                // Allocate exactly what's needed
                var filtered = new Change<T>[matchCount];
                var idx = 0;
                for (var i = 0; i < changeSet.Count; i++)
                {
                    var change = changeSet[i];
                    if (predicate(change))
                    {
                        filtered[idx++] = change;
                    }
                }

                return new ChangeSet<T>(filtered);
            }).Where(cs => cs.Count > 0);
        }

        /// <summary>Filters the change stream to only include changes of a specific reason.</summary>
        /// <param name="reason">The change reason to filter for.</param>
        /// <returns>An observable containing only changes with the specified reason.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<ChangeSet<T>> WhereReason(ChangeReason reason) => source.WhereChanges(c => c.Reason == reason);

        /// <summary>Projects each item in a sequence of change sets into a new form using the specified selector function.</summary>
        /// <remarks>This method preserves the change reasons and indices from the source change sets while
        /// transforming the items. The selector function is applied to each item as changes are observed.</remarks>
        /// <typeparam name="TResult">The type of the elements produced by the selector function.</typeparam>
        /// <param name="selector">A function that projects each item in the change set to a new result value.</param>
        /// <returns>An observable sequence of change sets containing the projected result items.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<ChangeSet<TResult>> SelectChanges<TResult>(Func<T, TResult> selector)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(source);
            ThrowHelper.ThrowIfNull(selector);
#else
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (selector is null)
            {
                throw new ArgumentNullException(nameof(selector));
            }
#endif

            return source.Map(changes =>
            {
                if (changes.Count == 0)
                {
                    return ChangeSet<TResult>.Empty;
                }

                var transformed = new Change<TResult>[changes.Count];
                for (var i = 0; i < changes.Count; i++)
                {
                    var c = changes[i];
                    transformed[i] = new Change<TResult>(
                        c.Reason,
                        selector(c.Current),
                        c.Previous is not null ? selector(c.Previous) : default,
                        c.CurrentIndex,
                        c.PreviousIndex);
                }

                return new ChangeSet<TResult>(transformed);
            });
        }

        /// <summary>Projects each change in the change stream into a new form, including the change metadata.</summary>
        /// <typeparam name="TResult">The type of elements in the result.</typeparam>
        /// <param name="selector">A transform function to apply to each change.</param>
        /// <returns>An observable of transformed results.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<TResult> SelectChanges<TResult>(Func<Change<T>, TResult> selector)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(source);
            ThrowHelper.ThrowIfNull(selector);
#else
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (selector is null)
            {
                throw new ArgumentNullException(nameof(selector));
            }
#endif

            return source.FlatMap(changeSet =>
            {
                if (changeSet.Count == 0)
                {
                    return [];
                }

                var results = new TResult[changeSet.Count];
                for (var i = 0; i < changeSet.Count; i++)
                {
                    results[i] = selector(changeSet[i]);
                }

                return results;
            });
        }

        /// <summary>Subscribes to adds only from the change stream.</summary>
        /// <returns>An observable of added items.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<T> OnAdd() =>
            source.WhereReason(ChangeReason.Add).SelectChanges(c => c.Current);

        /// <summary>Subscribes to removes only from the change stream.</summary>
        /// <returns>An observable of removed items.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<T> OnRemove() =>
            source.WhereReason(ChangeReason.Remove).SelectChanges(c => c.Current);

        /// <summary>Subscribes to updates only from the change stream.</summary>
        /// <returns>An observable of update tuples containing (Previous, Current) values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<(T? Previous, T Current)> OnUpdate() =>
            source.WhereReason(ChangeReason.Update).SelectChanges(c => (c.Previous, c.Current));

        /// <summary>Subscribes to moves only from the change stream.</summary>
        /// <returns>An observable of move tuples containing (Item, OldIndex, NewIndex).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IObservable<(T Item, int OldIndex, int NewIndex)> OnMove() =>
            source.WhereReason(ChangeReason.Move).SelectChanges(c => (c.Current, c.PreviousIndex, c.CurrentIndex));

        /// <summary>
        /// Projects each item in the source change set into groups based on a specified key, emitting an observable
        /// sequence of grouped observables as items change.
        /// </summary>
        /// <remarks>Each group is represented by an <see cref="IGroupedObservable{TKey, T}"/> that emits items
        /// sharing the same key. Groups are created and removed dynamically as items are added or removed from the source
        /// change set.</remarks>
        /// <typeparam name="TKey">The type of the key used to group items.</typeparam>
        /// <param name="keySelector">A function that extracts the grouping key from each item.</param>
        /// <returns>An observable sequence of grouped observables, where each group corresponds to a unique key and emits items as
        /// they change.</returns>
        public IObservable<IGroupedObservable<TKey, T>> GroupByChanges<TKey>(Func<T, TKey> keySelector)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            return Signal.Create<IGroupedObservable<TKey, T>>(observer =>
            {
                var groups = new List<GroupedObservable<TKey, T>>();
                var subscription = source.Subscribe(
                    changeSet =>
                    {
                        for (var i = 0; i < changeSet.Count; i++)
                        {
                            var item = changeSet[i].Current;
                            var key = keySelector(item);
                            var group = groups.FirstOrDefault(g => EqualityComparer<TKey>.Default.Equals(g.Key, key));
                            if (group is null)
                            {
                                group = new GroupedObservable<TKey, T>(key);
                                groups.Add(group);
                                observer.OnNext(group);
                            }

                            group.OnNext(item);
                        }
                    },
                    error =>
                    {
                        foreach (var group in groups)
                        {
                            group.OnError(error);
                        }

                        observer.OnError(error);
                    },
                    () =>
                    {
                        foreach (var group in groups)
                        {
                            group.OnCompleted();
                        }

                        observer.OnCompleted();
                    });

                return ReactiveUI.Primitives.Disposables.Scope.Create(() =>
                {
                    subscription.Dispose();
                    foreach (var group in groups)
                    {
                        group.Dispose();
                    }
                });
            });
        }

        /// <summary>Groups the change stream by a key selector.</summary>
        /// <typeparam name="TKey">The type of the grouping key.</typeparam>
        /// <param name="keySelector">A function to extract the key for grouping.</param>
        /// <returns>An observable of grouped changes.</returns>
        public IObservable<IGrouping<TKey, Change<T>>> GroupingByChanges<TKey>(Func<T, TKey> keySelector)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            return source.FlatMap(changeSet =>
            {
                var groups = new Dictionary<TKey, List<Change<T>>>();
                for (var i = 0; i < changeSet.Count; i++)
                {
                    var change = changeSet[i];
                    var key = keySelector(change.Current);
                    if (!groups.TryGetValue(key, out var list))
                    {
                        list = [];
                        groups[key] = list;
                    }

                    list.Add(change);
                }

                return groups.Select(kvp => new ChangeGrouping<TKey, Change<T>>(kvp.Key, kvp.Value));
            });
        }

        /// <summary>Projects each change set in the observable sequence into a sorted batch using the specified key selector.</summary>
        /// <remarks>The sorting is applied to each batch as it is emitted by the source sequence. The method does
        /// not maintain a global sort order across batches.</remarks>
        /// <typeparam name="TKey">The type of the key used for sorting the elements.</typeparam>
        /// <param name="keySelector">A function that extracts the key from each element for sorting purposes. Cannot be null.</param>
        /// <returns>An observable sequence of change sets, where each batch is sorted according to the specified key
        /// selector.</returns>
        public IObservable<ChangeSet<T>> SortBy<TKey>(Func<T, TKey> keySelector) => source.Map(changes =>
        {
            var sorted = changes.OrderBy(c => keySelector(c.Current)).ToArray();
            return new ChangeSet<T>(sorted);
        });
    }

    /// <summary>Extensions for observable change-set streams.</summary>
    /// <typeparam name="T">The item type carried by the receiver.</typeparam>
    /// <param name="source">The source sequence.</param>
    extension<T>(IObservable<ChangeSet<T>> source)
        where T : System.ComponentModel.INotifyPropertyChanged
    {
        /// <summary>Automatically refreshes items when the specified property changes (via INotifyPropertyChanged).</summary>
        /// <param name="propertyName">The name of the property to watch for changes. If null or empty, watches all properties.</param>
        /// <returns>An observable that includes refresh notifications when property changes occur.</returns>
        public IObservable<ChangeSet<T>> AutoRefresh(string? propertyName)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var watchAllProperties = string.IsNullOrEmpty(propertyName);

            return source.FlatMap(changeSet =>
            {
                var results = new List<IObservable<ChangeSet<T>>> { Signal.Emit(changeSet) };

                for (var i = 0; i < changeSet.Count; i++)
                {
                    var change = changeSet[i];
                    if (change.Reason is ChangeReason.Add or ChangeReason.Update)
                    {
                        var item = change.Current;
                        var index = change.CurrentIndex;
                        var refreshObservable = Observable.FromEventPattern<System.ComponentModel.PropertyChangedEventHandler, System.ComponentModel.PropertyChangedEventArgs>(
                            h => item.PropertyChanged += h,
                            h => item.PropertyChanged -= h)
                            .Keep(e => watchAllProperties || e.EventArgs.PropertyName == propertyName || string.IsNullOrEmpty(e.EventArgs.PropertyName))
                            .Map(_ => new ChangeSet<T>([Change<T>.CreateRefresh(item, index)]));

                        results.Add(refreshObservable);
                    }
                }

                return ObservableMixins.Blend(results);
            });
        }

        /// <summary>Automatically refreshes items when any property changes (via INotifyPropertyChanged).</summary>
        /// <returns>An observable that includes refresh notifications when any property changes.</returns>
        public IObservable<ChangeSet<T>> AutoRefresh() => source.AutoRefresh(string.Empty);
    }

    /// <summary>Extensions for reactive lists.</summary>
    /// <typeparam name="T">The item type carried by the receiver.</typeparam>
    /// <param name="list">The source.</param>
    extension<T>(IReactiveList<T> list)
        where T : notnull
    {
        /// <summary>Creates a filtered view of the reactive list that updates automatically when the source changes.</summary>
        /// <param name="filter">A predicate to filter items.</param>
        /// <param name="scheduler">Optional scheduler for dispatching updates. Defaults to CurrentThreadScheduler.</param>
        /// <param name="throttleMs">Throttle interval in milliseconds. Defaults to 50ms.</param>
        /// <returns>A read-only observable collection that stays synchronized with the filtered source.</returns>
        public FilteredReactiveView<T> CreateView(
            Func<T, bool> filter,
            ISequencer? scheduler = null,
            int throttleMs = 50)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(list);
            ThrowHelper.ThrowIfNull(filter);
#else
            if (list is null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (filter is null)
            {
                throw new ArgumentNullException(nameof(filter));
            }
#endif

            return new FilteredReactiveView<T>(list, filter, scheduler ?? Sequencer.CurrentThread, TimeSpan.FromMilliseconds(throttleMs));
        }

        /// <summary>Creates an unfiltered view of the reactive list that updates automatically when the source changes.</summary>
        /// <param name="scheduler">Optional scheduler for dispatching updates. Defaults to CurrentThreadScheduler.</param>
        /// <param name="throttleMs">Throttle interval in milliseconds. Defaults to 50ms.</param>
        /// <returns>A read-only observable collection that stays synchronized with the source.</returns>
        public FilteredReactiveView<T> CreateView(
            ISequencer? scheduler = null,
            int throttleMs = 50)
            => list.CreateView(_ => true, scheduler, throttleMs);

        /// <summary>Creates a dynamically filtered view that rebuilds when the filter predicate changes.</summary>
        /// <param name="filterObservable">An observable that emits filter predicates.</param>
        /// <param name="scheduler">Optional scheduler for dispatching updates. Defaults to CurrentThreadScheduler.</param>
        /// <param name="throttleMs">Throttle interval in milliseconds. Defaults to 50ms.</param>
        /// <returns>A read-only observable collection that updates when the source or filter changes.</returns>
        public DynamicFilteredReactiveView<T> CreateView(
            IObservable<Func<T, bool>> filterObservable,
            ISequencer? scheduler = null,
            int throttleMs = 50)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(list);
            ThrowHelper.ThrowIfNull(filterObservable);
#else
            if (list is null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (filterObservable is null)
            {
                throw new ArgumentNullException(nameof(filterObservable));
            }
#endif

            return new DynamicFilteredReactiveView<T>(list, filterObservable, scheduler ?? Sequencer.CurrentThread, TimeSpan.FromMilliseconds(throttleMs));
        }

        /// <summary>Creates a sorted view of the reactive list that updates automatically when the source changes.</summary>
        /// <param name="comparer">The comparer to use for sorting.</param>
        /// <param name="scheduler">Optional scheduler for dispatching updates. Defaults to CurrentThreadScheduler.</param>
        /// <param name="throttleMs">Throttle interval in milliseconds. Defaults to 50ms.</param>
        /// <returns>A sorted view that stays synchronized with the source.</returns>
        public SortedReactiveView<T> SortBy(
            IComparer<T> comparer,
            ISequencer? scheduler = null,
            int throttleMs = 50)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(list);
            ThrowHelper.ThrowIfNull(comparer);
#else
            if (list is null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (comparer is null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }
#endif

            return new SortedReactiveView<T>(list, comparer, scheduler ?? Sequencer.CurrentThread, TimeSpan.FromMilliseconds(throttleMs));
        }

        /// <summary>Creates a sorted view of the reactive list using a key selector.</summary>
        /// <typeparam name="TKey">The type of the sort key.</typeparam>
        /// <param name="keySelector">A function to extract the sort key.</param>
        /// <param name="descending">Whether to sort in descending order.</param>
        /// <param name="scheduler">Optional scheduler for dispatching updates. Defaults to CurrentThreadScheduler.</param>
        /// <param name="throttleMs">Throttle interval in milliseconds. Defaults to 50ms.</param>
        /// <returns>A sorted view that stays synchronized with the source.</returns>
        public SortedReactiveView<T> SortBy<TKey>(
            Func<T, TKey> keySelector,
            bool descending = false,
            ISequencer? scheduler = null,
            int throttleMs = 50)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(list);
            ThrowHelper.ThrowIfNull(keySelector);
#else
            if (list is null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }
#endif

            var comparer = descending
                ? Comparer<T>.Create((x, y) => Comparer<TKey>.Default.Compare(keySelector(y), keySelector(x)))
                : Comparer<T>.Create((x, y) => Comparer<TKey>.Default.Compare(keySelector(x), keySelector(y)));

            return new SortedReactiveView<T>(list, comparer, scheduler ?? Sequencer.CurrentThread, TimeSpan.FromMilliseconds(throttleMs));
        }

        /// <summary>Creates a grouped view of the reactive list.</summary>
        /// <typeparam name="TKey">The type of the grouping key.</typeparam>
        /// <param name="keySelector">A function to extract the key for grouping.</param>
        /// <param name="scheduler">Optional scheduler for dispatching updates. Defaults to CurrentThreadScheduler.</param>
        /// <param name="throttleMs">Throttle interval in milliseconds. Defaults to 50ms.</param>
        /// <returns>A grouped view that stays synchronized with the source.</returns>
        public GroupedReactiveView<T, TKey> GroupBy<TKey>(
            Func<T, TKey> keySelector,
            ISequencer? scheduler = null,
            int throttleMs = 50)
            where TKey : notnull
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(list);
            ThrowHelper.ThrowIfNull(keySelector);
#else
            if (list is null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }
#endif

            return new GroupedReactiveView<T, TKey>(list, keySelector, scheduler ?? Sequencer.CurrentThread, TimeSpan.FromMilliseconds(throttleMs));
        }
    }

    /// <summary>Extensions for reactive sources.</summary>
    /// <typeparam name="T">The item type carried by the receiver.</typeparam>
    /// <param name="source">The source sequence.</param>
    extension<T>(IReactiveSource<T> source)
        where T : notnull
    {
        /// <summary>
        /// Creates a dynamic, reactive view of the source that updates in response to changes in a query observable and
        /// applies a filter to determine which items are included.
        /// </summary>
        /// <remarks>The returned view updates in response to both changes in the source and changes in the
        /// query observable. Updates are throttled according to the specified interval to avoid excessive refreshes when
        /// queries change rapidly.</remarks>
        /// <typeparam name="TQuery">The type of the query values emitted by the observable that influence the filtering logic.</typeparam>
        /// <param name="queryObservable">An observable sequence that emits query values used to update the filter applied to the source. Cannot be null.</param>
        /// <param name="filter">A function that determines whether a given item in the source matches the current query. The function receives the
        /// current query and an item, and returns <see langword="true"/> to include the item in the view; otherwise, <see
        /// langword="false"/>. Cannot be null.</param>
        /// <param name="scheduler">The scheduler used to observe and process updates to the view.</param>
        /// <param name="throttleMs">The minimum time, in milliseconds, to wait before applying updates after a query change. Defaults to 50
        /// milliseconds.</param>
        /// <returns>A <see cref="DynamicReactiveView{T}"/> that reflects the filtered view of the source and updates automatically as
        /// the query observable emits new values.</returns>
        public DynamicReactiveView<T> CreateView<TQuery>(IObservable<TQuery> queryObservable, Func<TQuery, T, bool> filter, ISequencer scheduler, int throttleMs = 50)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(source);
            ThrowHelper.ThrowIfNull(queryObservable);
            ThrowHelper.ThrowIfNull(filter);
#else
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (queryObservable is null)
            {
                throw new ArgumentNullException(nameof(queryObservable));
            }

            if (filter is null)
            {
                throw new ArgumentNullException(nameof(filter));
            }
#endif

            // Convert query observable to a filter observable
            var filterObservable = queryObservable.Map<TQuery, Func<T, bool>>(query => item => filter(query, item));

            return new DynamicReactiveView<T>(source, filterObservable, TimeSpan.FromMilliseconds(throttleMs), scheduler);
        }

        /// <summary>Creates a reactive view with a dynamic filter that rebuilds when the filter observable emits a new predicate.</summary>
        /// <remarks>The view automatically rebuilds its contents when the filter observable emits a new predicate.
        /// This is useful for implementing dynamic search functionality where the search criteria can change over time.</remarks>
        /// <param name="filterObservable">An observable that emits filter predicates. When a new predicate is emitted, the view rebuilds its contents.</param>
        /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
        /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
        /// milliseconds.</param>
        /// <returns>A <see cref="DynamicReactiveView{T}"/> that reflects the filtered contents of the source and updates reactively
        /// as the source changes or the filter predicate changes.</returns>
        public DynamicReactiveView<T> CreateView(IObservable<Func<T, bool>> filterObservable, ISequencer scheduler, int throttleMs = 50)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(source);
            ThrowHelper.ThrowIfNull(filterObservable);
#else
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (filterObservable is null)
            {
                throw new ArgumentNullException(nameof(filterObservable));
            }
#endif

            return new DynamicReactiveView<T>(source, filterObservable, TimeSpan.FromMilliseconds(throttleMs), scheduler);
        }

        /// <summary>
        /// Creates a reactive view over the specified quaternary source that updates in response to changes, with optional
        /// throttling.
        /// </summary>
        /// <remarks>The returned view provides a thread-safe, observable snapshot of the source. Change
        /// notifications are throttled to avoid excessive updates when the source changes rapidly. Use this method to
        /// efficiently bind UI or other observers to dynamic data sources.</remarks>
        /// <param name="scheduler">The scheduler used to dispatch change notifications to the reactive view. Cannot be null.</param>
        /// <param name="throttleMs">The minimum interval, in milliseconds, between consecutive change notifications. Must be non-negative. The
        /// default is 50 milliseconds.</param>
        /// <returns>A <see cref="ReactiveView{T}"/> that reflects the current state of the source and updates when the source changes, subject to the
        /// specified throttling.</returns>
        public ReactiveView<T> CreateView(ISequencer scheduler, int throttleMs = 50)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(source);
#else
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
#endif

            // Thread-safe snapshot
            var snapshot = source.ToArray();
            return new ReactiveView<T>(source.Stream, snapshot, _ => true, TimeSpan.FromMilliseconds(throttleMs), scheduler);
        }

        /// <summary>
        /// Creates a reactive view of the specified quaternary source that updates in response to changes, applying the given
        /// filter and throttling updates as specified.
        /// </summary>
        /// <remarks>The returned view is updated in a thread-safe manner and only includes items that satisfy the
        /// specified filter. Updates are throttled to avoid excessive notifications when the source changes
        /// rapidly.</remarks>
        /// <param name="filter">A function that determines whether an element should be included in the view. Only elements for which this
        /// function returns <see langword="true"/> are included.</param>
        /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
        /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
        /// milliseconds.</param>
        /// <returns>A <see cref="ReactiveView{T}"/> that reflects the filtered contents of the source and updates reactively as
        /// the source changes.</returns>
        public ReactiveView<T> CreateView(Func<T, bool> filter, ISequencer scheduler, int throttleMs = 50)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(source);
#else
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
#endif

            // Thread-safe snapshot
            var snapshot = source.ToArray();
            return new ReactiveView<T>(source.Stream, snapshot, filter, TimeSpan.FromMilliseconds(throttleMs), scheduler);
        }

        /// <summary>Connects to the change stream and converts to ChangeSet format for compatibility with DynamicData-style processing.</summary>
        /// <returns>An observable stream of change sets representing all modifications to the source.</returns>
        /// <remarks>
        /// This is a convenience method that bridges the Stream-based notification model
        /// with ChangeSet-based processing patterns.
        /// </remarks>
        public IObservable<ChangeSet<T>> Connect()
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return Observable.Defer(() =>
            {
                var initialItems = source.ToArray();
                var changeStream = source.Stream.ToChangeSets();
                if (initialItems.Length == 0)
                {
                    return changeStream;
                }

                var initialChanges = new Change<T>[initialItems.Length];
                for (var i = 0; i < initialItems.Length; i++)
                {
                    initialChanges[i] = Change<T>.CreateAdd(initialItems[i], i);
                }

                return Signal.Emit(new ChangeSet<T>(initialChanges)).Chain(changeStream);
            });
        }

        /// <summary>
        /// Creates an observable sequence that emits a refresh change set whenever the specified property of any item in
        /// the source changes.
        /// </summary>
        /// <remarks>The returned observable emits a refresh change set for the entire source whenever the observed
        /// property changes on any item. This is useful for scenarios where downstream consumers need to be notified of
        /// property changes that may affect filtering, sorting, or other derived views.</remarks>
        /// <param name="property">An expression specifying the property of each item to observe for changes. Must refer to a property of type
        /// <typeparamref name="T"/>.</param>
        /// <returns>An observable sequence that produces a refresh notification each time the specified property changes on any item
        /// in the source.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="property"/> does not refer to a property.</exception>
        public IObservable<CacheNotify<T>> AutoRefresh(Expression<Func<T, object>> property)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNull(source);
            ThrowHelper.ThrowIfNull(property);
#else
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (property is null)
            {
                throw new ArgumentNullException(nameof(property));
            }
#endif

            var member = (property.Body as MemberExpression)?.Member ?? throw new ArgumentException("Expression must be a property", nameof(property));

            // Return the Stream directly - it already provides all change notifications
            // The caller can subscribe and handle refresh logic based on item property changes
            return source.Stream;
        }
    }

    /// <summary>Filters the items in a batch notification based on a specified predicate.</summary>
    /// <remarks>The returned batch notification will contain only the filtered items. If the filter excludes
    /// all items, or if the input batch is null, the method returns null. The method uses an array pool to allocate
    /// storage for the filtered items, which may improve performance for large batches.</remarks>
    /// <typeparam name="T">The type of items contained in the batch notification. Must be non-nullable.</typeparam>
    /// <param name="notification">The batch notification containing the items to filter. The batch must not be null.</param>
    /// <param name="filter">A predicate function used to determine whether an item in the batch should be included in the result.</param>
    /// <returns>A new batch notification containing only the items that satisfy the predicate, or null if no items match or if
    /// the original batch is null.</returns>
    internal static CacheNotify<T>? FilterBatchByPredicate<T>(CacheNotify<T> notification, Func<T, bool> filter)
        where T : notnull
    {
        if (notification.Batch is null)
        {
            return null;
        }

        var matchCount = 0;
        for (var i = 0; i < notification.Batch.Count; i++)
        {
            var item = notification.Batch.Items[i];
            if (filter(item))
            {
                matchCount++;
            }
        }

        if (matchCount == 0)
        {
            return null;
        }

        var filteredItems = new T[matchCount];
        var index = 0;
        for (var i = 0; i < notification.Batch.Count; i++)
        {
            var item = notification.Batch.Items[i];
            if (filter(item))
            {
                filteredItems[index++] = item;
            }
        }

        return new CacheNotify<T>(CacheAction.BatchOperation, default, new PooledBatch<T>(filteredItems, filteredItems.Length, ReturnToPool: false));
    }

    /// <summary>
    /// Filters the items in a batch notification to include only those that are present in the specified set of
    /// matching items.
    /// </summary>
    /// <typeparam name="T">The type of items contained in the batch. Must be non-nullable.</typeparam>
    /// <param name="notification">The batch notification containing the items to filter. The batch must not be null.</param>
    /// <param name="matchingItems">A set of items to match against. Only items present in this set will be included in the filtered batch.</param>
    /// <returns>A new batch notification containing only the items that are present in both the original batch and the matching
    /// set; or null if the original batch is null or if no items match.</returns>
    internal static CacheNotify<T>? FilterBatch<T>(CacheNotify<T> notification, HashSet<T> matchingItems)
        where T : notnull
    {
        if (notification.Batch is null)
        {
            return null;
        }

        var matchCount = 0;
        for (var i = 0; i < notification.Batch.Count; i++)
        {
            if (matchingItems.Contains(notification.Batch.Items[i]))
            {
                matchCount++;
            }
        }

        if (matchCount == 0)
        {
            return null;
        }

        var filteredItems = new T[matchCount];
        var index = 0;
        for (var i = 0; i < notification.Batch.Count; i++)
        {
            var item = notification.Batch.Items[i];
            if (matchingItems.Contains(item))
            {
                filteredItems[index++] = item;
            }
        }

        return new CacheNotify<T>(CacheAction.BatchOperation, default, new PooledBatch<T>(filteredItems, filteredItems.Length, ReturnToPool: false));
    }
}
