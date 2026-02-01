// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows;
using CrissCross;
using ReactiveListTestApp.Views;
using ReactiveUI;
using ReactiveUI.Builder;
using Splat;

namespace ReactiveListTestApp;

/// <summary>
/// Interaction logic for App.xaml.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Handles application startup logic when the application is launched.
    /// </summary>
    /// <param name="e">An object that contains the event data for the startup event.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithWpf()
            .BuildApp();

        // Register ViewModels
        AppLocator.CurrentMutable.RegisterConstant(new MainWindowViewModel());
        AppLocator.CurrentMutable.RegisterConstant(new MainViewModel());
        AppLocator.CurrentMutable.RegisterConstant(new AddressBookViewModel());

        // Register Views
        AppLocator.CurrentMutable.Register<IViewFor<MainViewModel>>(() => new MainView());
        AppLocator.CurrentMutable.Register<IViewFor<AddressBookViewModel>>(() => new AddressBookView());
        AppLocator.CurrentMutable.SetupComplete();

        base.OnStartup(e);
    }
}
