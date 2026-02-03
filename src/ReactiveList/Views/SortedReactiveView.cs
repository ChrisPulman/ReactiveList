// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using CP.Reactive.Collections;
using CP.Reactive.Core;

namespace CP.Reactive.Views;

/// <summary>
/// Provides a sorted, read-only view over a <see cref="IReactiveList{T}"/> that automatically
/// updates when the source list changes.
/// </summary>
/// <typeparam name="T">The type of elements in the view.</typeparam>
public sealed class SortedReactiveView<T> : IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged, IDisposable
    where T : notnull
{
    private readonly IReactiveList<T> _source;
    private readonly IComparer<T> _comparer;
    private readonly ObservableCollection<T> _sortedItems;
    private readonly CompositeDisposable _disposables = [];
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new object();
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="SortedReactiveView{T}"/> class.
    /// </summary>
    /// <param name="source">The source reactive list to sort.</param>
    /// <param name="comparer">The comparer for sorting items.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    public SortedReactiveView(
        IReactiveList<T> source,
        IComparer<T> comparer,
        IScheduler scheduler,
#if NET8_0_OR_GREATER
        in TimeSpan throttle)
#else
        TimeSpan throttle)
#endif
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));

        _sortedItems = [];
        Items = new ReadOnlyObservableCollection<T>(_sortedItems);

        // Initialize with current items sorted
        RebuildView();

        // Subscribe to changes using Stream with ToChangeSets()
        var subscription = _source.Stream
            .ToChangeSets()
            .Throttle(throttle)
            .ObserveOn(scheduler)
            .Subscribe(OnSourceChanged);

        _disposables.Add(subscription);

        // Forward collection changed events
        _sortedItems.CollectionChanged += (s, e) => CollectionChanged?.Invoke(this, e);
    }

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the number of items in the sorted view.
    /// </summary>
    public int Count => _sortedItems.Count;

    /// <summary>
    /// Gets the underlying read-only observable collection for UI binding.
    /// </summary>
    public ReadOnlyObservableCollection<T> Items { get; }

    /// <summary>
    /// Gets the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to get.</param>
    /// <returns>The item at the specified index.</returns>
    public T this[int index] => _sortedItems[index];

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _sortedItems.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Forces a rebuild of the sorted view from the source.
    /// </summary>
    public void Refresh()
    {
        lock (_lock)
        {
            RebuildView();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _disposables.Dispose();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnSourceChanged(ChangeSet<T> changes)
    {
        lock (_lock)
        {
            var needsRebuild = false;

            for (var i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        InsertSorted(change.Current);
                        break;

                    case ChangeReason.Remove:
                        _sortedItems.Remove(change.Current);
                        break;

                    case ChangeReason.Update:
                        // Remove old, insert new in sorted position
                        if (change.Previous != null)
                        {
                            _sortedItems.Remove(change.Previous);
                        }

                        InsertSorted(change.Current);
                        break;

                    case ChangeReason.Clear:
                        _sortedItems.Clear();
                        break;

                    case ChangeReason.Move:
                    case ChangeReason.Refresh:
                        // Rebuild for move/refresh as sort order may have changed
                        needsRebuild = true;
                        break;
                }
            }

            if (needsRebuild)
            {
                RebuildView();
            }
        }

        OnPropertyChanged(nameof(Count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InsertSorted(T item)
    {
        // Binary search for correct insertion position
        var index = BinarySearch(item);
        if (index < 0)
        {
            index = ~index;
        }

        _sortedItems.Insert(index, item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int BinarySearch(T item)
    {
        var lo = 0;
        var hi = _sortedItems.Count - 1;

        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            var comparison = _comparer.Compare(_sortedItems[mid], item);

            if (comparison == 0)
            {
                return mid;
            }

            if (comparison < 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return ~lo;
    }

    private void RebuildView()
    {
        _sortedItems.Clear();
        var sorted = _source.ToList();
        sorted.Sort(_comparer);
        foreach (var item in sorted)
        {
            _sortedItems.Add(item);
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
