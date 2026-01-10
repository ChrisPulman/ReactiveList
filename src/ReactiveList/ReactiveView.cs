// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET6_0_OR_GREATER

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace CP.Reactive;

/// <summary>
/// Provides a reactive, observable view over a collection of items of type <typeparamref name="T"/>, supporting dynamic
/// updates and notifications based on an external data stream and filter criteria.
/// </summary>
/// <remarks>ReactiveView of T maintains a read-only, observable collection that reflects changes from an external
/// data stream, filtered according to the specified predicate. The view automatically updates its contents in response
/// to notifications from the stream, and raises property change events to support data binding scenarios. Thread
/// synchronization is handled to ensure updates occur on the appropriate context for UI applications. Dispose the
/// instance when no longer needed to release resources and unsubscribe from the data stream.</remarks>
/// <typeparam name="T">The type of items contained in the view. Must be non-nullable.</typeparam>
public class ReactiveView<T> : INotifyPropertyChanged, IDisposable
    where T : notnull
{
    private readonly ObservableCollection<T> _internalCollection = [];
    private IDisposable? _subscription;
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveView{T}"/> class, providing a filtered, observable view of items that react.
    /// to changes from a source stream with throttling.
    /// </summary>
    /// <remarks>The view is updated in response to changes from the source stream, with updates applied in
    /// batches according to the specified throttle interval. The Items property provides a read-only observable
    /// collection that reflects the current state of the view. Property change notifications are raised when the view
    /// is updated. Thread affinity for updates is determined by the current synchronization context, if
    /// available.</remarks>
    /// <param name="stream">An observable sequence of cache notifications that supplies updates to the view.</param>
    /// <param name="filter">A predicate function used to determine which items from the source and updates are included in the view.</param>
    /// <param name="initialData">The initial collection of items to populate the view, filtered by the specified predicate.</param>
    /// <param name="throttle">The time interval used to buffer incoming notifications before applying them to the view. Updates are processedin batches at this interval.</param>
    /// <param name="scheduler">The scheduler used to observe and apply updates to the view. This determines the context on which updates.</param>
    public ReactiveView(IObservable<CacheNotify<T>> stream, Func<T, bool> filter, IEnumerable<T> initialData, TimeSpan throttle, IScheduler scheduler)
    {
        foreach (var item in initialData.Where(filter))
        {
            _internalCollection.Add(item);
        }

        Items = new ReadOnlyObservableCollection<T>(_internalCollection);

        _subscription = stream
            .Buffer(throttle)
            .Where(b => b.Count > 0)
            .ObserveOn(scheduler)
            .Subscribe(batch =>
            {
                _internalCollection.Clear();
                foreach (var n in batch)
                {
                    if (n.Batch == null)
                    {
                        continue;
                    }

                    using (n.Batch)
                    {
                        foreach (var item in n.Batch.Items.Where(filter))
                        {
                            _internalCollection.Add(item);
                        }
                    }
                }

                Items = new ReadOnlyObservableCollection<T>(_internalCollection);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
            });
    }

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    /// <remarks>This event is typically raised by the implementation of the INotifyPropertyChanged interface
    /// to notify subscribers that a property value has changed. Handlers receive the name of the property that changed
    /// in the event arguments. This event is commonly used in data binding scenarios to update UI elements when
    /// underlying data changes.</remarks>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets a read-only, observable collection containing the items managed by the instance.
    /// </summary>
    /// <remarks>The returned collection reflects changes to the underlying data and notifies observers of
    /// updates. Modifications to the collection must be performed through the instance's public methods; direct changes
    /// to the collection are not supported.</remarks>
    public ReadOnlyObservableCollection<T> Items { get; private set; }

    /// <summary>
    /// Applies the current collection of items to the specified setter action and returns the current view instance.
    /// </summary>
    /// <param name="setter">An action that receives the read-only observable collection of items to be set. Cannot be null.</param>
    /// <returns>The current instance of <see cref="ReactiveView{T}"/> to allow for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="setter"/> is null.</exception>
    public ReactiveView<T> ToProperty(Action<ReadOnlyObservableCollection<T>> setter)
    {
        if (setter == null)
        {
            throw new ArgumentNullException(nameof(setter));
        }

        setter(Items);
        return this;
    }

    /// <summary>
    /// Releases all resources used by the current instance of the class.
    /// </summary>
    /// <remarks>Call this method when you are finished using the object to free unmanaged resources and
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
    /// <remarks>This method is called by both the public Dispose() method and the finalizer. When disposing
    /// is true, this method should release all managed resources. When disposing is false, only unmanaged resources
    /// should be released. Override this method to provide custom disposal logic in derived classes.</remarks>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _subscription?.Dispose();
            }

            _disposedValue = true;
        }
    }
}
#endif
