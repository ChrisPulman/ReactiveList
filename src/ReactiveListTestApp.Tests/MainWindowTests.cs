// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using TUnit.Core.Executors;

namespace ReactiveListTestApp.Tests;

/// <summary>Verifies the WPF shell's command, collection and edit bindings.</summary>
[STAThreadExecutor]
[NotInParallel]
public sealed class MainWindowTests
{
    /// <summary>Creates the shell with its application resources and releases the producer on close.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_ApplicationResources_ConfiguresBindingsAndOwnsViewModel()
    {
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
        MainWindow? window = null;
        try
        {
            window = new();
            await Assert.That(window.DataContext).IsTypeOf<MainWindowViewModel>();
            var model = (MainWindowViewModel)window.DataContext;
            model.StartPauseCommand.Execute(null);
            var pause = await Assert.That(window.FindName("StartPauseButton")).IsTypeOf<Button>();
            var slider = await Assert.That(window.FindName("RateSlider")).IsTypeOf<Slider>();
            var search = await Assert.That(window.FindName("SearchBox")).IsTypeOf<TextBox>();
            var alerts = await Assert.That(window.FindName("AlertOnlyCheckBox")).IsTypeOf<CheckBox>();
            var venue = await Assert.That(window.FindName("VenueComboBox")).IsTypeOf<ComboBox>();
            var tape = await Assert.That(window.FindName("LiveTapeGrid")).IsTypeOf<DataGrid>();
            var commandBinding = await Assert.That(BindingOperations.GetBinding(pause, Button.CommandProperty)).IsNotNull();
            var rateBinding = await Assert.That(BindingOperations.GetBinding(slider, RangeBase.ValueProperty)).IsNotNull();
            var searchBinding = await Assert.That(BindingOperations.GetBinding(search, TextBox.TextProperty)).IsNotNull();
            var alertBinding = await Assert.That(BindingOperations.GetBinding(alerts, ToggleButton.IsCheckedProperty)).IsNotNull();
            var venueBinding = await Assert.That(BindingOperations.GetBinding(venue, Selector.SelectedItemProperty)).IsNotNull();
            var tapeBinding = await Assert.That(BindingOperations.GetBinding(tape, ItemsControl.ItemsSourceProperty)).IsNotNull();
            await Assert.That(commandBinding.Path.Path).IsEqualTo(nameof(MainWindowViewModel.StartPauseCommand));
            await Assert.That(rateBinding.Mode).IsEqualTo(BindingMode.TwoWay);
            await Assert.That(searchBinding.Mode).IsEqualTo(BindingMode.TwoWay);
            await Assert.That(searchBinding.UpdateSourceTrigger).IsEqualTo(UpdateSourceTrigger.PropertyChanged);
            await Assert.That(alertBinding.Mode).IsEqualTo(BindingMode.TwoWay);
            await Assert.That(venueBinding.Mode).IsEqualTo(BindingMode.TwoWay);
            await Assert.That(tapeBinding.Path.Path).IsEqualTo(nameof(MainWindowViewModel.LiveTape));
            window.Close();
            window.Dispose();
            await Assert.That(() => model.StepCommand.Execute(null)).Throws<ObjectDisposedException>();
        }
        finally
        {
            window?.Dispose();
            app.Shutdown();
        }
    }
}
