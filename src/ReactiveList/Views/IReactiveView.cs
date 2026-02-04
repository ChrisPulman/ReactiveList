// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace CP.Reactive.Views;

/// <summary>
/// Defines methods for binding a reactive view's items collection to a property.
/// </summary>
/// <typeparam name="TView">The concrete view type (for fluent chaining).</typeparam>
/// <typeparam name="TItem">The type of items in the view's collection.</typeparam>
public interface IReactiveView<TView, TItem> : IDisposable
    where TView : IReactiveView<TView, TItem>
{
    /// <summary>
    /// Gets a read-only, observable collection of items.
    /// </summary>
    /// <remarks>The collection reflects changes to the underlying data source and notifies observers of any
    /// modifications. Items cannot be added to or removed from this collection directly.</remarks>
    ReadOnlyObservableCollection<TItem> Items { get; }

    /// <summary>
    /// Assigns the current collection of items to a property using the specified setter action.
    /// </summary>
    /// <remarks>This method is typically used to bind the internal collection to an external property, such
    /// as a view model property, in a reactive UI pattern.</remarks>
    /// <param name="propertySetter">An action that sets a property to the current read-only observable collection of items. Cannot be null.</param>
    /// <returns>The current instance to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertySetter"/> is null.</exception>
    TView ToProperty(Action<ReadOnlyObservableCollection<TItem>> propertySetter);

    /// <summary>
    /// Returns the current instance and provides a read-only observable collection of items contained in the view.
    /// </summary>
    /// <param name="collection">When this method returns, contains a read-only observable collection of items managed by the view.</param>
    /// <returns>The current instance.</returns>
    TView ToProperty(out ReadOnlyObservableCollection<TItem> collection);
}
