# ReactiveList

[![NuGet](https://img.shields.io/nuget/v/ReactiveList.svg?style=flat-square)](https://www.nuget.org/packages/ReactiveList/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ReactiveList.svg?style=flat-square)](https://www.nuget.org/packages/ReactiveList/)
[![License](https://img.shields.io/github/license/ChrisPulman/ReactiveList.svg?style=flat-square)](LICENSE)
[![Build Status](https://img.shields.io/github/actions/workflow/status/ChrisPulman/ReactiveList/BuildOnly.yml?branch=main&style=flat-square)](https://github.com/ChrisPulman/ReactiveList/actions)

A high-performance, thread-safe, observable collection library for .NET that combines the power of reactive extensions with standard list and dictionary operations. ReactiveList provides real-time change notifications, making it ideal for data-binding, reactive programming, and scenarios where collection changes need to be tracked and responded to—especially with continuous live data streams.

---

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
  - [The Stream Property](#the-stream-property)
  - [CacheNotify and CacheAction](#cachenotify-and-cacheaction)
  - [ChangeSet and Change](#changeset-and-change)
- [Collections](#collections)
  - [ReactiveList\<T\>](#reactivelistt)
  - [Reactive2DList\<T\>](#reactive2dlistt)
  - [QuaternaryList\<T\>](#quaternarylistt)
  - [QuaternaryDictionary\<TKey, TValue\>](#quaternarydictionarytkey-tvalue)
- [Views](#views)
  - [FilteredReactiveView\<T\>](#filteredreactiveviewt)
  - [SortedReactiveView\<T\>](#sortedreactiveviewt)
  - [GroupedReactiveView\<T, TKey\>](#groupedreactiveviewt-tkey)
  - [DynamicFilteredReactiveView\<T\>](#dynamicfilteredreactiveviewt)
  - [DynamicReactiveView\<T\>](#dynamicreactiveviewt)
  - [ReactiveView\<T\>](#reactiveviewt)
  - [Secondary Index Views](#secondary-index-views)
- [Extension Methods Reference](#extension-methods-reference)
  - [Stream Extensions](#stream-extensions-cachenotifyextensions)
  - [ChangeSet Extensions](#changeset-extensions-reactivelistextensions)
  - [View Creation Extensions](#view-creation-extensions)
- [API Reference](#api-reference)
  - [Core Contracts](#core-contracts)
  - [Core Records and Batches](#core-records-and-batches)
  - [Collection-Specific APIs](#collection-specific-apis)
  - [View APIs](#view-apis)
  - [Advanced Shard APIs](#advanced-shard-apis)
- [Real-World Examples](#real-world-examples)
  - [Live Stock Ticker](#live-stock-ticker)
  - [IoT Sensor Dashboard](#iot-sensor-dashboard)
  - [Chat Application](#chat-application)
  - [WPF Data Binding](#wpf-data-binding)
  - [Avalonia UI Data Binding](#avalonia-ui-data-binding)
- [Thread Safety](#thread-safety)
- [Performance Considerations](#performance-considerations)
- [Benchmark Results](#benchmark-results)
- [License](#license)

---

## Features

- **Thread-Safe Operations**: All public methods are thread-safe with proper synchronization
- **Reactive Notifications**: Observe additions, removals, updates, moves, and clears in real-time via `IObservable<T>`
- **Batch Operations**: Efficient `AddRange`, `RemoveRange`, `InsertRange`, `RemoveMany`, and `ReplaceAll` methods
- **Edit Transactions**: Batch multiple operations with single notification via `Edit()`
- **Views**: Create filtered, sorted, grouped, and secondary-indexed views that auto-update
- **Change Sets**: Fine-grained change tracking with `ChangeSet<T>` for DynamicData-compatible processing
- **Secondary Indices**: O(1) lookups by custom keys for high-performance filtering
- **AOT Compatible**: Supports Native AOT on .NET 8+
- **Cross-Platform**: Targets `net462`, `net472`, `net48`, `net481`, `net8.0`, `net9.0`, `net10.0`, and matching Windows TFMs
- **High Performance**: QuaternaryList/Dictionary provide low-allocation sharded storage and outperform DynamicData in the measured large-list and dictionary benchmarks

---

## Installation

```shell
dotnet add package CP.ReactiveList
```

**Required Dependencies:**
- `System.Reactive` (automatically included)

**Namespaces:**
```csharp
using CP.Reactive;             // Extension methods
using CP.Reactive.Collections; // Collections (ReactiveList, Reactive2DList, QuaternaryList, QuaternaryDictionary)
using CP.Reactive.Core;        // Core types (CacheNotify, ChangeSet, Change, CacheAction, ChangeReason)
using CP.Reactive.Views;       // View types (FilteredReactiveView, SortedReactiveView, etc.)
```

---

## Quick Start

### Basic Usage

```csharp
using CP.Reactive.Collections;
using System.Reactive.Linq;

// Create a reactive list
var sensorReadings = new ReactiveList<double>();

// Subscribe to additions - great for logging/monitoring
sensorReadings.Added.Subscribe(readings => 
    Console.WriteLine($"New readings: {string.Join(", ", readings:F2)}"));

// Subscribe to removals
sensorReadings.Removed.Subscribe(readings => 
    Console.WriteLine($"Removed: {string.Join(", ", readings)}"));

// Simulate live sensor data arriving
sensorReadings.Add(23.5);
sensorReadings.AddRange([24.1, 23.8, 24.5]);

// Remove old readings
sensorReadings.Remove(23.5);
```

### Observing All Changes with Stream

The `Stream` property is the primary way to observe all collection changes:

```csharp
using CP.Reactive.Collections;
using CP.Reactive.Core;

var orders = new ReactiveList<Order>();

// Subscribe to the unified change stream
orders.Stream.Subscribe(notification =>
{
    switch (notification.Action)
    {
        case CacheAction.Added:
            Console.WriteLine($"Order added: {notification.Item.Id}");
            break;
        case CacheAction.Removed:
            Console.WriteLine($"Order removed: {notification.Item.Id}");
            break;
        case CacheAction.BatchAdded:
            Console.WriteLine($"Batch of {notification.Batch?.Count} orders added");
            break;
        case CacheAction.Cleared:
            Console.WriteLine("All orders cleared");
            break;
    }
});

orders.Add(new Order { Id = 1, Amount = 100 });
orders.AddRange([new Order { Id = 2 }, new Order { Id = 3 }]);
orders.Clear();
```

### Batch Edit Operations

Use `Edit()` to batch multiple operations and emit a single notification:

```csharp
var products = new ReactiveList<Product>();

// Multiple operations, single notification at the end
products.Edit(editor =>
{
    editor.Add(new Product { Id = 1, Name = "Widget" });
    editor.Add(new Product { Id = 2, Name = "Gadget" });
    editor.Add(new Product { Id = 3, Name = "Gizmo" });
    editor.RemoveAt(0);  // Remove first item
});
// Only ONE change notification is emitted after Edit() completes
```

---

## Core Concepts

### The Stream Property

Every reactive collection exposes a `Stream` property that emits `CacheNotify<T>` notifications for all changes. This is the most flexible way to observe collection changes.

```csharp
IObservable<CacheNotify<T>> Stream { get; }
```

### CacheNotify and CacheAction

`CacheNotify<T>` is a record that describes what changed:

```csharp
public sealed record CacheNotify<T>(
    CacheAction Action,           // What happened (Added, Removed, Updated, etc.)
    T? Item,                      // The item involved (for single-item operations)
    PooledBatch<T>? Batch = null, // Batch of items (for batch operations)
    int CurrentIndex = -1,        // Current index of the item
    int PreviousIndex = -1,       // Previous index (for moves)
    T? Previous = default         // Previous value (for updates)
);
```

**CacheAction Enumeration:**

| Action | Description |
|--------|-------------|
| `Added` | Single item was added |
| `Removed` | Single item was removed |
| `Updated` | Item was updated/replaced |
| `Moved` | Item was moved to a different index |
| `Refreshed` | Item was refreshed (re-evaluated) |
| `Cleared` | Collection was cleared |
| `BatchOperation` | Multiple mixed operations occurred |
| `BatchAdded` | Multiple items were added at once |
| `BatchRemoved` | Multiple items were removed at once |

### ChangeSet and Change

For DynamicData-style processing, convert the stream to `ChangeSet<T>`:

```csharp
var list = new ReactiveList<string>();

// Convert stream to change sets
list.Stream
    .ToChangeSets()  // IObservable<ChangeSet<T>>
    .Subscribe(changeSet =>
    {
        Console.WriteLine($"Changes: {changeSet.Adds} adds, {changeSet.Removes} removes");
        
        foreach (var change in changeSet)
        {
            Console.WriteLine($"  {change.Reason}: {change.Current}");
        }
    });
```

**ChangeReason Enumeration:**

| Reason | Description |
|--------|-------------|
| `Add` | Item was added |
| `Remove` | Item was removed |
| `Update` | Item was updated |
| `Move` | Item was moved |
| `Refresh` | Item was refreshed |
| `Clear` | Collection was cleared |

---

## Collections

### ReactiveList\<T\>

A thread-safe, observable list that provides reactive notifications for all changes.

#### Constructor Overloads

```csharp
// Empty list
var list = new ReactiveList<string>();

// Initialize with items
var list = new ReactiveList<string>(["apple", "banana", "cherry"]);

// Initialize with single item
var list = new ReactiveList<string>("hello");
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count` | `int` | Number of items in the list |
| `Items` | `ReadOnlyObservableCollection<T>` | Bindable read-only collection |
| `ItemsAdded` | `ReadOnlyObservableCollection<T>` | Items added during last change |
| `ItemsRemoved` | `ReadOnlyObservableCollection<T>` | Items removed during last change |
| `ItemsChanged` | `ReadOnlyObservableCollection<T>` | Items changed during last change |
| `Stream` | `IObservable<CacheNotify<T>>` | Primary change notification stream |
| `Added` | `IObservable<IEnumerable<T>>` | Observable of added items |
| `Removed` | `IObservable<IEnumerable<T>>` | Observable of removed items |
| `Changed` | `IObservable<IEnumerable<T>>` | Observable of changed items |
| `CurrentItems` | `IObservable<IEnumerable<T>>` | Emits current items on subscription and after changes |
| `Version` | `long` | Incremented on each modification (atomic) |
| `IsDisposed` | `bool` | Whether the list has been disposed |

#### Methods

**Adding Items:**

```csharp
var list = new ReactiveList<Order>();

// Add single item
list.Add(new Order { Id = 1 });

// Add multiple items
list.AddRange([new Order { Id = 2 }, new Order { Id = 3 }]);

// Insert at specific index
list.Insert(0, new Order { Id = 0 });

// Insert range at specific index
list.InsertRange(2, [new Order { Id = 10 }, new Order { Id = 11 }]);

// .NET 6+: Add from span (zero-copy when possible)
ReadOnlySpan<Order> orders = stackalloc[] { new Order { Id = 100 } };
list.AddRange(orders);
```

**Removing Items:**

```csharp
// Remove specific item
bool removed = list.Remove(orderToRemove);

// Remove at index
list.RemoveAt(0);

// Remove range by index and count
list.RemoveRange(startIndex: 2, count: 3);

// Remove multiple items matching predicate
int removedCount = list.RemoveMany(order => order.Amount < 10);

// Remove all items matching condition
list.Remove(list.Where(o => o.IsCancelled));

// Clear all items
list.Clear();

// .NET 6+: Clear without deallocating internal array (performance optimization)
list.ClearWithoutDeallocation();
```

**Updating Items:**

```csharp
// Replace item at index
list[0] = new Order { Id = 999 };

// Update specific item
list.Update(oldOrder, newOrder);

// Move item to new position
list.Move(oldIndex: 0, newIndex: 5);
```

**Batch Operations:**

```csharp
// Edit transaction - single notification for multiple operations
list.Edit(editor =>
{
    editor.Add(new Order { Id = 1 });
    editor.Add(new Order { Id = 2 });
    editor.RemoveAt(0);
    editor.Insert(0, new Order { Id = 0 });
});

// Replace all items atomically
list.ReplaceAll([new Order { Id = 100 }, new Order { Id = 200 }]);
```

**Querying:**

```csharp
// Check if item exists
bool exists = list.Contains(order);

// Find index of item
int index = list.IndexOf(order);

// Access by index
Order order = list[0];

// Enumerate (thread-safe snapshot)
foreach (var item in list)
{
    Console.WriteLine(item);
}

// .NET 6+: Get as array
Order[] array = list.ToArray();

// .NET 6+: Get as span (WARNING: not thread-safe, must ensure no concurrent modifications)
ReadOnlySpan<Order> span = list.AsSpan();

// .NET 6+: Get as memory (WARNING: not thread-safe)
ReadOnlyMemory<Order> memory = list.AsMemory();
```

#### Complete Example: Live Order Processing

```csharp
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using System.Reactive.Linq;
using System.Reactive.Disposables;

public class OrderProcessor : IDisposable
{
    private readonly ReactiveList<Order> _orders = new();
    private readonly CompositeDisposable _subscriptions = new();

    public OrderProcessor()
    {
        // Log all new orders
        _subscriptions.Add(
            _orders.Stream
                .WhereAdded()
                .SelectAllItems()
                .Subscribe(order => 
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] New order: #{order.Id} - ${order.Amount:F2}"))
        );

        // Alert on high-value orders
        _subscriptions.Add(
            _orders.Stream
                .OnItemAdded()
                .Where(order => order.Amount > 1000)
                .Subscribe(order => 
                    Console.WriteLine($"*** HIGH VALUE ORDER: #{order.Id} - ${order.Amount:F2} ***"))
        );

        // Track order count changes
        _subscriptions.Add(
            _orders.CurrentItems
                .Select(items => items.Count())
                .DistinctUntilChanged()
                .Subscribe(count => 
                    Console.WriteLine($"Total orders: {count}"))
        );
    }

    public void ProcessIncomingOrder(Order order) => _orders.Add(order);
    
    public void ProcessBatch(IEnumerable<Order> orders) => _orders.AddRange(orders);
    
    public void CancelOrder(int orderId)
    {
        var order = _orders.FirstOrDefault(o => o.Id == orderId);
        if (order != null)
            _orders.Remove(order);
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        _orders.Dispose();
    }
}

public record Order
{
    public int Id { get; init; }
    public decimal Amount { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
```

---

### Reactive2DList\<T\>

A two-dimensional reactive list where each element is itself a `ReactiveList<T>`. Perfect for representing tables, matrices, or hierarchical data.

#### Constructor Overloads

```csharp
// Empty 2D list
var grid = new Reactive2DList<int>();

// Initialize from jagged collections
var grid = new Reactive2DList<int>([
    [1, 2, 3],
    [4, 5, 6],
    [7, 8, 9]
]);

// Initialize from flat collection (each item becomes a row)
var grid = new Reactive2DList<string>(["row1", "row2", "row3"]);

// Initialize with single row
var grid = new Reactive2DList<int>(new ReactiveList<int>([1, 2, 3]));
```

#### Unique Methods

| Method | Description |
|--------|-------------|
| `AddToInner(outerIndex, item)` | Add item to specific inner list |
| `AddToInner(outerIndex, items)` | Add items to specific inner list |
| `GetItem(outerIndex, innerIndex)` | Get item at specific position |
| `SetItem(outerIndex, innerIndex, value)` | Set item at specific position |
| `RemoveFromInner(outerIndex, innerIndex)` | Remove item from inner list |
| `ClearInner(outerIndex)` | Clear specific inner list |
| `Flatten()` | Get all items as flat sequence |
| `TotalCount()` | Get total count of all items |
| `Insert(index, items)` | Insert new row with items |
| `Insert(index, items, innerIndex)` | Insert items into existing row |

#### Example: Spreadsheet-like Data

```csharp
using CP.Reactive.Collections;

// Create a spreadsheet-like structure
var spreadsheet = new Reactive2DList<string>();

// Add rows
spreadsheet.Add(new ReactiveList<string>(["Name", "Age", "City"]));  // Header row
spreadsheet.Add(new ReactiveList<string>(["Alice", "30", "NYC"]));
spreadsheet.Add(new ReactiveList<string>(["Bob", "25", "LA"]));

// Add data to specific cell (row 1, column 2)
spreadsheet.SetItem(1, 2, "Boston");

// Add new column to all rows
for (int row = 0; row < spreadsheet.Count; row++)
{
    spreadsheet.AddToInner(row, row == 0 ? "Country" : "USA");
}

// Get flattened data
var allCells = spreadsheet.Flatten().ToList();

// Get total cell count
int totalCells = spreadsheet.TotalCount();

// Subscribe to changes in any cell
spreadsheet.Stream.Subscribe(notification =>
{
    if (notification.Action == CacheAction.Added && notification.Item != null)
    {
        // A new row was added
        notification.Item.Stream.Subscribe(innerNotification =>
        {
            Console.WriteLine($"Cell changed: {innerNotification.Action}");
        });
    }
});
```

---

### QuaternaryList\<T\>

A high-performance, thread-safe list that partitions elements across four internal shards for efficient concurrent access. Optimized for large datasets with frequent remove operations.

> **Target support:** Available on the .NET Framework and modern .NET target frameworks supported by the package. It is best for large sets, batch operations, secondary-index lookups, and keyed or hash-distributed workloads. For very small ordered batches, `SourceList<T>` can still be marginally faster.

#### Key Advantages

- Faster and lower-allocation than DynamicData `SourceList<T>` for measured 1,000 and 10,000 item `AddRange` workloads
- Much lower memory usage at scale in current benchmarks
- Built-in **secondary indices** for O(1) lookups
- Optimized for **parallel** operations on large datasets

#### Constructor

```csharp
var list = new QuaternaryList<Product>();
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count` | `int` | Total number of items across all shards |
| `Stream` | `IObservable<CacheNotify<T>>` | Change notification stream |
| `IsReadOnly` | `bool` | Always returns `false` |

#### Methods

**Basic Operations:**

```csharp
var products = new QuaternaryList<Product>();

// Add items
products.Add(new Product { Id = 1, Category = "Electronics" });
products.AddRange([product1, product2, product3]);

// Remove items
bool removed = products.Remove(product);
products.RemoveRange([product1, product2]);

// Remove by predicate
int removedCount = products.RemoveMany(p => p.Price < 10);

// Check existence
bool exists = products.Contains(product);

// Replace all items atomically
products.ReplaceAll(newProducts);

// Batch edit
products.Edit(collection =>
{
    collection.Add(newProduct);
    collection.Clear();
    // Add more operations...
});

// Get snapshot
IReadOnlyList<Product> snapshot = products.Snapshot();
```

**Secondary Indices:**

Secondary indices enable O(1) lookups by custom keys:

```csharp
var products = new QuaternaryList<Product>();

// Add secondary index by category
products.AddIndex("ByCategory", p => p.Category);

// Add secondary index by price range
products.AddIndex("ByPriceRange", p => p.Price switch
{
    < 10 => "Budget",
    < 100 => "Standard",
    _ => "Premium"
});

// Add products
products.AddRange([
    new Product { Id = 1, Category = "Electronics", Price = 299 },
    new Product { Id = 2, Category = "Books", Price = 15 },
    new Product { Id = 3, Category = "Electronics", Price = 49 }
]);

// O(1) lookup by category
var electronics = products.GetItemsBySecondaryIndex("ByCategory", "Electronics");

// O(1) lookup by price range
var premiumProducts = products.GetItemsBySecondaryIndex("ByPriceRange", "Premium");

// Check if item matches index key
bool isElectronics = products.ItemMatchesSecondaryIndex("ByCategory", product, "Electronics");
```

**Creating Views with Secondary Index:**

```csharp
using CP.Reactive;
using System.Reactive.Concurrency;

// Create a reactive view filtered by secondary index
var electronicsView = products.CreateViewBySecondaryIndex(
    indexName: "ByCategory",
    key: "Electronics",
    scheduler: Scheduler.Default,
    throttleMs: 50
);

// View automatically updates when products change
electronicsView.Items.CollectionChanged += (s, e) =>
{
    Console.WriteLine($"Electronics view updated: {electronicsView.Count} items");
};

// Create view with multiple keys (OR logic)
var budgetAndStandardView = products.CreateViewBySecondaryIndex(
    indexName: "ByPriceRange",
    keys: ["Budget", "Standard"],
    scheduler: Scheduler.Default
);
```

#### Complete Example: Product Inventory System

```csharp
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Disposables;

public class InventorySystem : IDisposable
{
    private readonly QuaternaryList<Product> _inventory = new();
    private readonly CompositeDisposable _subscriptions = new();

    public InventorySystem()
    {
        // Setup secondary indices
        _inventory.AddIndex("ByCategory", p => p.Category);
        _inventory.AddIndex("BySku", p => p.Sku);
        _inventory.AddIndex("BySupplier", p => p.SupplierId);

        // Subscribe to low stock alerts
        _subscriptions.Add(
            _inventory.Stream
                .OnItemAdded()
                .Merge(_inventory.Stream.OnItemUpdated())
                .Where(p => p.StockQuantity < 10)
                .Subscribe(p => 
                    Console.WriteLine($"LOW STOCK ALERT: {p.Name} - Only {p.StockQuantity} left!"))
        );

        // Track inventory value changes
        _subscriptions.Add(
            _inventory.Stream
                .Throttle(TimeSpan.FromSeconds(1))
                .Select(_ => _inventory.Sum(p => p.Price * p.StockQuantity))
                .DistinctUntilChanged()
                .Subscribe(totalValue => 
                    Console.WriteLine($"Total inventory value: ${totalValue:N2}"))
        );
    }

    // O(1) lookup by SKU
    public Product? GetBySku(string sku) =>
        _inventory.GetItemsBySecondaryIndex("BySku", sku).FirstOrDefault();

    // O(1) lookup by category
    public IEnumerable<Product> GetByCategory(string category) =>
        _inventory.GetItemsBySecondaryIndex("ByCategory", category);

    // Efficient bulk operations
    public void RestockFromSupplier(int supplierId, Dictionary<string, int> quantities)
    {
        var supplierProducts = _inventory.GetItemsBySecondaryIndex("BySupplier", supplierId);
        
        _inventory.Edit(collection =>
        {
            foreach (var product in supplierProducts)
            {
                if (quantities.TryGetValue(product.Sku, out int qty))
                {
                    // Update stock (would need to handle immutability appropriately)
                    collection.Remove(product);
                    collection.Add(product with { StockQuantity = product.StockQuantity + qty });
                }
            }
        });
    }

    // Create reactive view for specific category
    public ReactiveView<Product> CreateCategoryView(string category) =>
        _inventory.CreateViewBySecondaryIndex("ByCategory", category, Scheduler.Default);

    public void Dispose()
    {
        _subscriptions.Dispose();
        _inventory.Dispose();
    }
}

public record Product
{
    public int Id { get; init; }
    public string Sku { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public int SupplierId { get; init; }
    public decimal Price { get; init; }
    public int StockQuantity { get; init; }
}
```

---

### QuaternaryDictionary\<TKey, TValue\>

A high-performance, thread-safe dictionary that distributes key-value pairs across four internal shards for improved concurrency. Optimized for large datasets with frequent lookups and modifications.

> **Target support:** Available on the .NET Framework and modern .NET target frameworks supported by the package. It is the cache-style collection to prefer when you need key lookup, value-based secondary indices, reactive notifications, and low allocation bulk writes.

#### Key Advantages

- Faster and lower-allocation than DynamicData `SourceCache<TValue, TKey>` for measured `AddRange` workloads
- Lower memory usage at each measured size in current benchmarks
- Built-in **secondary value indices** for O(1) lookups by value properties
- Thread-safe with fine-grained locking per shard

#### Constructor

```csharp
var cache = new QuaternaryDictionary<int, Customer>();
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count` | `int` | Total number of entries |
| `Keys` | `ICollection<TKey>` | All keys |
| `Values` | `ICollection<TValue>` | All values |
| `Stream` | `IObservable<CacheNotify<KeyValuePair<TKey, TValue>>>` | Change notifications |

#### Methods

**Basic Operations:**

```csharp
var customers = new QuaternaryDictionary<int, Customer>();

// Add items
customers.Add(1, new Customer { Id = 1, Name = "Alice" });
customers.AddRange([
    new KeyValuePair<int, Customer>(2, new Customer { Id = 2, Name = "Bob" }),
    new KeyValuePair<int, Customer>(3, new Customer { Id = 3, Name = "Charlie" })
]);

// Try add (returns false if key exists)
bool added = customers.TryAdd(4, new Customer { Id = 4, Name = "Diana" });

// Add or update
customers.AddOrUpdate(1, new Customer { Id = 1, Name = "Alice Updated" });

// Use indexer (adds or updates)
customers[5] = new Customer { Id = 5, Name = "Eve" };

// Get value
Customer customer = customers[1];

// Try get value
if (customers.TryGetValue(1, out Customer? found))
{
    Console.WriteLine($"Found: {found.Name}");
}

// Lookup (returns tuple)
var (hasValue, value) = customers.Lookup(1);

// Check existence
bool exists = customers.ContainsKey(1);
bool containsPair = customers.Contains(new KeyValuePair<int, Customer>(1, customer));

// Remove
bool removed = customers.Remove(1);
customers.RemoveKeys([2, 3, 4]);

// Remove by predicate
int removedCount = customers.RemoveMany(kvp => kvp.Value.IsInactive);

// Batch edit
customers.Edit(dict =>
{
    dict[100] = new Customer { Id = 100, Name = "New Customer" };
    dict.Remove(5);
});

// Clear
customers.Clear();
```

**Secondary Value Indices:**

Index values by their properties for O(1) lookups:

```csharp
var customers = new QuaternaryDictionary<int, Customer>();

// Add index by customer region
customers.AddValueIndex("ByRegion", c => c.Region);

// Add index by account manager
customers.AddValueIndex("ByManager", c => c.AccountManagerId);

// Add customers
customers.AddRange([
    new(1, new Customer { Id = 1, Name = "Acme Corp", Region = "West", AccountManagerId = 101 }),
    new(2, new Customer { Id = 2, Name = "Globex", Region = "East", AccountManagerId = 101 }),
    new(3, new Customer { Id = 3, Name = "Initech", Region = "West", AccountManagerId = 102 })
]);

// O(1) lookup by region
var westCustomers = customers.GetValuesBySecondaryIndex("ByRegion", "West");

// O(1) lookup by manager
var manager101Customers = customers.GetValuesBySecondaryIndex("ByManager", 101);

// Check if value matches index
bool isWestCustomer = customers.ValueMatchesSecondaryIndex("ByRegion", customer, "West");

// Create reactive view filtered by secondary index
var westView = customers.CreateViewBySecondaryIndex<string>(
    indexName: "ByRegion",
    key: "West",
    scheduler: Scheduler.Default,
    throttleMs: 50
);
```

#### Complete Example: Real-Time User Session Cache

```csharp
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Disposables;

public class SessionCache : IDisposable
{
    private readonly QuaternaryDictionary<string, UserSession> _sessions = new();
    private readonly CompositeDisposable _subscriptions = new();

    public SessionCache()
    {
        // Setup secondary indices
        _sessions.AddValueIndex("ByUserId", s => s.UserId);
        _sessions.AddValueIndex("ByRole", s => s.Role);
        _sessions.AddValueIndex("ByRegion", s => s.Region);

        // Monitor session count
        _subscriptions.Add(
            _sessions.Stream
                .Select(_ => _sessions.Count)
                .DistinctUntilChanged()
                .Subscribe(count => 
                    Console.WriteLine($"Active sessions: {count}"))
        );

        // Alert on admin logins
        _subscriptions.Add(
            _sessions.Stream
                .WhereAdded()
                .SelectAllItems()
                .Where(kvp => kvp.Value.Role == "Admin")
                .Subscribe(kvp => 
                    Console.WriteLine($"ADMIN LOGIN: User {kvp.Value.UserId} from {kvp.Value.IpAddress}"))
        );

        // Auto-expire sessions
        _subscriptions.Add(
            Observable.Interval(TimeSpan.FromMinutes(1))
                .Subscribe(_ => ExpireOldSessions())
        );
    }

    public void CreateSession(string sessionId, UserSession session) =>
        _sessions.TryAdd(sessionId, session);

    public void UpdateSession(string sessionId, UserSession session) =>
        _sessions.AddOrUpdate(sessionId, session);

    public UserSession? GetSession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) ? session : null;

    // Get all sessions for a user (O(1) via secondary index)
    public IEnumerable<UserSession> GetUserSessions(int userId) =>
        _sessions.GetValuesBySecondaryIndex("ByUserId", userId);

    // Get all admin sessions
    public IEnumerable<UserSession> GetAdminSessions() =>
        _sessions.GetValuesBySecondaryIndex("ByRole", "Admin");

    // Create reactive view for region
    public SecondaryIndexReactiveView<string, UserSession, string> CreateRegionView(string region) =>
        _sessions.CreateViewBySecondaryIndex<string>("ByRegion", region, Scheduler.Default);

    public void EndSession(string sessionId) =>
        _sessions.Remove(sessionId);

    private void ExpireOldSessions()
    {
        var expireBefore = DateTime.UtcNow.AddHours(-24);
        int expired = _sessions.RemoveMany(kvp => kvp.Value.LastActivity < expireBefore);
        if (expired > 0)
            Console.WriteLine($"Expired {expired} inactive sessions");
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        _sessions.Dispose();
    }
}

public record UserSession
{
    public int UserId { get; init; }
    public string Role { get; init; } = "User";
    public string Region { get; init; } = "";
    public string IpAddress { get; init; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastActivity { get; init; } = DateTime.UtcNow;
}
```

---

## Views

Views are auto-updating projections of a reactive collection. They implement `INotifyCollectionChanged` and `INotifyPropertyChanged` for seamless UI binding.

### FilteredReactiveView\<T\>

Creates a filtered view that automatically updates when the source changes.

```csharp
using CP.Reactive;
using CP.Reactive.Collections;
using System.Reactive.Concurrency;

var employees = new ReactiveList<Employee>();
employees.AddRange(GetAllEmployees());

// Create filtered view - only active employees
var activeEmployees = employees.CreateView(
    filter: e => e.IsActive,
    scheduler: Scheduler.Default,
    throttleMs: 50
);

// Bind to UI
myListBox.ItemsSource = activeEmployees.Items;

// Or use ToProperty for view models
activeEmployees.ToProperty(items => ActiveEmployeesList = items);

// Or use out parameter pattern
var view = employees.CreateView(e => e.IsActive)
    .ToProperty(out ReadOnlyObservableCollection<Employee> activeList);

// View automatically updates when:
// - Items are added/removed from source
// - Item properties change (if tracked)

// Force refresh if needed
activeEmployees.Refresh();

// Don't forget to dispose when done
activeEmployees.Dispose();
```

### SortedReactiveView\<T\>

Creates a sorted view that maintains sort order as items change.

```csharp
var products = new ReactiveList<Product>();

// Sort by price ascending
var byPrice = products.SortBy(
    comparer: Comparer<Product>.Create((a, b) => a.Price.CompareTo(b.Price)),
    scheduler: Scheduler.Default,
    throttleMs: 50
);

// Or use key selector
var byName = products.SortBy(
    keySelector: p => p.Name,
    descending: false,
    scheduler: Scheduler.Default
);

// Sort descending by date
var byDateDesc = products.SortBy(
    keySelector: p => p.CreatedAt,
    descending: true
);

// Bind to UI
productGrid.ItemsSource = byPrice.Items;
```

### GroupedReactiveView\<T, TKey\>

Groups items by a key and maintains groups as items change.

```csharp
var tasks = new ReactiveList<TaskItem>();

// Group by status
var byStatus = tasks.GroupBy(
    keySelector: t => t.Status,
    scheduler: Scheduler.Default,
    throttleMs: 50
);

// Access groups
foreach (var group in byStatus.Groups)
{
    Console.WriteLine($"{group.Key}: {group.Count} tasks");
    foreach (var task in group)
    {
        Console.WriteLine($"  - {task.Title}");
    }
}

// Check if group exists
if (byStatus.ContainsKey("Completed"))
{
    var completedTasks = byStatus["Completed"];
}

// Try get group
if (byStatus.TryGetValue("InProgress", out var inProgress))
{
    // Use the group...
}

// Bind groups to hierarchical UI
treeView.ItemsSource = byStatus.Groups;
```

### DynamicFilteredReactiveView\<T\>

A filtered view where the filter can change dynamically.

```csharp
using System.Reactive.Subjects;

var products = new ReactiveList<Product>();

// Create observable filter
var searchFilter = new BehaviorSubject<Func<Product, bool>>(_ => true);

// Create dynamic view
var searchResults = products.CreateView(
    filterObservable: searchFilter,
    scheduler: Scheduler.Default,
    throttleMs: 100
);

// Update filter dynamically (view auto-rebuilds)
searchFilter.OnNext(p => p.Name.Contains("widget", StringComparison.OrdinalIgnoreCase));

// Change filter to show only expensive items
searchFilter.OnNext(p => p.Price > 100);

// Clear filter
searchFilter.OnNext(_ => true);

// Bind to search UI
searchResultsList.ItemsSource = searchResults.Items;
```

### ReactiveView\<T\>

A general-purpose view for QuaternaryList that supports filtering.

```csharp
var items = new QuaternaryList<DataPoint>();

// Create unfiltered view
var allItems = items.CreateView(Scheduler.Default, throttleMs: 50);

// Create filtered view
var recentItems = items.CreateView(
    filter: dp => dp.Timestamp > DateTime.UtcNow.AddHours(-1),
    scheduler: Scheduler.Default,
    throttleMs: 50
);

// Create view with dynamic filter
var queryFilter = new BehaviorSubject<Func<DataPoint, bool>>(_ => true);
var dynamicView = items.CreateView(queryFilter, Scheduler.Default);

// Create view with query observable
var queryObservable = textBox.TextChanged.Select(e => e.Text);
var searchView = items.CreateView(
    queryObservable: queryObservable,
    filter: (query, item) => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase),
    scheduler: Scheduler.Default
);
```

### Secondary Index Views

Views filtered by secondary index for efficient large dataset filtering.

```csharp
// For QuaternaryList
var products = new QuaternaryList<Product>();
products.AddIndex("ByCategory", p => p.Category);

// Static key view
var electronicsView = products.CreateViewBySecondaryIndex(
    indexName: "ByCategory",
    key: "Electronics",
    scheduler: Scheduler.Default
);

// Multiple keys view (OR logic)
var multiCategoryView = products.CreateViewBySecondaryIndex(
    indexName: "ByCategory",
    keys: ["Electronics", "Computers"],
    scheduler: Scheduler.Default
);

// Dynamic key view (key changes over time)
var categorySelector = new BehaviorSubject<string[]>(["Electronics"]);
var dynamicCategoryView = products.CreateDynamicViewBySecondaryIndex(
    indexName: "ByCategory",
    keysObservable: categorySelector,
    scheduler: Scheduler.Default
);

// Change category filter
categorySelector.OnNext(["Books", "Music"]);
```

---

## Extension Methods Reference

### Stream Extensions (CacheNotifyExtensions)

Extensions for working with `IObservable<CacheNotify<T>>`:

| Method | Description | Example |
|--------|-------------|---------|
| `WhereAction(action)` | Filter by specific action | `stream.WhereAction(CacheAction.Added)` |
| `WhereAdded()` | Filter to add notifications (single + batch) | `stream.WhereAdded()` |
| `WhereRemoved()` | Filter to remove notifications (single + batch) | `stream.WhereRemoved()` |
| `SelectItems()` | Project single items from notifications | `stream.SelectItems()` |
| `SelectAllItems()` | Project all items (single + batch) | `stream.SelectAllItems()` |
| `OnItemAdded()` | Get added items only | `stream.OnItemAdded()` |
| `OnItemRemoved()` | Get removed items only | `stream.OnItemRemoved()` |
| `OnItemUpdated()` | Get updated items only | `stream.OnItemUpdated()` |
| `OnItemMoved()` | Get moved items with indices | `stream.OnItemMoved()` |
| `OnCleared()` | Get clear notifications | `stream.OnCleared()` |
| `BufferNotifications(timeSpan)` | Buffer notifications over time | `stream.BufferNotifications(TimeSpan.FromSeconds(1))` |
| `ThrottleNotifications(timeSpan)` | Throttle notification frequency | `stream.ThrottleNotifications(TimeSpan.FromMilliseconds(100))` |
| `ObserveOnScheduler(scheduler)` | Observe on specific scheduler | `stream.ObserveOnScheduler(Scheduler.Default)` |
| `TransformItems(selector)` | Transform items | `stream.TransformItems(x => x.ToString())` |
| `FilterItems(predicate)` | Filter items | `stream.FilterItems(x => x.IsValid)` |
| `AutoDisposeBatches()` | Dispose pooled batch payloads after processing | `stream.AutoDisposeBatches()` |
| `CountByAction()` | Project notifications to action/count tuples | `stream.CountByAction()` |
| `ToChange()` | Convert one notification to a single change when possible | `notification.ToChange()` |
| `ToChangeSets()` | Convert to ChangeSet stream | `stream.ToChangeSets()` |

### ChangeSet Extensions (ReactiveListExtensions)

Extensions for working with `IObservable<ChangeSet<T>>`:

| Method | Description | Example |
|--------|-------------|---------|
| `WhereChanges(predicate)` | Filter changes by predicate | `changeSets.WhereChanges(c => c.CurrentIndex > 0)` |
| `WhereReason(reason)` | Filter by change reason | `changeSets.WhereReason(ChangeReason.Add)` |
| `SelectChanges<TResult>(selector)` | Transform changes | `changeSets.SelectChanges(c => c.Current.Name)` |
| `OnAdd()` | Get added items | `changeSets.OnAdd()` |
| `OnRemove()` | Get removed items | `changeSets.OnRemove()` |
| `OnUpdate()` | Get update tuples (previous, current) | `changeSets.OnUpdate()` |
| `OnMove()` | Get move tuples (item, old index, new index) | `changeSets.OnMove()` |
| `GroupByChanges(keySelector)` | Group changes by key | `changeSets.GroupByChanges(c => c.Category)` |
| `GroupingByChanges(keySelector)` | Group as IGrouping | `changeSets.GroupingByChanges(c => c.Type)` |
| `SortBy(keySelector)` | Sort changes by key | `changeSets.SortBy(c => c.Name)` |
| `AutoRefresh(propertyName)` | Refresh on property changes | `changeSets.AutoRefresh("Price")` |
| `AutoRefresh()` | Refresh on any property change | `changeSets.AutoRefresh()` |
| `FilterDynamic(filterObservable)` | Dynamic filtering | `stream.FilterDynamic(filterSubject)` |
| `WhereItems(predicate)` | Filter cache notifications by item predicate | `stream.WhereItems(x => x.IsActive)` |
| `Connect()` | Connect to stream as ChangeSet | `source.Connect()` |

### View Creation Extensions

Extensions for creating views:

| Method | Collection | Description |
|--------|------------|-------------|
| `CreateView(filter, scheduler, throttleMs)` | `IReactiveList<T>` | Create filtered view |
| `CreateView(scheduler, throttleMs)` | `IReactiveList<T>` | Create unfiltered view |
| `CreateView(filterObservable, scheduler, throttleMs)` | `IReactiveList<T>` | Create dynamic filtered view |
| `SortBy(comparer, scheduler, throttleMs)` | `IReactiveList<T>` | Create sorted view |
| `SortBy(keySelector, descending, scheduler, throttleMs)` | `IReactiveList<T>` | Create sorted view by key |
| `GroupBy(keySelector, scheduler, throttleMs)` | `IReactiveList<T>` | Create grouped view |
| `CreateView(filter, scheduler, throttleMs)` | `IReactiveSource<T>` | Create view from any source |
| `CreateView(filterObservable, scheduler, throttleMs)` | `IReactiveSource<T>` | Create dynamic view |
| `CreateView(queryObservable, filter, scheduler, throttleMs)` | `IReactiveSource<T>` | Create query-based view |
| `CreateViewBySecondaryIndex(indexName, key, scheduler, throttleMs)` | `QuaternaryList<T>` | View by secondary index |
| `CreateViewBySecondaryIndex(indexName, keys, scheduler, throttleMs)` | `QuaternaryList<T>` | View by multiple keys |
| `CreateDynamicViewBySecondaryIndex(indexName, keysObservable, scheduler, throttleMs)` | `QuaternaryList<T>` | Dynamic secondary index view |
| `CreateViewBySecondaryIndex(indexName, key, scheduler, throttleMs)` | `QuaternaryDictionary<K,V>` | Dictionary secondary index view |
| `CreateViewBySecondaryIndex(indexName, keys, scheduler, throttleMs)` | `QuaternaryDictionary<K,V>` | Dictionary view by multiple value-index keys |
| `CreateDynamicViewBySecondaryIndex(indexName, keysObservable, scheduler, throttleMs)` | `QuaternaryDictionary<K,V>` | Dynamic dictionary secondary index view |
| `FilterBySecondaryIndex(list, indexName, key)` | Stream | Filter stream by index |
| `FilterBySecondaryIndex(list, indexName, keys)` | Stream | Filter stream by multiple keys |
| `FilterBySecondaryIndex(dict, indexName, key)` | Dictionary stream | Filter dictionary stream by value index |
| `FilterBySecondaryIndex(dict, indexName, keys)` | Dictionary stream | Filter dictionary stream by multiple value-index keys |

---

## API Reference

This section is the compact API map for the package. All examples assume:

```csharp
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Views;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
```

### Core Contracts

| Contract | Purpose | Main Members |
|----------|---------|--------------|
| `IReactiveSource<T>` | Common reactive source for lists, dictionaries, quaternary collections, and views over them. | `Count`, `IsReadOnly`, `Version`, `Stream`, `ToArray()`, `CollectionChanged`, `Dispose()` |
| `IReactiveList<T>` | Ordered, UI-friendly reactive list contract. | `Items`, `ItemsAdded`, `ItemsRemoved`, `ItemsChanged`, `Added`, `Removed`, `Changed`, `CurrentItems`, `AddRange`, `InsertRange`, `RemoveRange`, `RemoveMany`, `Move`, `Update`, `ReplaceAll`, `Edit` |
| `IQuaternaryList<T>` | Thread-safe sharded list with secondary indices. | `AddRange`, `RemoveRange`, `RemoveMany`, `ReplaceAll`, `Edit`, `AddIndex`, `GetItemsBySecondaryIndex` |
| `IQuaternaryDictionary<TKey,TValue>` | Thread-safe sharded dictionary with value indices. | `AddOrUpdate`, `TryAdd`, `Lookup`, `AddRange`, `RemoveKeys`, `RemoveMany`, `Edit`, `AddValueIndex`, `GetValuesBySecondaryIndex`, `ValueMatchesSecondaryIndex` |
| `IReactiveView<TView,TItem>` | Common view contract for bindable read-only views. | `Items`, `ToProperty(Action<ReadOnlyObservableCollection<TItem>>)`, `ToProperty(out ReadOnlyObservableCollection<TItem>)`, `Dispose()` |

```csharp
IReactiveSource<Order> source = new QuaternaryList<Order>();

using var subscription = source.Stream
    .CountByAction()
    .Subscribe(change => Console.WriteLine($"{change.Action}: {change.Count}"));

Console.WriteLine($"Version before edit: {source.Version}");
```

```csharp
IReactiveList<Order> orders = new ReactiveList<Order>();

orders.Added.Subscribe(batch => Console.WriteLine($"Added {batch.Count()} orders"));
orders.CurrentItems.Subscribe(snapshot => Console.WriteLine($"Current count: {snapshot.Count()}"));

orders.AddRange([new Order(1, "Open"), new Order(2, "Open")]);
orders.Update(orders[0], orders[0] with { Status = "Packed" });
orders.Move(oldIndex: 1, newIndex: 0);
orders.RemoveMany(order => order.Status == "Cancelled");
```

```csharp
IQuaternaryList<Order> indexedOrders = new QuaternaryList<Order>();

indexedOrders.AddIndex("ByStatus", order => order?.Status ?? "");
indexedOrders.AddRange([new Order(1, "Open"), new Order(2, "Closed")]);

IEnumerable<Order> openOrders = indexedOrders.GetItemsBySecondaryIndex("ByStatus", "Open");
indexedOrders.ReplaceAll(openOrders.Select(order => order with { Status = "Reviewed" }));
```

```csharp
IQuaternaryDictionary<int, Customer> customers = new QuaternaryDictionary<int, Customer>();

customers.AddValueIndex("ByRegion", customer => customer?.Region ?? "");
customers.AddOrUpdate(42, new Customer(42, "Alice", "EMEA"));

var lookup = customers.Lookup(42);
var emeaCustomers = customers.GetValuesBySecondaryIndex("ByRegion", "EMEA");
customers.RemoveKeys([42]);
```

```csharp
using var activeView = orders.CreateView(order => order.Status == "Open", Scheduler.Default);
activeView.ToProperty(out var bindableOrders);
```

### Core Records and Batches

`CacheNotify<T>` is the low-level stream payload. `Action` identifies the operation, `Item` is set for single-item notifications, `Batch` is set for batch notifications, `CurrentIndex` and `PreviousIndex` describe moves or indexed list operations, and `Previous` is set for updates.

```csharp
using var sub = orders.Stream.Subscribe(notification =>
{
    if (notification.Action == CacheAction.Updated)
    {
        Console.WriteLine($"{notification.Previous} -> {notification.Item}");
    }
});
```

`PooledBatch<T>` wraps an array rented from `ArrayPool<T>`. If you manually handle `CacheNotify<T>.Batch`, dispose it after processing. `AutoDisposeBatches()` is the easiest way to make a stream own that lifecycle.

```csharp
using var sub = indexedOrders.Stream
    .Do(notification =>
    {
        if (notification.Batch is { } batch)
        {
            for (var i = 0; i < batch.Count; i++)
            {
                Console.WriteLine(batch.Items[i]);
            }
        }
    })
    .AutoDisposeBatches()
    .Subscribe();
```

`Change<T>` is the struct form used by DynamicData-style pipelines. Use factory methods for clarity.

```csharp
var added = Change<Order>.CreateAdd(new Order(3, "Open"), index: 0);
var updated = Change<Order>.CreateUpdate(
    current: new Order(3, "Closed"),
    previous: new Order(3, "Open"),
    index: 0);
```

`ChangeSet<T>` is an ordered batch of `Change<T>`. It exposes `Count`, `Adds`, `Removes`, `Updates`, `Moves`, an indexer, enumeration, and `AsSpan()` on supported TFMs. Dispose it when you own a manually created instance or when a downstream pipeline keeps it past immediate processing.

```csharp
using var changes = new ChangeSet<Order>([
    Change<Order>.CreateAdd(new Order(4, "Open")),
    Change<Order>.CreateRemove(new Order(5, "Cancelled"))
]);

foreach (var change in changes)
{
    Console.WriteLine($"{change.Reason}: {change.Current}");
}
```

```csharp
using var sub = orders.Connect().Subscribe(changeSet =>
{
    try
    {
        Console.WriteLine($"Adds={changeSet.Adds}, Removes={changeSet.Removes}");

#if NET6_0_OR_GREATER
        foreach (var change in changeSet.AsSpan())
        {
            Console.WriteLine(change.Current);
        }
#endif
    }
    finally
    {
        changeSet.Dispose();
    }
});
```

### Collection-Specific APIs

#### ReactiveList<T>

Use `ReactiveList<T>` when you need ordered list semantics, UI binding, `ReadOnlyObservableCollection<T>` projections, and familiar `IList<T>` behavior.

```csharp
var tasks = new ReactiveList<TodoItem>();

tasks.CollectionChanged += (_, args) => Console.WriteLine(args.Action);
tasks.PropertyChanged += (_, args) => Console.WriteLine(args.PropertyName);

tasks.AddRange([new TodoItem("Write docs", false), new TodoItem("Run tests", false)]);
tasks.InsertRange(1, [new TodoItem("Review benchmarks", false)]);
tasks[0] = tasks[0] with { Done = true };
tasks.RemoveRange(index: 1, count: 1);

TodoItem[] copy = new TodoItem[tasks.Count];
tasks.CopyTo(copy, 0);

Span<TodoItem> buffer = new TodoItem[tasks.Count];
tasks.CopyTo(buffer);

IList nonGeneric = tasks;
nonGeneric.Add(new TodoItem("Legacy IList consumer", false));

tasks.Subscribe(snapshot => Console.WriteLine($"Snapshot size: {snapshot.Count()}"));
tasks.ClearWithoutDeallocation(notifyChange: true);
```

#### Reactive2DList<T>

Use `Reactive2DList<T>` for row/column style data where each row is itself a reactive list.

```csharp
var grid = new Reactive2DList<string>();

grid.Add(new ReactiveList<string>(["Name", "Region"]));
grid.Add(new ReactiveList<string>(["Alice", "EMEA"]));
grid.SetItem(row: 1, column: 1, value: "APAC");
grid.AddToInner(row: 0, item: "Status");

IEnumerable<string> cells = grid.Flatten();
int totalCells = grid.TotalCount();
```

#### QuaternaryList<T>

`QuaternaryList<T>` distributes items across four internal shards by hash. It is not a positional list for insert/set operations; use add, remove, batch, predicate, replace, and secondary-index APIs.

```csharp
var readings = new QuaternaryList<SensorReading>();
var selectedDeviceIds = new BehaviorSubject<string[]>(["pump-1"]);

readings.AddIndex("ByDevice", reading => reading.DeviceId);
readings.AddIndex("BySeverity", reading => reading.Severity);
readings.AddRange(batch);

var critical = readings.GetItemsBySecondaryIndex("BySeverity", Severity.Critical);
readings.RemoveMany(reading => reading.Timestamp < cutoff);

using var view = readings.CreateDynamicViewBySecondaryIndex(
    "ByDevice",
    selectedDeviceIds,
    Scheduler.Default);
```

#### QuaternaryDictionary<TKey,TValue>

`QuaternaryDictionary<TKey,TValue>` is the cache-style API. Use dictionary keys for identity and secondary value indices for alternative lookups.

```csharp
var sessions = new QuaternaryDictionary<string, UserSession>();

sessions.AddValueIndex("ByTenant", session => session?.TenantId ?? "");
sessions.AddRange(incoming.Select(s => KeyValuePair.Create(s.SessionId, s)));
sessions.AddOrUpdate("abc", new UserSession("abc", "tenant-1", DateTimeOffset.UtcNow));

if (sessions.TryGetValue("abc", out var session))
{
    Console.WriteLine(session.TenantId);
}

var tenantSessions = sessions.GetValuesBySecondaryIndex("ByTenant", "tenant-1");
using var tenantView = sessions.CreateViewBySecondaryIndex("ByTenant", "tenant-1", Scheduler.Default);
sessions.RemoveMany(pair => pair.Value.LastSeen < cutoff);
```

### View APIs

All view types expose a read-only `Items` collection and implement `IDisposable`. Dispose views when the screen, component, or pipeline that owns them is closed.

| View | Create With | Use For |
|------|-------------|---------|
| `FilteredReactiveView<T>` | `list.CreateView(predicate)` | Static predicate over `IReactiveList<T>` |
| `SortedReactiveView<T>` | `list.SortBy(...)` | Sorted bindable projection |
| `GroupedReactiveView<T,TKey>` | `list.GroupBy(keySelector)` | Bindable groups |
| `DynamicFilteredReactiveView<T>` | `list.CreateView(filterObservable)` | Dynamic predicate over `IReactiveList<T>` |
| `DynamicReactiveView<T>` | `source.CreateView(filterObservable)` or query overloads | Dynamic predicate over any `IReactiveSource<T>` |
| `ReactiveView<T>` | `source.CreateView(...)` or secondary-index helpers | General source view |
| `SecondaryIndexReactiveView<TKey,TValue,TIndexKey>` | `dict.CreateViewBySecondaryIndex(...)` | Dictionary value-index view |
| `DynamicSecondaryIndexReactiveView<T,TKey>` | `list.CreateDynamicViewBySecondaryIndex(...)` | List secondary-index keys that change over time |
| `DynamicSecondaryIndexDictionaryReactiveView<TKey,TValue,TIndexKey>` | `dict.CreateDynamicViewBySecondaryIndex(...)` | Dictionary value-index keys that change over time |

```csharp
var search = new BehaviorSubject<string>("");

using var searchView = readings.CreateView(
    queryObservable: search,
    filter: (query, reading) => reading.DeviceId.Contains(query, StringComparison.OrdinalIgnoreCase),
    scheduler: Scheduler.Default);

search.OnNext("pump-");
```

```csharp
using var grouped = tasks.GroupBy(task => task.Done ? "Done" : "Open", Scheduler.Default);

if (grouped.TryGetValue("Open", out var openTasks))
{
    Console.WriteLine(openTasks.Count);
}
```

### Advanced Shard APIs

`QuadList<T>`, `QuadDictionary<TKey,TValue>`, `IQuad<T>`, and `QuaternaryBase<TItem,TQuad,TValue>` are public for advanced scenarios and testing, but most application code should use `QuaternaryList<T>` or `QuaternaryDictionary<TKey,TValue>`.

| Type | Purpose | Notes |
|------|---------|-------|
| `IQuad<T>` | Minimal shard contract. | `Count`, `Clear()`, enumeration, `Dispose()` |
| `QuadList<T>` | Pooled, non-thread-safe shard list. | Supports indexer, `Add`, `AddRange(ReadOnlySpan<T>)`, `Remove`, `RemoveAt`, `Contains`, `AsSpan()`, `Clear`, `Dispose()` |
| `QuadDictionary<TKey,TValue>` | Pooled, non-thread-safe shard dictionary. | Supports `Add`, `TryAdd`, `TryGetValue`, `GetValueRefOrAddDefault`, `Remove`, `ContainsKey`, key/value enumeration, `EnsureCapacity`, `Dispose()` |
| `QuaternaryBase<TItem,TQuad,TValue>` | Shared base for the quaternary collections. | Owns shards, locks, versioning, notification stream, and secondary-index storage |

```csharp
using var shard = new QuadList<int>();
shard.AddRange([1, 2, 3]);

foreach (var value in shard)
{
    Console.WriteLine(value);
}
```

```csharp
using var shard = new QuadDictionary<string, int>();
ref var value = ref shard.GetValueRefOrAddDefault("count", out var exists);
value = exists ? value + 1 : 1;
```

---

## Real-World Examples

### Live Stock Ticker

A real-time stock price monitoring system:

```csharp
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;

public class StockTicker : IDisposable
{
    private readonly QuaternaryDictionary<string, StockQuote> _quotes = new();
    private readonly CompositeDisposable _subscriptions = new();
    
    public IObservable<StockQuote> PriceUpdates { get; }
    public IObservable<StockQuote> SignificantChanges { get; }
    
    public StockTicker()
    {
        // Setup indices
        _quotes.AddValueIndex("BySector", q => q.Sector);
        _quotes.AddValueIndex("ByExchange", q => q.Exchange);
        
        // Create price update stream
        PriceUpdates = _quotes.Stream
            .WhereAction(CacheAction.Updated)
            .SelectItems()
            .Select(kvp => kvp.Value);
        
        // Alert on significant price changes (>5%)
        SignificantChanges = _quotes.Stream
            .Where(n => n.Action == CacheAction.Updated && n.Previous != null)
            .Select(n => (Current: n.Item!.Value, Previous: n.Previous!.Value.Value))
            .Where(x => Math.Abs((x.Current.Price - x.Previous.Price) / x.Previous.Price) > 0.05m)
            .Select(x => x.Current);
        
        // Log all updates
        _subscriptions.Add(
            _quotes.Stream
                .ThrottleNotifications(TimeSpan.FromMilliseconds(100))
                .Subscribe(n => Console.WriteLine($"Quote updated: {n.Item?.Key}"))
        );
    }
    
    // Called from market data feed
    public void UpdateQuote(string symbol, decimal price, decimal volume)
    {
        var quote = new StockQuote
        {
            Symbol = symbol,
            Price = price,
            Volume = volume,
            Timestamp = DateTime.UtcNow,
            Sector = GetSector(symbol),
            Exchange = GetExchange(symbol)
        };
        
        _quotes.AddOrUpdate(symbol, quote);
    }
    
    // Efficient sector lookup
    public IEnumerable<StockQuote> GetBySector(string sector) =>
        _quotes.GetValuesBySecondaryIndex("BySector", sector);
    
    // Create live view for a sector
    public SecondaryIndexReactiveView<string, StockQuote, string> CreateSectorView(string sector) =>
        _quotes.CreateViewBySecondaryIndex<string>("BySector", sector, Scheduler.Default);
    
    private string GetSector(string symbol) => symbol[0] switch
    {
        'A' or 'B' or 'C' => "Technology",
        'D' or 'E' or 'F' => "Finance",
        _ => "Other"
    };
    
    private string GetExchange(string symbol) => "NYSE"; // Simplified
    
    public void Dispose()
    {
        _subscriptions.Dispose();
        _quotes.Dispose();
    }
}

public record StockQuote
{
    public string Symbol { get; init; } = "";
    public decimal Price { get; init; }
    public decimal Volume { get; init; }
    public DateTime Timestamp { get; init; }
    public string Sector { get; init; } = "";
    public string Exchange { get; init; } = "";
}
```

### IoT Sensor Dashboard

Monitor sensors with real-time updates and alerting:

```csharp
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;

public class SensorDashboard : IDisposable
{
    private readonly ReactiveList<SensorReading> _readings = new();
    private readonly CompositeDisposable _subscriptions = new();
    
    // Reactive views for different data needs
    public FilteredReactiveView<SensorReading> RecentReadings { get; }
    public GroupedReactiveView<SensorReading, string> ReadingsByDevice { get; }
    
    // Alert streams
    public IObservable<SensorReading> HighTemperatureAlerts { get; }
    public IObservable<SensorReading> LowBatteryAlerts { get; }
    
    public SensorDashboard()
    {
        // Create view of last hour's readings
        RecentReadings = _readings.CreateView(
            filter: r => r.Timestamp > DateTime.UtcNow.AddHours(-1),
            scheduler: Scheduler.Default,
            throttleMs: 100
        );
        
        // Group readings by device
        ReadingsByDevice = _readings.GroupBy(
            keySelector: r => r.DeviceId,
            scheduler: Scheduler.Default
        );
        
        // High temperature alerts
        HighTemperatureAlerts = _readings.Stream
            .OnItemAdded()
            .Where(r => r.SensorType == "Temperature" && r.Value > 85);
        
        // Low battery alerts (unique per device)
        LowBatteryAlerts = _readings.Stream
            .OnItemAdded()
            .Where(r => r.BatteryLevel < 20)
            .GroupBy(r => r.DeviceId)
            .SelectMany(g => g.Throttle(TimeSpan.FromMinutes(5)));
        
        // Cleanup old readings periodically
        _subscriptions.Add(
            Observable.Interval(TimeSpan.FromMinutes(5))
                .Subscribe(_ => CleanupOldReadings())
        );
        
        // Subscribe to alerts
        _subscriptions.Add(
            HighTemperatureAlerts.Subscribe(r =>
                Console.WriteLine($"🔥 HIGH TEMP: Device {r.DeviceId} = {r.Value}°F"))
        );
        
        _subscriptions.Add(
            LowBatteryAlerts.Subscribe(r =>
                Console.WriteLine($"🔋 LOW BATTERY: Device {r.DeviceId} = {r.BatteryLevel}%"))
        );
    }
    
    // Called from IoT hub
    public void ProcessReading(SensorReading reading)
    {
        _readings.Add(reading);
    }
    
    // Bulk import
    public void ProcessBatch(IEnumerable<SensorReading> readings)
    {
        _readings.AddRange(readings);
    }
    
    // Get statistics for a device
    public DeviceStats? GetDeviceStats(string deviceId)
    {
        if (!ReadingsByDevice.ContainsKey(deviceId))
            return null;
            
        var readings = ReadingsByDevice[deviceId]
            .Where(r => r.Timestamp > DateTime.UtcNow.AddHours(-1))
            .ToList();
            
        if (!readings.Any())
            return null;
            
        return new DeviceStats
        {
            DeviceId = deviceId,
            AverageValue = readings.Average(r => r.Value),
            MinValue = readings.Min(r => r.Value),
            MaxValue = readings.Max(r => r.Value),
            ReadingCount = readings.Count
        };
    }
    
    private void CleanupOldReadings()
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        int removed = _readings.RemoveMany(r => r.Timestamp < cutoff);
        Console.WriteLine($"Cleaned up {removed} old readings");
    }
    
    public void Dispose()
    {
        _subscriptions.Dispose();
        RecentReadings.Dispose();
        ReadingsByDevice.Dispose();
        _readings.Dispose();
    }
}

public record SensorReading
{
    public string DeviceId { get; init; } = "";
    public string SensorType { get; init; } = "";
    public double Value { get; init; }
    public int BatteryLevel { get; init; } = 100;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public record DeviceStats
{
    public string DeviceId { get; init; } = "";
    public double AverageValue { get; init; }
    public double MinValue { get; init; }
    public double MaxValue { get; init; }
    public int ReadingCount { get; init; }
}
```

### Chat Application

Real-time message handling with conversation grouping:

```csharp
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;

public class ChatService : IDisposable
{
    private readonly ReactiveList<ChatMessage> _messages = new();
    private readonly CompositeDisposable _subscriptions = new();
    
    // Search functionality
    private readonly BehaviorSubject<string> _searchQuery = new("");
    public DynamicFilteredReactiveView<ChatMessage> SearchResults { get; }
    
    // Grouped by conversation
    public GroupedReactiveView<ChatMessage, string> Conversations { get; }
    
    // Sorted by timestamp
    public SortedReactiveView<ChatMessage> Timeline { get; }
    
    // Real-time message stream
    public IObservable<ChatMessage> NewMessages { get; }
    public IObservable<ChatMessage> MentionAlerts { get; }
    
    public ChatService(string currentUserId)
    {
        // New message notifications
        NewMessages = _messages.Stream.OnItemAdded();
        
        // Mentions of current user
        MentionAlerts = NewMessages
            .Where(m => m.Text.Contains($"@{currentUserId}", StringComparison.OrdinalIgnoreCase));
        
        // Search results with dynamic query
        var searchFilter = _searchQuery
            .Throttle(TimeSpan.FromMilliseconds(300))
            .Select<string, Func<ChatMessage, bool>>(query =>
                string.IsNullOrWhiteSpace(query)
                    ? _ => false
                    : m => m.Text.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           m.SenderName.Contains(query, StringComparison.OrdinalIgnoreCase));
        
        SearchResults = _messages.CreateView(searchFilter, Scheduler.Default, 100);
        
        // Group messages by conversation
        Conversations = _messages.GroupBy(
            m => m.ConversationId,
            Scheduler.Default
        );
        
        // Timeline view (most recent first)
        Timeline = _messages.SortBy(
            m => m.Timestamp,
            descending: true,
            Scheduler.Default
        );
        
        // Subscribe to mention alerts
        _subscriptions.Add(
            MentionAlerts.Subscribe(m =>
                Console.WriteLine($"📢 You were mentioned by {m.SenderName}: {m.Text}"))
        );
    }
    
    // Called when message received
    public void ReceiveMessage(ChatMessage message)
    {
        _messages.Add(message);
    }
    
    // Search messages
    public void Search(string query)
    {
        _searchQuery.OnNext(query);
    }
    
    // Get messages for conversation
    public IReadOnlyList<ChatMessage>? GetConversation(string conversationId)
    {
        return Conversations.TryGetValue(conversationId, out var messages)
            ? messages
            : null;
    }
    
    // Delete old messages
    public void PurgeOldMessages(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        _messages.RemoveMany(m => m.Timestamp < cutoff);
    }
    
    public void Dispose()
    {
        _subscriptions.Dispose();
        SearchResults.Dispose();
        Conversations.Dispose();
        Timeline.Dispose();
        _messages.Dispose();
        _searchQuery.Dispose();
    }
}

public record ChatMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string ConversationId { get; init; } = "";
    public string SenderId { get; init; } = "";
    public string SenderName { get; init; } = "";
    public string Text { get; init; } = "";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
```

### WPF Data Binding

```csharp
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Windows.Threading;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ReactiveList<TodoItem> _todos = new();
    private readonly FilteredReactiveView<TodoItem> _activeTodosView;
    private readonly FilteredReactiveView<TodoItem> _completedTodosView;
    private readonly CompositeDisposable _subscriptions = new();
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    // Bindable collections
    public ReadOnlyObservableCollection<TodoItem> AllTodos { get; }
    public ReadOnlyObservableCollection<TodoItem> ActiveTodos { get; private set; }
    public ReadOnlyObservableCollection<TodoItem> CompletedTodos { get; private set; }
    
    public int TotalCount => _todos.Count;
    public int ActiveCount => ActiveTodos?.Count ?? 0;
    
    public MainViewModel()
    {
        // Use Dispatcher scheduler for WPF thread safety
        var wpfScheduler = DispatcherScheduler.Current;
        
        AllTodos = _todos.Items;
        
        // Active todos view
        _activeTodosView = _todos.CreateView(
            filter: t => !t.IsCompleted,
            scheduler: wpfScheduler,
            throttleMs: 50
        ).ToProperty(out ActiveTodos!);
        
        // Completed todos view
        _completedTodosView = _todos.CreateView(
            filter: t => t.IsCompleted,
            scheduler: wpfScheduler,
            throttleMs: 50
        ).ToProperty(out CompletedTodos!);
        
        // Update counts when collection changes
        _subscriptions.Add(
            _todos.Stream
                .ObserveOn(wpfScheduler)
                .Subscribe(_ =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalCount)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveCount)));
                })
        );
    }
    
    public void AddTodo(string title)
    {
        _todos.Add(new TodoItem { Title = title });
    }
    
    public void ToggleComplete(TodoItem todo)
    {
        var index = _todos.IndexOf(todo);
        if (index >= 0)
        {
            _todos[index] = todo with { IsCompleted = !todo.IsCompleted };
        }
    }
    
    public void RemoveTodo(TodoItem todo)
    {
        _todos.Remove(todo);
    }
    
    public void ClearCompleted()
    {
        _todos.RemoveMany(t => t.IsCompleted);
    }
    
    public void Dispose()
    {
        _subscriptions.Dispose();
        _activeTodosView.Dispose();
        _completedTodosView.Dispose();
        _todos.Dispose();
    }
}

public record TodoItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Title { get; init; } = "";
    public bool IsCompleted { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}
```

**XAML:**

```xml
<Window x:Class="TodoApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Todo App" Height="400" Width="600">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <StackPanel Orientation="Horizontal" Margin="10">
            <TextBox x:Name="NewTodoText" Width="300" Margin="0,0,10,0"/>
            <Button Content="Add" Click="AddTodo_Click"/>
        </StackPanel>
        
        <TabControl Grid.Row="1" Margin="10">
            <TabItem Header="{Binding TotalCount, StringFormat='All ({0})'}">
                <ListBox ItemsSource="{Binding AllTodos}"/>
            </TabItem>
            <TabItem Header="{Binding ActiveCount, StringFormat='Active ({0})'}">
                <ListBox ItemsSource="{Binding ActiveTodos}"/>
            </TabItem>
            <TabItem Header="Completed">
                <ListBox ItemsSource="{Binding CompletedTodos}"/>
            </TabItem>
        </TabControl>
        
        <Button Grid.Row="2" Content="Clear Completed" Click="ClearCompleted_Click" Margin="10"/>
    </Grid>
</Window>
```

### Avalonia UI Data Binding

```csharp
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Views;
using System.Collections.ObjectModel;
using ReactiveUI;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

public class MainViewModel : ReactiveObject, IDisposable
{
    private readonly ReactiveList<Product> _products = new();
    private readonly CompositeDisposable _subscriptions = new();
    
    private string _searchText = "";
    private ReadOnlyObservableCollection<Product>? _filteredProducts;
    
    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }
    
    public ReadOnlyObservableCollection<Product>? FilteredProducts
    {
        get => _filteredProducts;
        private set => this.RaiseAndSetIfChanged(ref _filteredProducts, value);
    }
    
    public MainViewModel()
    {
        // Create dynamic search filter
        var searchFilter = this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .Select<string, Func<Product, bool>>(text =>
                string.IsNullOrWhiteSpace(text)
                    ? _ => true
                    : p => p.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                           p.Category.Contains(text, StringComparison.OrdinalIgnoreCase));
        
        // Create filtered view with Avalonia's UI thread scheduler
        var view = _products.CreateView(
            filterObservable: searchFilter,
            scheduler: RxApp.MainThreadScheduler,
            throttleMs: 50
        ).ToProperty(out var items);
        
        FilteredProducts = items;
        _subscriptions.Add(view);
        
        // Load sample data
        LoadSampleData();
    }
    
    private void LoadSampleData()
    {
        _products.AddRange([
            new Product { Name = "Laptop", Category = "Electronics", Price = 999.99m },
            new Product { Name = "Headphones", Category = "Electronics", Price = 149.99m },
            new Product { Name = "Desk Chair", Category = "Furniture", Price = 299.99m },
            new Product { Name = "Monitor", Category = "Electronics", Price = 399.99m },
            new Product { Name = "Keyboard", Category = "Electronics", Price = 79.99m }
        ]);
    }
    
    public void Dispose()
    {
        _subscriptions.Dispose();
        _products.Dispose();
    }
}

public record Product
{
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public decimal Price { get; init; }
}
```

**Avalonia XAML:**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="ProductCatalog.MainWindow"
        Title="Product Catalog" Width="600" Height="400">
    <DockPanel Margin="10">
        <TextBox DockPanel.Dock="Top" 
                 Watermark="Search products..." 
                 Text="{Binding SearchText}"
                 Margin="0,0,0,10"/>
        
        <DataGrid ItemsSource="{Binding FilteredProducts}" 
                  AutoGenerateColumns="False"
                  IsReadOnly="True">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="*"/>
                <DataGridTextColumn Header="Category" Binding="{Binding Category}" Width="*"/>
                <DataGridTextColumn Header="Price" Binding="{Binding Price, StringFormat=C}" Width="100"/>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</Window>
```

---

## Thread Safety

All ReactiveList collections are designed to be thread-safe:

```csharp
var list = new ReactiveList<int>();

// Safe to call from multiple threads
Parallel.For(0, 1000, i => list.Add(i));

// Safe concurrent reads and writes
var tasks = new[]
{
    Task.Run(() => { for (int i = 0; i < 100; i++) list.Add(i); }),
    Task.Run(() => { for (int i = 0; i < 100; i++) list.Remove(i % 50); }),
    Task.Run(() => { foreach (var item in list) Console.Write(item); })
};
await Task.WhenAll(tasks);
```

**Key Thread Safety Features:**

1. **ReactiveList\<T\>**: Uses lock-based synchronization
2. **Reactive2DList\<T\>**: Inherits ReactiveList thread safety
3. **QuaternaryList\<T\>**: Uses `ReaderWriterLockSlim` with 4 independent shards
4. **QuaternaryDictionary\<TKey, TValue\>**: Uses `ReaderWriterLockSlim` with 4 independent shards

**Warning:** Some methods like `AsSpan()` and `AsMemory()` return direct references without copying and are NOT thread-safe. Use only when you can guarantee no concurrent modifications.

---

## Performance Considerations

### Use Edit() for Batch Operations

```csharp
// ❌ Bad: Multiple notifications
for (int i = 0; i < 1000; i++)
    list.Add(i);

// ✅ Good: Single notification
list.Edit(editor =>
{
    for (int i = 0; i < 1000; i++)
        editor.Add(i);
});

// ✅ Also good: AddRange
list.AddRange(Enumerable.Range(0, 1000));
```

### Use Throttling for Rapid Updates

```csharp
// Views throttle by default (50ms)
var view = list.CreateView(x => x.IsActive, throttleMs: 100);

// Manual throttling on streams
list.Stream
    .ThrottleNotifications(TimeSpan.FromMilliseconds(100))
    .Subscribe(HandleChange);
```

### Use QuaternaryList for Large Datasets

```csharp
// For datasets > 1000 items with frequent removes
var largeList = new QuaternaryList<DataPoint>();

// Low-allocation sharded storage for large batches
largeList.RemoveMany(dp => dp.Timestamp < cutoff);
```

### Use Secondary Indices for Frequent Lookups

```csharp
// O(n) without index
var result = list.Where(x => x.Category == "Electronics").ToList();

// O(1) with index
list.AddIndex("ByCategory", x => x.Category);
var result = list.GetItemsBySecondaryIndex("ByCategory", "Electronics");
```

### Dispose Views When Done

```csharp
// Views hold subscriptions - always dispose
using var view = list.CreateView(x => x.IsActive);
// or
view.Dispose();
```

---

## Benchmark Results

> Benchmarks run May 4, 2026 on Windows 11, Intel Core Ultra 9 185H, .NET SDK 10.0.203, .NET 10.0.7, BenchmarkDotNet 0.15.8, ShortRun. DynamicData comparisons use `ReactiveMarbles.DynamicData`. The `SourceCache<TObject,TKey>` `AddRange` benchmark uses prebuilt `Item[]` values so object creation and LINQ projection are not charged to DynamicData.

### `QuaternaryList<T>` vs `SourceList<T>` AddRange

| Method | Count | Mean | Allocated |
|--------|------:|-----:|----------:|
| QuaternaryList_AddRange | 100 | 1,268.94 ns | 2,576 B |
| SourceList_AddRange | 100 | 1,278.49 ns | 2,456 B |
| QuaternaryList_AddRange | 1,000 | 3,262.75 ns | 6,160 B |
| SourceList_AddRange | 1,000 | 9,160.45 ns | 13,296 B |
| QuaternaryList_AddRange | 10,000 | 25,206.70 ns | 67,600 B |
| SourceList_AddRange | 10,000 | 93,731.01 ns | 172,272 B |

**Summary:** `QuaternaryList<T>` now edges `SourceList<T>` in the measured 100 item micro-batch and has wider wins at 1,000 and 10,000 items while allocating much less memory. Prefer it for larger batches, sharded workloads, secondary-index lookups, and memory-sensitive streams.

### `QuaternaryDictionary<TKey, TValue>` vs `SourceCache<TValue, TKey>` AddRange

| Method | Count | Mean | Allocated |
|--------|------:|-----:|----------:|
| QuaternaryDictionary_AddRange | 100 | 2.349 us | 7.23 KB |
| SourceCache_AddRange | 100 | 2.697 us | 10.68 KB |
| QuaternaryDictionary_AddRange | 1,000 | 10.413 us | 42.23 KB |
| SourceCache_AddRange | 1,000 | 25.070 us | 100.55 KB |
| QuaternaryDictionary_AddRange | 10,000 | 80.972 us | 322.24 KB |
| SourceCache_AddRange | 10,000 | 545.788 us | 920.76 KB |

**Summary:** `QuaternaryDictionary<TKey,TValue>` is faster and allocates less memory than `SourceCache<TValue,TKey>` at every measured size. Prefer it when you need a thread-safe reactive cache with bulk writes, key lookup, and optional value-based secondary indices.

### When to Use Which Collection

| Scenario | Recommendation |
|----------|---------------|
| Ordered, UI-bound lists with familiar `IList<T>` behavior | `ReactiveList<T>` |
| Two-dimensional row/column data | `Reactive2DList<T>` |
| Small ordered batches where DynamicData operators are already central | DynamicData `SourceList<T>` |
| Large list batches, frequent removes, secondary-index lookups, or lower allocation pressure | `QuaternaryList<T>` |
| Key-value cache workloads with bulk writes and value indices | `QuaternaryDictionary<TKey,TValue>` |
| Rich DynamicData operator graph already required | DynamicData `SourceList<T>` / `SourceCache<TValue,TKey>` |
| Thread-safe concurrent access with reactive notifications | All ReactiveList collections |
| .NET Framework `net462`, `net472`, `net48`, or `net481` | `ReactiveList<T>`, `Reactive2DList<T>`, `QuaternaryList<T>`, `QuaternaryDictionary<TKey,TValue>` |

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

**ReactiveList** - Empowering Reactive Applications with Observable Collections
