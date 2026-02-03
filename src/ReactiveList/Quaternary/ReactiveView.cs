// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace CP.Reactive.Quaternary;

/// <summary>
/// Represents a dynamic, filtered, and observable view over a collection that updates in response to changes from an
/// observable data stream.
/// </summary>
/// <remarks>ReactiveView of T maintains a live, read-only collection that reflects both an initial snapshot and
/// ongoing changes from an observable source. The view automatically applies a filter to all items and batches updates
/// to optimize UI responsiveness. This class is designed for scenarios where a UI or other consumer needs to observe a
/// collection that changes over time, such as in MVVM applications. The view raises property change notifications when
/// its contents are updated. Thread safety is provided for UI-bound scenarios by observing updates on the current
/// synchronization context.</remarks>
/// <typeparam name="T">The type of items contained in the view. Must be non-nullable.</typeparam>
public class ReactiveView<T> : INotifyPropertyChanged, IDisposable
    where T : notnull
{
    private readonly ObservableCollection<T?> _target = [];
    private readonly IDisposable? _sub;
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveView{T}"/> class, providing a filtered, observable, and throttled view over.
    /// a stream of cache notifications and an optional initial snapshot.
    /// </summary>
    /// <remarks>The view is populated with items from the initial snapshot that satisfy the filter, and then
    /// kept up to date by subscribing to the provided stream. Notifications from the stream are buffered according to
    /// the specified throttle interval and processed on the given scheduler. This design helps reduce UI update
    /// frequency and ensures thread-safe updates when used with UI frameworks.</remarks>
    /// <param name="stream">An observable sequence of cache notifications to monitor for changes. Cannot be null.</param>
    /// <param name="snapshot">An optional initial collection of items to populate the view before processing the stream. Only items matching
    /// the filter are included.</param>
    /// <param name="filter">A predicate used to determine which items from the snapshot and stream are included in the view. Cannot be null.</param>
    /// <param name="throttle">The time interval used to batch incoming notifications from the stream before processing.</param>
    /// <param name="sheduler">The scheduler on which to observe and process batched notifications, typically used to marshal updates to the
    /// appropriate thread (such as the UI thread).</param>
    /// <exception cref="ArgumentNullException">Thrown if stream or filter is null.</exception>
    public ReactiveView(IObservable<CacheNotify<T>> stream, IEnumerable<T?> snapshot, Func<T?, bool> filter, in TimeSpan throttle, IScheduler sheduler)
    {
        Items = new ReadOnlyObservableCollection<T?>(_target);

        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (filter == null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        if (snapshot != null)
        {
            // 1. Load Initial State (Snapshot)
            foreach (var item in snapshot)
            {
                if (filter(item))
                {
                    _target.Add(item);
                }
            }
        }

        // 2. Subscribe to Stream with Throttling
        _sub = stream
            .Buffer(throttle) // Batch changes by time
            .Where(b => b.Count > 0)
            .ObserveOn(sheduler) // Jump to UI Thread
            .Subscribe(batch =>
            {
                foreach (var notify in batch)
                {
                    ApplyChange(notify, filter);

                    // Critical: Return array to pool
                    notify.Batch?.Dispose();
                }

                // Signal UI to refresh
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
            });
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
    public ReadOnlyObservableCollection<T?> Items { get; }

    /// <summary>
    /// Assigns the current collection of items to a property using the specified setter action.
    /// </summary>
    /// <remarks>This method is typically used to bind the internal collection to an external property, such
    /// as a view model property, in a reactive UI pattern.</remarks>
    /// <param name="propertySetter">An action that sets a property to the current read-only observable collection of items. Cannot be null.</param>
    /// <returns>The current instance of <see cref="ReactiveView{T}"/> to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    public ReactiveView<T> ToProperty(Action<ReadOnlyObservableCollection<T?>> propertySetter)
    {
        if (propertySetter == null)
        {
            throw new ArgumentNullException(nameof(propertySetter));
        }

        propertySetter(Items);
        return this;
    }

    /// <summary>
    /// Returns the current instance and assigns the underlying items collection to the specified output parameter.
    /// </summary>
    /// <remarks>This method provides direct access to the underlying items collection without modifying the
    /// state of the view. The returned collection reflects the current contents and updates automatically as the view
    /// changes.</remarks>
    /// <param name="collection">When the method returns, contains a read-only observable collection of items managed by this view.</param>
    /// <returns>The current instance of <see cref="ReactiveView{T}"/>.</returns>
    public ReactiveView<T> ToProperty(out ReadOnlyObservableCollection<T?> collection)
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
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
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
                _sub?.Dispose();
            }

            _disposedValue = true;
        }
    }

    /// <summary>
    /// Applies the specified cache notification to the target collection, optionally filtering items to be added.
    /// </summary>
    /// <remarks>Depending on the action specified in the notification, this method may add, remove, or clear
    /// items in the target collection. When adding items, only those for which the filter returns <see
    /// langword="true"/> are included.</remarks>
    /// <param name="n">The cache notification describing the action to apply and the item or batch of items affected. Cannot be null.</param>
    /// <param name="filter">A predicate used to determine whether an item should be added to the target collection. Cannot be null.</param>
    private void ApplyChange(CacheNotify<T> n, Func<T, bool> filter)
    {
        switch (n.Action)
        {
            case CacheAction.Added:
                if (n.Item != null && filter(n.Item))
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
                        if (filter(item))
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
#endif
