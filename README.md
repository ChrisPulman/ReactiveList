# ReactiveList

[![NuGet](https://img.shields.io/nuget/v/ReactiveList.svg?style=flat-square)](https://www.nuget.org/packages/ReactiveList/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ReactiveList.svg?style=flat-square)](https://www.nuget.org/packages/ReactiveList/)
[![License](https://img.shields.io/github/license/ChrisPulman/ReactiveList.svg?style=flat-square)](LICENSE)
[![Build Status](https://img.shields.io/github/actions/workflow/status/ChrisPulman/ReactiveList/BuildOnly.yml?branch=main&style=flat-square)](https://github.com/ChrisPulman/ReactiveList/actions)

A high-performance, thread-safe, observable collection library for .NET that combines the power of reactive extensions with standard list operations. ReactiveList provides real-time change notifications, making it ideal for data-binding, reactive programming, and scenarios where collection changes need to be tracked and responded to.

## Features

- **Thread-Safe Operations**: All public methods are thread-safe
- **Reactive Notifications**: Observe additions, removals, and changes in real-time via `IObservable<T>`
- **Batch Operations**: Efficient `AddRange`, `RemoveRange`, `InsertRange`, and `ReplaceAll` methods
- **Views**: Create filtered, sorted, grouped, and secondary-indexed views that auto-update
- **Change Sets**: Fine-grained change tracking with `ChangeSet<T>` for advanced scenarios
- **AOT Compatible**: Supports Native AOT on .NET 8+
- **Cross-Platform**: Targets .NET 8, .NET 9, .NET 10, and .NET Framework 4.7.2/4.8

## Installation

```shell
dotnet add package CP.ReactiveList
```

## Quick Start

### Basic Usage

```csharp
using CP.Reactive.Collections;

// Create a reactive list
var list = new ReactiveList<string>();

// Subscribe to additions
list.Added.Subscribe(items => 
    Console.WriteLine($"Added: {string.Join(", ", items)}"));

// Subscribe to removals
list.Removed.Subscribe(items => 
    Console.WriteLine($"Removed: {string.Join(", ", items)}"));

// Add items (triggers Added notification)
list.Add("Hello");
list.AddRange(["World", "!"]);

// Remove items (triggers Removed notification)
list.Remove("World");
```

### Observing Changes with Stream

```csharp
using CP.Reactive;
using CP.Reactive.Collections;

var list = new ReactiveList<int>();

// Subscribe to the change stream
list.Stream.Subscribe(notification =>
{
    Console.WriteLine($"Action: {notification.Action}, Item: {notification.Item}");
});

list.Add(1);      // Action: Add, Item: 1
list.Add(2);      // Action: Add, Item: 2
list.Remove(1);   // Action: Remove, Item: 1
```

### Batch Edit Operations

```csharp
var list = new ReactiveList<int>();

// Batch multiple operations for efficiency
list.Edit(editor =>
{
    editor.Add(1);
    editor.Add(2);
    editor.Add(3);
    editor.RemoveAt(0);
});
// Single change notification emitted after Edit completes
```

### Creating Views

#### Filtered View

```csharp
using CP.Reactive;

var list = new ReactiveList<int>();
list.AddRange([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);

// Create a filtered view that only shows even numbers
var evenNumbers = list.ToFilteredView(x => x % 2 == 0);

// evenNumbers.Items contains: [2, 4, 6, 8, 10]
// Adding/removing items from list automatically updates the view
```

#### Sorted View

```csharp
var list = new ReactiveList<Person>();
list.AddRange(people);

// Create a sorted view by name
var sortedByName = list.ToSortedView(
    Comparer<Person>.Create((a, b) => string.Compare(a.Name, b.Name)));

// The view automatically re-sorts when items change
```

#### Grouped View

```csharp
var list = new ReactiveList<Person>();

// Group people by department
var byDepartment = list.ToGroupedView(p => p.Department);

// Access groups
foreach (var group in byDepartment)
{
    Console.WriteLine($"{group.Key}: {group.Count} people");
}
```

### Working with Change Sets

```csharp
using CP.Reactive;
using CP.Reactive.Core;

var list = new ReactiveList<string>();

// Convert stream to change sets for fine-grained control
list.Stream
    .ToChangeSets()
    .WhereReason(ChangeReason.Add)
    .Subscribe(changeSet =>
    {
        foreach (var change in changeSet)
        {
            Console.WriteLine($"New item added: {change.Item}");
        }
    });
```

## Available Observables

| Property | Description |
|----------|-------------|
| `Added` | Observable of items added to the list |
| `Removed` | Observable of items removed from the list |
| `Changed` | Observable of items that changed |
| `CurrentItems` | Observable that emits current items on subscription and after changes |
| `Stream` | Observable of `CacheNotify<T>` for detailed change information |

## Available Collections

| Collection | Description |
|------------|-------------|
| `ReactiveList<T>` | Thread-safe observable list with reactive notifications |
| `Reactive2DList<T>` | Two-dimensional reactive list |
| `QuaternaryDictionary<TKey, TValue>` | High-performance dictionary with quaternary structure |
| `QuaternaryList<T>` | High-performance list with quaternary structure |

## Available Views

| View | Description |
|------|-------------|
| `FilteredReactiveView<T>` | Filtered, auto-updating view |
| `SortedReactiveView<T>` | Sorted, auto-updating view |
| `GroupedReactiveView<T, TKey>` | Grouped, auto-updating view |
| `DynamicFilteredReactiveView<T>` | Filtered view with dynamic filter changes |
| `SecondaryIndexReactiveView<T, TKey>` | View with secondary index for fast lookups |

## Thread Safety

`ReactiveList<T>` is designed to be thread-safe. All public operations use appropriate synchronization:

```csharp
var list = new ReactiveList<int>();

// Safe to call from multiple threads
Parallel.For(0, 1000, i => list.Add(i));
```

## Performance Considerations

- Use `Edit()` for batch operations to minimize change notifications
- Views use throttling by default to batch rapid changes
- The library uses object pooling and buffer reuse for reduced allocations
- AOT-compatible on .NET 8+ for improved startup performance


---

## Benchmark Results

> Benchmarks run on Windows 11, 12th Gen Intel Core i7-12650H, .NET 10.0.2

### `ReactiveList<T>` vs `SourceList<T>` (DynamicData) - .NET 10

| Method | Count | Mean | Allocated |
|--------|------:|-----:|----------:|
| ReactiveList_AddRange | 10,000 | 602,415 ns | 3,462 KB |
| SourceList_AddRange | 10,000 | 76,536 ns | 172.2 KB |
| ReactiveList_Clear | 10,000 | 1,055,010 ns | 5,619 KB |
| SourceList_Clear | 10,000 | 156,889 ns | 252.9 KB |
| ReactiveList_Connect | 10,000 | 1,037,696 ns | 3,990 KB |
| SourceList_Connect | 10,000 | 77,675 ns | 172.8 KB |
| ReactiveList_RemoveMany | 10,000 | 20,227,197 ns | 5,001 KB |
| SourceList_RemoveMany | 10,000 | 10,773,439 ns | 2,759 KB |

**Summary**: SourceList is faster. ReactiveList provides fine-grained change tracking with higher overhead.

### `ReactiveList<T>` vs `List<T>` (DynamicData) - .NET 10

| Method | Count | Mean | Allocated |
|--------|------:|-----:|----------:|
| List_AddRange | 10,000 | 3,821 ns | 40.1 KB |
| ReactiveList_AddRange | 10,000 | 602,415 ns | 3,462 KB |
| List_Clear | 10,000 | 4,036 ns | 40.1 KB |
| ReactiveList_Clear | 10,000 | 1,055,010 ns | 5,619 KB |
| List_Filter | 10,000 | 7,044 ns | 40.1 KB |
| ReactiveList_Filter | 10,000 | 598,058 ns | 3,462 KB |

**Summary**: List is ~100x faster for raw operations. Use ReactiveList when you need reactive notifications.

### `QuaternaryList<T>` vs `SourceList<T>` (DynamicData) - .NET 10

| Method | Count | Mean | Allocated |
|--------|------:|-----:|----------:|
| QuaternaryList_AddRange | 10,000 | 101,599 ns | 72.4 KB |
| SourceList_AddRange | 10,000 | 77,280 ns | 172.3 KB |
| QuaternaryList_RemoveRange | 10,000 | 1,363,441 ns | 75.1 KB |
| SourceList_RemoveRange | 10,000 | 24,111,156 ns | 2,373 KB |
| QuaternaryList_Remove | 10,000 | 5,243,958 ns | 72.4 KB |
| SourceList_Remove | 10,000 | 34,751,282 ns | 1,333 KB |
| QuaternaryList_RemoveMany | 10,000 | 1,294,411 ns | 72.4 KB |
| SourceList_RemoveMany | 10,000 | 10,546,519 ns | 2,759 KB |
| QuaternaryList_MixedOperations | 10,000 | 607,775 ns | 72.4 KB |
| SourceList_MixedOperations | 10,000 | 4,500,532 ns | 1,842 KB |

**Summary**: QuaternaryList is **6-17x faster** for Remove operations and uses **3-4x less memory** at scale.

### `QuaternaryDictionary<TKey TValue>` vs `SourceCache<TValue, TKey>` (DynamicData) - .NET 10

| Method | Count | Mean | Allocated |
|--------|------:|-----:|----------:|
| QuaternaryDictionary_AddRange | 10,000 | 132.2 us | 327.2 KB |
| SourceCache_AddRange | 10,000 | 602.3 us | 1,155.7 KB |
| QuaternaryDictionary_Clear | 10,000 | 142.4 us | 327.2 KB |
| SourceCache_Clear | 10,000 | 486.7 us | 1,155.7 KB |
| QuaternaryDictionary_Lookup | 10,000 | 133.7 us | 327.2 KB |
| SourceCache_Lookup | 10,000 | 302.4 us | 1,155.6 KB |
| QuaternaryDictionary_Stream_Add | 10,000 | 180.7 us | 455.8 KB |
| SourceCache_Stream_Add | 10,000 | 665.6 us | 2,437.8 KB |
| QuaternaryDictionary_IterateAll | 10,000 | 220.6 us | 327.2 KB |
| SourceCache_IterateAll | 10,000 | 349.1 us | 1,233.9 KB |

**Summary**: QuaternaryDictionary is **3-5x faster** and uses **3-4x less memory** than SourceCache at scale.

### `QuaternaryDictionary<TKey TValue>` vs `Dictionary<TKey, TValue>` (DynamicData) - .NET 10

| Method | Count | Mean | Allocated |
|--------|------:|-----:|----------:|
| Dictionary_AddRange | 10,000 | 235.9 us | 657.6 KB |
| QuaternaryDictionary_AddRange | 10,000 | 132.2 us | 327.2 KB |
| Dictionary_Clear | 10,000 | 113.3 us | 197.5 KB |
| QuaternaryDictionary_Clear | 10,000 | 142.4 us | 327.2 KB |
| Dictionary_TryGetValue | 10,000 | 91.9 us | 197.5 KB |
| QuaternaryDictionary_TryGetValue | 10,000 | 134.3 us | 327.2 KB |
| Dictionary_IterateAll | 10,000 | 87.1 us | 197.5 KB |
| QuaternaryDictionary_IterateAll | 10,000 | 220.6 us | 327.2 KB |

**Summary**: Dictionary is faster for raw operations. QuaternaryDictionary is **1.8x faster for bulk AddRange** and adds thread-safety, reactive notifications, and secondary indices.

### When to Use Which Collection

| Scenario | Recommendation |
|----------|---------------|
| Small datasets (<1,000 items) | `ReactiveList<T>` or `SourceList<T>` |
| Large datasets with Remove operations | `QuaternaryList<T>` **(6-17x faster)** |
| Large datasets with bulk dictionary ops | `QuaternaryDictionary<TKey,TValue>` **(3-5x faster)** |
| Memory-constrained environments | `QuaternaryList/Dictionary` **(3-4x less memory)** |
| Rich LINQ operators needed | `SourceList<T>` / `SourceCache<TValue, TKey>` |
| Secondary indices for O(1) lookups | `QuaternaryList<T>` / `QuaternaryDictionary<TKey,TValue>` |
| Thread-safe concurrent access | `QuaternaryList<T>` / `QuaternaryDictionary<TKey,TValue>` |

---


## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

**ReactiveList** - Empowering Reactive Applications with Observable Collections

