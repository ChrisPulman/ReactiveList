// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows.Controls;
using ReactiveUI;
using Splat;

namespace ReactiveListTestApp.Views;

/// <summary>
/// Interaction logic for MainView.xaml.
/// </summary>
public partial class MainView : UserControl, IViewFor<MainViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainView"/> class.
    /// </summary>
    public MainView()
    {
        InitializeComponent();
        DataContext = ViewModel = AppLocator.Current.GetService<MainViewModel>() ?? throw new InvalidOperationException("Could not locate MainViewModel.");
    }

    /// <inheritdoc/>
    public MainViewModel? ViewModel
    {
        get => DataContext as MainViewModel;
        set => DataContext = value;
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as MainViewModel;
    }
}
