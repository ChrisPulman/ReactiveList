// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.Reactive.Views;

/// <summary>Provides a sorted, read-only view over a <see cref="IReactiveList{T}"/> that automatically updates when the source list changes.</summary>
/// <typeparam name="T">The type of elements in the view.</typeparam>
public sealed class SortedReactiveView<T> : IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged, IReactiveView<SortedReactiveView<T>, T>, IDisposable
where T : notnull
{
    private readonly IReactiveList<T> _source;

    private readonly IComparer<T> _comparer;

    private readonly ObservableCollection<T> _sortedItems;

    private readonly MultipleDisposable _disposables = [];

    private readonly Lock _lock = new();

    /// <summary>Initializes a new instance of the <see cref="SortedReactiveView{T}"/> class.</summary>
    /// <param name="source">The source reactive list to sort.</param>
    /// <param name="comparer">The comparer for sorting items.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    public SortedReactiveView(
        IReactiveList<T> source,
        IComparer<T> comparer,
        ISequencer scheduler,
        TimeSpan throttle)
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

    /// <summary>Gets the number of items in the sorted view.</summary>
    public int Count => _sortedItems.Count;

    /// <summary>Gets the underlying read-only observable collection for UI binding.</summary>
    public ReadOnlyObservableCollection<T> Items { get; }

    /// <summary>Gets the item at the specified index.</summary>
    /// <param name="index">The zero-based index of the item to get.</param>
    /// <returns>The item at the specified index.</returns>
    public T this[int index] => _sortedItems[index];

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _sortedItems.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Forces a rebuild of the sorted view from the source.</summary>
    public void Refresh()
    {
        lock (_lock)
        {
            RebuildView();
        }
    }

    /// <summary>Assigns the current collection of items to a property using the specified setter action.</summary>
    /// <remarks>This method is typically used to bind the internal collection to an external property, such
    /// as a view model property, in a reactive UI pattern.</remarks>
    /// <param name="propertySetter">An action that sets a property to the current read-only observable collection of items. Cannot be null.</param>
    /// <returns>The current instance of <see cref="SortedReactiveView{T}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public SortedReactiveView<T> ToProperty(Action<ReadOnlyObservableCollection<T>> propertySetter)
    {
#if NET8_0_OR_GREATER
        ThrowHelper.ThrowIfNull(propertySetter);
#else
        if (propertySetter is null)
        {
            throw new ArgumentNullException(nameof(propertySetter));
        }
#endif
        propertySetter(Items);
        return this;
    }

    /// <summary>Returns the current instance and provides a read-only observable collection of items contained in the view.</summary>
    /// <param name="collection">When this method returns, contains a read-only observable collection of items managed by this view.</param>
    /// <returns>The current <see cref="SortedReactiveView{T}"/> instance.</returns>
    public SortedReactiveView<T> ToProperty(out ReadOnlyObservableCollection<T> collection)
    {
        collection = Items;
        return this;
    }

    /// <inheritdoc/>
    public void Dispose() => _disposables.Dispose();

    /// <summary>Handles source change notifications.</summary>
    /// <param name="changes">The changes value.</param>
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
                        {
                            InsertSorted(change.Current);
                            break;
                        }

                    case ChangeReason.Remove:
                        {
                            _sortedItems.Remove(change.Current);
                            break;
                        }

                    case ChangeReason.Update:
                        {
                            // Remove old, insert new in sorted position
                            if (change.Previous is not null)
                            {
                                _sortedItems.Remove(change.Previous);
                            }

                            InsertSorted(change.Current);
                            break;
                        }

                    case ChangeReason.Clear:
                        {
                            _sortedItems.Clear();
                            break;
                        }

                    case ChangeReason.Move or ChangeReason.Refresh:
                        {
                            // Rebuild for move/refresh as sort order may have changed
                            needsRebuild = true;
                            break;
                        }
                }
            }

            if (needsRebuild)
            {
                RebuildView();
            }
        }

        OnPropertyChanged(nameof(Count));
    }

    /// <summary>Inserts data for the InsertSorted operation.</summary>
    /// <param name="item">The item value.</param>
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

    /// <summary>Searches for the sorted insertion index.</summary>
    /// <param name="item">The item value.</param>
    /// <returns>The matching item index or the bitwise complement of the insertion index.</returns>
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

    /// <summary>Rebuilds the view from the current source state.</summary>
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

    /// <summary>Handles property change notifications.</summary>
    /// <param name="propertyName">The propertyName value.</param>
    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
