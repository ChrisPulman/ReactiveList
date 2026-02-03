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
/// Provides a filtered, read-only view over a <see cref="IReactiveList{T}"/> that automatically
/// updates when the source list changes.
/// </summary>
/// <typeparam name="T">The type of elements in the view.</typeparam>
public sealed class FilteredReactiveView<T> : IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged, IDisposable
    where T : notnull
{
    private readonly IReactiveList<T> _source;
    private readonly Func<T, bool> _filter;
    private readonly ObservableCollection<T> _filteredItems;
    private readonly CompositeDisposable _disposables = [];
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FilteredReactiveView{T}"/> class.
    /// </summary>
    /// <param name="source">The source reactive list to filter.</param>
    /// <param name="filter">The filter predicate.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    public FilteredReactiveView(
        IReactiveList<T> source,
        Func<T, bool> filter,
        IScheduler scheduler,
        TimeSpan throttle)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));

        _filteredItems = [];
        Items = new ReadOnlyObservableCollection<T>(_filteredItems);

        // Initialize with current items
        RebuildView();

        // Subscribe to changes using Stream with ToChangeSets()
        var subscription = _source.Stream
            .ToChangeSets()
            .Throttle(throttle)
            .ObserveOn(scheduler)
            .Subscribe(OnSourceChanged);

        _disposables.Add(subscription);

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
    /// Forces a rebuild of the filtered view from the source.
    /// </summary>
    public void Refresh()
    {
        lock (_lock)
        {
            RebuildView();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
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
                if (_filter(change.Current))
                {
                    _filteredItems.Add(change.Current);
                }

                break;

            case ChangeReason.Remove:
                _filteredItems.Remove(change.Current);
                break;

            case ChangeReason.Update:
                var wasIncluded = change.Previous != null && _filteredItems.Contains(change.Previous);
                var shouldInclude = _filter(change.Current);

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
            if (_filter(item))
            {
                _filteredItems.Add(item);
            }
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
