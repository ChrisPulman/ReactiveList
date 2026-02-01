// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables.Fluent;
using CrissCross;
using ReactiveUI;

namespace ReactiveListTestApp;

/// <summary>
/// ViewModel for the MainWindow that handles navigation.
/// </summary>
public class MainWindowViewModel : RxObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    public MainWindowViewModel()
    {
        NavigateToMainCommand = ReactiveCommand.Create(() =>
            this.NavigateToView<MainViewModel>("mainWindow")).DisposeWith(Disposables);

        NavigateToAddressBookCommand = ReactiveCommand.Create(() =>
            this.NavigateToView<AddressBookViewModel>("mainWindow")).DisposeWith(Disposables);
    }

    /// <summary>
    /// Gets the command to navigate to the main view.
    /// </summary>
    public ReactiveCommand<Unit, Unit> NavigateToMainCommand { get; }

    /// <summary>
    /// Gets the command to navigate to the address book view.
    /// </summary>
    public ReactiveCommand<Unit, Unit> NavigateToAddressBookCommand { get; }
}
