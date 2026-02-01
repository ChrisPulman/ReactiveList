# ReactiveList

[![NuGet](https://img.shields.io/nuget/v/ReactiveList.svg?style=flat-square)](https://www.nuget.org/packages/ReactiveList/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ReactiveList.svg?style=flat-square)](https://www.nuget.org/packages/ReactiveList/)
[![License](https://img.shields.io/github/license/ChrisPulman/ReactiveList.svg?style=flat-square)](LICENSE)
[![Build Status](https://img.shields.io/github/actions/workflow/status/ChrisPulman/ReactiveList/BuildOnly.yml?branch=main&style=flat-square)](https://github.com/ChrisPulman/ReactiveList/actions)

A lightweight, high-performance reactive list with fine-grained change tracking built on [System.Reactive](https://github.com/dotnet/reactive).

**Targets:** .NET Standard 2.0 | .NET 8 | .NET 9 | .NET 10

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

- [Why ReactiveList?](#why-reactivelist)
- [Quick Start](#quick-start)
- [ReactiveList Features](#reactivelist-features)
  - [Basic Operations](#basic-operations)
  - [Batch Operations with Edit](#batch-operations-with-edit)
  - [Moving Items](#moving-items)
  - [Reactive Subscriptions](#reactive-subscriptions)
  - [Change Tracking](#change-tracking)
- [Reactive2DList Features](#reactive2dlist-features)
  - [Constructing a 2D List](#constructing-a-2d-list)
  - [Accessing Items](#accessing-items)
  - [Modifying Inner Lists](#modifying-inner-lists)
  - [Utility Methods](#utility-methods)
- [UI Binding](#ui-binding)
  - [WPF / WinUI](#wpf--winui)
  - [Avalonia](#avalonia)
- [Use Cases](#use-cases)
- [API Reference](#api-reference)
- [Behavior Notes](#behavior-notes)
- [Building Locally](#building-locally)
- [License](#license)

---

## Why ReactiveList?

| Feature | Description |
|---------|-------------|
| **Reactive** | Subscribe to changes as they happen with `Added`, `Removed`, `Changed`, and `CurrentItems` observables |
| **UI-friendly** | Implements `INotifyCollectionChanged` and `INotifyPropertyChanged` for seamless data binding |
| **Easy binding** | Exposes `ReadOnlyObservableCollection<T>` for direct UI binding |
| **Granular change info** | Access the last Added/Removed/Changed batch via collections and observables |
| **Batch operations** | Use `Edit()` for atomic, batched modifications with a single notification |
| **Familiar API** | Implements `IList<T>`, `IList`, `IReadOnlyList<T>`, and `ICancelable` |
| **Thread-safe synchronization** | Internal synchronization for `ReplaceAll` operations |

---

## Quick Start

This library provides:

- A `ReactiveList<T>` implementation that allows you to observe changes in real-time.
- A `Reactive2DList<T>` for managing a list of lists (2D structure) with reactive capabilities.

> Net 8 and above also includes:
- A `QuaternaryDictionary<TKey, TValue>` for high-performance key-value storage with reactive features.
- A `QuaternaryList<T>` for optimized list like operations at scale with reactive capabilities.

Here's a quick example to get you started:

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

// Cleanup
list.Dispose();
```

---

## ReactiveList Features

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
list.Clear();                               // Remove all items

// Updating items
list.Update("old", "new");                  // Replace specific item

// Replacing all items
list.ReplaceAll(["new", "items"]);         // Clear and add in one operation

// Accessing items
var item = list[0];                         // Get by index
list[0] = "updated";                        // Set by index
var index = list.IndexOf("item");           // Find index
var contains = list.Contains("item");       // Check existence
var count = list.Count;                     // Get count
```

### Batch Operations with Edit

The `Edit` method allows you to perform multiple operations atomically with a single change notification. This is more efficient than individual operations when making multiple changes.

```csharp
var list = new ReactiveList<int>([1, 2, 3, 4, 5]);

// Without Edit: 3 separate notifications
list.Add(6);
list.RemoveAt(0);
list.Insert(2, 100);

// With Edit: single notification for all changes
list.Edit(l =>
{
    l.Add(6);
    l.RemoveAt(0);
    l.Insert(2, 100);
    l.AddRange([7, 8, 9]);
    l.Move(0, 3);
});

// Complex transformations
list.Edit(l =>
{
    // Clear and rebuild
    l.Clear();
    for (int i = 0; i < 10; i++)
    {
        l.Add(i * 10);
    }
});
```

### Moving Items

Reorder items within the list without remove/add overhead:

```csharp
var list = new ReactiveList<string>(["A", "B", "C", "D", "E"]);

// Move "A" from index 0 to index 3
list.Move(0, 3);
// Result: ["B", "C", "D", "A", "E"]

// Move last item to first position
list.Move(list.Count - 1, 0);
// Result: ["E", "B", "C", "D", "A"]

// Moving to same index is a no-op
list.Move(2, 2); // No change, no notification
```

### Reactive Subscriptions

Subscribe to various change streams:

```csharp
var list = new ReactiveList<string>();

// Subscribe to items added in each change
var addedSub = list.Added.Subscribe(added =>
{
    foreach (var item in added)
    {
        Console.WriteLine($"Added: {item}");
    }
});

// Subscribe to items removed in each change
var removedSub = list.Removed.Subscribe(removed =>
{
    Console.WriteLine($"Removed {removed.Count()} items");
});

// Subscribe to any change (add/remove/replace)
var changedSub = list.Changed.Subscribe(changed =>
{
    Console.WriteLine($"Changed: {string.Join(", ", changed)}");
});

// Subscribe to current items snapshot (fires on count changes)
var currentSub = list.CurrentItems.Subscribe(items =>
{
    Console.WriteLine($"Current count: {items.Count()}");
    Console.WriteLine($"Sum: {items.Sum(x => x.Length)}"); // Example aggregation
});

// You can also subscribe directly to the list (subscribes to CurrentItems)
var directSub = list.Subscribe(items =>
{
    Console.WriteLine($"Items: [{string.Join(", ", items)}]");
});

// Cleanup
addedSub.Dispose();
removedSub.Dispose();
changedSub.Dispose();
currentSub.Dispose();
directSub.Dispose();
list.Dispose();
```

### Change Tracking

Access change information via collections (snapshot of last change):

```csharp
var list = new ReactiveList<string>();

list.AddRange(["one", "two", "three"]);

Console.WriteLine($"Items Added: {list.ItemsAdded.Count}");       // 3
Console.WriteLine($"Items Changed: {list.ItemsChanged.Count}");   // 3
Console.WriteLine($"Items Removed: {list.ItemsRemoved.Count}");   // 0

list.Remove("two");

Console.WriteLine($"Items Added: {list.ItemsAdded.Count}");       // 0
Console.WriteLine($"Items Changed: {list.ItemsChanged.Count}");   // 1
Console.WriteLine($"Items Removed: {list.ItemsRemoved.Count}");   // 1

// ReplaceAll behavior
list.ReplaceAll(["a", "b"]);

Console.WriteLine($"Items Added: {list.ItemsAdded.Count}");       // 2 (new items)
Console.WriteLine($"Items Changed: {list.ItemsChanged.Count}");   // 2 (cleared items)
Console.WriteLine($"Items Removed: {list.ItemsRemoved.Count}");   // 2 (cleared items)
```

---

## Reactive2DList Features

`Reactive2DList<T>` is a reactive list of reactive lists, perfect for grid-like or table data structures.

```csharp
Reactive2DList<T> : ReactiveList<ReactiveList<T>>
```

### Constructing a 2D List

```csharp
using CP.Reactive;

// Empty 2D list
var grid = new Reactive2DList<int>();

// From nested collections
var grid2 = new Reactive2DList<int>(new[]
{
    new[] { 1, 2, 3 },
    new[] { 4, 5, 6 },
    new[] { 7, 8, 9 }
});

// From existing ReactiveList rows
var row1 = new ReactiveList<int>([10, 20]);
var row2 = new ReactiveList<int>([30, 40]);
var grid3 = new Reactive2DList<int>([row1, row2]);

// From flat collection (each item becomes a single-element row)
var grid4 = new Reactive2DList<int>([1, 2, 3]);
// Result: [ [1], [2], [3] ]

// From single ReactiveList (becomes single row)
var grid5 = new Reactive2DList<int>(new ReactiveList<int>([1, 2, 3]));
// Result: [ [1, 2, 3] ]

// From single value (one row, one item)
var grid6 = new Reactive2DList<int>(42);
// Result: [ [42] ]
```

### Accessing Items

```csharp
var grid = new Reactive2DList<string>(new[]
{
    new[] { "A1", "A2", "A3" },
    new[] { "B1", "B2" },
    new[] { "C1", "C2", "C3", "C4" }
});

// Access via GetItem (bounds-checked)
var item = grid.GetItem(1, 0);  // "B1"

// Access via indexers
var row = grid[0];              // ReactiveList<string> ["A1", "A2", "A3"]
var cell = grid[0][1];          // "A2"

// Set item at specific position
grid.SetItem(2, 1, "UPDATED");  // grid[2][1] = "UPDATED"

// Get total count of all items
var total = grid.TotalCount();  // 9

// Get flattened enumerable of all items
var all = grid.Flatten();       // ["A1", "A2", "A3", "B1", "B2", "C1", "UPDATED", "C3", "C4"]
```

### Modifying Inner Lists

```csharp
var grid = new Reactive2DList<int>(new[]
{
    new[] { 1, 2 },
    new[] { 3, 4 }
});

// Add items to a specific row
grid.AddToInner(0, 99);                  // Row 0: [1, 2, 99]
grid.AddToInner(1, new[] { 5, 6 });      // Row 1: [3, 4, 5, 6]

// Remove item from inner list
grid.RemoveFromInner(0, 1);              // Row 0: [1, 99] (removed index 1)

// Clear a specific row
grid.ClearInner(1);                      // Row 1: [] (empty)

// Insert items into a row at specific position
grid.Insert(0, new[] { 10, 20 }, 0);     // Insert at row 0, inner index 0
                                          // Row 0: [10, 20, 1, 99]
```

### Utility Methods

```csharp
var grid = new Reactive2DList<int>(new[]
{
    new[] { 1, 2, 3 },
    new[] { 4, 5 },
    new[] { 6 }
});

// Get total count across all rows
var total = grid.TotalCount();  // 6

// Get flattened list
var flat = grid.Flatten().ToList();  // [1, 2, 3, 4, 5, 6]

// Use with LINQ
var sum = grid.Flatten().Sum();      // 21
var max = grid.Flatten().Max();      // 6

// Check if any row is empty
var hasEmpty = grid.Items.Any(row => row.Count == 0);  // false

// Find row with most items
var largestRow = grid.Items.OrderByDescending(r => r.Count).First();
```

### Adding and Inserting Rows

```csharp
var grid = new Reactive2DList<string>();

// Add rows from nested collections
grid.AddRange(new[]
{
    new[] { "a", "b" },
    new[] { "c", "d", "e" }
});

// Add single-element rows from flat collection
grid.AddRange(new[] { "x", "y" });  // Adds rows: ["x"], ["y"]

// Insert a new row at specific index
grid.Insert(0, new[] { "first", "row" });

// Insert a single-element row
grid.Insert(1, "solo");
```

---

## UI Binding

### WPF / WinUI

`ReactiveList<T>` implements `INotifyCollectionChanged` and `INotifyPropertyChanged`, making it directly bindable.

**ViewModel:**
```csharp
public class MainViewModel : IDisposable
{
    public IReactiveList<string> Items { get; } = new ReactiveList<string>(["One", "Two", "Three"]);

    public void AddItem(string item) => Items.Add(item);
    public void RemoveItem(string item) => Items.Remove(item);
    public void ClearItems() => Items.Clear();

    public void Dispose() => Items.Dispose();
}
```

**XAML (direct binding):**
```xml
<ListBox ItemsSource="{Binding Items}" />
```

**XAML (binding to Items property):**
```xml
<ListBox ItemsSource="{Binding Items.Items}" />
```

**Nested binding for Reactive2DList:**
```xml
<ItemsControl ItemsSource="{Binding Grid}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <ItemsControl ItemsSource="{Binding Items}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <StackPanel Orientation="Horizontal"/>
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border BorderBrush="Gray" BorderThickness="1" Padding="8">
                            <TextBlock Text="{Binding}"/>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

### Avalonia

Works the same as WPF:

```xml
<ListBox ItemsSource="{Binding Items}" />
```

---

## Use Cases

### Real-time Data Feed

```csharp
public class StockTickerViewModel : IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public IReactiveList<StockPrice> Prices { get; } = new ReactiveList<StockPrice>();

    public StockTickerViewModel(IObservable<StockPrice> priceFeed)
    {
        // Subscribe to real-time feed
        priceFeed
            .ObserveOn(RxApp.MainThreadScheduler) // Dispatch to UI thread
            .Subscribe(price =>
            {
                var existing = Prices.Items.FirstOrDefault(p => p.Symbol == price.Symbol);
                if (existing != null)
                {
                    Prices.Update(existing, price);
                }
                else
                {
                    Prices.Add(price);
                }
            })
            .DisposeWith(_disposables);
    }

    public void Dispose() => _disposables.Dispose();
}

public record StockPrice(string Symbol, decimal Price, DateTime Timestamp);
```

### Task List with Change Logging

```csharp
public class TaskListViewModel : IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public IReactiveList<TodoItem> Tasks { get; } = new ReactiveList<TodoItem>();
    public ReactiveList<string> ActivityLog { get; } = new();

    public TaskListViewModel()
    {
        Tasks.Added
            .Subscribe(items =>
            {
                foreach (var item in items)
                {
                    ActivityLog.Add($"[{DateTime.Now:HH:mm:ss}] Added: {item.Title}");
                }
            })
            .DisposeWith(_disposables);

        Tasks.Removed
            .Subscribe(items =>
            {
                foreach (var item in items)
                {
                    ActivityLog.Add($"[{DateTime.Now:HH:mm:ss}] Removed: {item.Title}");
                }
            })
            .DisposeWith(_disposables);
    }

    public void AddTask(string title) => Tasks.Add(new TodoItem(title));

    public void CompleteTask(TodoItem task)
    {
        Tasks.Remove(task);
        ActivityLog.Add($"[{DateTime.Now:HH:mm:ss}] Completed: {task.Title}");
    }

    public void Dispose() => _disposables.Dispose();
}

public record TodoItem(string Title);
```

### Spreadsheet-like Grid

```csharp
public class SpreadsheetViewModel : IDisposable
{
    public Reactive2DList<CellValue> Cells { get; }

    public SpreadsheetViewModel(int rows, int cols)
    {
        var data = Enumerable.Range(0, rows)
            .Select(r => Enumerable.Range(0, cols)
                .Select(c => new CellValue($"R{r}C{c}"))
                .ToArray())
            .ToArray();

        Cells = new Reactive2DList<CellValue>(data);
    }

    public void SetCell(int row, int col, string value)
    {
        Cells.SetItem(row, col, new CellValue(value));
    }

    public CellValue GetCell(int row, int col) => Cells.GetItem(row, col);

    public void AddRow()
    {
        var cols = Cells.Count > 0 ? Cells[0].Count : 1;
        var newRow = Enumerable.Range(0, cols)
            .Select(c => new CellValue(string.Empty))
            .ToArray();
        Cells.Add(new ReactiveList<CellValue>(newRow));
    }

    public void AddColumn()
    {
        for (int i = 0; i < Cells.Count; i++)
        {
            Cells.AddToInner(i, new CellValue(string.Empty));
        }
    }

    public IEnumerable<string> GetAllValues() =>
        Cells.Flatten().Select(c => c.Value);

    public void Dispose() => Cells.Dispose();
}

public record CellValue(string Value);
```

### Batch Import with Progress

```csharp
public class DataImportViewModel : IDisposable
{
    public IReactiveList<DataRecord> Records { get; } = new ReactiveList<DataRecord>();

    public async Task ImportBatchAsync(IEnumerable<DataRecord> records, IProgress<int> progress)
    {
        var batch = records.ToList();
        var processed = 0;

        // Use Edit for efficient batch import
        Records.Edit(list =>
        {
            foreach (var record in batch)
            {
                list.Add(record);
                processed++;

                if (processed % 100 == 0)
                {
                    progress.Report(processed);
                }
            }
        });

        progress.Report(processed);
    }

    public void ClearAndReplace(IEnumerable<DataRecord> newRecords)
    {
        // Atomic replace
        Records.ReplaceAll(newRecords);
    }

    public void Dispose() => Records.Dispose();
}

public record DataRecord(int Id, string Name, DateTime Created);
```

---

## API Reference

### IReactiveList&lt;T&gt; Interface

**Interfaces Implemented:**
- `IList<T>`, `IList`, `IReadOnlyList<T>`
- `INotifyCollectionChanged`, `INotifyPropertyChanged`
- `ICancelable`

**Properties:**

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
| `IsDisposed` | `bool` | Whether the list has been disposed |

**Methods:**

| Method | Description |
|--------|-------------|
| `Add(T item)` | Add single item |
| `AddRange(IEnumerable<T> items)` | Add multiple items |
| `Insert(int index, T item)` | Insert at index |
| `InsertRange(int index, IEnumerable<T> items)` | Insert range at index |
| `Remove(T item)` | Remove by value |
| `Remove(IEnumerable<T> items)` | Remove multiple items |
| `RemoveAt(int index)` | Remove at index |
| `RemoveRange(int index, int count)` | Remove range |
| `Clear()` | Remove all items |
| `Move(int oldIndex, int newIndex)` | Move item to new position |
| `Update(T item, T newValue)` | Replace specific item |
| `ReplaceAll(IEnumerable<T> items)` | Clear and add atomically |
| `Edit(Action<IExtendedList<T>> editAction)` | Batch operations |
| `IndexOf(T item)` | Find index of item |
| `Contains(T item)` | Check if item exists |
| `Subscribe(IObserver<IEnumerable<T>> observer)` | Subscribe to CurrentItems |
| `Dispose()` | Clean up resources |

**Events:**

| Event | Description |
|-------|-------------|
| `CollectionChanged` | Raised when collection changes |
| `PropertyChanged` | Raised when properties change |

### Reactive2DList&lt;T&gt; Class

Inherits from `ReactiveList<ReactiveList<T>>` and adds:

| Method | Description |
|--------|-------------|
| `GetItem(int outerIndex, int innerIndex)` | Get item at [row][col] |
| `SetItem(int outerIndex, int innerIndex, T value)` | Set item at [row][col] |
| `AddToInner(int outerIndex, T item)` | Add item to specific row |
| `AddToInner(int outerIndex, IEnumerable<T> items)` | Add items to specific row |
| `RemoveFromInner(int outerIndex, int innerIndex)` | Remove item from row |
| `ClearInner(int outerIndex)` | Clear specific row |
| `Flatten()` | Get all items as flat enumerable |
| `TotalCount()` | Get total count across all rows |
| `Insert(int index, IEnumerable<T> items)` | Insert new row |
| `Insert(int index, T item)` | Insert single-element row |
| `Insert(int index, IEnumerable<T> items, int innerIndex)` | Insert into existing row |
| `AddRange(IEnumerable<IEnumerable<T>> items)` | Add multiple rows |
| `AddRange(IEnumerable<T> items)` | Add single-element rows |

---

## Behavior Notes

- **Scheduler**: Observables run on `Scheduler.Immediate` inside the list. Dispatch to your UI thread if updating UI from subscriptions.

- **Change snapshots**: `ItemsAdded`, `ItemsRemoved`, `ItemsChanged` contain only the last change batch (not cumulative).

- **ReplaceAll semantics**: Performs clear + add-range internally:
  - `ItemsRemoved` contains the cleared items
  - `ItemsAdded` contains the new items
  - `ItemsChanged` reflects the clear operation
  - A `Reset` notification is raised

- **Edit batching**: Changes within `Edit()` result in a single property change notification.

- **Move optimization**: `Move()` is more efficient than Remove + Insert for reordering.

- **Thread safety**: `ReplaceAll` uses `ManualResetEventSlim` for efficient synchronization.

- **Disposal**: Always dispose `ReactiveList` instances to clean up subscriptions and internal resources.

---

## QuaternaryList&lt;T&gt; and QuaternaryDictionary&lt;TKey, TValue&gt;

High-performance, thread-safe, sharded collections optimized for large-scale reactive applications. These collections distribute data across four internal partitions (quads) to improve concurrency and reduce lock contention.

**Key Features:**
- **Sharded architecture**: Data distributed across 4 partitions for parallel access
- **Thread-safe**: Uses `ReaderWriterLockSlim` with fine-grained locking per shard
- **Reactive streams**: `Stream` property exposes `IObservable<CacheNotification<T>>` for change notifications
- **Secondary indices**: O(1) lookup support via custom key selectors
- **Low allocation**: Optimized with `ArrayPool<T>`, `Span<T>`, and custom pooled collections (`QuadList<T>`)
- **Batch operations**: Efficient bulk add/remove with parallel processing for large datasets

---

## QuaternaryList vs SourceList Benchmark Results

### Performance Comparison (Mean Time)

| Operation | Count | QuaternaryList | SourceList | Winner |
|-----------|-------|----------------|------------|--------|
| AddRange | 100 | 75.0 μs | 2.2 μs | SourceList |
| AddRange | 1,000 | 78.3 μs | 21.3 μs | SourceList |
| AddRange | 10,000 | 92.9 μs | 75.0 μs | SourceList (1.2x) |
| RemoveRange | 100 | 106.3 μs | 201.5 μs | **QuaternaryList 1.9x** |
| RemoveRange | 1,000 | 282.9 μs | 2,341.7 μs | **QuaternaryList 8.3x** |
| RemoveRange | 10,000 | **1,300.5 μs** | 22,886.7 μs | **QuaternaryList 17.6x** |
| Clear | 100 | 75.9 μs | 2.5 μs | SourceList |
| Clear | 1,000 | 79.1 μs | 22.7 μs | SourceList |
| Clear | 10,000 | **94.7 μs** | 150.9 μs | **QuaternaryList 1.6x** |
| Stream (Add) | 100 | 74.5 μs | 2.4 μs | SourceList |
| Stream (Add) | 1,000 | 78.9 μs | 7.8 μs | SourceList |
| Stream (Add) | 10,000 | 96.8 μs | 74.6 μs | SourceList (1.3x) |
| Edit (batch) | 100 | 79.9 μs | 1.4 μs | SourceList |
| Edit (batch) | 1,000 | 79.6 μs | 14.2 μs | SourceList |
| Edit (batch) | 10,000 | **107.3 μs** | 153.6 μs | **QuaternaryList 1.4x** |
| RemoveMany | 100 | 105.4 μs | 6.1 μs | SourceList |
| RemoveMany | 1,000 | 182.9 μs | 80.4 μs | SourceList |
| RemoveMany | 10,000 | **1,221.0 μs** | 10,353.3 μs | **QuaternaryList 8.5x** |

### Memory Allocation Comparison

| Operation | Count | QuaternaryList | SourceList | Winner |
|-----------|-------|----------------|------------|--------|
| AddRange | 1,000 | **15.6 KB** | 26.8 KB | **QuaternaryList 1.7x** |
| AddRange | 10,000 | **72.3 KB** | 172.3 KB | **QuaternaryList 2.4x** |
| RemoveRange | 1,000 | **17.9 KB** | 234.9 KB | **QuaternaryList 13.1x** |
| RemoveRange | 10,000 | **75.0 KB** | 2,373.3 KB | **QuaternaryList 31.6x** |
| Clear | 1,000 | **15.6 KB** | 26.8 KB | **QuaternaryList 1.7x** |
| Clear | 10,000 | **72.3 KB** | 253.0 KB | **QuaternaryList 3.5x** |
| Stream (Add) | 1,000 | **15.6 KB** | 13.9 KB | SourceList |
| Stream (Add) | 10,000 | **138.6 KB** | 172.8 KB | **QuaternaryList 1.2x** |
| Edit (batch) | 1,000 | **12.9 KB** | 26.2 KB | **QuaternaryList 2.0x** |
| Edit (batch) | 10,000 | **105.0 KB** | 344.2 KB | **QuaternaryList 3.3x** |
| RemoveMany | 1,000 | **15.1 KB** | 253.7 KB | **QuaternaryList 16.8x** |
| RemoveMany | 10,000 | **138.2 KB** | 2,758.6 KB | **QuaternaryList 20.0x** |

### Key Takeaways

**QuaternaryList excels at (10,000 items):**
- **RemoveRange**: 17.6x faster than SourceList
- **RemoveMany**: 8.5x faster than SourceList  
- **Edit (batch)**: 1.4x faster than SourceList
- **Clear**: 1.6x faster than SourceList
- **Memory**: Uses 2-32x less memory depending on operation

**SourceList is better for:**
- Small datasets (<500 items)
- Individual add operations
- Simple single-item workflows

---

## QuaternaryDictionary vs SourceCache Benchmark Results

### Performance Comparison (Mean Time)

| Operation | Count | QuaternaryDict | SourceCache | Winner |
|-----------|-------|----------------|-------------|--------|
| AddRange | 100 | 72.3 μs | 2.2 μs | SourceCache |
| AddRange | 1,000 | 77.9 μs | 19.3 μs | SourceCache |
| AddRange | 10,000 | **135.9 μs** | 387.7 μs | **QuaternaryDict 2.9x** |
| Clear | 100 | 76.9 μs | 2.4 μs | SourceCache |
| Clear | 1,000 | 78.1 μs | 21.2 μs | SourceCache |
| Clear | 10,000 | **138.9 μs** | 420.1 μs | **QuaternaryDict 3.0x** |
| Lookup | 100 | 72.5 μs | 2.3 μs | SourceCache |
| Lookup | 1,000 | 77.5 μs | 21.3 μs | SourceCache |
| Lookup | 10,000 | **136.5 μs** | 397.4 μs | **QuaternaryDict 2.9x** |
| Stream (Add) | 10,000 | **198.2 μs** | 1,739.9 μs | **QuaternaryDict 8.8x** |

### Memory Allocation Comparison

| Operation | Count | QuaternaryDict | SourceCache | Winner |
|-----------|-------|----------------|-------------|--------|
| AddRange | 1,000 | **47.0 KB** | 124.1 KB | **QuaternaryDict 2.6x** |
| AddRange | 10,000 | **327.2 KB** | 1,155.7 KB | **QuaternaryDict 3.5x** |
| Clear | 1,000 | **47.0 KB** | 124.2 KB | **QuaternaryDict 2.6x** |
| Clear | 10,000 | **327.2 KB** | 1,155.8 KB | **QuaternaryDict 3.5x** |
| Lookup | 10,000 | **327.2 KB** | 1,155.7 KB | **QuaternaryDict 3.5x** |
| Stream | 10,000 | **456.0 KB** | 2,437.9 KB | **QuaternaryDict 5.3x** |

---

## When to Use Quaternary Collections

### Choose QuaternaryDictionary/QuaternaryList when:
1. **Large datasets (>1,000 items)** - Performance advantage kicks in at scale
2. **Bulk removal operations** - Up to 17x faster RemoveRange, 8x faster RemoveMany
3. **Memory-constrained environments** - 2-32x less allocation than DynamicData
4. **Batch edit operations at scale** - 1.4x faster Edit at 10,000+ items
5. **High-concurrency scenarios** - Sharded design reduces lock contention
6. **Real-time data feeds** - Efficient streaming with low allocation

### Choose DynamicData (SourceCache/SourceList) when:
1. **Small datasets (<500 items)** - Lower fixed overhead
2. **Frequent single-item operations** - Individual Add/Remove is faster
3. **Rich query operators** - DynamicData has extensive LINQ-like operators
4. **Existing codebase** - Already using DynamicData patterns

---

## QuaternaryList&lt;T&gt; API Reference


### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count` | `int` | Total number of items across all shards |
| `IsReadOnly` | `bool` | Always returns `false` |
| `Stream` | `IObservable<CacheNotification<T>>` | Observable stream of change notifications |

### Methods

| Method | Description |
|--------|-------------|
| `Add(T item)` | Add single item to appropriate shard |
| `AddRange(IEnumerable<T> items)` | Bulk add with parallel processing for large sets |
| `Remove(T item)` | Remove item from collection |
| `RemoveRange(IEnumerable<T> items)` | Bulk remove items |
| `RemoveMany(Func<T, bool> predicate)` | Remove items matching predicate |
| `Clear()` | Remove all items from all shards |
| `Contains(T item)` | Check if item exists (O(1) average) |
| `Edit(Action<IList<T>> editAction)` | Batch operations with single notification |
| `AddIndex<TKey>(string name, Func<T, TKey> keySelector)` | Add secondary index |
| `GetItemsBySecondaryIndex<TKey>(string indexName, TKey key)` | Query using secondary index |
| `CopyTo(T[] array, int arrayIndex)` | Copy all items to an array |
| `GetEnumerator()` | Enumerate all items across shards |

### Unsupported Methods (Due to Sharded Architecture)

| Method | Alternative |
|--------|-------------|
| `IndexOf(T item)` | Use `Contains(item)` to check existence |
| `Insert(int index, T item)` | Use `Add(item)` - sharding determines placement |
| `RemoveAt(int index)` | Use `Remove(item)` or `RemoveMany(predicate)` |
| `this[int index] { set; }` | Use `Remove` + `Add` for updates |

> **Note**: QuaternaryList distributes items across 4 shards based on hash code. Index-based operations would be misleading since item order is determined by the sharding algorithm, not insertion order.

### Secondary Indices

```csharp
var list = new QuaternaryList<Contact>();

// Add index for fast lookups by city
list.AddIndex("ByCity", c => c.City);

// Add index for department
list.AddIndex("ByDepartment", c => c.Department);

// Bulk add contacts
list.AddRange(contacts);

// Fast O(1) query by index
var newYorkers = list.GetItemsBySecondaryIndex("ByCity", "New York");
var engineers = list.GetItemsBySecondaryIndex("ByDepartment", "Engineering");
```

---

## QuaternaryDictionary&lt;TKey, TValue&gt; API Reference

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count` | `int` | Total number of key-value pairs |
| `Keys` | `ICollection<TKey>` | All keys in the dictionary |
| `Values` | `ICollection<TValue>` | All values in the dictionary |
| `IsReadOnly` | `bool` | Always returns `false` |
| `Stream` | `IObservable<CacheNotification<KeyValuePair<TKey, TValue>>>` | Change notifications |

### Methods

| Method | Description |
|--------|-------------|
| `Add(TKey key, TValue value)` | Add key-value pair (throws if exists) |
| `TryAdd(TKey key, TValue value)` | Add if not exists, returns success |
| `AddOrUpdate(TKey key, TValue value)` | Add or update existing value |
| `AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)` | Bulk add/update |
| `Remove(TKey key)` | Remove by key |
| `RemoveKeys(IEnumerable<TKey> keys)` | Bulk remove by keys |
| `RemoveMany(Func<TValue, bool> predicate)` | Remove values matching predicate |
| `Clear()` | Remove all entries |
| `TryGetValue(TKey key, out TValue value)` | Try get value by key |
| `Lookup(TKey key)` | Get `Optional<TValue>` by key |
| `ContainsKey(TKey key)` | Check if key exists |
| `Edit(Action<IDictionary<TKey, TValue>> editAction)` | Batch operations |
| `AddValueIndex<TIndexKey>(string name, Func<TValue, TIndexKey> keySelector)` | Add secondary value index |
| `QueryByValue<TIndexKey>(string indexName, TIndexKey key)` | Query by value index |

---

## Basic Usage Examples

### QuaternaryList Basic Operations

```csharp
using CP.Reactive;

// Create and populate
var list = new QuaternaryList<string>();
list.Add("item1");
list.AddRange(["item2", "item3", "item4"]);

// Query
Console.WriteLine(list.Count);            // 4
Console.WriteLine(list.Contains("item2")); // true

// Remove
list.Remove("item2");
list.RemoveMany(s => s.StartsWith("item")); // Remove matching items

// Batch operations (single notification)
list.Edit(l =>
{
    l.Add("new1");
    l.Add("new2");
    l.Clear();
    l.Add("fresh");
});

// Cleanup
list.Dispose();
```

### QuaternaryDictionary Basic Operations

```csharp
using CP.Reactive;

// Create and populate
var dict = new QuaternaryDictionary<int, string>();
dict.Add(1, "one");
dict.AddOrUpdate(2, "two");

// Bulk add
dict.AddRange(new[]
{
    KeyValuePair.Create(3, "three"),
    KeyValuePair.Create(4, "four")
});

// Query
if (dict.TryGetValue(1, out var value))
{
    Console.WriteLine(value); // "one"
}

// Using Lookup (returns Optional<T>)
var result = dict.Lookup(2);
if (result.HasValue)
{
    Console.WriteLine(result.Value); // "two"
}

// Remove
dict.Remove(1);
dict.RemoveKeys([2, 3]);

// Cleanup
dict.Dispose();
```

---

## Reactive Streams

### Subscribing to Changes

```csharp
using CP.Reactive;
using System.Reactive.Linq;

var list = new QuaternaryList<int>();

// Subscribe to all changes
var subscription = list.Stream
    .Subscribe(notification =>
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
            case CacheAction.BatchOperation:
                Console.WriteLine($"Batch completed: {notification.BatchItems?.Count()} items");
                break;
        }
    });

list.Add(1);     // Added: 1
list.Add(2);     // Added: 2
list.Remove(1);  // Removed: 1
list.Clear();    // Collection cleared

subscription.Dispose();
list.Dispose();
```

### Creating Filtered Views

```csharp
using CP.Reactive;
using ReactiveUI;

var contacts = new QuaternaryList<Contact>();

// Create an auto-updating view of favorites
var favoritesView = contacts.CreateView(
    c => c.IsFavorite,
    RxApp.MainThreadScheduler,
    throttleMs: 100);

// Bind to UI
favoritesView.ToProperty(x => FavoriteContacts = x);
```

---

## Migrating from DynamicData

### SourceList → QuaternaryList

```csharp
// DynamicData SourceList
var sourceList = new SourceList<Person>();
sourceList.AddRange(people);
sourceList.Edit(list =>
{
    list.Add(new Person("John"));
    list.RemoveAt(0);  // SourceList supports index-based removal
});
sourceList.Connect()
    .Filter(p => p.Age > 18)
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(changes => { });

// QuaternaryList equivalent
// Note: QuaternaryList is sharded, so index-based operations are not supported.
// Use item-based Remove() or RemoveMany() instead of RemoveAt().
var quaternaryList = new QuaternaryList<Person>();
quaternaryList.AddRange(people);
quaternaryList.Edit(list =>
{
    list.Add(new Person("John"));
    // Use Remove(item) instead of RemoveAt(index)
    var firstItem = list.FirstOrDefault();
    if (firstItem != null)
    {
        list.Remove(firstItem);
    }
});
quaternaryList.Stream
    .Where(n => n.Action == CacheAction.Added && n.Item?.Age > 18)
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(notification => { });

// Or use CreateView for filtered collections
quaternaryList.CreateView(p => p.Age > 18, RxApp.MainThreadScheduler)
    .ToProperty(x => Adults = x);
```

### SourceCache → QuaternaryDictionary

```csharp
// DynamicData SourceCache
var sourceCache = new SourceCache<Person, Guid>(p => p.Id);
sourceCache.AddOrUpdate(person);
sourceCache.Edit(cache =>
{
    cache.AddOrUpdate(new Person { Id = Guid.NewGuid(), Name = "John" });
    cache.Remove(oldId);
});
sourceCache.Connect()
    .Filter(p => p.IsActive)
    .ObserveOn(RxApp.MainThreadScheduler)
    .Bind(out var activeUsers)
    .Subscribe();


// QuaternaryDictionary equivalent
var quaternaryDict = new QuaternaryDictionary<Guid, Person>();
quaternaryDict.AddOrUpdate(person.Id, person);
quaternaryDict.Edit(dict =>
{
    dict[Guid.NewGuid()] = new Person { Name = "John" };
    dict.Remove(oldId);
});
quaternaryDict.Stream
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(notification => { });

// For filtered views
quaternaryDict.CreateView(p => p.IsActive, RxApp.MainThreadScheduler)
    .ToProperty(x => ActiveUsers = x);
```

### Key Migration Differences

| DynamicData | Quaternary | Notes |
|-------------|------------|-------|
| `Connect()` | `Stream` | Direct property access |
| `Filter()` | `CreateView()` or LINQ on `Stream` | Use CreateView for UI binding |
| `Bind()` | `ToProperty()` | Extension method for binding |
| `Watch()` | Subscribe to `Stream` | Filter by key in subscription |
| `SourceCache<T, TKey>` | `QuaternaryDictionary<TKey, T>` | Key selector not needed |
| `AddOrUpdate(item)` | `AddOrUpdate(key, value)` | Explicit key required |
| `RemoveAt(index)` | `Remove(item)` | Index-based ops not supported (sharded) |
| `Insert(index, item)` | `Add(item)` | Index-based ops not supported (sharded) |
| `IndexOf(item)` | `Contains(item)` | Use Contains for existence check |

---


## Advanced Example: Address Book Application

```csharp
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CP.Reactive;
using ReactiveUI;

public class AddressBookViewModel : IDisposable
{
    private readonly QuaternaryList<Contact> _contactList = [];
    private readonly QuaternaryDictionary<Guid, Contact> _contactMap = [];
    private readonly BehaviorSubject<string> _searchText = new(string.Empty);
    private bool _disposedValue;

    public AddressBookViewModel()
    {
        InitializeIndices();
        InitializePipelines();
    }

    public ReadOnlyObservableCollection<Contact> AllContacts { get; private set; }
    public ReadOnlyObservableCollection<Contact> FavoriteContacts { get; private set; }
    public ReadOnlyObservableCollection<Contact> NewYorkContacts { get; private set; }
    public ReadOnlyObservableCollection<Contact> SearchResults { get; private set; }

    public string SearchQuery
    {
        get => _searchText.Value;
        set => _searchText.OnNext(value ?? string.Empty);
    }

    public void BulkImport(int count)
    {
        var newContacts = Enumerable.Range(0, count).Select(i =>
            new Contact(
                Guid.NewGuid(),
                $"User{i}",
                $"Smith{i}",
                $"user{i}@company.com",
                i % 2 == 0 ? "Engineering" : "HR",
                i % 10 == 0,
                new Address("123 Main", i % 5 == 0 ? "New York" : "London", "10001", "USA"))).ToList();

        // High-speed parallel add
        _contactList.AddRange(newContacts);
        _contactMap.AddRange(newContacts.Select(c => new KeyValuePair<Guid, Contact>(c.Id, c)));
    }

    public void BulkRemoveByDepartment(string department)
    {
        // O(1) query using secondary index
        var targets = _contactList.GetItemsBySecondaryIndex("ByDepartment", department).ToList();
        
        // Bulk thread-safe remove
        _contactList.RemoveRange(targets);
        
        // Sync dictionary
        foreach (var c in targets)
        {
            _contactMap.Remove(c.Id);
        }
    }

    public void UpdateCityName(string oldCity, string newCity)
    {
        // Fast index query
        var targets = _contactList.GetItemsBySecondaryIndex("ByCity", oldCity).ToList();

        // Create updated records
        var updates = targets.Select(c => 
            c with { HomeAddress = c.HomeAddress with { City = newCity } }).ToList();

        // Atomic update
        _contactList.RemoveRange(targets);
        _contactList.AddRange(updates);
    }

    private void InitializeIndices()
    {
        // Secondary indices for O(1) lookups
        _contactList.AddIndex("ByCity", c => c.HomeAddress.City);
        _contactList.AddIndex("ByDepartment", c => c.Department);
        _contactMap.AddValueIndex("ByEmail", c => c.Email);
    }

    private void InitializePipelines()
    {
        // All contacts (throttled for performance)
        _contactList.CreateView(c => true, RxApp.MainThreadScheduler, throttleMs: 100)
                    .ToProperty(x => AllContacts = x);

        // Favorites only
        _contactList.CreateView(c => c.IsFavorite, RxApp.MainThreadScheduler, throttleMs: 100)
                    .ToProperty(x => FavoriteContacts = x);

        // New York contacts (using stream for updates)
        _contactList.CreateView(c => c.HomeAddress.City == "New York", RxApp.MainThreadScheduler, throttleMs: 200)
                    .ToProperty(x => NewYorkContacts = x);

        // Dynamic search with text filtering
        var searchPipeline = _contactList.Stream
            .CombineLatest(_searchText, (change, query) => new { change, query })
            .Where(x => Matches(x.change.Item, x.query))
            .Select(x => x.change);

        new ReactiveView<Contact>(
            searchPipeline,
            [.. _contactList],
            c => Matches(c, _searchText.Value),
            TimeSpan.FromMilliseconds(50),
            RxApp.MainThreadScheduler)
            .ToProperty(x => SearchResults = x);
    }

    private static bool Matches(Contact? c, string query) =>
        c != null && (string.IsNullOrWhiteSpace(query) ||
                      c.LastName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                      c.Email.Contains(query, StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

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
}

// Supporting records
public record Contact(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Department,
    bool IsFavorite,
    Address HomeAddress);

public record Address(string Street, string City, string PostalCode, string Country);
```

---

## Performance Optimization Tips

1. **Use AddRange for bulk operations** - Much faster than individual Add calls
2. **Leverage secondary indices** - O(1) vs O(n) for repeated queries
3. **Throttle UI updates** - Use `CreateView` with `throttleMs` for reactive bindings
4. **Batch with Edit** - Single notification for multiple changes
5. **Dispose properly** - Release resources and stop background processing

---

**Dependencies:**
- [System.Reactive](https://github.com/dotnet/reactive)

---

## License

[MIT](LICENSE)

---

**ReactiveList** - Empowering Reactive Applications with Observable Collections ⚡🚀
