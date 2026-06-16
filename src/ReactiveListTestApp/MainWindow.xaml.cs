// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CrissCross;
using ReactiveUI;

namespace ReactiveListTestApp;

/// <summary>Interaction logic for MainWindow.xaml.</summary>
public partial class MainWindow
{
    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel();
        ViewModel = viewModel;
        DataContext = viewModel;

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
