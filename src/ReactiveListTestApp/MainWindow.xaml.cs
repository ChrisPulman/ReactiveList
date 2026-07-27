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
    /// <summary>The shared whole-number display format.</summary>
    private const string NumberFormat = "{0:N0}";

    /// <summary>The view model owned by this window.</summary>
    private readonly MainWindowViewModel _viewModel;

    /// <summary>Tracks whether the window has released its owned resources.</summary>
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
        _ = StartPauseButton.SetBinding(Button.CommandProperty, CreateBinding(nameof(MainWindowViewModel.StartPauseCommand)));
        _ = StartPauseButton.SetBinding(ContentControl.ContentProperty, CreateBinding(nameof(MainWindowViewModel.StartPauseText)));
        _ = BurstButton.SetBinding(Button.CommandProperty, CreateBinding(nameof(MainWindowViewModel.BurstCommand)));
        _ = StepButton.SetBinding(Button.CommandProperty, CreateBinding(nameof(MainWindowViewModel.StepCommand)));
        _ = ResetButton.SetBinding(Button.CommandProperty, CreateBinding(nameof(MainWindowViewModel.ResetCommand)));
        _ = RateSlider.SetBinding(RangeBase.ValueProperty, CreateBinding(nameof(MainWindowViewModel.TargetRate), mode: BindingMode.TwoWay));
        _ = TargetRateText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.TargetRate), "{0:N0} events/s"));
        _ = StatusTextBlock.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.StatusText)));
        _ = LastOperationText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.LastOperation)));

        _ = RateMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.EventsPerSecond), NumberFormat));
        _ = FrameMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.UiFramesPerSecond), "{0:N1}"));
        _ = TotalMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.TotalEvents), NumberFormat));
        _ = GenerationMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.GenerationMilliseconds), "{0:N2}"));
        _ = AllocationMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.AllocatedKilobytesPerFrame), "{0:N1}"));
        _ = SourceMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.SourceCount), NumberFormat));
        _ = VisibleMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.ViewBacklogCount), NumberFormat));
        _ = AlertMetric.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.AlertCount), NumberFormat));

        _ = LiveTapeGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.LiveTape)));
        _ = AlertsGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.Alerts)));
        _ = SearchGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.SearchResults)));
        _ = LatencyGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.SlowestUpdates)));
        _ = SectorGroupsList.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.SectorGroups)));
        _ = IndexedListGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.IndexedVenueSnapshots)));
        _ = IndexedDictionaryGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.IndexedVenueDictionary)));
        _ = MatrixGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.MatrixRows)));
        _ = StreamGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.StreamEvents)));
        _ = FeaturesGrid.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.Features)));

        _ = SearchBox.SetBinding(TextBox.TextProperty, CreateBinding(nameof(MainWindowViewModel.SearchText), mode: BindingMode.TwoWay, trigger: UpdateSourceTrigger.PropertyChanged));
        _ = AlertOnlyCheckBox.SetBinding(ToggleButton.IsCheckedProperty, CreateBinding(nameof(MainWindowViewModel.OnlyAlerts), mode: BindingMode.TwoWay));
        _ = VenueComboBox.SetBinding(ItemsControl.ItemsSourceProperty, CreateBinding(nameof(MainWindowViewModel.Venues)));
        _ = VenueComboBox.SetBinding(Selector.SelectedItemProperty, CreateBinding(nameof(MainWindowViewModel.SelectedVenue), mode: BindingMode.TwoWay));

        _ = ReactiveVersionText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.ReactiveListVersion), "ReactiveList version: {0:N0}"));
        _ = QuaternaryListCountText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.QuaternaryListCount), "QuaternaryList retained: {0:N0}"));
        _ = QuaternaryDictionaryCountText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.QuaternaryDictionaryCount), "QuaternaryDictionary keys: {0:N0}"));
        _ = MatrixCellCountText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.MatrixCellCount), "{0:N0} live cells"));
        _ = HotTickCountText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.HotTickCount), "{0:N0} ticks in current scratch batch"));
        _ = HotDictionaryCountText.SetBinding(TextBlock.TextProperty, CreateBinding(nameof(MainWindowViewModel.HotDictionaryCount), "{0:N0} latest instrument snapshots"));
    }
}
