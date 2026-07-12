// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables.Fluent;
using CP.Primitives;
using CP.Primitives.Collections;
using CrissCross;
using ReactiveUI;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

namespace ReactiveListTestApp;

/// <summary>
/// Represents the view model for an address book, providing observable collections and operations for managing and
/// querying contacts.
/// </summary>
/// <remarks>The AddressBookViewModel exposes read-only collections for all contacts, favorites, contacts in New
/// York, and dynamic search results, enabling data binding in UI scenarios. It supports efficient bulk operations and
/// high-speed lookups using internal indices. The view model is designed for use in reactive or MVVM-based applications
/// and should be disposed when no longer needed to release resources.</remarks>
public class AddressBookViewModel : RxObject
{
    private const string CityIndex = "ByCity";

    private const string EngineeringDepartment = "Engineering";

    private const int DepartmentAlternation = 2;

    private const int FavoriteInterval = 10;

    private const int NewYorkInterval = 5;

    private readonly QuaternaryList<Contact> _contactList = [];

    private readonly QuaternaryDictionary<Guid, Contact> _contactMap = [];

    private readonly BehaviorSignal<string> _searchText = new(string.Empty);

    /// <summary>Initializes a new instance of the <see cref="AddressBookViewModel"/> class.</summary>
    /// <remarks>This constructor sets up the necessary indices and data pipelines required for the view model
    /// to function correctly. Use this constructor when creating a new AddressBookViewModel instance for managing
    /// address book data in the application.</remarks>
    public AddressBookViewModel()
    {
        InitializeIndices();
        InitializePipelines();
        InitializeCommands();
    }

    /// <summary>Gets a read-only collection containing all contacts currently managed by the application.</summary>
    /// <remarks>The collection reflects real-time changes to the underlying contacts. Subscribers to the
    /// collection's change events are notified when contacts are added, removed, or updated.</remarks>
    public ReadOnlyObservableCollection<Contact> AllContacts { get; private set; } = null!;

    /// <summary>Gets the collection of contacts marked as favorites by the user.</summary>
    /// <remarks>The returned collection is read-only and reflects changes to the underlying favorites list in
    /// real time. Items in this collection are automatically updated when contacts are added to or removed from the
    /// user's favorites.</remarks>
    public ReadOnlyObservableCollection<Contact> FavoriteContacts { get; private set; } = null!;

    /// <summary>Gets a read-only collection of contacts located in New York.</summary>
    public ReadOnlyObservableCollection<Contact> NewYorkContacts { get; private set; } = null!;

    /// <summary>Gets a read-only, observable collection of contacts that match the current search criteria.</summary>
    /// <remarks>The contents of the collection are updated automatically when the search criteria change or
    /// when the underlying data changes. Subscribers can monitor collection changes by handling the CollectionChanged
    /// event.</remarks>
    public ReadOnlyObservableCollection<Contact> SearchResults { get; private set; } = null!;

    /// <summary>Gets or sets the current search query text.</summary>
    public string SearchQuery
    {
        get => _searchText.Value;
        set => _searchText.OnNext(value ?? string.Empty);
    }

    /// <summary>Gets the command to bulk import contacts.</summary>
    public ReactiveCommand<int, Unit> BulkImportCommand { get; private set; } = null!;

    /// <summary>Gets the command to remove inactive HR contacts.</summary>
    public ReactiveCommand<Unit, Unit> BulkRemoveInactiveCommand { get; private set; } = null!;

    /// <summary>Adds the specified number of new contacts to the collection in bulk.</summary>
    /// <remarks>This method generates new contacts with sample data and adds them to the collection
    /// efficiently. Use this method to quickly populate the contact list for testing or initialization purposes. The
    /// method does not check for duplicate contacts.</remarks>
    /// <param name="count">The number of contacts to add. Must be non-negative.</param>
    public void BulkImport(int count)
    {
        var newContacts = Enumerable.Range(0, count).Select(i =>
            new Contact(
                Guid.NewGuid(),
                $"User{i}",
                $"Smith{i}",
                $"user{i}@company.com",
                i % DepartmentAlternation == 0 ? EngineeringDepartment : "HR",
                i % FavoriteInterval == 0, // 10% are favorites
                new Address("123 Main", i % NewYorkInterval == 0 ? "New York" : "London", "10001", "USA"))).ToList();

        // High-Speed Parallel Add
        _contactList.AddRange(newContacts);
        _contactMap.AddRange(newContacts.Select(c => new KeyValuePair<Guid, Contact>(c.Id, c)));
    }

    /// <summary>Removes all inactive contacts in the HR department from the contact list and associated mappings.</summary>
    /// <remarks>This method performs a bulk removal operation for contacts identified as inactive within the
    /// HR department. It updates both the main contact list and any related lookup structures to ensure consistency.
    /// The operation is optimized for performance and is thread-safe.</remarks>
    public void BulkRemoveInactive()
    {
        // Query utilizing Secondary Index for speed
        var departmentContacts = _contactList.GetItemsBySecondaryIndex("ByDepartment", "HR").ToArray();

        // Bulk Thread-Safe Remove
        _contactList.RemoveRange(departmentContacts);

        // Sync Dictionary
        _contactMap.RemoveKeys(departmentContacts.Select(c => c.Id));
    }

    /// <summary>Updates the city name for all contacts whose home address matches the specified old city.</summary>
    /// <remarks>This method updates all contacts in the list whose home address city matches the specified
    /// old city. If no contacts match, no changes are made. The operation replaces the entire city name for each
    /// affected contact's home address.</remarks>
    /// <param name="oldCity">The name of the city to be replaced in the home addresses of contacts. Cannot be null.</param>
    /// <param name="newCity">The new city name to assign to the matching contacts' home addresses. Cannot be null.</param>
    public void UpdateCityName(string oldCity, string newCity)
    {
        // 1. Find targets using Index (Fast)
        var targets = _contactList.GetItemsBySecondaryIndex(CityIndex, oldCity).ToArray();

        // 2. Modify and Update
        // Since Records are immutable, we replace the object
        var updates = targets.Select(c => c with { HomeAddress = c.HomeAddress with { City = newCity } }).ToArray();

        // 3. Apply updates (Remove old, Add new) effectively performs an update
        _contactList.RemoveRange(targets);
        _contactList.AddRange(updates);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _contactList.Dispose();
            _contactMap.Dispose();
            _searchText.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Determines whether the specified contact matches the given query based on last name or email address.</summary>
    /// <param name="c">The contact to evaluate. If <paramref name="c"/> is <see langword="null"/>, the method returns <see
    /// langword="false"/>.</param>
    /// <param name="query">The search query to match against the contact's last name or email address. If <paramref name="query"/> is <see
    /// langword="null"/>, empty, or consists only of white-space characters, the method returns <see langword="true"/>.</param>
    /// <returns>true if the contact's last name or email address contains the query string, ignoring case; otherwise, false.</returns>
    private static bool Matches(Contact? c, string query)
    {
        if (c is null)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(query) ? true : c.LastName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               c.Email.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Initializes the command properties used for bulk import and removal operations.</summary>
    /// <remarks>This method sets up the BulkImportCommand and BulkRemoveInactiveCommand properties with their
    /// respective actions. It should be called during object initialization to ensure that command properties are
    /// properly configured before use.</remarks>
    private void InitializeCommands()
    {
        BulkImportCommand = ReactiveCommand.Create<int>(BulkImport);
        BulkRemoveInactiveCommand = ReactiveCommand.Create(BulkRemoveInactive);
    }

    /// <summary>Initializes lookup indices for contact data to enable efficient access by city, department, and email address.</summary>
    /// <remarks>This method should be called before performing queries or updates that rely on indexed
    /// access. Initializing indices improves performance for lookups and updates based on city, department, or email,
    /// but must be done prior to using these features.</remarks>
    private void InitializeIndices()
    {
        // Add High-Speed Lookup Indices (O(1) access)
        _contactList.AddIndex(CityIndex, c => c.HomeAddress.City);
        _contactList.AddIndex("ByDepartment", c => c.Department);

        // Map Dictionary for ID-based updates
        _contactMap.AddValueIndex("ByEmail", c => c.Email);
    }

    /// <summary>Initializes contact-related data pipelines and views for the current instance.</summary>
    /// <remarks>This method sets up observable views for all contacts, favorites, contacts filtered by city,
    /// and dynamic search results. It configures throttling and filtering to optimize UI responsiveness and resource
    /// usage. This method should be called during initialization to ensure that contact views are available and kept up
    /// to date.</remarks>
    private void InitializePipelines()
    {
        var viewSequencer = SynchronizationContextSequencer.Current;

        // 1. ALL CONTACTS (Throttled 100ms)
        _contactList.CreateView(viewSequencer, throttleMs: 100)
                    .ToProperty(x => AllContacts = x)
                    .DisposeWith(Disposables);

        // 2. FAVORITES (Filtered Subset)
        _contactList.CreateView(c => c.IsFavorite, viewSequencer, throttleMs: 100)
                    .ToProperty(x => FavoriteContacts = x)
                    .DisposeWith(Disposables);

        // 3. SECONDARY KEY SUBSET (City == "New York")
        // Using CreateViewBySecondaryIndex for efficient secondary key filtering
        _contactList.CreateViewBySecondaryIndex(CityIndex, "New York", viewSequencer, throttleMs: 200)
                    .ToProperty(x => NewYorkContacts = x)
                    .DisposeWith(Disposables);

        // 4. DYNAMIC SEARCH QUERY
        // Using CreateView with IObservable for dynamic filter that rebuilds on search text change
        _contactList.CreateView(_searchText, (query, c) => Matches(c, query), viewSequencer, throttleMs: 100)
                    .ToProperty(x => SearchResults = x)
                    .DisposeWith(Disposables);
    }
}
