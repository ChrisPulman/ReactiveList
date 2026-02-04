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
- [Namespace Structure](#namespace-structure)
- [Quick Start](#quick-start)
- [ReactiveList&lt;T&gt;](#reactivelistt)
- [Reactive2DList&lt;T&gt;](#reactive2dlistt)
- [QuaternaryList&lt;T&gt;](#quaternarylistt) (.NET 8+)
- [QuaternaryDictionary&lt;TKey, TValue&gt;](#quaternarydictionarytkey-tvalue) (.NET 8+)
- [Benchmark Results](#benchmark-results)
- [UI Binding](#ui-binding)
- [Migrating from DynamicData](#migrating-from-dynamicdata)
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

## Namespace Structure

The library is organized into the following namespaces:

| Namespace | Description |
|-----------|-------------|
| `CP.Reactive` | Root namespace with extension methods (`ReactiveListExtensions`, `QuaternaryExtensions`) |
| `CP.Reactive.Collections` | Core collection types (`ReactiveList<T>`, `Reactive2DList<T>`, `QuaternaryList<T>`, `QuaternaryDictionary<TKey, TValue>`) |
| `CP.Reactive.Core` | Core types (`Change<T>`, `ChangeSet<T>`, `ChangeReason`, `CacheNotify<T>`, `CacheAction`) |
| `CP.Reactive.Views` | View types (`FilteredReactiveView<T>`, `SortedReactiveView<T>`, `GroupedReactiveView<T, TKey>`) |

### Common Using Statements

```csharp
// For basic ReactiveList usage
using CP.Reactive;
using CP.Reactive.Collections;

// For working with changes and change sets
using CP.Reactive.Core;

// For views (.NET 6+)
using CP.Reactive.Views;
```

---

## Quick Start

```csharp
using CP.Reactive;
using CP.Reactive.Collections;

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
using CP.Reactive.Collections;

var list = new ReactiveList<string>();

// Adding items
list.Add("item");
list.AddRange(["a", "b", "c"]);

// Removing items
list.Remove("item");
list.RemoveAt(0);
list.RemoveRange(0, 2);
list.RemoveMany(x => x.StartsWith("a"));
list.Clear();

// Replacing all items
list.ReplaceAll(["new", "items"]);

// Moving items
list.Move(0, 2);

// Batch operations (single notification)
list.Edit(l =>
{
    l.Add("item");
    l.RemoveAt(0);
});
```

### Extension Methods

```csharp
using CP.Reactive;
using CP.Reactive.Core;

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

// Project changes using Change metadata
list.Connect()
    .SelectChanges((Change<User> change) => change.Current.Name)
    .Subscribe(name => Console.WriteLine(name));

// Create a filtered view (.NET 6+)
var activeUsers = list.CreateView(user => user.IsActive, scheduler: RxApp.MainThreadScheduler, throttleMs: 50);

// Create a sorted view
var sortedByName = list.SortBy(user => user.Name);

// Create a grouped view
var groupedByDepartment = list.GroupBy(user => user.Department);
```

---

## Reactive2DList&lt;T&gt;

A two-dimensional reactive list for managing grid-like or tabular data.

```csharp
using CP.Reactive.Collections;

var grid = new Reactive2DList<int>(new[]
{
    new[] { 1, 2, 3 },
    new[] { 4, 5, 6 },
    new[] { 7, 8, 9 }
});

var item = grid.GetItem(1, 0);  // Row 1, Column 0 = 4
grid.SetItem(2, 1, 100);
grid.AddToInner(0, 10);
var flattened = grid.Flatten().ToList();
```

---

## QuaternaryList&lt;T&gt;

High-performance, thread-safe, sharded list optimized for large datasets (.NET 8+).

### Key Features

- **Sharded architecture**: Data distributed across 4 partitions for parallel access
- **Thread-safe**: Uses `ReaderWriterLockSlim` with fine-grained locking
- **Low allocation**: Uses `ArrayPool<T>` and custom pooled collections
- **Secondary indices**: O(1) lookup by custom keys

```csharp
using CP.Reactive.Collections;

var list = new QuaternaryList<Contact>();

// Add indices for O(1) lookups
list.AddIndex("ByDepartment", c => c.Department);

// Bulk add
list.AddRange(contacts);

// Fast O(1) query by index
var engineers = list.GetItemsBySecondaryIndex("ByDepartment", "Engineering");

// Batch operations (single notification)
list.Edit(l =>
{
    l.Add(new Contact("Bob", "Sales"));
    l.Clear();
});
```

---

## QuaternaryDictionary&lt;TKey, TValue&gt;

High-performance, thread-safe, sharded dictionary optimized for large datasets (.NET 8+).

```csharp
using CP.Reactive.Collections;

var dict = new QuaternaryDictionary<Guid, User>();

dict.AddOrUpdate(user.Id, user);
dict.AddValueIndex("ByDepartment", u => u.Department);

var engineers = dict.GetValuesBySecondaryIndex("ByDepartment", "Engineering");

dict.Edit(d =>
{
    d[Guid.NewGuid()] = new User("Alice");
    d.Remove(oldUserId);
});
```

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

## UI Binding

```csharp
using CP.Reactive.Collections;

public class MainViewModel : IDisposable
{
    public IReactiveList<string> Items { get; } = new ReactiveList<string>();
    public void Dispose() => Items.Dispose();
}
```

```xml
<ListBox ItemsSource="{Binding Items}" />
```

---

## Migrating from DynamicData

| DynamicData | ReactiveList |
|-------------|--------------|
| `SourceList<T>` | `ReactiveList<T>` or `QuaternaryList<T>` |
| `SourceCache<T, TKey>` | `QuaternaryDictionary<TKey, T>` |
| `Connect()` | `Stream` property or `Connect()` extension |
| `Filter()` | `CreateView()` or `WhereChanges()` |
| `Transform()` | `SelectChanges()` |

```csharp
using CP.Reactive;
using CP.Reactive.Core;

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

**ReactiveList** - Empowering Reactive Applications with Observable Collections
