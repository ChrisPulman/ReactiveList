# ReactiveList

[![NuGet](https://img.shields.io/nuget/v/ReactiveList.svg?style=flat-square)](https://www.nuget.org/packages/ReactiveList/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ReactiveList.svg?style=flat-square)](https://www.nuget.org/packages/ReactiveList/)
[![License](https://img.shields.io/github/license/ChrisPulman/ReactiveList.svg?style=flat-square)](LICENSE)
[![Build Status](https://img.shields.io/github/actions/workflow/status/ChrisPulman/ReactiveList/BuildOnly.yml?branch=main&style=flat-square)](https://github.com/ChrisPulman/ReactiveList/actions)

A lightweight, high-performance reactive list with fine-grained change tracking built on [DynamicData](https://github.com/reactivemarbles/DynamicData) and [System.Reactive](https://github.com/dotnet/reactive).

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

## Building Locally

```bash
git clone https://github.com/ChrisPulman/ReactiveList.git
cd ReactiveList/src
dotnet build
dotnet test
```

**Dependencies:**
- [DynamicData](https://github.com/reactivemarbles/DynamicData)
- [System.Reactive](https://github.com/dotnet/reactive)

---

## License

[MIT](LICENSE)

---

**ReactiveList** - Empowering Reactive Applications with Observable Collections ⚡🚀
