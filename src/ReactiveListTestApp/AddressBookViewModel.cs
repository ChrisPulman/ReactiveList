// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CP.Reactive;
using ReactiveUI;

namespace ReactiveListTestApp;

/// <summary>
/// Represents the view model for an address book, providing observable collections and operations for managing and
/// querying contacts.
/// </summary>
/// <remarks>The AddressBookViewModel exposes read-only collections for all contacts, favorites, contacts in New
/// York, and dynamic search results, enabling data binding in UI scenarios. It supports efficient bulk operations and
/// high-speed lookups using internal indices. The view model is designed for use in reactive or MVVM-based applications
/// and should be disposed when no longer needed to release resources.</remarks>
public class AddressBookViewModel : IDisposable
{
    // --- The Data Stores ---
    private readonly QuaternaryList<Contact> _contactList = [];
    private readonly QuaternaryDictionary<Guid, Contact> _contactMap = [];
    private readonly BehaviorSubject<string> _searchText = new(string.Empty);
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddressBookViewModel"/> class.
    /// </summary>
    /// <remarks>This constructor sets up the necessary indices and data pipelines required for the view model
    /// to function correctly. Use this constructor when creating a new AddressBookViewModel instance for managing
    /// address book data in the application.</remarks>
    public AddressBookViewModel()
    {
        InitializeIndices();
        InitializePipelines();
    }

    /// <summary>
    /// Gets a read-only collection containing all contacts currently managed by the application.
    /// </summary>
    /// <remarks>The collection reflects real-time changes to the underlying contacts. Subscribers to the
    /// collection's change events are notified when contacts are added, removed, or updated.</remarks>
    public ReadOnlyObservableCollection<Contact> AllContacts { get; private set; }

    /// <summary>
    /// Gets the collection of contacts marked as favorites by the user.
    /// </summary>
    /// <remarks>The returned collection is read-only and reflects changes to the underlying favorites list in
    /// real time. Items in this collection are automatically updated when contacts are added to or removed from the
    /// user's favorites.</remarks>
    public ReadOnlyObservableCollection<Contact> FavoriteContacts { get; private set; }

    /// <summary>
    /// Gets a read-only collection of contacts located in New York.
    /// </summary>
    public ReadOnlyObservableCollection<Contact> NewYorkContacts { get; private set; }

    /// <summary>
    /// Gets a read-only, observable collection of contacts that match the current search criteria.
    /// </summary>
    /// <remarks>The contents of the collection are updated automatically when the search criteria change or
    /// when the underlying data changes. Subscribers can monitor collection changes by handling the CollectionChanged
    /// event.</remarks>
    public ReadOnlyObservableCollection<Contact> SearchResults { get; private set; } // Dynamic

    /// <summary>
    /// Gets or sets the current search query text.
    /// </summary>
    public string SearchQuery
    {
        get => _searchText.Value;
        set => _searchText.OnNext(value ?? string.Empty);
    }

    /// <summary>
    /// Adds the specified number of new contacts to the collection in bulk.
    /// </summary>
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
                i % 2 == 0 ? "Engineering" : "HR",
                i % 10 == 0, // 10% are favorites
                new Address("123 Main", i % 5 == 0 ? "New York" : "London", "10001", "USA"))).ToList();

        // High-Speed Parallel Add
        _contactList.AddRange(newContacts);
        _contactMap.AddRange(newContacts.Select(c => new KeyValuePair<Guid, Contact>(c.Id, c)));
    }

    /// <summary>
    /// Removes all inactive contacts in the HR department from the contact list and associated mappings.
    /// </summary>
    /// <remarks>This method performs a bulk removal operation for contacts identified as inactive within the
    /// HR department. It updates both the main contact list and any related lookup structures to ensure consistency.
    /// The operation is optimized for performance and is thread-safe.</remarks>
    public void BulkRemoveInactive()
    {
        // Query utilizing Secondary Index for speed
        var hrDept = _contactList.Query("ByDepartment", "HR").ToList();

        // Bulk Thread-Safe Remove
        _contactList.RemoveRange(hrDept);

        // Sync Dictionary
        foreach (var c in hrDept)
        {
            _contactMap.Remove(c.Id);
        }
    }

    /// <summary>
    /// Updates the city name for all contacts whose home address matches the specified old city.
    /// </summary>
    /// <remarks>This method updates all contacts in the list whose home address city matches the specified
    /// old city. If no contacts match, no changes are made. The operation replaces the entire city name for each
    /// affected contact's home address.</remarks>
    /// <param name="oldCity">The name of the city to be replaced in the home addresses of contacts. Cannot be null.</param>
    /// <param name="newCity">The new city name to assign to the matching contacts' home addresses. Cannot be null.</param>
    public void UpdateCityName(string oldCity, string newCity)
    {
        // 1. Find targets using Index (Fast)
        var targets = _contactList.Query("ByCity", oldCity).ToList();

        // 2. Modify and Update
        // Since Records are immutable, we replace the object
        var updates = new List<Contact>();
        foreach (var c in targets)
        {
            updates.Add(c with { HomeAddress = c.HomeAddress with { City = newCity } });
        }

        // 3. Apply updates (Remove old, Add new) effectively performs an update
        _contactList.RemoveRange(targets);
        _contactList.AddRange(updates);
    }

    /// <summary>
    /// Releases all resources used by the current instance of the class.
    /// </summary>
    /// <remarks>Call this method when you are finished using the object to release unmanaged resources and
    /// perform other cleanup operations. After calling Dispose, the object should not be used further.</remarks>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the object and optionally releases the managed resources.
    /// </summary>
    /// <remarks>This method is called by public Dispose methods and the finalizer. When disposing is true,
    /// this method disposes all managed resources referenced by the object. Override this method to release additional
    /// resources in a derived class.</remarks>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _contactList.Dispose();
                _contactMap.Dispose();
                _searchText.Dispose();
            }

            _disposedValue = true;
        }
    }

    private static bool Matches(Contact? c, string query)
    {
        if (c == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return c.LastName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               c.Email.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void InitializeIndices()
    {
        // Add High-Speed Lookup Indices (O(1) access)
        _contactList.AddIndex("ByCity", c => c.HomeAddress.City);
        _contactList.AddIndex("ByDepartment", c => c.Department);

        // Map Dictionary for ID-based updates
        _contactMap.AddValueIndex("ByEmail", c => c.Email);
    }

    private void InitializePipelines()
    {
        // 1. ALL CONTACTS (Throttled 100ms)
        _contactList.CreateView(c => true, RxApp.MainThreadScheduler, throttleMs: 100)
                    .ToProperty(x => AllContacts = x);

        // 2. FAVORITES (Filtered Subset)
        _contactList.CreateView(c => c.IsFavorite, RxApp.MainThreadScheduler, throttleMs: 100)
                    .ToProperty(x => FavoriteContacts = x);

        // 3. SECONDARY KEY SUBSET (City == "New York")
        // Uses the Stream for updates, but efficient logic for the filter
        _contactList.CreateView(c => c.HomeAddress.City == "New York", RxApp.MainThreadScheduler, throttleMs: 200)
                    .ToProperty(x => NewYorkContacts = x);

        // 4. DYNAMIC SEARCH QUERY (Complex Pipeline)
        // Combines the Cache Stream + Search Text Stream
        var searchPipeline = _contactList.Stream
            .CombineLatest(_searchText, (change, query) => new { change, query })
            .Where(x => Matches(x.change.Item, x.query))
            .Select(x => x.change); // Project back to notification

        // Note: For a true search view, we usually rebuild the collection when query changes.
        // This simulates a "Live Search Result" stream.
        new ReactiveView<Contact>(
            searchPipeline,
            [.. _contactList], // Initial Snapshot
            c => Matches(c, _searchText.Value),
            TimeSpan.FromMilliseconds(50),
            RxApp.MainThreadScheduler)
            .ToProperty(x => SearchResults = x);
    }
}
