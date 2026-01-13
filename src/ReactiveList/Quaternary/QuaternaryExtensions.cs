// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace CP.Reactive;

/// <summary>
/// Provides extension methods for creating reactive views over quaternary collections with optional filtering and
/// throttling support.
/// </summary>
/// <remarks>These extension methods enable the creation of reactive views that reflect changes in the underlying
/// quaternary collections. The views can be filtered and updated at a specified throttling interval to balance
/// responsiveness and performance. All methods are thread-safe and return a snapshot of the collection at the time of
/// view creation.</remarks>
public static class QuaternaryExtensions
{
    /// <summary>
    /// Creates a reactive view of the specified quaternary list that updates in response to changes, applying the given
    /// filter and throttling updates as specified.
    /// </summary>
    /// <remarks>The returned view is updated in a thread-safe manner and only includes items that satisfy the
    /// specified filter. Updates are throttled to avoid excessive notifications when the source list changes
    /// rapidly.</remarks>
    /// <typeparam name="T">The type of elements contained in the quaternary list.</typeparam>
    /// <param name="list">The source quaternary list to observe for changes.</param>
    /// <param name="filter">A function that determines whether an element should be included in the view. Only elements for which this
    /// function returns <see langword="true"/> are included.</param>
    /// <param name="scheduler">The scheduler used to manage update notifications for the view.</param>
    /// <param name="throttleMs">The minimum time interval, in milliseconds, to wait before propagating updates to the view. Defaults to 50
    /// milliseconds.</param>
    /// <returns>A <see cref="ReactiveView{T}"/> that reflects the filtered contents of the source list and updates reactively as
    /// the list changes.</returns>
    public static ReactiveView<T> CreateView<T>(this QuaternaryList<T> list, Func<T, bool> filter, IScheduler scheduler, int throttleMs = 50)
        where T : notnull
    {
        if (list is null)
        {
            throw new ArgumentNullException(nameof(list));
        }

        // Thread-safe snapshot
        var snapshot = list.ToList();
        return new ReactiveView<T>(list.Stream, snapshot, filter, TimeSpan.FromMilliseconds(throttleMs), scheduler);
    }

    /// <summary>
    /// Creates a reactive view of the dictionary that emits filtered key-value pairs as the dictionary changes.
    /// </summary>
    /// <remarks>The returned view reflects changes to the source dictionary in real time, subject to the
    /// specified filter and throttle interval. Use this method to create dynamic, filtered projections of the
    /// dictionary that update automatically as the underlying data changes.</remarks>
    /// <typeparam name="TKey">The type of keys in the dictionary. Must not be null.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="dict">The source dictionary to observe for changes.</param>
    /// <param name="filter">A predicate used to filter which key-value pairs are included in the view. Only pairs for which the predicate
    /// returns <see langword="true"/> are included.</param>
    /// <param name="scheduler">The scheduler used to dispatch notifications of changes to the view.</param>
    /// <param name="throttleMs">The minimum time, in milliseconds, to wait before emitting updates after a change. Defaults to 50 milliseconds.</param>
    /// <returns>A <see cref="ReactiveView{KeyValuePair{TKey, TValue}}"/> that provides a filtered, observable view of the dictionary's
    /// contents.</returns>
    public static ReactiveView<KeyValuePair<TKey, TValue>> CreateView<TKey, TValue>(this QuaternaryDictionary<TKey, TValue> dict, Func<KeyValuePair<TKey, TValue>, bool> filter, IScheduler scheduler, int throttleMs = 50)
        where TKey : notnull
    {
        if (dict is null)
        {
            throw new ArgumentNullException(nameof(dict));
        }

        // Thread-safe snapshot
        var snapshot = dict.ToList();
        return new ReactiveView<KeyValuePair<TKey, TValue>>(dict.Stream, snapshot, filter, TimeSpan.FromMilliseconds(throttleMs), scheduler);
    }
}
#endif
