// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using System.Windows.Controls;
using ReactiveUI;
using Splat;

namespace ReactiveListTestApp.Views;

/// <summary>Interaction logic for AddressBookView.xaml.</summary>
public partial class AddressBookView : UserControl, IViewFor<AddressBookViewModel>
{
    private const int BulkImportCount = 100;

    /// <summary>Initializes a new instance of the <see cref="AddressBookView"/> class.</summary>
    public AddressBookView()
    {
        InitializeComponent();

        var viewModel = AppLocator.Current.GetService<AddressBookViewModel>() ?? throw new InvalidOperationException("Could not locate AddressBookViewModel.");
        ViewModel = viewModel;
        DataContext = viewModel;

        this.WhenActivated(d =>
        {
            d(this.BindCommand(ViewModel, vm => vm.BulkImportCommand, v => v.BulkImportButton, Observable.Return(BulkImportCount)));
            d(this.BindCommand(ViewModel, vm => vm.BulkRemoveInactiveCommand, v => v.BulkRemoveButton));
            d(this.Bind(ViewModel, vm => vm.SearchQuery, v => v.SearchTextBox.Text));
            d(this.OneWayBind(ViewModel, vm => vm.AllContacts, v => v.AllContactsListBox.ItemsSource));
            d(this.OneWayBind(ViewModel, vm => vm.FavoriteContacts, v => v.FavoritesListBox.ItemsSource));
            d(this.OneWayBind(ViewModel, vm => vm.NewYorkContacts, v => v.NewYorkListBox.ItemsSource));
            d(this.OneWayBind(ViewModel, vm => vm.SearchResults, v => v.SearchResultsListBox.ItemsSource));
        });
    }

    /// <inheritdoc/>
    public AddressBookViewModel? ViewModel
    {
        get => DataContext as AddressBookViewModel;
        set => DataContext = value;
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as AddressBookViewModel;
    }
}
