// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace ReactiveListTestApp;

/// <summary>Hosts the live ReactiveList feature showcase.</summary>
public sealed partial class MainWindow : Window, IDisposable
{
    private const string NumberFormat = "{0:N0}";

    private readonly MainWindowViewModel _viewModel;

    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new();
        DataContext = _viewModel;
        ConfigureBindings();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    /// <summary>Creates a WPF binding used by the C#-binding-first shell.</summary>
    /// <param name="path">The view-model property path.</param>
    /// <param name="format">The optional display format.</param>
    /// <param name="mode">The binding mode.</param>
    /// <param name="trigger">The source update trigger.</param>
    /// <returns>The configured binding.</returns>
    private static Binding CreateBinding(string path, string? format = null, BindingMode mode = BindingMode.OneWay, UpdateSourceTrigger trigger = UpdateSourceTrigger.Default) =>
        new(path) { StringFormat = format, Mode = mode, UpdateSourceTrigger = trigger };

    /// <summary>Attaches all application-level view-model bindings in C#.</summary>
    private void ConfigureBindings()
    {
        StartPauseButton.SetBinding(Button.CommandProperty, CreateBinding(nameof(MainWindowViewModel.StartPauseCommand)));
        StartPauseButton.SetBinding(ContentControl.ContentProperty, CreateBinding(nameof(MainWindowViewModel.StartPauseText)));
        BurstButton.SetBinding(Button.CommandProperty, CreateBinding(nameof(MainWindowViewModel.BurstCommand)));
        StepButton.SetBinding(Button.CommandProperty, CreateBinding(nameof(MainWindowViewModel.StepCommand)));
        ResetButton.SetBinding(Button.CommandProperty, CreateBinding(nameof(MainWindowViewModel.ResetCommand)));
        RateSlider.SetBinding(RangeBase.ValueProperty, CreateBinding(nameof(MainWindowViewModel.TargetRate), mode: BindingMode.TwoWay));
        TargetRateText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.TargetRate), "{0:N0} events/s"));
        StatusTextBlock.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.StatusText)));
        LastOperationText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.LastOperation)));

        RateMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.EventsPerSecond), NumberFormat));
        FrameMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.UiFramesPerSecond), "{0:N1}"));
        TotalMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.TotalEvents), NumberFormat));
        GenerationMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.GenerationMilliseconds), "{0:N2}"));
        AllocationMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.AllocatedKilobytesPerFrame), "{0:N1}"));
        SourceMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.SourceCount), NumberFormat));
        VisibleMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.ViewBacklogCount), NumberFormat));
        AlertMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.AlertCount), NumberFormat));

        LiveTapeGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.LiveTape)));
        AlertsGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.Alerts)));
        SearchGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.SearchResults)));
        LatencyGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.SlowestUpdates)));
        SectorGroupsList.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.SectorGroups)));
        IndexedListGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.IndexedVenueSnapshots)));
        IndexedDictionaryGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.IndexedVenueDictionary)));
        MatrixGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.MatrixRows)));
        StreamGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.StreamEvents)));
        FeaturesGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.Features)));

        SearchBox.SetBinding(TextBox.TextProperty, CreateBinding(nameof(MainWindowViewModel.SearchText), mode: BindingMode.TwoWay, trigger: UpdateSourceTrigger.PropertyChanged));
        AlertOnlyCheckBox.SetBinding(ToggleButton.IsCheckedProperty, CreateBinding(nameof(MainWindowViewModel.OnlyAlerts), mode: BindingMode.TwoWay));
        VenueComboBox.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.Venues)));
        VenueComboBox.SetBinding(Selector.SelectedItemProperty, CreateBinding(nameof(MainWindowViewModel.SelectedVenue), mode: BindingMode.TwoWay));

        ReactiveVersionText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.ReactiveListVersion), "ReactiveList version: {0:N0}"));
        QuaternaryListCountText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.QuaternaryListCount), "QuaternaryList retained: {0:N0}"));
        QuaternaryDictionaryCountText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.QuaternaryDictionaryCount), "QuaternaryDictionary keys: {0:N0}"));
        MatrixCellCountText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.MatrixCellCount), "{0:N0} live cells"));
        HotTickCountText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.HotTickCount), "{0:N0} ticks in current scratch batch"));
        HotDictionaryCountText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.HotDictionaryCount), "{0:N0} latest instrument snapshots"));
    }
}
