// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET6_0_OR_GREATER
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace CP.Reactive;

/// <summary>
/// Provides a dynamically filtered view over a <see cref="IReactiveList{T}"/> that automatically
/// updates when the source list changes or the filter predicate changes.
/// </summary>
/// <typeparam name="T">The type of elements in the view.</typeparam>
public sealed class DynamicFilteredReactiveView<T> : IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged, IDisposable
    where T : notnull
{
    private readonly IReactiveList<T> _source;
    private readonly ObservableCollection<T> _filteredItems;
    private readonly CompositeDisposable _disposables = [];
    private readonly object _lock = new();
    private Func<T, bool> _currentFilter;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicFilteredReactiveView{T}"/> class.
    /// </summary>
    /// <param name="source">The source reactive list to filter.</param>
    /// <param name="filterObservable">An observable that emits filter predicates.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    public DynamicFilteredReactiveView(
        IReactiveList<T> source,
        IObservable<Func<T, bool>> filterObservable,
        IScheduler scheduler,
        TimeSpan throttle)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        ArgumentNullException.ThrowIfNull(filterObservable);

        _currentFilter = _ => true;
        _filteredItems = [];
        Items = new ReadOnlyObservableCollection<T>(_filteredItems);

        // Initialize with current items (no filter initially)
        RebuildView();

        // Subscribe to filter changes
        var filterSubscription = filterObservable
            .Throttle(throttle)
            .ObserveOn(scheduler)
            .Subscribe(OnFilterChanged);

        _disposables.Add(filterSubscription);

        // Subscribe to source changes
        var sourceSubscription = _source.Connect()
            .Throttle(throttle)
            .ObserveOn(scheduler)
            .Subscribe(OnSourceChanged);

        _disposables.Add(sourceSubscription);

        // Forward collection changed events
        _filteredItems.CollectionChanged += (s, e) => CollectionChanged?.Invoke(this, e);
    }

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the number of items in the filtered view.
    /// </summary>
    public int Count => _filteredItems.Count;

    /// <summary>
    /// Gets the underlying read-only observable collection for UI binding.
    /// </summary>
    public ReadOnlyObservableCollection<T> Items { get; }

    /// <summary>
    /// Gets the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to get.</param>
    /// <returns>The item at the specified index.</returns>
    public T this[int index] => _filteredItems[index];

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _filteredItems.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Forces a rebuild of the filtered view from the source using the current filter.
    /// </summary>
    public void Refresh()
    {
        lock (_lock)
        {
            RebuildView();
        }

        OnPropertyChanged(nameof(Count));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
    }

    private void OnFilterChanged(Func<T, bool> newFilter)
    {
        lock (_lock)
        {
            _currentFilter = newFilter ?? (_ => true);
            RebuildView();
        }

        OnPropertyChanged(nameof(Count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnSourceChanged(ChangeSet<T> changes)
    {
        lock (_lock)
        {
            for (var i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                ProcessChange(change);
            }
        }

        OnPropertyChanged(nameof(Count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessChange(Change<T> change)
    {
        switch (change.Reason)
        {
            case ChangeReason.Add:
                if (_currentFilter(change.Current))
                {
                    _filteredItems.Add(change.Current);
                }

                break;

            case ChangeReason.Remove:
                _filteredItems.Remove(change.Current);
                break;

            case ChangeReason.Update:
                var wasIncluded = change.Previous != null && _filteredItems.Contains(change.Previous);
                var shouldInclude = _currentFilter(change.Current);

                if (wasIncluded && !shouldInclude)
                {
                    _filteredItems.Remove(change.Previous!);
                }
                else if (!wasIncluded && shouldInclude)
                {
                    _filteredItems.Add(change.Current);
                }
                else if (wasIncluded && shouldInclude)
                {
                    var idx = _filteredItems.IndexOf(change.Previous!);
                    if (idx >= 0)
                    {
                        _filteredItems[idx] = change.Current;
                    }
                }

                break;

            case ChangeReason.Clear:
                _filteredItems.Clear();
                break;

            case ChangeReason.Move:
            case ChangeReason.Refresh:
                // For move and refresh, rebuild the view to maintain correct order
                RebuildView();
                break;
        }
    }

    private void RebuildView()
    {
        _filteredItems.Clear();
        foreach (var item in _source)
        {
            if (_currentFilter(item))
            {
                _filteredItems.Add(item);
            }
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
#endif
