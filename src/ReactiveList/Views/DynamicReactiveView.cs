// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using CP.Reactive.Collections;
using CP.Reactive.Core;

namespace CP.Reactive.Views;

/// <summary>
/// Represents a dynamic, filtered, and observable view over a collection that updates in response to changes from an
/// observable data stream and supports dynamically changing filter predicates.
/// </summary>
/// <remarks>DynamicReactiveView of T maintains a live, read-only collection that reflects both an initial snapshot
/// and ongoing changes from an observable source. The view automatically rebuilds when the filter predicate changes,
/// and applies the current filter to all incoming items. Updates are batched to optimize UI responsiveness. This class
/// is designed for scenarios where a UI or other consumer needs to observe a collection that changes over time with
/// dynamic filtering, such as search functionality in MVVM applications. The view raises property change notifications
/// when its contents are updated. Thread safety is provided for UI-bound scenarios by observing updates on the
/// specified scheduler.</remarks>
/// <typeparam name="T">The type of items contained in the view. Must be non-nullable.</typeparam>
public class DynamicReactiveView<T> : INotifyPropertyChanged, IReactiveView<DynamicReactiveView<T>, T>
where T : notnull
{
    private readonly ObservableCollection<T> _target = [];
    private readonly IReactiveSource<T> _source;
    private readonly CompositeDisposable _disposables = [];
    private readonly IScheduler _scheduler;
    private readonly TimeSpan _throttle;
    private Func<T, bool> _currentFilter = static _ => true;
    private IDisposable? _streamSubscription;
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicReactiveView{T}"/> class, providing a filtered, observable,
    /// and throttled view over a quaternary source that can respond to dynamically changing filter predicates.
    /// </summary>
    /// <remarks>The view is populated with items from the source that satisfy the initial filter (all items
    /// by default), and then kept up to date by subscribing to the source's change stream. When the filter observable
    /// emits a new predicate, the view completely rebuilds its contents. Notifications are buffered according to the
    /// specified throttle interval and processed on the given scheduler.</remarks>
    /// <param name="source">The quaternary source to observe for changes. Cannot be null.</param>
    /// <param name="filterObservable">An observable sequence of filter predicates. When a new predicate is emitted, the view rebuilds its contents.</param>
    /// <param name="throttle">The time interval used to batch incoming notifications from the stream before processing.</param>
    /// <param name="scheduler">The scheduler on which to observe and process batched notifications, typically used to marshal updates to the
    /// appropriate thread (such as the UI thread).</param>
    /// <exception cref="ArgumentNullException">Thrown if source, filterObservable, or scheduler is null.</exception>
 #if NET8_0_OR_GREATER
    public DynamicReactiveView(IReactiveSource<T> source, IObservable<Func<T, bool>> filterObservable, in TimeSpan throttle, IScheduler scheduler)
#else
    public DynamicReactiveView(IReactiveSource<T> source, IObservable<Func<T, bool>> filterObservable, TimeSpan throttle, IScheduler scheduler)
#endif
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(filterObservable);
        ArgumentNullException.ThrowIfNull(scheduler);
#else
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (filterObservable == null)
        {
            throw new ArgumentNullException(nameof(filterObservable));
        }

        if (scheduler == null)
        {
            throw new ArgumentNullException(nameof(scheduler));
        }
#endif

        _source = source;
        _throttle = throttle;
        _scheduler = scheduler;
        Items = new ReadOnlyObservableCollection<T>(_target);

        // Get initial filter - for BehaviorSubject/ReplaySubject this should return immediately
        try
        {
            _currentFilter = filterObservable.FirstAsync().GetAwaiter().GetResult() ?? (static _ => true);
        }
        catch
        {
            _currentFilter = static _ => true;
        }

        RebuildView();
        SubscribeToStream();

        // Subscribe to subsequent filter changes with scheduler observation
        filterObservable
            .Skip(1)
            .ObserveOn(scheduler)
            .Subscribe(newFilter =>
            {
                _currentFilter = newFilter ?? (static _ => true);
                RebuildView();
                SubscribeToStream();
            }).DisposeWith(_disposables);
    }

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    /// <remarks>This event is typically raised by classes that implement the <see
    /// cref="INotifyPropertyChanged"/> interface to notify clients, such as data-binding frameworks, that a property
    /// value has changed.</remarks>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets a read-only, observable collection of items of type T.
    /// </summary>
    /// <remarks>The collection reflects changes to the underlying data source and notifies observers of any
    /// modifications. Items cannot be added to or removed from this collection directly.</remarks>
    public ReadOnlyObservableCollection<T> Items { get; }

    /// <summary>
    /// Assigns the current collection of items to a property using the specified setter action.
    /// </summary>
    /// <remarks>This method is typically used to bind the internal collection to an external property, such
    /// as a view model property, in a reactive UI pattern.</remarks>
    /// <param name="propertySetter">An action that sets a property to the current read-only observable collection of items. Cannot be null.</param>
    /// <returns>The current instance of <see cref="DynamicReactiveView{T}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public DynamicReactiveView<T> ToProperty(Action<ReadOnlyObservableCollection<T>> propertySetter)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(propertySetter);
#else
        if (propertySetter == null)
        {
            throw new ArgumentNullException(nameof(propertySetter));
        }
#endif
        propertySetter(Items);
        return this;
    }

    /// <summary>
    /// Returns the current instance and provides a read-only observable collection of items contained in the view.
    /// </summary>
    /// <param name="collection">When this method returns, contains a read-only observable collection of items of type <typeparamref name="T"/>
    /// managed by the view.</param>
    /// <returns>The current <see cref="DynamicReactiveView{T}"/> instance.</returns>
    public DynamicReactiveView<T> ToProperty(out ReadOnlyObservableCollection<T> collection)
    {
        collection = Items;
        return this;
    }

    /// <summary>
    /// Releases all resources used by the current instance of the class.
    /// </summary>
    /// <remarks>Call this method when you are finished using the object to release unmanaged resources and
    /// perform other cleanup operations. After calling Dispose, the object should not be used further.</remarks>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the object and optionally releases the managed resources.
    /// </summary>
    /// <remarks>This method is called by public Dispose methods and the finalizer. When disposing is true,
    /// this method releases all resources held by managed objects. When disposing is false, only unmanaged resources
    /// are released. Override this method to release resources specific to the derived class.</remarks>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _streamSubscription?.Dispose();
                _disposables.Dispose();
            }

            _disposedValue = true;
        }
    }

    /// <summary>
    /// Refreshes the view by reapplying the current filter to the source collection and updating the target collection
    /// accordingly.
    /// </summary>
    /// <remarks>Call this method to ensure that the target collection reflects the latest state of the source
    /// collection and filter. This method also raises the PropertyChanged event for the Items property to notify
    /// listeners of the update.</remarks>
    private void RebuildView()
    {
        _target.Clear();
        foreach (var item in _source.Where(item => _currentFilter(item)))
        {
            _target.Add(item);
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
    }

    /// <summary>
    /// Subscribes to the data stream and updates the collection when new items are received.
    /// </summary>
    /// <remarks>Disposes any existing stream subscription before creating a new one. Updates to the
    /// collection are batched and processed on the specified scheduler. Raises the PropertyChanged event for the Items
    /// property after each batch is applied.</remarks>
    private void SubscribeToStream()
    {
        // Dispose previous subscription
        _streamSubscription?.Dispose();

        // Subscribe to stream with current filter
        _streamSubscription = _source.Stream
            .Buffer(_throttle)
            .Where(b => b.Count > 0)
            .ObserveOn(_scheduler)
            .Subscribe(batch =>
            {
                foreach (var notify in batch)
                {
                    ApplyChange(notify);
                    notify.Batch?.Dispose();
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
            });
    }

    /// <summary>
    /// Applies the specified cache change notification to the target collection, updating its contents based on the
    /// action described.
    /// </summary>
    /// <remarks>Supported actions include adding, removing, or clearing items, as well as batch operations.
    /// Items are only added if they satisfy the current filter criteria. Batch operations process each item in the
    /// batch individually according to the action.</remarks>
    /// <param name="n">A cache notification describing the action to apply and the affected item or batch. Cannot be null.</param>
    private void ApplyChange(CacheNotify<T> n)
    {
        switch (n.Action)
        {
            case CacheAction.Added:
                if (n.Item != null && _currentFilter(n.Item))
                {
                    _target.Add(n.Item);
                }

                break;
            case CacheAction.Removed:
                if (n.Item != null)
                {
                    _target.Remove(n.Item);
                }

                break;
            case CacheAction.BatchOperation:
            case CacheAction.BatchAdded:
                if (n.Batch != null)
                {
                    for (var i = 0; i < n.Batch.Count; i++)
                    {
                        var item = n.Batch.Items[i];
                        if (_currentFilter(item))
                        {
                            _target.Add(item);
                        }
                    }
                }

                break;
            case CacheAction.BatchRemoved:
                if (n.Batch != null)
                {
                    for (var i = 0; i < n.Batch.Count; i++)
                    {
                        var item = n.Batch.Items[i];
                        _target.Remove(item);
                    }
                }

                break;
            case CacheAction.Cleared:
                _target.Clear();
                break;
        }
    }
}
