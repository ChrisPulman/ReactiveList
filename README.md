# ReactiveList

[![NuGet](https://img.shields.io/nuget/v/ReactiveList.svg?style=flat-square)](https://www.nuget.org/packages/ReactiveList/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ReactiveList.svg?style=flat-square)](https://www.nuget.org/packages/ReactiveList/)
[![License](https://img.shields.io/github/license/ChrisPulman/ReactiveList.svg?style=flat-square)](LICENSE)
[![Build Status](https://img.shields.io/github/actions/workflow/status/ChrisPulman/ReactiveList/BuildOnly.yml?branch=main&style=flat-square)](https://github.com/ChrisPulman/ReactiveList/actions)

A lightweight, high-performance reactive collection library with fine-grained change tracking built on [System.Reactive](https://github.com/dotnet/reactive).

**Targets:** .NET Framework 4.7.2 / 4.8 | .NET 8 | .NET 9 | .NET 10

## Installation

```bash
dotnet add package ReactiveList
```

Or via the NuGet Package Manager:

```powershell
Install-Package ReactiveList
```

---

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [ReactiveList&lt;T&gt;](#reactivelistt)
  - [Basic Operations](#basic-operations)
  - [Batch Operations with Edit](#batch-operations-with-edit)
  - [Reactive Subscriptions](#reactive-subscriptions)
  - [Change Tracking](#change-tracking)
  - [Extension Methods](#reactivelist-extension-methods)
- [Reactive2DList&lt;T&gt;](#reactive2dlistt)
- [QuaternaryList&lt;T&gt;](#quaternarylistt) (.NET 8+)
- [QuaternaryDictionary&lt;TKey, TValue&gt;](#quaternarydictionarytkey-tvalue) (.NET 8+)
- [Benchmark Results](#benchmark-results)
- [Full API Reference](#full-api-reference)
- [UI Binding](#ui-binding)
- [Use Cases](#use-cases)
- [License](#license)

---

## Overview

This library provides four reactive collection types:

| Collection | Description | Targets |
|------------|-------------|---------|
| `ReactiveList<T>` | Observable list with fine-grained change tracking | All |
| `Reactive2DList<T>` | Two-dimensional reactive list (list of lists) | All |
| `QuaternaryList<T>` | High-performance sharded list for large datasets | .NET 8+ |
| `QuaternaryDictionary<TKey, TValue>` | High-performance sharded dictionary | .NET 8+ |

---

## Quick Start

```csharp
using CP.Reactive;

// Create a reactive list
var list = new ReactiveList<string>();

// Subscribe to changes
list.Added.Subscribe(items => Console.WriteLine($"Added: {string.Join(", ", items)}"));
list.Removed.Subscribe(items => Console.WriteLine($"Removed: {string.Join(", ", items)}"));

// Work with it like a normal list
list.Add("one");
list.AddRange(["two", "three"]);
list.Remove("two");

// Batch multiple operations with a single notification
list.Edit(l =>
{
    l.Add("four");
    l.Add("five");
    l.RemoveAt(0);
});

// Move items
list.Move(0, 2);

// Replace all items atomically
list.ReplaceAll(["a", "b", "c"]);

// Subscribe to the Stream for detailed change notifications
list.Stream.Subscribe(notification =>
{
    Console.WriteLine($"{notification.Action}: {notification.Item}");
});

// Or use Connect() extension for ChangeSet-based processing
list.Connect().Subscribe(changeSet =>
{
    foreach (var change in changeSet)
    {
        Console.WriteLine($"{change.Reason}: {change.Current}");
    }
});

// Cleanup
list.Dispose();
```

---

## ReactiveList&lt;T&gt;

A reactive, observable list that notifies subscribers of changes in real-time.

### Basic Operations

```csharp
var list = new ReactiveList<string>();

// Adding items
list.Add("item");                           // Add single item
list.AddRange(["a", "b", "c"]);            // Add multiple items

// Inserting items
list.Insert(1, "inserted");                 // Insert at index
list.InsertRange(2, ["x", "y"]);           // Insert range at index

// Removing items
list.Remove("item");                        // Remove by value
list.Remove(["a", "b"]);                   // Remove multiple items
list.RemoveAt(0);                           // Remove at index
list.RemoveRange(0, 2);                     // Remove range
list.RemoveMany(x => x.StartsWith("a"));   // Remove matching predicate
list.Clear();                               // Remove all items

// Updating items
list.Update("old", "new");                  // Replace specific item

// Replacing all items
list.ReplaceAll(["new", "items"]);         // Clear and add atomically

// Moving items
list.Move(0, 2);                           // Move item from index 0 to index 2

// Accessing items
var item = list[0];                         // Get by index
list[0] = "updated";                        // Set by index
var index = list.IndexOf("item");           // Find index
var contains = list.Contains("item");       // Check existence
var count = list.Count;                     // Get count
```

### Batch Operations with Edit

The `Edit` method performs multiple operations atomically with a single change notification:

```csharp
var list = new ReactiveList<int>([1, 2, 3, 4, 5]);

// All changes result in a single notification
list.Edit(l =>
{
    l.Add(6);
    l.RemoveAt(0);
    l.Insert(2, 100);
    l.AddRange([7, 8, 9]);
    l.Move(0, 3);
});
```

### Reactive Subscriptions

```csharp
var list = new ReactiveList<string>();

// Subscribe to items added
list.Added.Subscribe(added =>
{
    foreach (var item in added)
        Console.WriteLine($"Added: {item}");
});

// Subscribe to items removed
list.Removed.Subscribe(removed =>
{
    Console.WriteLine($"Removed {removed.Count()} items");
});

// Subscribe to any change
list.Changed.Subscribe(changed =>
{
    Console.WriteLine($"Changed: {string.Join(", ", changed)}");
});

// Subscribe to current items snapshot
list.CurrentItems.Subscribe(items =>
{
    Console.WriteLine($"Current count: {items.Count()}");
});

// Subscribe to the Stream property for detailed change notifications
list.Stream.Subscribe(notification =>
{
    Console.WriteLine($"Action: {notification.Action}, Item: {notification.Item}");
});

// Or use Connect() extension for ChangeSet-based processing (DynamicData-compatible)
list.Connect().Subscribe(changeSet =>
{
    foreach (var change in changeSet)
    {
        Console.WriteLine($"Reason: {change.Reason}, Item: {change.Current}, Index: {change.CurrentIndex}");
    }
});
```

### Change Tracking

Access change information via collections:

```csharp
var list = new ReactiveList<string>();

list.AddRange(["one", "two", "three"]);
Console.WriteLine($"Items Added: {list.ItemsAdded.Count}");       // 3
Console.WriteLine($"Items Changed: {list.ItemsChanged.Count}");   // 3
Console.WriteLine($"Items Removed: {list.ItemsRemoved.Count}");   // 0

list.Remove("two");
Console.WriteLine($"Items Removed: {list.ItemsRemoved.Count}");   // 1
```

### ReactiveList Extension Methods

#### Change Stream Filtering

```csharp
// Filter changes by predicate
list.Connect()
    .WhereChanges(change => change.Current.StartsWith("A"))
    .Subscribe(changeSet => { });

// Filter by change reason
list.Connect()
    .WhereReason(ChangeReason.Add)
    .Subscribe(changeSet => { });

// Subscribe to specific change types
list.Connect().OnAdd().Subscribe(item => Console.WriteLine($"Added: {item}"));
list.Connect().OnRemove().Subscribe(item => Console.WriteLine($"Removed: {item}"));
list.Connect().OnUpdate().Subscribe(tuple => Console.WriteLine($"Updated: {tuple.Previous} -> {tuple.Current}"));
list.Connect().OnMove().Subscribe(tuple => Console.WriteLine($"Moved: {tuple.Item} from {tuple.OldIndex} to {tuple.NewIndex}"));
```

#### Creating Views (.NET 6+)

```csharp
// Create a filtered view
var activeUsers = list.CreateView(user => user.IsActive, scheduler: RxApp.MainThreadScheduler, throttleMs: 50);

// Create an unfiltered view
var allUsers = list.CreateView(scheduler: RxApp.MainThreadScheduler);

// Create a dynamic filtered view (filter changes over time)
var searchText = new BehaviorSubject<string>("");
var filterObservable = searchText.Select<string, Func<User, bool>>(text => 
    user => user.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
var searchResults = list.CreateView(filterObservable, scheduler: RxApp.MainThreadScheduler);

// Create a sorted view
var sortedByName = list.SortBy(user => user.Name);
var sortedDescending = list.SortBy(user => user.Age, descending: true);
var sortedWithComparer = list.SortBy(Comparer<User>.Create((a, b) => a.Name.CompareTo(b.Name)));

// Create a grouped view
var groupedByDepartment = list.GroupBy(user => user.Department);
```

#### Auto-Refresh

```csharp
// Automatically refresh when property changes (for INotifyPropertyChanged items)
list.Connect()
    .AutoRefresh("Name")
    .Subscribe(changeSet => { });
```

#### Transformation

```csharp
// Project changes
list.Connect()
    .SelectChanges(item => item.Name)
    .Subscribe(name => Console.WriteLine(name));

// Project with full change metadata
list.Connect()
    .SelectChanges(change => $"{change.Reason}: {change.Current}")
    .Subscribe(description => Console.WriteLine(description));

// Group changes by key
list.Connect()
    .GroupByChanges(item => item.Category)
    .Subscribe(grouping => Console.WriteLine($"Group {grouping.Key}: {grouping.Count()} items"));
```

---

## Reactive2DList&lt;T&gt;

A two-dimensional reactive list for managing grid-like or tabular data.

```csharp
// Create from nested collections
var grid = new Reactive2DList<int>(new[]
{
    new[] { 1, 2, 3 },
    new[] { 4, 5, 6 },
    new[] { 7, 8, 9 }
});

// Access items
var item = grid.GetItem(1, 0);  // Row 1, Column 0 = 4
var row = grid[0];               // First row as ReactiveList<int>

// Set items
grid.SetItem(2, 1, 100);         // Set row 2, column 1 to 100

// Add to inner lists
grid.AddToInner(0, 10);          // Add 10 to first row
grid.AddToInner(1, [11, 12]);    // Add multiple to second row

// Remove from inner lists
grid.RemoveFromInner(0, 2);      // Remove item at index 2 from first row
grid.ClearInner(1);              // Clear second row

// Insert rows
grid.Insert(0, [100, 200]);      // Insert new row at index 0
grid.Insert(1, 999);             // Insert single-element row

// Utility methods
var totalCount = grid.TotalCount();          // Total items across all rows
var flattened = grid.Flatten().ToList();     // All items as flat list
```

---

## QuaternaryList&lt;T&gt;

High-performance, thread-safe, sharded list optimized for large datasets (.NET 8+).

### Key Features

- **Sharded architecture**: Data distributed across 4 partitions for parallel access
- **Thread-safe**: Uses `ReaderWriterLockSlim` with fine-grained locking
- **Low allocation**: Uses `ArrayPool<T>` and custom pooled collections
- **Secondary indices**: O(1) lookup by custom keys
- **Batch operations**: Efficient bulk add/remove with parallel processing

### Basic Operations

```csharp
using CP.Reactive.Quaternary;

var list = new QuaternaryList<Contact>();

// Add items
list.Add(new Contact("Alice", "Engineering"));
list.AddRange(contacts);

// Remove items
list.Remove(contact);
list.RemoveRange(contactsToRemove);
list.RemoveMany(c => c.Department == "HR");

// Check existence
var exists = list.Contains(contact);

// Batch operations (single notification)
list.Edit(l =>
{
    l.Add(new Contact("Bob", "Sales"));
    l.Clear();
    l.Add(new Contact("Charlie", "Engineering"));
});

// Replace all
list.ReplaceAll(newContacts);

// Copy to array
var array = new Contact[list.Count];
list.CopyTo(array, 0);
```

### Secondary Indices

```csharp
var list = new QuaternaryList<Contact>();

// Add indices for O(1) lookups
list.AddIndex("ByDepartment", c => c.Department);
list.AddIndex("ByCity", c => c.City);

// Bulk add
list.AddRange(contacts);

// Fast O(1) query by index
var engineers = list.GetItemsBySecondaryIndex("ByDepartment", "Engineering");
var newYorkers = list.GetItemsBySecondaryIndex("ByCity", "New York");

// Check if item matches index
bool isEngineer = list.ItemMatchesSecondaryIndex("ByDepartment", contact, "Engineering");
```

### Reactive Streams

```csharp
// Subscribe to all changes
list.Stream.Subscribe(notification =>
{
    switch (notification.Action)
    {
        case CacheAction.Added:
            Console.WriteLine($"Added: {notification.Item}");
            break;
        case CacheAction.Removed:
            Console.WriteLine($"Removed: {notification.Item}");
            break;
        case CacheAction.Cleared:
            Console.WriteLine("Collection cleared");
            break;
        case CacheAction.BatchAdded:
        case CacheAction.BatchRemoved:
        case CacheAction.BatchOperation:
            Console.WriteLine($"Batch: {notification.Batch?.Count} items");
            break;
    }
});

// Version tracking for change detection
long initialVersion = list.Version;
list.Add(contact);
if (list.Version != initialVersion)
{
    Console.WriteLine("Collection changed!");
}
```

### Creating Views

```csharp
using System.Reactive.Concurrency;

// Create an unfiltered view
var allContactsView = list.CreateView(RxApp.MainThreadScheduler, throttleMs: 100);

// Create a filtered view
var activeView = list.CreateView(c => c.IsActive, RxApp.MainThreadScheduler, throttleMs: 100);

// Create a view filtered by secondary index
var engineersView = list.CreateViewBySecondaryIndex<Contact, string>(
    "ByDepartment", 
    "Engineering",
    RxApp.MainThreadScheduler,
    throttleMs: 100);

// Create a view with multiple index keys
var techDeptView = list.CreateViewBySecondaryIndex<Contact, string>(
    "ByDepartment",
    new[] { "Engineering", "QA", "DevOps" },
    RxApp.MainThreadScheduler,
    throttleMs: 100);

// Dynamic filtering with observable
var departmentFilter = new BehaviorSubject<string[]>(new[] { "Engineering" });
var dynamicView = list.CreateViewBySecondaryIndex(
    "ByDepartment",
    departmentFilter,
    RxApp.MainThreadScheduler);

// Change filter dynamically
departmentFilter.OnNext(new[] { "HR", "Finance" });
```

### Stream Filtering Extensions

```csharp
// Filter stream by secondary index
var engineeringStream = list.Stream
    .FilterBySecondaryIndex(list, "ByDepartment", "Engineering");

// Filter stream by multiple keys
var techStream = list.Stream
    .FilterBySecondaryIndex(list, "ByDepartment", "Engineering", "QA", "DevOps");

// Dynamic stream filtering
var filterObservable = new BehaviorSubject<Func<Contact, bool>>(c => c.IsActive);
var filteredStream = list.Stream.FilterDynamic(filterObservable);

// Filter items in stream
var activeStream = list.Stream.WhereItems(c => c.IsActive);
```

---

## QuaternaryDictionary&lt;TKey, TValue&gt;

High-performance, thread-safe, sharded dictionary optimized for large datasets (.NET 8+).

### Basic Operations

```csharp
using CP.Reactive.Quaternary;

var dict = new QuaternaryDictionary<Guid, User>();

// Add items
dict.Add(user.Id, user);                    // Throws if key exists
dict.TryAdd(user.Id, user);                 // Returns false if key exists
dict.AddOrUpdate(user.Id, user);            // Add or update
dict.AddRange(users.Select(u => KeyValuePair.Create(u.Id, u)));

// Access items
if (dict.TryGetValue(userId, out var user))
{
    Console.WriteLine(user.Name);
}

// Using Lookup (returns tuple)
var result = dict.Lookup(userId);
if (result.HasValue)
{
    Console.WriteLine(result.Value.Name);
}

// Indexer access
var user = dict[userId];                    // Throws if not found
dict[userId] = updatedUser;                 // Add or update

// Remove items
dict.Remove(userId);
dict.RemoveKeys(userIds);
dict.RemoveMany(kvp => kvp.Value.IsDeleted);
dict.Clear();

// Check existence
var exists = dict.ContainsKey(userId);

// Enumerate
foreach (var key in dict.Keys) { }
foreach (var value in dict.Values) { }
foreach (var kvp in dict) { }
```

### Batch Operations

```csharp
// Batch edit (single notification)
dict.Edit(d =>
{
    d[Guid.NewGuid()] = new User("Alice");
    d[Guid.NewGuid()] = new User("Bob");
    d.Remove(oldUserId);
});
```

### Secondary Value Indices

```csharp
var dict = new QuaternaryDictionary<Guid, User>();

// Add index on value property
dict.AddValueIndex("ByDepartment", u => u.Department);
dict.AddValueIndex("ByRole", u => u.Role);

// Query by secondary index
var engineers = dict.GetValuesBySecondaryIndex("ByDepartment", "Engineering");

// Check if value matches index
bool isEngineer = dict.ValueMatchesSecondaryIndex("ByDepartment", user, "Engineering");
```

### Reactive Streams

```csharp
// Subscribe to changes
dict.Stream.Subscribe(notification =>
{
    switch (notification.Action)
    {
        case CacheAction.Added:
            Console.WriteLine($"Added: {notification.Item.Key} = {notification.Item.Value}");
            break;
        case CacheAction.Removed:
            Console.WriteLine($"Removed: {notification.Item.Key}");
            break;
    }
});

// Version tracking
long version = dict.Version;
```

### Creating Views

```csharp
// Create an unfiltered view
var allUsersView = dict.CreateView(RxApp.MainThreadScheduler);

// Create a filtered view
var activeUsersView = dict.CreateView(
    kvp => kvp.Value.IsActive,
    RxApp.MainThreadScheduler,
    throttleMs: 100);

// Create a view filtered by secondary value index
var engineersView = dict.CreateViewBySecondaryIndex<Guid, User, string>(
    "ByDepartment",
    "Engineering",
    RxApp.MainThreadScheduler);

// Create a view with multiple index keys
var techView = dict.CreateViewBySecondaryIndex<Guid, User, string>(
    "ByDepartment",
    new[] { "Engineering", "QA" },
    RxApp.MainThreadScheduler);

// Dynamic view with observable keys
var deptFilter = new BehaviorSubject<string[]>(new[] { "Engineering" });
var dynamicView = dict.CreateViewBySecondaryIndex(
    "ByDepartment",
    deptFilter,
    RxApp.MainThreadScheduler);
```

### Stream Filtering Extensions

```csharp
// Filter stream by secondary value index
var engineeringStream = dict.Stream
    .FilterBySecondaryIndex(dict, "ByDepartment", "Engineering");

// Filter by multiple keys
var techStream = dict.Stream
    .FilterBySecondaryIndex(dict, "ByDepartment", "Engineering", "QA");
```

---

## Benchmark Results

### QuaternaryList vs SourceList (DynamicData)

**Performance Comparison (10,000 items)**

| Operation | QuaternaryList | SourceList | Improvement |
|-----------|----------------|------------|-------------|
| RemoveRange | 1,423 μs | 23,708 μs | **16.7x faster** |
| RemoveMany | 1,453 μs | 10,491 μs | **7.2x faster** |
| Edit (batch) | 127 μs | 155 μs | **1.2x faster** |
| Clear | 154 μs | 150 μs | ~Same |
| ReplaceAll | 139 μs | 145 μs | ~Same |
| Stream (Add) | 117 μs | 76 μs | SourceList faster |
| AddRange | 92 μs | 75 μs | SourceList faster |

**Memory Allocation (10,000 items)**

| Operation | QuaternaryList | SourceList | Improvement |
|-----------|----------------|------------|-------------|
| RemoveRange | 75 KB | 2,373 KB | **31.6x less** |
| RemoveMany | 72 KB | 2,759 KB | **38.3x less** |
| Edit | 72 KB | 344 KB | **4.8x less** |
| Clear | 72 KB | 253 KB | **3.5x less** |
| ReplaceAll | 112 KB | 293 KB | **2.6x less** |
| Stream | 139 KB | 173 KB | **1.2x less** |

### QuaternaryDictionary vs SourceCache (DynamicData)

**Performance Comparison (10,000 items)**

| Operation | QuaternaryDict | SourceCache | Improvement |
|-----------|----------------|-------------|-------------|
| Stream (Add) | 264 μs | 1,107 μs | **4.2x faster** |
| Clear | 168 μs | 406 μs | **2.4x faster** |
| Lookup | 191 μs | 402 μs | **2.1x faster** |
| AddRange | 152 μs | 352 μs | **2.3x faster** |
| RemoveKeys | 269 μs | N/A | N/A |

**Memory Allocation (10,000 items)**

| Operation | QuaternaryDict | SourceCache | Improvement |
|-----------|----------------|-------------|-------------|
| Stream | 456 KB | 2,438 KB | **5.3x less** |
| Clear | 327 KB | 1,156 KB | **3.5x less** |
| Lookup | 327 KB | 1,156 KB | **3.5x less** |
| AddRange | 327 KB | 1,156 KB | **3.5x less** |

### When to Use Which Collection

**Use QuaternaryList/QuaternaryDictionary when:**
- Large datasets (>1,000 items)
- Bulk removal operations (up to 17x faster)
- Memory-constrained environments (2-38x less allocation)
- High-concurrency scenarios
- Secondary index lookups needed

**Use ReactiveList/DynamicData when:**
- Small datasets (<500 items)
- Rich LINQ-like query operators needed
- Existing DynamicData integration

---

## Full API Reference

### IReactiveList&lt;T&gt;

**Interfaces:** `IList<T>`, `IList`, `IReadOnlyList<T>`, `INotifyCollectionChanged`, `INotifyPropertyChanged`, `ICancelable`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Items` | `ReadOnlyObservableCollection<T>` | Current items for UI binding |
| `ItemsAdded` | `ReadOnlyObservableCollection<T>` | Items added in last change |
| `ItemsRemoved` | `ReadOnlyObservableCollection<T>` | Items removed in last change |
| `ItemsChanged` | `ReadOnlyObservableCollection<T>` | Items changed in last change |
| `Added` | `IObservable<IEnumerable<T>>` | Stream of added items |
| `Removed` | `IObservable<IEnumerable<T>>` | Stream of removed items |
| `Changed` | `IObservable<IEnumerable<T>>` | Stream of changed items |
| `CurrentItems` | `IObservable<IEnumerable<T>>` | Current items snapshot stream |
| `Count` | `int` | Number of items |
| `IsDisposed` | `bool` | Whether disposed |

#### Methods

| Method | Description |
|--------|-------------|
| `Add(T)` | Add single item |
| `AddRange(IEnumerable<T>)` | Add multiple items |
| `Insert(int, T)` | Insert at index |
| `InsertRange(int, IEnumerable<T>)` | Insert range at index |
| `Remove(T)` | Remove by value |
| `Remove(IEnumerable<T>)` | Remove multiple items |
| `RemoveAt(int)` | Remove at index |
| `RemoveRange(int, int)` | Remove range |
| `RemoveMany(Func<T, bool>)` | Remove matching predicate |
| `Clear()` | Remove all items |
| `Move(int, int)` | Move item |
| `Update(T, T)` | Replace item |
| `ReplaceAll(IEnumerable<T>)` | Replace all items |
| `Edit(Action<IEditableList<T>>)` | Batch operations |
| `Dispose()` | Clean up resources |

#### IReactiveSource&lt;T&gt; Properties

| Property | Type | Description |
|----------|------|-------------|
| `Stream` | `IObservable<CacheNotify<T>>` | Primary change notification stream |
| `Version` | `long` | Increments on each change |

#### Extension Methods

| Method | Description |
|--------|-------------|
| `Connect()` | Convert Stream to ChangeSet format (DynamicData compatible) |

### IQuaternaryList&lt;T&gt; (.NET 8+)

**Interfaces:** `ICollection<T>`, `IQuaternarySource<T>`, `INotifyCollectionChanged`, `ICancelable`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count` | `int` | Total items across shards |
| `IsReadOnly` | `bool` | Always false |
| `Stream` | `IObservable<CacheNotify<T>>` | Change notifications |
| `Version` | `long` | Increments on each change |
| `IsDisposed` | `bool` | Whether disposed |

#### Methods

| Method | Description |
|--------|-------------|
| `Add(T)` | Add item to appropriate shard |
| `AddRange(IEnumerable<T>)` | Bulk add (parallel for large sets) |
| `Remove(T)` | Remove item |
| `RemoveRange(IEnumerable<T>)` | Bulk remove |
| `RemoveMany(Func<T, bool>)` | Remove matching predicate |
| `Clear()` | Clear all shards |
| `Contains(T)` | Check existence (O(1) average) |
| `Edit(Action<ICollection<T>>)` | Batch operations |
| `ReplaceAll(IEnumerable<T>)` | Replace all atomically |
| `AddIndex<TKey>(string, Func<T, TKey>)` | Add secondary index |
| `GetItemsBySecondaryIndex<TKey>(string, TKey)` | Query by index |
| `ItemMatchesSecondaryIndex<TKey>(string, T, TKey)` | Check index match |
| `CopyTo(T[], int)` | Copy to array |
| `Dispose()` | Clean up resources |

### IQuaternaryDictionary&lt;TKey, TValue&gt; (.NET 8+)

**Interfaces:** `IDictionary<TKey, TValue>`, `IQuaternarySource<KeyValuePair<TKey, TValue>>`, `INotifyCollectionChanged`, `ICancelable`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count` | `int` | Total key-value pairs |
| `Keys` | `ICollection<TKey>` | All keys |
| `Values` | `ICollection<TValue>` | All values |
| `IsReadOnly` | `bool` | Always false |
| `Stream` | `IObservable<CacheNotify<KVP>>` | Change notifications |
| `Version` | `long` | Increments on each change |
| `IsDisposed` | `bool` | Whether disposed |

#### Methods

| Method | Description |
|--------|-------------|
| `Add(TKey, TValue)` | Add (throws if exists) |
| `TryAdd(TKey, TValue)` | Add if not exists |
| `AddOrUpdate(TKey, TValue)` | Add or update |
| `AddRange(IEnumerable<KVP>)` | Bulk add/update |
| `Remove(TKey)` | Remove by key |
| `RemoveKeys(IEnumerable<TKey>)` | Bulk remove |
| `RemoveMany(Func<KVP, bool>)` | Remove matching predicate |
| `Clear()` | Remove all |
| `TryGetValue(TKey, out TValue)` | Try get value |
| `Lookup(TKey)` | Get (HasValue, Value) tuple |
| `ContainsKey(TKey)` | Check key exists |
| `Edit(Action<IDictionary<TKey, TValue>>)` | Batch operations |
| `AddValueIndex<TIndexKey>(string, Func<TValue, TIndexKey>)` | Add value index |
| `GetValuesBySecondaryIndex<TIndexKey>(string, TIndexKey)` | Query by index |
| `ValueMatchesSecondaryIndex<TIndexKey>(string, TValue, TIndexKey)` | Check index match |
| `Dispose()` | Clean up resources |

### Extension Methods

#### ReactiveList Extensions

| Method | Description |
|--------|-------------|
| `WhereChanges(Func<Change<T>, bool>)` | Filter change stream |
| `WhereReason(ChangeReason)` | Filter by change reason |
| `SelectChanges(Func<T, TResult>)` | Project changes |
| `OnAdd()` | Subscribe to adds only |
| `OnRemove()` | Subscribe to removes only |
| `OnUpdate()` | Subscribe to updates only |
| `OnMove()` | Subscribe to moves only |
| `CreateView(Func<T, bool>, IScheduler, int)` | Create filtered view |
| `CreateView(IObservable<Func<T, bool>>, IScheduler, int)` | Create dynamic filtered view |
| `SortBy(IComparer<T>, IScheduler, int)` | Create sorted view |
| `SortBy(Func<T, TKey>, bool, IScheduler, int)` | Create sorted view by key |
| `GroupBy(Func<T, TKey>, IScheduler, int)` | Create grouped view |
| `GroupByChanges(Func<T, TKey>)` | Group change stream |
| `AutoRefresh(string)` | Auto-refresh on property change |

#### QuaternaryList Extensions

| Method | Description |
|--------|-------------|
| `CreateView(IScheduler, int)` | Create unfiltered view |
| `CreateView(Func<T, bool>, IScheduler, int)` | Create filtered view |
| `CreateView(IObservable<Func<T, bool>>, IScheduler, int)` | Create dynamic view |
| `CreateViewBySecondaryIndex<TKey>(string, TKey, IScheduler, int)` | View by index key |
| `CreateViewBySecondaryIndex<TKey>(string, TKey[], IScheduler, int)` | View by multiple keys |
| `CreateViewBySecondaryIndex<TKey>(string, IObservable<TKey[]>, IScheduler, int)` | Dynamic index view |
| `FilterBySecondaryIndex<TKey>(stream, list, string, TKey)` | Filter stream by index |
| `FilterBySecondaryIndex<TKey>(stream, list, string, params TKey[])` | Filter stream by keys |
| `FilterDynamic(stream, IObservable<Func<T, bool>>)` | Dynamic stream filter |
| `WhereItems(stream, Func<T, bool>)` | Filter stream items |
| `AutoRefresh(Expression<Func<T, object>>)` | Auto-refresh view |

#### QuaternaryDictionary Extensions

| Method | Description |
|--------|-------------|
| `CreateView(IScheduler, int)` | Create unfiltered view |
| `CreateView(Func<KVP, bool>, IScheduler, int)` | Create filtered view |
| `CreateView(IObservable<Func<KVP, bool>>, IScheduler, int)` | Create dynamic view |
| `CreateViewBySecondaryIndex<TIndexKey>(string, TIndexKey, IScheduler, int)` | View by value index |
| `CreateViewBySecondaryIndex<TIndexKey>(string, TIndexKey[], IScheduler, int)` | View by multiple keys |
| `CreateViewBySecondaryIndex<TIndexKey>(string, IObservable<TIndexKey[]>, IScheduler, int)` | Dynamic index view |
| `FilterBySecondaryIndex<TIndexKey>(stream, dict, string, TIndexKey)` | Filter stream |
| `FilterBySecondaryIndex<TIndexKey>(stream, dict, string, params TIndexKey[])` | Filter stream by keys |

---

## UI Binding

### WPF / WinUI

```csharp
public class MainViewModel : IDisposable
{
    public IReactiveList<string> Items { get; } = new ReactiveList<string>();
    
    public void Dispose() => Items.Dispose();
}
```

```xml
<ListBox ItemsSource="{Binding Items}" />
<!-- or -->
<ListBox ItemsSource="{Binding Items.Items}" />
```

### With QuaternaryList

```csharp
public class MainViewModel : IDisposable
{
    private readonly QuaternaryList<Contact> _contacts = new();
    
    public ReadOnlyObservableCollection<Contact> Contacts { get; }
    
    public MainViewModel()
    {
        var view = _contacts.CreateView(RxApp.MainThreadScheduler);
        view.ToProperty(x => Contacts = x);
    }
    
    public void Dispose() => _contacts.Dispose();
}
```

---

## Use Cases

### Real-time Data Feed

```csharp
public class StockTickerViewModel : IDisposable
{
    private readonly QuaternaryDictionary<string, StockPrice> _prices = new();
    
    public StockTickerViewModel(IObservable<StockPrice> feed)
    {
        _prices.AddValueIndex("BySector", p => p.Sector);
        
        feed.ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(price => _prices.AddOrUpdate(price.Symbol, price));
    }
    
    public IEnumerable<StockPrice> GetBySector(string sector) =>
        _prices.GetValuesBySecondaryIndex("BySector", sector);
    
    public void Dispose() => _prices.Dispose();
}
```

### Address Book with Secondary Indices

```csharp
public class AddressBookViewModel : IDisposable
{
    private readonly QuaternaryList<Contact> _contacts = new();
    
    public AddressBookViewModel()
    {
        _contacts.AddIndex("ByCity", c => c.City);
        _contacts.AddIndex("ByDepartment", c => c.Department);
    }
    
    public void BulkImport(IEnumerable<Contact> contacts) =>
        _contacts.AddRange(contacts);
    
    public IEnumerable<Contact> GetByCity(string city) =>
        _contacts.GetItemsBySecondaryIndex("ByCity", city);
    
    public int RemoveByDepartment(string dept)
    {
        var targets = _contacts.GetItemsBySecondaryIndex("ByDepartment", dept).ToList();
        _contacts.RemoveRange(targets);
        return targets.Count;
    }
    
    public void Dispose() => _contacts.Dispose();
}
```

---

## Migrating from DynamicData

| DynamicData | ReactiveList |
|-------------|--------------|
| `SourceList<T>` | `ReactiveList<T>` or `QuaternaryList<T>` |
| `SourceCache<T, TKey>` | `QuaternaryDictionary<TKey, T>` |
| `Connect()` | `Stream` property or `Connect()` extension method |
| `Filter()` | `CreateView()` or `WhereChanges()` |
| `Transform()` | `SelectChanges()` |
| `Bind()` | `CreateView().ToProperty()` |
| `AddOrUpdate(item)` | `AddOrUpdate(key, value)` |
| `Edit(inner => ...)` | `Edit(inner => ...)` |

### Stream vs Connect

ReactiveList provides two ways to observe changes:

1. **Stream property** - Returns `IObservable<CacheNotify<T>>` with action-based notifications
   - Lower allocations for high-throughput scenarios
   - Direct access to batch operations
   - Use `Stream.ToChangeSets()` to convert to ChangeSet format

2. **Connect() extension** - Returns `IObservable<ChangeSet<T>>` (DynamicData-compatible)
   - Familiar API for DynamicData users
   - Works with existing ChangeSet-based operators

```csharp
// Using Stream directly
list.Stream.Subscribe(notification =>
{
    switch (notification.Action)
    {
        case CacheAction.Added:
            Console.WriteLine($"Added: {notification.Item}");
            break;
        case CacheAction.BatchAdded:
            Console.WriteLine($"Batch added: {notification.Batch?.Count} items");
            break;
    }
});

// Using Connect() for ChangeSet-based processing
list.Connect()
    .WhereReason(ChangeReason.Add)
    .Subscribe(changeSet => { });
```

---

## License

[MIT](LICENSE)

---

**ReactiveList** - Empowering Reactive Applications with Observable Collections ⚡🚀
