// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Internal;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;

namespace CP.Reactive.Views;

/// <summary>
/// Provides a dynamically filtered view over a <see cref="IReactiveList{T}"/> that automatically
/// updates when the source list changes or the filter predicate changes.
/// </summary>
/// <typeparam name="T">The type of elements in the view.</typeparam>
public sealed class DynamicFilteredReactiveView<T> : IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged, IReactiveView<DynamicFilteredReactiveView<T>, T>, IDisposable
where T : notnull
{
    private readonly IReactiveList<T> _source;

    private readonly ObservableCollection<T> _filteredItems;

    private readonly MultipleDisposable _disposables = new();

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private Func<T, bool> _currentFilter;

    /// <summary>Initializes a new instance of the <see cref="DynamicFilteredReactiveView{T}"/> class.</summary>
    /// <param name="source">The source reactive list to filter.</param>
    /// <param name="filterObservable">An observable that emits filter predicates.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    public DynamicFilteredReactiveView(
        IReactiveList<T> source,
        IObservable<Func<T, bool>> filterObservable,
        ISequencer scheduler,
        TimeSpan throttle)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
#if NET8_0_OR_GREATER
        CP.Reactive.Internal.ThrowHelper.ThrowIfNull(filterObservable);
#else
        if (filterObservable is null)
        {
            throw new ArgumentNullException(nameof(filterObservable));
        }
#endif

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

        // Subscribe to source changes using Stream with ToChangeSets()
        var sourceSubscription = _source.Stream
            .ToChangeSets()
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

    /// <summary>Gets the number of items in the filtered view.</summary>
    public int Count => _filteredItems.Count;

    /// <summary>Gets the underlying read-only observable collection for UI binding.</summary>
    public ReadOnlyObservableCollection<T> Items { get; }

    /// <summary>Gets the item at the specified index.</summary>
    /// <param name="index">The zero-based index of the item to get.</param>
    /// <returns>The item at the specified index.</returns>
    public T this[int index] => _filteredItems[index];

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _filteredItems.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Forces a rebuild of the filtered view from the source using the current filter.</summary>
    public void Refresh()
    {
        lock (_lock)
        {
            RebuildView();
        }

        OnPropertyChanged(nameof(Count));
    }

    /// <summary>Assigns the current collection of items to a property using the specified setter action.</summary>
    /// <remarks>This method is typically used to bind the internal collection to an external property, such
    /// as a view model property, in a reactive UI pattern.</remarks>
    /// <param name="propertySetter">An action that sets a property to the current read-only observable collection of items. Cannot be null.</param>
    /// <returns>The current instance of <see cref="DynamicFilteredReactiveView{T}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public DynamicFilteredReactiveView<T> ToProperty(Action<ReadOnlyObservableCollection<T>> propertySetter)
    {
#if NET8_0_OR_GREATER
        CP.Reactive.Internal.ThrowHelper.ThrowIfNull(propertySetter);
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
    /// <returns>The current <see cref="DynamicFilteredReactiveView{T}"/> instance.</returns>
    public DynamicFilteredReactiveView<T> ToProperty(out ReadOnlyObservableCollection<T> collection)
    {
        collection = Items;
        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
    }

    /// <summary>Handles filter change notifications.</summary>
    /// <param name="newFilter">The newFilter value.</param>
    private void OnFilterChanged(Func<T, bool> newFilter)
    {
        lock (_lock)
        {
            _currentFilter = newFilter ?? (_ => true);
            RebuildView();
        }

        OnPropertyChanged(nameof(Count));
    }

    /// <summary>Handles source change notifications.</summary>
    /// <param name="changes">The changes value.</param>
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

    /// <summary>Processes a source collection change.</summary>
    /// <param name="change">The change value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessChange(Change<T> change)
    {
        switch (change.Reason)
        {
            case ChangeReason.Add:
                {
                    if (_currentFilter(change.Current))
                    {
                        _filteredItems.Add(change.Current);
                    }

                    break;
                }

            case ChangeReason.Remove:
                {
                    _filteredItems.Remove(change.Current);
                    break;
                }

            case ChangeReason.Update:
                {
                    var wasIncluded = change.Previous is not null && _filteredItems.Contains(change.Previous);
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
                }

            case ChangeReason.Clear:
                {
                    _filteredItems.Clear();
                    break;
                }

            case ChangeReason.Move or ChangeReason.Refresh:
                {
                    // For move and refresh, rebuild the view to maintain correct order
                    RebuildView();
                    break;
                }
        }
    }

    /// <summary>Rebuilds the view from the current source state.</summary>
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

    /// <summary>Handles property change notifications.</summary>
    /// <param name="propertyName">The propertyName value.</param>
    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
