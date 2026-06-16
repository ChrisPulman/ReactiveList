// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using CP.Reactive;
using CP.Reactive.Collections;
using CrissCross;
using ReactiveUI;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

namespace ReactiveListTestApp;

/// <summary>
/// ViewModel for the MainView containing collection diagnostics for the ReactiveList library.
/// </summary>
public class MainViewModel : RxObject
{
    private const string DepartmentIndex = "ByDepartment";
    private readonly ReactiveList<int> _numbers = [];
    private readonly BehaviorSignal<Func<int, bool>> _numberFilter = new(static number => number >= 0);
    private readonly Reactive2DList<string> _matrix = [];
    private readonly QuaternaryList<Contact> _contacts = [];
    private readonly QuaternaryDictionary<Guid, Contact> _contactsById = [];
    private readonly IDisposable _itemsStreamSubscription;
    private bool _paused;
    private int _contactBatch;
    private int _nextItem;
    private int _nextNumber = 13;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    public MainViewModel()
    {
        InitializeReactiveListViews();
        InitializeQuaternaryViews();

        _itemsStreamSubscription = Items.Connect()
            .Subscribe(changes => Log($"ReactiveList stream observed {changes.Count} change(s)."));

        SeedCollections();

        AddItemCommand = ReactiveCommand.Create<string>(AddItem).DisposeWith(Disposables);
        ClearItemsCommand = ReactiveCommand.Create(ClearItems).DisposeWith(Disposables);
        ReplaceAllCommand = ReactiveCommand.Create(ReplaceAllItems).DisposeWith(Disposables);
        PauseCommand = ReactiveCommand.Create(TogglePause).DisposeWith(Disposables);
        RunReactiveListScenarioCommand = ReactiveCommand.Create(RunReactiveListScenario).DisposeWith(Disposables);
        RunMatrixScenarioCommand = ReactiveCommand.Create(RunMatrixScenario).DisposeWith(Disposables);
        RunIndexScenarioCommand = ReactiveCommand.Create(RunIndexScenario).DisposeWith(Disposables);
    }

    /// <summary>
    /// Gets the items collection.
    /// </summary>
    public IReactiveList<string> Items { get; } = new ReactiveList<string>();

    /// <summary>
    /// Gets all numbers projected through a read-only reactive view.
    /// </summary>
    public ReadOnlyObservableCollection<int> AllNumbers { get; private set; } = null!;

    /// <summary>
    /// Gets even numbers projected through a filtered reactive view.
    /// </summary>
    public ReadOnlyObservableCollection<int> EvenNumbers { get; private set; } = null!;

    /// <summary>
    /// Gets numbers projected through a dynamic filter signal.
    /// </summary>
    public ReadOnlyObservableCollection<int> DynamicNumbers { get; private set; } = null!;

    /// <summary>
    /// Gets contacts projected from a QuaternaryList secondary index view.
    /// </summary>
    public ReadOnlyObservableCollection<Contact> EngineeringContacts { get; private set; } = null!;

    /// <summary>
    /// Gets contacts projected from a QuaternaryDictionary secondary index view.
    /// </summary>
    public ReadOnlyObservableCollection<KeyValuePair<Guid, Contact>> EngineeringDictionaryContacts { get; private set; } = null!;

    /// <summary>
    /// Gets row summaries for the Reactive2DList test surface.
    /// </summary>
    public ReactiveList<string> MatrixRows { get; } = [];

    /// <summary>
    /// Gets direct dictionary lookup results.
    /// </summary>
    public ReactiveList<string> DictionaryLookups { get; } = [];

    /// <summary>
    /// Gets operation results shown in the UI.
    /// </summary>
    public ReactiveList<string> OperationLog { get; } = [];

    /// <summary>
    /// Gets the command to add an item.
    /// </summary>
    public ReactiveCommand<string, Unit> AddItemCommand { get; }

    /// <summary>
    /// Gets the command to clear items.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ClearItemsCommand { get; }

    /// <summary>
    /// Gets the command to replace all items.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ReplaceAllCommand { get; }

    /// <summary>
    /// Gets the command to pause or resume manual mutations.
    /// </summary>
    public ReactiveCommand<Unit, bool> PauseCommand { get; }

    /// <summary>
    /// Gets the command that exercises list mutation, views, and stream observations.
    /// </summary>
    public ReactiveCommand<Unit, Unit> RunReactiveListScenarioCommand { get; }

    /// <summary>
    /// Gets the command that exercises Reactive2DList row and inner-item operations.
    /// </summary>
    public ReactiveCommand<Unit, Unit> RunMatrixScenarioCommand { get; }

    /// <summary>
    /// Gets the command that exercises quaternary secondary-index and dictionary operations.
    /// </summary>
    public ReactiveCommand<Unit, Unit> RunIndexScenarioCommand { get; }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _itemsStreamSubscription.Dispose();
            _numberFilter.Dispose();
            _numbers.Dispose();
            _matrix.Dispose();
            _contacts.Dispose();
            _contactsById.Dispose();
            Items.Dispose();
            MatrixRows.Dispose();
            DictionaryLookups.Dispose();
            OperationLog.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Contact CreateContact(int index) =>
        new(
            Guid.NewGuid(),
            $"User{index}",
            $"Person{index}",
            $"user{index}@company.test",
            index % 2 == 0 ? "Engineering" : "HR",
            index % 3 == 0,
            new Address("1 Test Street", index % 4 == 0 ? "New York" : "London", "10001", "UK"));

    private void AddItem(string prefix)
    {
        if (_paused)
        {
            Log("ReactiveList mutation ignored while paused.");
            return;
        }

        Items.Add($"{prefix}{_nextItem++}");
    }

    private void ClearItems()
    {
        Items.Clear();
        Log("ReactiveList items cleared.");
    }

    private void InitializeQuaternaryViews()
    {
        var sequencer = SynchronizationContextSequencer.Current;
        _contacts.AddIndex(DepartmentIndex, static contact => contact.Department);
        _contactsById.AddValueIndex(DepartmentIndex, static contact => contact.Department);

        _contacts.CreateViewBySecondaryIndex(DepartmentIndex, "Engineering", sequencer, throttleMs: 0)
            .ToProperty(collection => EngineeringContacts = collection)
            .DisposeWith(Disposables);

        QuaternaryExtensions.CreateViewBySecondaryIndex<Guid, Contact, string>(_contactsById, DepartmentIndex, "Engineering", sequencer, throttleMs: 0)
            .ToProperty(collection => EngineeringDictionaryContacts = collection)
            .DisposeWith(Disposables);
    }

    private void InitializeReactiveListViews()
    {
        var sequencer = SynchronizationContextSequencer.Current;

        _numbers.CreateView(sequencer, throttleMs: 0)
            .ToProperty(collection => AllNumbers = collection)
            .DisposeWith(Disposables);

        _numbers.CreateView(static number => (number & 1) == 0, sequencer, throttleMs: 0)
            .ToProperty(collection => EvenNumbers = collection)
            .DisposeWith(Disposables);

        _numbers.CreateView(_numberFilter, sequencer, throttleMs: 0)
            .ToProperty(collection => DynamicNumbers = collection)
            .DisposeWith(Disposables);
    }

    private void Log(string message)
    {
        OperationLog.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
        if (OperationLog.Count > 16)
        {
            OperationLog.RemoveRange(16, OperationLog.Count - 16);
        }
    }

    private void RefreshDictionaryLookups()
    {
        var engineering = _contactsById
            .GetValuesBySecondaryIndex(DepartmentIndex, "Engineering")
            .OrderBy(static contact => contact.Email)
            .Take(6)
            .Select(static contact => $"{contact.Email} ({contact.HomeAddress.City})");

        DictionaryLookups.ReplaceAll(engineering);
    }

    private void RefreshMatrixRows()
    {
        var rows = _matrix
            .Select((row, index) => $"Row {index}: {string.Join(", ", row)}")
            .Append($"Total cells: {_matrix.TotalCount()}");

        MatrixRows.ReplaceAll(rows);
    }

    private void ReplaceAllItems()
    {
        Items.ReplaceAll(["One", "Two", "Three", "Four", "Five"]);
        Log("ReactiveList ReplaceAll completed.");
    }

    private void RunIndexScenario()
    {
        var contacts = Enumerable.Range(_contactBatch * 6, 6).Select(CreateContact).ToArray();
        _contactBatch++;

        _contacts.AddRange(contacts);
        _contactsById.AddRange(contacts.Select(static contact => KeyValuePair.Create(contact.Id, contact)));

        var removableHrContacts = _contacts
            .GetItemsBySecondaryIndex(DepartmentIndex, "HR")
            .Take(1)
            .ToArray();
        _contacts.RemoveRange(removableHrContacts);
        _contactsById.RemoveKeys(removableHrContacts.Select(static contact => contact.Id));

        var lookupSucceeded = contacts.Length > 0 && _contactsById.TryGetValue(contacts[0].Id, out _);
        RefreshDictionaryLookups();
        Log($"Quaternary indexes added {contacts.Length}, removed {removableHrContacts.Length}, lookup={lookupSucceeded}.");
    }

    private void RunMatrixScenario()
    {
        if (_matrix.Count == 0)
        {
            _matrix.AddRange([["A1", "A2"], ["B1", "B2"]]);
        }

        _matrix.AddToInner(0, $"A{_nextItem++}");

        if (_matrix.Count > 1 && _matrix[1].Count > 0)
        {
            _matrix.SetItem(1, 0, $"B{_nextItem++}");
            _matrix.Insert(1, [$"B{_nextItem++}"], innerIndex: 1);
        }

        _matrix.Insert(_matrix.Count, [$"R{_matrix.Count}-1", $"R{_matrix.Count}-2"]);

        if (_matrix.Count > 3)
        {
            _matrix.ClearInner(_matrix.Count - 1);
        }

        RefreshMatrixRows();
        Log("Reactive2DList row and inner-item mutations completed.");
    }

    private void RunReactiveListScenario()
    {
        if (_paused)
        {
            Log("ReactiveList scenario ignored while paused.");
            return;
        }

        Items.AddRange(["Alpha", "Beta", "Gamma"]);
        Items.Remove("Beta");
        Items.InsertRange(1, ["Delta", "Epsilon"]);
        Items.Move(0, Items.Items.Count - 1);

        _numbers.Add(_nextNumber++);
        _numbers.InsertRange(0, [-_nextNumber]);
        _numbers.RemoveMany(static number => number < 0);
        _numberFilter.OnNext(_nextNumber % 2 == 0
            ? static number => number % 3 == 0
            : static number => number >= 10);

        Log("ReactiveList batch, INCC views, and dynamic filter updated.");
    }

    private void SeedCollections()
    {
        Items.AddRange(["Alpha", "Beta", "Gamma"]);
        _numbers.AddRange(Enumerable.Range(1, 12));
        _matrix.AddRange([["A1", "A2"], ["B1", "B2", "B3"]]);

        var contacts = Enumerable.Range(0, 8).Select(CreateContact).ToArray();
        _contactBatch = 2;
        _contacts.AddRange(contacts);
        _contactsById.AddRange(contacts.Select(static contact => KeyValuePair.Create(contact.Id, contact)));

        RefreshMatrixRows();
        RefreshDictionaryLookups();
        Log("Seeded ReactiveList, Reactive2DList, QuaternaryList, and QuaternaryDictionary.");
    }

    private bool TogglePause()
    {
        _paused = !_paused;
        Log(_paused ? "Manual ReactiveList mutations paused." : "Manual ReactiveList mutations resumed.");
        return _paused;
    }
}
