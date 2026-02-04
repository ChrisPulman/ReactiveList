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
/// Provides a grouped view over a <see cref="IReactiveList{T}"/> that automatically
/// updates when the source list changes.
/// </summary>
/// <typeparam name="T">The type of elements in the view.</typeparam>
/// <typeparam name="TKey">The type of the grouping key.</typeparam>
public sealed class GroupedReactiveView<T, TKey> : IReadOnlyDictionary<TKey, IReadOnlyList<T>>, INotifyCollectionChanged, INotifyPropertyChanged, IReactiveView<GroupedReactiveView<T, TKey>, ReactiveGroup<TKey, T>>, IDisposable
where T : notnull
where TKey : notnull
{
    private readonly IReactiveList<T> _source;
    private readonly Func<T, TKey> _keySelector;
    private readonly Dictionary<TKey, ObservableCollection<T>> _groups = [];
    private readonly ObservableCollection<ReactiveGroup<TKey, T>> _groupCollection = [];
    private readonly CompositeDisposable _disposables = [];
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupedReactiveView{T, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source reactive list to group.</param>
    /// <param name="keySelector">A function to extract the grouping key.</param>
    /// <param name="scheduler">The scheduler for dispatching updates.</param>
    /// <param name="throttle">The throttle duration for updates.</param>
    public GroupedReactiveView(
        IReactiveList<T> source,
        Func<T, TKey> keySelector,
        IScheduler scheduler,
        TimeSpan throttle)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));

        Groups = new ReadOnlyObservableCollection<ReactiveGroup<TKey, T>>(_groupCollection);

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
        _groupCollection.CollectionChanged += (s, e) => CollectionChanged?.Invoke(this, e);
    }

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the number of groups.
    /// </summary>
    public int Count => _groups.Count;

    /// <summary>
    /// Gets the collection of groups for UI binding.
    /// </summary>
    public ReadOnlyObservableCollection<ReactiveGroup<TKey, T>> Groups { get; }

    /// <summary>
    /// Gets the collection of groups for UI binding. This is an alias for <see cref="Groups"/>.
    /// </summary>
    /// <remarks>This property exists to satisfy the <see cref="IReactiveView{TView, TItem}"/> interface.</remarks>
    public ReadOnlyObservableCollection<ReactiveGroup<TKey, T>> Items => Groups;

    /// <summary>
    /// Gets the keys of all groups.
    /// </summary>
    public IEnumerable<TKey> Keys => _groups.Keys;

    /// <summary>
    /// Gets the values (item lists) of all groups.
    /// </summary>
    public IEnumerable<IReadOnlyList<T>> Values => _groups.Values.Cast<IReadOnlyList<T>>();

    /// <summary>
    /// Gets the items in the specified group.
    /// </summary>
    /// <param name="key">The group key.</param>
    /// <returns>The items in the group.</returns>
    public IReadOnlyList<T> this[TKey key] => _groups[key];

    /// <summary>
    /// Determines whether the view contains a group with the specified key.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>true if the view contains a group with the key; otherwise, false.</returns>
    public bool ContainsKey(TKey key) => _groups.ContainsKey(key);

    /// <summary>
    /// Gets the items in the specified group, if it exists.
    /// </summary>
    /// <param name="key">The group key.</param>
    /// <param name="value">When this method returns, contains the items, if the key is found; otherwise, null.</param>
    /// <returns>true if the view contains a group with the specified key; otherwise, false.</returns>
    public bool TryGetValue(TKey key, out IReadOnlyList<T> value)
    {
        if (_groups.TryGetValue(key, out var list))
        {
            value = list;
            return true;
        }

        value = Array.Empty<T>();
        return false;
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, IReadOnlyList<T>>> GetEnumerator() =>
        _groups.Select(kvp => new KeyValuePair<TKey, IReadOnlyList<T>>(kvp.Key, kvp.Value)).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Forces a rebuild of the grouped view from the source.
    /// </summary>
    public void Refresh()
    {
        lock (_lock)
        {
            RebuildView();
        }
    }

    /// <summary>
    /// Assigns the current collection of groups to a property using the specified setter action.
    /// </summary>
    /// <remarks>This method is typically used to bind the internal collection to an external property, such
    /// as a view model property, in a reactive UI pattern.</remarks>
    /// <param name="propertySetter">An action that sets a property to the current read-only observable collection of groups. Cannot be null.</param>
    /// <returns>The current instance of <see cref="GroupedReactiveView{T, TKey}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public GroupedReactiveView<T, TKey> ToProperty(Action<ReadOnlyObservableCollection<ReactiveGroup<TKey, T>>> propertySetter)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(propertySetter);
#else
        if (propertySetter == null)
        {
            throw new ArgumentNullException(nameof(propertySetter));
        }
#endif
        propertySetter(Groups);
        return this;
    }

    /// <summary>
    /// Returns the current instance and provides a read-only observable collection of groups contained in the view.
    /// </summary>
    /// <param name="collection">When this method returns, contains a read-only observable collection of groups managed by this view.</param>
    /// <returns>The current <see cref="GroupedReactiveView{T, TKey}"/> instance.</returns>
    public GroupedReactiveView<T, TKey> ToProperty(out ReadOnlyObservableCollection<ReactiveGroup<TKey, T>> collection)
    {
        collection = Groups;
        return this;
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
                AddToGroup(change.Current);
                break;

            case ChangeReason.Remove:
                RemoveFromGroup(change.Current);
                break;

            case ChangeReason.Update:
                if (change.Previous != null)
                {
                    var oldKey = _keySelector(change.Previous);
                    var newKey = _keySelector(change.Current);

                    if (EqualityComparer<TKey>.Default.Equals(oldKey, newKey))
                    {
                        // Same group - just update in place
                        if (_groups.TryGetValue(oldKey, out var group))
                        {
                            var index = group.IndexOf(change.Previous);
                            if (index >= 0)
                            {
                                group[index] = change.Current;
                            }
                        }
                    }
                    else
                    {
                        // Different groups - remove from old, add to new
                        RemoveFromGroup(change.Previous);
                        AddToGroup(change.Current);
                    }
                }
                else
                {
                    AddToGroup(change.Current);
                }

                break;

            case ChangeReason.Clear:
                _groups.Clear();
                _groupCollection.Clear();
                break;

            case ChangeReason.Move:
            case ChangeReason.Refresh:
                // Rebuild for move/refresh
                RebuildView();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddToGroup(T item)
    {
        var key = _keySelector(item);
        if (!_groups.TryGetValue(key, out var group))
        {
            group = [];
            _groups[key] = group;
            _groupCollection.Add(new ReactiveGroup<TKey, T>(key, group));
        }

        group.Add(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveFromGroup(T item)
    {
        var key = _keySelector(item);
        if (_groups.TryGetValue(key, out var group))
        {
            group.Remove(item);
            if (group.Count == 0)
            {
                _groups.Remove(key);
                var reactiveGroup = _groupCollection.FirstOrDefault(g => EqualityComparer<TKey>.Default.Equals(g.Key, key));
                if (reactiveGroup != null)
                {
                    _groupCollection.Remove(reactiveGroup);
                }
            }
        }
    }

    private void RebuildView()
    {
        _groups.Clear();
        _groupCollection.Clear();

        foreach (var item in _source)
        {
            AddToGroup(item);
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
