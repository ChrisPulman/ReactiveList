// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Windows;
using CrissCross;
using ReactiveUI;

namespace ReactiveListTestApp;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModel = new MainWindowViewModel();

        this.WhenActivated(d =>
        {
            var backCommand = ReactiveCommand.Create(() => this.NavigateBack(), this.CanNavigateBack());
            NavBack.Command = backCommand;
            d(backCommand);

            // Navigate to the MainView on startup
            this.NavigateToView<MainViewModel>();
        });
    }
}
