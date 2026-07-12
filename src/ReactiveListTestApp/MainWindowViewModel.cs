// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using CP.Primitives.Views;
using ReactiveListTestApp.Infrastructure;
using ReactiveListTestApp.Models;
using ReactiveListTestApp.Services;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

namespace ReactiveListTestApp;

/// <summary>Coordinates the live producer, reactive collections, views and sampled WPF metrics.</summary>
internal sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private const int ListViewThrottleMilliseconds = 0;

    private const int IndexViewThrottleMilliseconds = 0;

    private const int TapeRetention = 800;

    private const int IndexedRetention = 1_200;

    private const int StreamRetention = 160;

    private const string VenueIndexName = "ByVenue";

    private const string DataViewCategory = "Data view";

    private const int MinimumRate = 1_000;

    private const int MaximumRate = 100_000;

    private const int RateStep = 1_000;

    private const int ProjectionFramesPerSecond = 8;

    private const int MinimumStepEvents = 100;

    private const int MatrixRowCount = 4;

    private const int MatrixColumnCount = 10;

    private const int MetricsSampleMilliseconds = 500;

    private const int MatrixTextCapacity = 80;

    private const double BytesPerKilobyte = 1_024d;

    private readonly LiveDataEngine _engine = new();

    private readonly ReactiveList<InstrumentSnapshot> _tape = [];

    private readonly Reactive2DList<double> _priceMatrix = CreatePriceMatrix();

    private readonly QuaternaryList<InstrumentSnapshot> _indexedSnapshots = [];

    private readonly QuaternaryDictionary<int, InstrumentSnapshot> _latestByInstrument = [];

    private readonly ReactiveList<MatrixRow> _matrixRows = [];

    private readonly ReactiveList<StreamEvent> _streamEvents = [];

    private readonly BehaviorSignal<Func<InstrumentSnapshot, bool>> _dynamicFilter = new(static _ => true);

    private readonly BehaviorSignal<string[]> _venueKeys = new(["LSE"]);

    private readonly List<IDisposable> _disposables = [];

    private readonly Dispatcher _dispatcher;

    private readonly Stopwatch _rateWindow = Stopwatch.StartNew();

    private readonly EventHandler<MarketFrame> _frameHandler;

    private readonly FilteredReactiveView<InstrumentSnapshot> _allView;

    private readonly FilteredReactiveView<InstrumentSnapshot> _alertsView;

    private readonly DynamicFilteredReactiveView<InstrumentSnapshot> _searchView;

    private readonly SortedReactiveView<InstrumentSnapshot> _latencyView;

    private readonly GroupedReactiveView<InstrumentSnapshot, string> _sectorView;

    private readonly DynamicSecondaryIndexReactiveView<InstrumentSnapshot, string> _indexedVenueView;

    private readonly DynamicSecondaryIndexDictionaryReactiveView<int, InstrumentSnapshot> _dictionaryVenueView;

    private readonly FilteredReactiveView<StreamEvent> _streamView;

    private long _windowEvents;

    private long _lastWindowEvents;

    private long _uiFrames;

    private long _streamSequence;

    private bool _isRunning = true;

    private int _targetRate = 10_000;

    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="MainWindowViewModel"/> class.</summary>
    public MainWindowViewModel()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _frameHandler = (_, frame) => ApplyFrame(frame);
        var sequencer = new DispatcherSequencer(_dispatcher, DispatcherPriority.Background);

        _indexedSnapshots.AddIndex(VenueIndexName, static snapshot => snapshot.Venue);
        _latestByInstrument.AddValueIndex(VenueIndexName, static snapshot => snapshot.Venue);
        _matrixRows.AddRange([
            new("LSE", string.Empty, 0d, 0d, 0d),
            new("XNAS", string.Empty, 0d, 0d, 0d),
            new("XNYS", string.Empty, 0d, 0d, 0d),
            new("XEUR", string.Empty, 0d, 0d, 0d)
        ]);

        _allView = _tape.CreateView(sequencer, ListViewThrottleMilliseconds);
        _alertsView = _tape.CreateView(static snapshot => snapshot.IsAlert, sequencer, ListViewThrottleMilliseconds);
        _searchView = _tape.CreateView(_dynamicFilter, sequencer, ListViewThrottleMilliseconds);
        _latencyView = _tape.SortBy(static snapshot => snapshot.LatencyMilliseconds, descending: true, sequencer, ListViewThrottleMilliseconds);
        _sectorView = _tape.GroupBy(static snapshot => snapshot.Sector, sequencer, ListViewThrottleMilliseconds);
        _indexedVenueView = _indexedSnapshots.CreateDynamicViewBySecondaryIndex(VenueIndexName, _venueKeys, sequencer, IndexViewThrottleMilliseconds);
        _dictionaryVenueView = _latestByInstrument.CreateDynamicViewBySecondaryIndex(VenueIndexName, _venueKeys, sequencer, IndexViewThrottleMilliseconds);
        _streamView = _streamEvents.CreateView(sequencer, ListViewThrottleMilliseconds);

        _disposables.AddRange([
            _tape.Stream.Subscribe(new ActionObserver<CacheNotify<InstrumentSnapshot>>(notification => RecordStream("ReactiveList", notification.Action, notification.Batch?.Count ?? 1))),
            _tape.Connect().Subscribe(new ActionObserver<ChangeSet<InstrumentSnapshot>>(changes => RecordStream("Connect() ChangeSet", CacheAction.BatchOperation, changes.Count))),
            _indexedSnapshots.Stream.Subscribe(new ActionObserver<CacheNotify<InstrumentSnapshot>>(notification => RecordStream("QuaternaryList", notification.Action, notification.Batch?.Count ?? 1))),
            _latestByInstrument.Stream.Subscribe(new ActionObserver<CacheNotify<KeyValuePair<int, InstrumentSnapshot>>>(notification => RecordStream("QuaternaryDictionary", notification.Action, notification.Batch?.Count ?? 1)))
        ]);

        StartPauseCommand = new DelegateCommand(ToggleRunning);
        ResetCommand = new DelegateCommand(Reset);
        BurstCommand = new DelegateCommand(Burst);
        StepCommand = new DelegateCommand(Step, () => !_isRunning);

        Features =
        [
            new("ReactiveList<T>", "Reactive collection", "Thread-safe list with pooled batch mutations, versioned stream and activity collections.", "Bounded market tape using AddRange and RemoveRange."),
            new("Reactive2DList<T>", "Reactive collection", "Observable rows plus inner Add, Insert, Set, Remove, Flatten and TotalCount operations.", "Four live venue lanes with ten instruments per row."),
            new("QuaternaryList<T>", "Concurrent collection", "Four sharded writers with snapshots, batches, RemoveMany and named secondary indexes.", "Retained indexed ticks filtered by a dynamic venue signal."),
            new("QuaternaryDictionary<TKey,TValue>", "Concurrent collection", "Sharded lookup map with AddOrUpdate, range edits and value secondary indexes.", "Latest quote per instrument with a dynamic venue index view."),
            new("QuadList<T>", "Pooled hot path", "ArrayPool-backed non-reactive list optimized for spans and low-allocation batch work.", "Per-frame raw tick scratch buffer on the producer thread."),
            new("QuadDictionary<TKey,TValue>", "Pooled hot path", "Pooled hash table with direct ref lookup and no notification overhead.", "Latest snapshot aggregation before reactive publication."),
            new("Filtered / Dynamic views", DataViewCategory, "Read-only observable projections updated from source streams and changing predicates.", "All, alert-only and live text/alert query grids."),
            new("Sorted / Grouped views", DataViewCategory, "Incrementally refreshed order and ReactiveGroup projections.", "Latency leaderboard and sector group summaries."),
            new("Secondary-index views", DataViewCategory, "Static or signal-driven views that query named quaternary indexes.", "Venue selector drives list and dictionary views together."),
            new("Connect / ChangeSet pipeline", "Stream", "Cache notifications bridge to ChangeSet transforms, filters, grouping and action callbacks.", "Bounded stream inspector shows real collection actions.")
        ];

        _engine.FrameProduced += _frameHandler;
        _engine.TargetEventsPerSecond = _targetRate;
        _engine.Start();
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the unfiltered bounded tape projection.</summary>
    public ReadOnlyObservableCollection<InstrumentSnapshot> LiveTape => _allView.Items;

    /// <summary>Gets the static alert-only filtered view.</summary>
    public ReadOnlyObservableCollection<InstrumentSnapshot> Alerts => _alertsView.Items;

    /// <summary>Gets the signal-driven text and alert filter view.</summary>
    public ReadOnlyObservableCollection<InstrumentSnapshot> SearchResults => _searchView.Items;

    /// <summary>Gets the snapshots sorted by descending latency.</summary>
    public ReadOnlyObservableCollection<InstrumentSnapshot> SlowestUpdates => _latencyView.Items;

    /// <summary>Gets live sector groups.</summary>
    public ReadOnlyObservableCollection<ReactiveGroup<string, InstrumentSnapshot>> SectorGroups => _sectorView.Groups;

    /// <summary>Gets the dynamic secondary-index list view.</summary>
    public ReadOnlyObservableCollection<InstrumentSnapshot> IndexedVenueSnapshots => _indexedVenueView.Items;

    /// <summary>Gets the dynamic secondary-index dictionary view.</summary>
    public ReadOnlyObservableCollection<KeyValuePair<int, InstrumentSnapshot>> IndexedVenueDictionary => _dictionaryVenueView.Items;

    /// <summary>Gets display rows projected from the two-dimensional collection.</summary>
    public ReadOnlyObservableCollection<MatrixRow> MatrixRows => _matrixRows.Items;

    /// <summary>Gets the bounded cache notification log.</summary>
    public ReadOnlyObservableCollection<StreamEvent> StreamEvents => _streamView.Items;

    /// <summary>Gets the feature catalog shown beside the stream inspector.</summary>
    public IReadOnlyList<FeatureCard> Features { get; }

    /// <summary>Gets the available secondary-index keys.</summary>
    public IReadOnlyList<string> Venues { get; } = ["LSE", "XNAS", "XNYS", "XEUR"];

    /// <summary>Gets the pause or resume command.</summary>
    public ICommand StartPauseCommand { get; }

    /// <summary>Gets the reset command.</summary>
    public ICommand ResetCommand { get; }

    /// <summary>Gets the immediate high-rate burst command.</summary>
    public ICommand BurstCommand { get; }

    /// <summary>Gets the paused single-frame command.</summary>
    public ICommand StepCommand { get; }

    /// <summary>Gets or sets the dynamic view search text.</summary>
    public string SearchText
    {
        get => field;
        set
        {
            if (!SetField(ref field, value ?? string.Empty))
            {
                return;
            }

            PublishDynamicFilter();
        }
    } = string.Empty;

    /// <summary>Gets or sets the active secondary-index venue.</summary>
    public string SelectedVenue
    {
        get => field;
        set
        {
            if (!SetField(ref field, value))
            {
                return;
            }

            _venueKeys.OnNext([value]);
            LastOperation = $"Dynamic secondary index switched to {value}";
        }
    }
= "LSE";

    /// <summary>Gets or sets a value indicating whether the dynamic view shows only alerts.</summary>
    public bool OnlyAlerts
    {
        get => field;
        set
        {
            if (!SetField(ref field, value))
            {
                return;
            }

            PublishDynamicFilter();
        }
    }

    /// <summary>Gets or sets the target raw events per second.</summary>
    public int TargetRate
    {
        get => _targetRate;
        set
        {
            var rounded = Math.Clamp((value / RateStep) * RateStep, MinimumRate, MaximumRate);
            if (!SetField(ref _targetRate, rounded))
            {
                return;
            }

            _engine.TargetEventsPerSecond = rounded;
            LastOperation = $"Producer target changed to {rounded:N0} events/s";
        }
    }

    /// <summary>Gets a value indicating whether continuous production is running.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set => SetField(ref _isRunning, value);
    }

    /// <summary>Gets the pause or resume button label.</summary>
    public string StartPauseText => IsRunning ? "Pause" : "Resume";

    /// <summary>Gets the sampled producer status.</summary>
    public string StatusText
    {
        get => field;
        private set => SetField(ref field, value);
    } = "LIVE · worker ingesting full-rate batches";

    /// <summary>Gets the most recent user-visible operation description.</summary>
    public string LastOperation
    {
        get => field;
        private set => SetField(ref field, value);
    } = "Starting continuous feed";

    /// <summary>Gets the measured raw events per second.</summary>
    public double EventsPerSecond
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <summary>Gets the measured dispatcher projection frames per second.</summary>
    public double UiFramesPerSecond
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <summary>Gets the last raw batch generation duration.</summary>
    public double GenerationMilliseconds
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <summary>Gets allocated kilobytes for the most recent bounded frame.</summary>
    public double AllocatedKilobytesPerFrame
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <summary>Gets the cumulative raw event count.</summary>
    public long TotalEvents
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <summary>Gets the bounded ReactiveList source count.</summary>
    public int SourceCount
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <summary>Gets the current dynamic-view count.</summary>
    public int VisibleCount
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <summary>Gets the positive item lag between the asynchronous view and bounded source.</summary>
    public int ViewBacklogCount
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <summary>Gets the alert count in the latest frame.</summary>
    public int AlertCount
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <summary>Gets the current pooled tick scratch count.</summary>
    public int HotTickCount
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <summary>Gets the current pooled hot dictionary count.</summary>
    public int HotDictionaryCount
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <summary>Gets the total number of inner Reactive2DList cells.</summary>
    public int MatrixCellCount => _priceMatrix.TotalCount();

    /// <summary>Gets the current ReactiveList version.</summary>
    public long ReactiveListVersion
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <summary>Gets the retained QuaternaryList count.</summary>
    public int QuaternaryListCount
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <summary>Gets the QuaternaryDictionary key count.</summary>
    public int QuaternaryDictionaryCount
    {
        get => field;
        private set => SetField(ref field, value);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _engine.FrameProduced -= _frameHandler;
        _engine.Dispose();
        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            _disposables[i].Dispose();
        }

        _streamView.Dispose();
        _dictionaryVenueView.Dispose();
        _indexedVenueView.Dispose();
        _sectorView.Dispose();
        _latencyView.Dispose();
        _searchView.Dispose();
        _alertsView.Dispose();
        _allView.Dispose();
        _dynamicFilter.Dispose();
        _venueKeys.Dispose();
        _tape.Dispose();
        _priceMatrix.Dispose();
        _indexedSnapshots.Dispose();
        _latestByInstrument.Dispose();
        _matrixRows.Dispose();
        _streamEvents.Dispose();
    }

    /// <summary>Creates four pre-sized inner rows for allocation-stable updates.</summary>
    /// <returns>The initialized price matrix.</returns>
    private static Reactive2DList<double> CreatePriceMatrix() =>
        new(Enumerable.Range(0, MatrixRowCount).Select(_ => Enumerable.Repeat(0d, MatrixColumnCount)));

    /// <summary>Pauses or resumes the continuous worker.</summary>
    private void ToggleRunning()
    {
        _engine.TogglePause();
        IsRunning = !_engine.IsPaused;
        StatusText = IsRunning ? "LIVE · worker ingesting full-rate batches" : "PAUSED · views remain queryable";
        LastOperation = IsRunning ? "Continuous producer resumed" : "Continuous producer paused";
        Interlocked.Exchange(ref _uiFrames, 0);
        _lastWindowEvents = Interlocked.Read(ref _windowEvents);
        _rateWindow.Restart();
        if (!IsRunning)
        {
            EventsPerSecond = 0;
            UiFramesPerSecond = 0;
        }

        RaisePropertyChanged(nameof(StartPauseText));
        ((DelegateCommand)StepCommand).RaiseCanExecuteChanged();
    }

    /// <summary>Clears all showcase state while retaining pooled storage.</summary>
    private void Reset()
    {
        _engine.Reset();
        _tape.ClearWithoutDeallocation();
        _indexedSnapshots.Clear();
        _latestByInstrument.Clear();
        _streamEvents.ClearWithoutDeallocation();
        for (var row = 0; row < MatrixRowCount; row++)
        {
            for (var column = 0; column < MatrixColumnCount; column++)
            {
                _priceMatrix.SetItem(row, column, 0d);
            }

            _matrixRows[row] = new(Venues[row], string.Empty, 0d, 0d, 0d);
        }

        Interlocked.Exchange(ref _windowEvents, 0);
        Interlocked.Exchange(ref _uiFrames, 0);
        _lastWindowEvents = 0;
        _rateWindow.Restart();
        EventsPerSecond = 0;
        UiFramesPerSecond = 0;
        TotalEvents = 0;
        SourceCount = 0;
        VisibleCount = 0;
        ViewBacklogCount = 0;
        AlertCount = 0;
        ReactiveListVersion = 0;
        QuaternaryListCount = 0;
        QuaternaryDictionaryCount = 0;
        LastOperation = "All live collections reset without abandoning pooled hot-path storage";
    }

    /// <summary>Generates one immediate batch containing a full target-rate second.</summary>
    private void Burst()
    {
        var frame = _engine.GenerateFrame(Math.Min(TargetRate, MaximumRate));
        ApplyFrame(frame);
        LastOperation = $"Burst generated {frame.EventCount:N0} events in {frame.GenerationTime.TotalMilliseconds:N2} ms";
    }

    /// <summary>Generates one projection frame while the worker is paused.</summary>
    private void Step()
    {
        var frame = _engine.GenerateFrame(Math.Max(MinimumStepEvents, TargetRate / ProjectionFramesPerSecond));
        ApplyFrame(frame);
        LastOperation = $"Single projection frame generated from {frame.EventCount:N0} events";
    }

    /// <summary>Publishes a frame to every reactive collection.</summary>
    /// <param name="frame">The generated frame.</param>
    private void ApplyFrame(MarketFrame frame)
    {
        _tape.AddRange(frame.Snapshots.AsSpan());
        if (_tape.Count > TapeRetention)
        {
            _tape.RemoveRange(0, _tape.Count - TapeRetention);
        }

        _indexedSnapshots.AddRange(frame.Snapshots);
        if (_indexedSnapshots.Count > IndexedRetention)
        {
            var retainedFrameCount = IndexedRetention / frame.Snapshots.Length;
            var cutoff = frame.Sequence - ((long)frame.EventCount * retainedFrameCount);
            _indexedSnapshots.RemoveMany(snapshot => snapshot.Sequence < cutoff);
        }

        foreach (ref readonly var snapshot in frame.Snapshots.AsSpan())
        {
            _latestByInstrument.AddOrUpdate(snapshot.InstrumentId, snapshot);
        }

        Interlocked.Add(ref _windowEvents, frame.EventCount);
        _dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => ApplyUiFrame(frame));
    }

    /// <summary>Updates dispatcher-owned collections and sampled scalar metrics.</summary>
    /// <param name="frame">The generated frame.</param>
    private void ApplyUiFrame(MarketFrame frame)
    {
        if (_disposed)
        {
            return;
        }

        UpdateMatrix(frame.Snapshots);
        TotalEvents = frame.TotalEvents;
        SourceCount = _tape.Count;
        VisibleCount = _searchView.Count;
        ViewBacklogCount = Math.Max(0, VisibleCount - SourceCount);
        AlertCount = frame.Snapshots.Count(static snapshot => snapshot.IsAlert);
        GenerationMilliseconds = frame.GenerationTime.TotalMilliseconds;
        AllocatedKilobytesPerFrame = frame.AllocatedBytes / BytesPerKilobyte;
        HotTickCount = _engine.HotTickCapacityCount;
        HotDictionaryCount = _engine.HotDictionaryCount;
        ReactiveListVersion = _tape.Version;
        QuaternaryListCount = _indexedSnapshots.Count;
        QuaternaryDictionaryCount = _latestByInstrument.Count;
        Interlocked.Increment(ref _uiFrames);

        if (_rateWindow.ElapsedMilliseconds < MetricsSampleMilliseconds)
        {
            return;
        }

        var elapsed = _rateWindow.Elapsed.TotalSeconds;
        var events = Interlocked.Read(ref _windowEvents);
        EventsPerSecond = (events - _lastWindowEvents) / elapsed;
        UiFramesPerSecond = Interlocked.Exchange(ref _uiFrames, 0) / elapsed;
        _lastWindowEvents = events;
        _rateWindow.Restart();
    }

    /// <summary>Writes current prices to the Reactive2DList and its compact row view.</summary>
    /// <param name="snapshots">The current instrument projections.</param>
    private void UpdateMatrix(InstrumentSnapshot[] snapshots)
    {
        for (var row = 0; row < MatrixRowCount; row++)
        {
            var start = row * MatrixColumnCount;
            var values = snapshots.AsSpan(start, MatrixColumnCount);
            var minimum = double.MaxValue;
            var maximum = double.MinValue;
            var total = 0d;
            var formattedValues = new StringBuilder(MatrixTextCapacity);
            for (var column = 0; column < values.Length; column++)
            {
                var price = values[column].Price;
                _priceMatrix.SetItem(row, column, price);
                minimum = Math.Min(minimum, price);
                maximum = Math.Max(maximum, price);
                total += price;
                if (column > 0)
                {
                    formattedValues.Append("  ");
                }

                formattedValues.Append(CultureInfo.CurrentCulture, $"{price:N2}");
            }

            _matrixRows[row] = new(
                Venues[row],
                formattedValues.ToString(),
                total / values.Length,
                minimum,
                maximum);
        }

        RaisePropertyChanged(nameof(MatrixCellCount));
    }

    /// <summary>Publishes the latest user query as a dynamic filter predicate.</summary>
    private void PublishDynamicFilter()
    {
        var search = SearchText.Trim();
        var alertsOnly = OnlyAlerts;
        _dynamicFilter.OnNext(snapshot =>
            (!alertsOnly || snapshot.IsAlert) &&
            (search.Length == 0 ||
             snapshot.Symbol.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             snapshot.Sector.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             snapshot.Venue.Contains(search, StringComparison.OrdinalIgnoreCase)));
        LastOperation = alertsOnly ? "Dynamic view is filtering search results to alerts" : "Dynamic view predicate updated";
    }

    /// <summary>Records a bounded cache notification summary.</summary>
    /// <param name="source">The collection source.</param>
    /// <param name="action">The cache action.</param>
    /// <param name="count">The affected item count.</param>
    private void RecordStream(string source, CacheAction action, int count)
    {
        var detail = (source.StartsWith("Connect()", StringComparison.Ordinal), action) switch
        {
            (true, _) => "ChangeSet projection",
            (false, CacheAction.BatchAdded or CacheAction.BatchRemoved) => "Pooled batch notification",
            _ => "Single cache notification"
        };

        var item = new StreamEvent(
            Interlocked.Increment(ref _streamSequence),
            DateTime.Now.ToString("HH:mm:ss.fff"),
            source,
            action.ToString(),
            count,
            detail);
        _streamEvents.Add(item);
        if (_streamEvents.Count <= StreamRetention)
        {
            return;
        }

        _streamEvents.RemoveRange(0, _streamEvents.Count - StreamRetention);
    }

    /// <summary>Sets a property backing field and raises a change notification.</summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="field">The backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns><see langword="true"/> when the value changed.</returns>
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Raises a property change for a dependent property.</summary>
    /// <param name="propertyName">The dependent property name.</param>
    private void RaisePropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Raises a property change notification.</summary>
    /// <param name="propertyName">The changed property name.</param>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
