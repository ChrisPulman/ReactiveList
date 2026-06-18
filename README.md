# ReactiveList

[![NuGet](https://img.shields.io/nuget/v/ReactiveList.svg?style=flat-square)](https://www.nuget.org/packages/ReactiveList/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ReactiveList.svg?style=flat-square)](https://www.nuget.org/packages/ReactiveList/)
[![License](https://img.shields.io/github/license/ChrisPulman/ReactiveList.svg?style=flat-square)](LICENSE)
[![Build Status](https://img.shields.io/github/actions/workflow/status/ChrisPulman/ReactiveList/BuildDeploy.yml?branch=main&style=flat-square)](https://github.com/ChrisPulman/ReactiveList/actions)


ReactiveList is a high-performance, thread-safe observable collection library for .NET. It provides list, dictionary, sharded, indexed, and view-based collection types that publish change streams for UI binding, reactive workflows, live data processing, and low-allocation batch operations.


<p align="center">
  <img src="Images/HeroImage.png" alt="ReactiveList V5 dual package configuration" width="500" />
</p>


## V5.0.x Breaking Change

ReactiveList V5.0.x introduces a dual package configuration and changes the base namespaces.

This is a breaking change from ReactiveList V4.x.x.

| V5 package | Dependency model | Base namespace | Scheduler type | Use when |
| --- | --- | --- | --- | --- |
| `ReactiveList` | `ReactiveUI.Primitives` | `CP.Primitives.*` | `ReactiveUI.Primitives.Concurrency.ISequencer` | You want the lean ReactiveUI.Primitives package without a System.Reactive dependency. |
| `ReactiveList.Reactive` | `ReactiveUI.Primitives.Reactive` and `System.Reactive` | `CP.Reactive.*` | `System.Reactive.Concurrency.IScheduler` | You already use System.Reactive and want Rx scheduler conventions in public APIs. |

Do not mix the two package variants in the same pipeline casually. Their namespaces and scheduler conventions are deliberately different. If an application needs both, keep a clear boundary between the lean `CP.Primitives.*` code and the System.Reactive `CP.Reactive.*` code.

## Contents

- [Installation](#installation)
- [Package Selection](#package-selection)
- [Migration From V4.x.x To V5.x.x](#migration-from-v4xx-to-v5xx)
- [Quick Start](#quick-start)
- [Using R3](#using-r3)
- [Core Concepts](#core-concepts)
- [Collections](#collections)
- [Views](#views)
- [Extension Methods](#extension-methods)
- [API Reference](#api-reference)
- [Thread Safety](#thread-safety)
- [Performance](#performance)
- [Target Frameworks](#target-frameworks)
- [Validation](#validation)
- [License](#license)

## Installation

Lean primitives package:

```shell
dotnet add package ReactiveList --version 5.0.*
```

System.Reactive package:

```shell
dotnet add package ReactiveList.Reactive --version 5.0.*
```

The package dependencies are brought in automatically:

- `ReactiveList` references `ReactiveUI.Primitives`.
- `ReactiveList.Reactive` references `ReactiveUI.Primitives.Reactive`, which references `System.Reactive`.

## Package Selection

Use only one variant unless you have a specific interop boundary.

### Lean Package

```csharp
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using CP.Primitives.Views;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
```

Use this package when:

- You want BCL `IObservable<T>` without a direct System.Reactive dependency.
- You use `ReactiveUI.Primitives.Concurrency.ISequencer`.
- You use `Sequencer.Default`, `Sequencer.Immediate`, or `SynchronizationContextSequencer.Current`.
- You want the R3 bridge source generator packed by `ReactiveUI.Primitives`.

### System.Reactive Package

```csharp
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Views;
using ReactiveUI.Primitives.Reactive;
using System.Reactive.Concurrency;
```

Use this package when:

- Your application already uses System.Reactive operators and schedulers.
- You want ReactiveList view creation APIs to accept `IScheduler`.
- You want to use schedulers such as `ImmediateScheduler.Instance`, `CurrentThreadScheduler.Instance`, or `Scheduler.Default`.

## Migration From V4.x.x To V5.x.x

V4 code usually used `CP.Reactive.*` from the main package. In V5 you must choose the package variant first.

### Option A: Move To The Lean V5 Package

Use this path if you do not need System.Reactive scheduler types in your public API.

1. Update the package reference to `ReactiveList` V5.0.x.
2. Replace namespaces:

```text
CP.Reactive             -> CP.Primitives
CP.Reactive.Collections -> CP.Primitives.Collections
CP.Reactive.Core        -> CP.Primitives.Core
CP.Reactive.Views       -> CP.Primitives.Views
CP.Reactive.Internal    -> CP.Primitives.Internal
```

3. Keep `ReactiveUI.Primitives` imports:

```csharp
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;
```

4. Keep lean scheduler usage:

```csharp
using CP.Primitives.Collections;
using ReactiveUI.Primitives.Concurrency;

var list = new ReactiveList<int>();
using var view = list.CreateView(Sequencer.Immediate, throttleMs: 0);
```

### Option B: Keep `CP.Reactive.*` Namespaces

Use this path if you want the old `CP.Reactive.*` namespace shape and System.Reactive scheduler conventions.

1. Replace the package reference with `ReactiveList.Reactive` V5.0.x.
2. Keep `CP.Reactive.*` namespaces.
3. Replace `ReactiveUI.Primitives.Concurrency.ISequencer` schedulers with `System.Reactive.Concurrency.IScheduler`.
4. Replace lean scheduler values with System.Reactive schedulers:

```csharp
using CP.Reactive.Collections;
using System.Reactive.Concurrency;

var list = new ReactiveList<int>();
using var view = list.CreateView(ImmediateScheduler.Instance, throttleMs: 0);
```

### Common Migration Checks

- Update global usings and aliases first.
- Rebuild before changing behavior. Most migration errors are namespace or scheduler-type errors.
- Avoid referencing both package variants in a single test assembly unless you fully qualify imports. Extension methods from `ReactiveUI.Primitives` and `System.Reactive` can otherwise create ambiguous `Subscribe` calls.
- Update UI code to use the scheduler type from the selected package.
- Update documentation and samples to say `CP.Primitives.*` for the lean package and `CP.Reactive.*` for the System.Reactive package.

## Quick Start

### Lean Package Example

```csharp
using CP.Primitives.Collections;
using CP.Primitives.Core;
using ReactiveUI.Primitives;

var orders = new ReactiveList<Order>();

using var subscription = orders.Stream.Subscribe(notification =>
{
    if (notification.Action == CacheAction.Added && notification.Item is not null)
    {
        Console.WriteLine($"Added order {notification.Item.Id}");
    }
});

orders.Add(new Order(1, 125.00m));
orders.AddRange([new Order(2, 40.00m), new Order(3, 75.00m)]);

public sealed record Order(int Id, decimal Amount);
```

### System.Reactive Package Example

```csharp
using CP.Reactive.Collections;
using CP.Reactive.Core;
using System;
using System.Reactive.Concurrency;

var orders = new ReactiveList<Order>();

using var subscription = orders.Stream.Subscribe(notification =>
{
    if (notification.Action == CacheAction.Added && notification.Item is not null)
    {
        Console.WriteLine($"Added order {notification.Item.Id}");
    }
});

orders.Add(new Order(1, 125.00m));
using var view = orders.CreateView(ImmediateScheduler.Instance, throttleMs: 0);

public sealed record Order(int Id, decimal Amount);
```

### Batch Edit

```csharp
using CP.Primitives.Collections;

var list = new ReactiveList<string>();

list.Edit(editor =>
{
    editor.Add("alpha");
    editor.Add("beta");
    editor.Insert(1, "middle");
    editor.Remove("alpha");
});
```

`Edit` batches collection mutations and emits one batch notification after the transaction completes.

## Using R3

ReactiveList does not require R3, but the lean `ReactiveList` package depends on `ReactiveUI.Primitives`, which packs the ReactiveUI.Primitives R3 bridge source generator as an analyzer. Do not add `ReactiveUI.Primitives.R3Bridge.Generator` directly.

To use R3:

```shell
dotnet add package ReactiveList --version 5.0.*
dotnet add package R3
```

Then import the generated bridge namespace:

```csharp
using CP.Primitives.Collections;
using ReactiveUI.Primitives.R3Bridge;

var list = new ReactiveList<int>();

// System.IObservable<CacheNotify<int>> -> R3.Observable<CacheNotify<int>>
var r3Stream = list.Stream.AsR3Observable();
```

The source generator emits bridge methods only when the consumer assembly references the required R3 symbols:

| R3 symbol availability | Generated bridge methods |
| --- | --- |
| `R3.Observable<T>` and `System.IObservable<T>` | `AsPrimitivesSignal<T>(this R3.Observable<T>)`, `AsR3Observable<T>(this System.IObservable<T>)` |
| `R3.Observable<T>` and `ReactiveUI.Primitives.Async.IObservableAsync<T>` | `AsPrimitivesAsyncObservable<T>(this R3.Observable<T>)`, `AsR3Observable<T>(this IObservableAsync<T>)` |
| `R3Async.AsyncObservable<T>` and `IObservableAsync<T>` | `AsPrimitivesAsyncObservable<T>(this R3Async.AsyncObservable<T>)`, `AsR3AsyncObservable<T>(this IObservableAsync<T>)` |

Use bridges at the edge of your application. Keep the internal pipeline in one model after conversion.

Example with an R3 filter stream:

```csharp
using CP.Primitives.Collections;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.R3Bridge;

var list = new ReactiveList<Product>();

R3.Observable<Func<Product, bool>> r3Filter = GetFilterObservable();
var primitivesFilter = r3Filter.AsPrimitivesSignal();

using var view = list.CreateView(primitivesFilter, Sequencer.Immediate, throttleMs: 0);

static R3.Observable<Func<Product, bool>> GetFilterObservable() =>
    R3.Observable.Return<Func<Product, bool>>(product => product.IsActive);

public sealed record Product(string Name, bool IsActive);
```

For System.Reactive-first projects, prefer `ReactiveList.Reactive`. Use R3 conversion at boundaries only if that assembly also references R3 and the required ReactiveUI.Primitives bridge symbols.

## Core Concepts

### Change Stream

Every collection exposes a stream of cache notifications:

```csharp
IObservable<CacheNotify<T>> Stream { get; }
```

The stream emits `CacheNotify<T>` values for single-item, batch, clear, move, update, and refresh operations.

### CacheNotify

`CacheNotify<T>` describes a collection change.

| Member | Meaning |
| --- | --- |
| `Action` | The `CacheAction` that occurred. |
| `Item` | The current item for single-item operations. |
| `Batch` | A pooled batch for batch operations. Dispose batches when using low-level APIs directly, or use `AutoDisposeBatches`. |
| `CurrentIndex` | Current item index when available. |
| `PreviousIndex` | Previous item index for move operations when available. |
| `Previous` | Previous value for update operations. |

`CacheAction` values:

- `Added`
- `Removed`
- `Updated`
- `Moved`
- `Refreshed`
- `Cleared`
- `BatchOperation`
- `BatchAdded`
- `BatchRemoved`

### ChangeSet

`ChangeSet<T>` is a compact `IReadOnlyList<Change<T>>` used for DynamicData-style processing.

Useful members:

- `Count`
- `Adds`
- `Removes`
- `Updates`
- `Moves`
- `AsSpan()`
- `Dispose()`

`Change<T>` provides factory methods for `CreateAdd`, `CreateRemove`, `CreateUpdate`, `CreateMove`, and `CreateRefresh`.

`ChangeReason` values:

- `Add`
- `Remove`
- `Update`
- `Move`
- `Refresh`
- `Clear`

## Collections

### ReactiveList<T>

`ReactiveList<T>` is a thread-safe observable list implementing `IList<T>`, non-generic `IList`, `IReadOnlyList<T>`, `INotifyCollectionChanged`, `INotifyPropertyChanged`, and `IDisposable`.

Constructors:

```csharp
new ReactiveList<T>();
new ReactiveList<T>(IEnumerable<T> items);
new ReactiveList<T>(T item);
```

Properties and streams:

| API | Description |
| --- | --- |
| `Count` | Current item count. |
| `Items` | Bindable `ReadOnlyObservableCollection<T>`. |
| `ItemsAdded` | Last added items. |
| `ItemsRemoved` | Last removed items. |
| `ItemsChanged` | Last changed items. |
| `Stream` | Unified `IObservable<CacheNotify<T>>`. |
| `Added` | `IObservable<IEnumerable<T>>` for added items. |
| `Removed` | `IObservable<IEnumerable<T>>` for removed items. |
| `Changed` | `IObservable<IEnumerable<T>>` for changed items. |
| `CurrentItems` | Emits snapshots on subscription and after changes. |
| `Version` | Atomic modification version. |
| `IsDisposed` | Disposal state. |
| `IsReadOnly`, `IsFixedSize`, `IsSynchronized` | Collection compatibility members. |

Mutation methods:

| API | Description |
| --- | --- |
| `Add(T)` / `Add(object?)` | Add one item. |
| `AddRange(IEnumerable<T>)` | Add multiple items. |
| `AddRange(ReadOnlySpan<T>)` | Add span data on modern TFMs. |
| `Insert(int, T)` / `Insert(int, object?)` | Insert one item. |
| `InsertRange(int, IEnumerable<T>)` | Insert multiple items. |
| `Remove(T)` / `Remove(object?)` | Remove one item. |
| `Remove(IEnumerable<T>)` | Remove multiple explicit items. |
| `RemoveAt(int)` | Remove by index. |
| `RemoveRange(int, int)` | Remove a contiguous range. |
| `RemoveMany(Func<T, bool>)` | Remove all matching items. |
| `Move(int, int)` | Move an item between indexes. |
| `Update(T, T)` | Replace an existing item. |
| `ReplaceAll(IEnumerable<T>)` | Replace the complete contents. |
| `Clear()` | Clear and notify. |
| `ClearWithoutDeallocation(bool notifyChange = true)` | Clear while retaining internal capacity. |
| `Edit(Action<IEditableList<T>>)` | Batch mutations and emit a single notification. |
| `Dispose()` | Release streams and internal resources. |

Read and copy methods:

| API | Description |
| --- | --- |
| Indexer `this[int]` | Get or set item by index. |
| `Contains(T)` / `Contains(object?)` | Check membership. |
| `IndexOf(T)` / `IndexOf(object?)` | Locate an item. |
| `CopyTo(T[], int)`, `CopyTo(Array, int)`, `CopyTo(Span<T>)` | Copy items. |
| `ToArray()` | Snapshot to array. |
| `AsSpan()` | Exposes a read-only span over current storage. Use only when no concurrent writes can occur. |
| `AsMemory()` | Snapshot as memory. |
| `GetEnumerator()` | Enumerate a safe snapshot. |

### Reactive2DList<T>

`Reactive2DList<T>` derives from `ReactiveList<ReactiveList<T>>` and models rows, grids, matrices, and nested collections.

Constructors:

```csharp
new Reactive2DList<T>();
new Reactive2DList<T>(IEnumerable<IEnumerable<T>> items);
new Reactive2DList<T>(IEnumerable<ReactiveList<T>> items);
new Reactive2DList<T>(IEnumerable<T> items);
new Reactive2DList<T>(ReactiveList<T> item);
new Reactive2DList<T>(T item);
```

Additional methods:

| API | Description |
| --- | --- |
| `AddRange(IEnumerable<IEnumerable<T>>)` | Add multiple rows. |
| `AddRange(IEnumerable<T>)` | Add one row from values. |
| `AddToInner(int, T)` | Add one item to a row. |
| `AddToInner(int, IEnumerable<T>)` | Add multiple items to a row. |
| `GetItem(int, int)` | Read a cell. |
| `SetItem(int, int, T)` | Replace a cell. |
| `Insert(int, T)` | Insert a row containing one item. |
| `Insert(int, IEnumerable<T>)` | Insert a row from values. |
| `Insert(int, IEnumerable<T>, int)` | Insert values into an existing row. |
| `RemoveFromInner(int, int)` | Remove a cell. |
| `ClearInner(int)` | Clear a row. |
| `Flatten()` | Return all cells as a flat sequence. |
| `TotalCount()` | Count all cells. |

### QuaternaryList<T>

`QuaternaryList<T>` is a four-shard thread-safe observable collection for large data sets, lower allocation pressure, parallel-friendly operations, and secondary-index lookups.

Core APIs:

| API | Description |
| --- | --- |
| `Add(T)` | Add one item. |
| `AddRange(IEnumerable<T>)` | Add many items. |
| `Remove(T)` | Remove one item. |
| `RemoveRange(IEnumerable<T>)` | Remove many explicit items. |
| `RemoveMany(Func<T, bool>)` | Remove matching items. |
| `ReplaceAll(IEnumerable<T>)` | Replace all items. |
| `Edit(Action<ICollection<T>>)` | Batch edits. |
| `Clear()` | Clear all shards. |
| `Contains(T)` | Check membership. |
| `CopyTo(T[], int)` | Copy to array. |
| `Snapshot()` | Return an immutable point-in-time list. |
| `ToArray()` | Copy all items to an array. |
| `Stream` | Change notification stream. |
| `Count`, `Version`, `IsDisposed` | State and diagnostics. |

Secondary index APIs:

| API | Description |
| --- | --- |
| `AddIndex<TKey>(string, Func<T, TKey>)` | Add a named item index. |
| `GetItemsBySecondaryIndex<TKey>(string, TKey)` | Lookup by index key. |
| `ItemMatchesSecondaryIndex<TKey>(string, T, TKey)` | Test whether an item belongs to an index key. |
| `FilterBySecondaryIndex` | Stream changes that match one or more index keys. |
| `CreateViewBySecondaryIndex` | Create a live view for one or more index keys. |
| `CreateDynamicViewBySecondaryIndex` | Create a live view whose index keys come from an observable. |

### QuaternaryDictionary<TKey, TValue>

`QuaternaryDictionary<TKey, TValue>` is a four-shard thread-safe observable dictionary for key-value caches, bulk writes, and value-indexed lookup.

Core APIs:

| API | Description |
| --- | --- |
| `Add(TKey, TValue)` | Add one pair. |
| `TryAdd(TKey, TValue)` | Add when absent. |
| `AddOrUpdate(TKey, TValue)` | Add or replace. |
| `AddRange(IEnumerable<KeyValuePair<TKey, TValue>>)` | Add many pairs. |
| `Remove(TKey)` | Remove by key. |
| `Remove(KeyValuePair<TKey, TValue>)` | Remove exact pair. |
| `RemoveKeys(IEnumerable<TKey>)` | Remove many keys. |
| `RemoveMany(Func<KeyValuePair<TKey, TValue>, bool>)` | Remove matching pairs. |
| `TryGetValue(TKey, out TValue)` | Lookup by key. |
| `Lookup(TKey)` | Lookup by key with a `(HasValue, Value)` tuple result. |
| `ContainsKey(TKey)` | Check key presence. |
| `Contains(KeyValuePair<TKey, TValue>)` | Check exact pair. |
| `Edit(Action<IDictionary<TKey, TValue>>)` | Batch edits. |
| `Clear()` | Clear all shards. |
| `CopyTo(KeyValuePair<TKey, TValue>[], int)` | Copy pairs. |
| `Keys`, `Values`, indexer | Dictionary members. |
| `Stream` | Change notification stream. |

Secondary index APIs:

| API | Description |
| --- | --- |
| `AddValueIndex<TIndexKey>(string, Func<TValue, TIndexKey>)` | Add a named value index. |
| `GetValuesBySecondaryIndex<TIndexKey>(string, TIndexKey)` | Lookup values by secondary key. |
| `ValueMatchesSecondaryIndex<TIndexKey>(string, TValue, TIndexKey)` | Test value/index membership. |
| `CreateViewBySecondaryIndex` | Create a live dictionary view for an index key. |
| `FilterBySecondaryIndex` | Stream matching dictionary changes. |
| `CreateDynamicViewBySecondaryIndex` | Create a live view from observable index keys. |

### Low-Level Quad Types

`QuadList<T>` and `QuadDictionary<TKey, TValue>` are lower-level shard containers used by the quaternary collections. They expose direct add, remove, clear, copy, capacity, enumeration, and dispose operations. Most application code should use `QuaternaryList<T>` or `QuaternaryDictionary<TKey, TValue>` instead.

## Views

Views are disposable live projections over collection streams. Dispose them when the UI or workflow no longer needs updates.

Common view members:

- `Items`
- `Count`
- indexer for list-like views
- `CollectionChanged`
- `PropertyChanged`
- `Refresh()`
- `ToProperty(Action<ReadOnlyObservableCollection<T>>)`
- `ToProperty(out ReadOnlyObservableCollection<T>)`
- `Dispose()`

| View | Purpose |
| --- | --- |
| `ReactiveView<T>` | Basic live filtered view from stream and snapshot. |
| `FilteredReactiveView<T>` | Static predicate filtered list view. |
| `DynamicFilteredReactiveView<T>` | Filter predicate is supplied by an observable. |
| `DynamicReactiveView<T>` | Query/filter based dynamic view over an `IReactiveSource<T>`. |
| `SortedReactiveView<T>` | Sorted live list view. |
| `GroupedReactiveView<T, TKey>` | Live `IReadOnlyDictionary<TKey, IReadOnlyList<T>>` groups. |
| `SecondaryIndexReactiveView<TKey, TValue>` | Live dictionary-value view using a named secondary index. |
| `DynamicSecondaryIndexReactiveView<T, TKey>` | Live list secondary-index view with observable keys. |
| `DynamicSecondaryIndexDictionaryReactiveView<TKey, TValue>` | Live dictionary secondary-index view with observable keys. |

### View Examples

```csharp
using CP.Primitives;
using CP.Primitives.Collections;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

var products = new QuaternaryList<Product>();
products.AddIndex("ByCategory", product => product.Category);

using var active = products.CreateView(product => product.IsActive, Sequencer.Default);
using var grouped = products.GroupBy(product => product.Category, Sequencer.Default);
using var indexed = products.CreateViewBySecondaryIndex("ByCategory", "Hardware", Sequencer.Default);

var query = new BehaviorSignal<string>("hardware");
using var search = products.CreateView(
    query,
    static (text, product) => product.Name.Contains(text, StringComparison.OrdinalIgnoreCase),
    Sequencer.Default);

public sealed record Product(string Name, string Category, bool IsActive);
```

## Extension Methods

### CacheNotifyExtensions

These operate on `IObservable<CacheNotify<T>>`.

| API | Description |
| --- | --- |
| `ToChange()` | Convert one notification to a `Change<T>`. |
| `WhereAction(CacheAction)` | Filter by action. |
| `WhereAdded()` / `WhereRemoved()` | Filter add/remove notifications. |
| `SelectItems()` | Select the single current item from matching notifications. |
| `SelectAllItems()` | Flatten single and batch item notifications. |
| `OnItemAdded()` / `OnItemRemoved()` / `OnItemUpdated()` | Convenience item streams. |
| `OnItemMoved()` | Stream move tuples with old and new index. |
| `OnCleared()` | Stream clear notifications. |
| `BufferNotifications(TimeSpan)` | Buffer notifications for a duration. |
| `ThrottleNotifications(TimeSpan)` | Throttle notification bursts. |
| `ObserveOnScheduler(...)` | Observe notifications on the selected scheduler/sequencer. |
| `TransformItems(Func<T, TResult>)` | Transform item payloads. |
| `FilterItems(Func<T, bool>)` | Filter item payloads. |
| `AutoDisposeBatches()` | Dispose pooled batches after observation. |
| `CountByAction()` | Count notifications by action. |
| `ToChangeSets()` | Convert notifications to `ChangeSet<T>` streams. |

### ReactiveListExtensions

These operate on collection streams and change sets.

| API | Description |
| --- | --- |
| `FilterDynamic(...)` | Apply observable predicates to list or dictionary notifications. |
| `WhereItems(Func<T, bool>)` | Filter notification items. |
| `WhereChanges(Func<Change<T>, bool>)` | Filter changes in change sets. |
| `WhereReason(ChangeReason)` | Filter changes by reason. |
| `SelectChanges(...)` | Project change sets or individual changes. |
| `OnAdd()` / `OnRemove()` / `OnUpdate()` / `OnMove()` | Stream item-level changes. |
| `GroupByChanges(Func<T, TKey>)` | Group changes by key. |
| `GroupingByChanges(Func<T, TKey>)` | Stream `IGrouping<TKey, Change<T>>`. |
| `SortBy(Func<T, TKey>)` | Sort change-set payloads. |
| `AutoRefresh(string?)` | Emit refreshes for property changes. |
| `AutoRefresh(Expression<Func<T, object>>)` | Typed property refresh helper. |
| `Connect()` | Connect an `IReactiveSource<T>` to change-set streams. |
| `CreateView(...)` | Create static, dynamic, query-driven, and unfiltered views. |
| `SortBy(...)` | Create sorted live views. |
| `GroupBy(...)` | Create grouped live views. |

### QuaternaryExtensions

These operate on `QuaternaryList<T>` and `QuaternaryDictionary<TKey, TValue>`.

| API | Description |
| --- | --- |
| `FilterBySecondaryIndex(...)` | Filter stream notifications by one or more secondary-index keys. |
| `CreateViewBySecondaryIndex(...)` | Create live views for one or more secondary-index keys. |
| `CreateDynamicViewBySecondaryIndex(...)` | Create live views whose index keys are supplied by an observable. |

## API Reference

### Interfaces

| Interface | Purpose |
| --- | --- |
| `IReactiveSource<T>` | Base observable collection contract with `Stream`, enumeration, `INotifyCollectionChanged`, and disposal. |
| `IReactiveList<T>` | `ReactiveList<T>` contract: `IList<T>`, `IList`, `IReadOnlyList<T>`, `IReactiveSource<T>`, and property notifications. |
| `IQuaternaryList<T>` | Sharded list contract with indexing and batch edit support. |
| `IQuaternaryDictionary<TKey, TValue>` | Sharded dictionary contract with value indexing and batch edit support. |
| `IReactiveView<TView, TItem>` | Disposable live-view contract. |
| `IEditableList<T>` | Edit transaction surface used by `ReactiveList<T>.Edit`. |
| `IGroupedObservable<TKey, TElement>` | Observable grouping contract. |
| `ISecondaryIndex<T>` | Secondary-index contract. |
| `IResettable` | Reset contract for pooled internals. |
| `IQuad<T>` | Lower-level shard contract. |

### Core Types

| Type | Purpose |
| --- | --- |
| `CacheNotify<T>` | One collection notification. |
| `CacheAction` | Notification action enum. |
| `Change<T>` | One DynamicData-style change. |
| `ChangeReason` | Change reason enum. |
| `ChangeSet<T>` | Compact collection of changes. |
| `PooledBatch<T>` | Disposable pooled batch container. |
| `ReactiveGroup<TKey, T>` | Bindable group used by grouped views. |
| `SecondaryIndex<T, TKey>` | Named lookup index implementation. |
| `EditableListWrapperPool` | Pool configuration and rental for edit wrappers. |
| `PooledEditableListWrapper<T>` | Pooled edit wrapper used by transactions. |

### Namespaces

| Package | Root | Collections | Core | Views |
| --- | --- | --- | --- | --- |
| `ReactiveList` | `CP.Primitives` | `CP.Primitives.Collections` | `CP.Primitives.Core` | `CP.Primitives.Views` |
| `ReactiveList.Reactive` | `CP.Reactive` | `CP.Reactive.Collections` | `CP.Reactive.Core` | `CP.Reactive.Views` |

## Thread Safety

All public collection operations are designed for thread-safe use.

- `ReactiveList<T>` uses lock-based synchronization for list and observable collection state.
- `Reactive2DList<T>` inherits the thread-safety model from `ReactiveList<T>`.
- `QuaternaryList<T>` and `QuaternaryDictionary<TKey, TValue>` use four internal shards and reader/writer locking to reduce contention for larger data sets.
- Enumerators and snapshots are safe point-in-time views.
- Observable streams are notification surfaces; core collection mutations do not depend on observers completing expensive work.

Use care with direct-memory APIs:

- `AsSpan()` exposes current storage and should be used only when you can guarantee no concurrent writes.
- Prefer `ToArray()`, `Snapshot()`, or `AsMemory()` for defensive snapshots.

## Performance

ReactiveList is optimized for low-allocation notifications, pooled batches, sharded storage, and batch editing.

Use these patterns:

| Scenario | Recommendation |
| --- | --- |
| Multiple related edits | Use `Edit(...)` or `AddRange(...)` rather than many independent writes. |
| UI-bound views | Use `CreateView`, `SortBy`, or `GroupBy` with a UI scheduler/sequencer and a throttle. |
| Large list batches | Use `QuaternaryList<T>`. |
| Key-value cache workloads | Use `QuaternaryDictionary<TKey, TValue>`. |
| Frequent category or value lookups | Add a secondary index and query with index APIs. |
| Notification bursts | Use `BufferNotifications`, `ThrottleNotifications`, or view throttling. |
| Pooled batch handling | Use `AutoDisposeBatches` when observing low-level batches manually. |

Measured benchmark highlights from the V5 work:

| Scenario | Result |
| --- | --- |
| `QuaternaryList<T>` vs DynamicData `SourceList<T>` AddRange | Lower allocations and faster large-batch throughput at 1,000 and 10,000 item sizes. |
| `QuaternaryDictionary<TKey, TValue>` vs DynamicData `SourceCache<TValue, TKey>` AddRange | Faster and lower allocation at all measured sizes. |
| Observable isolation | Core collection operations remain independent from subscriber-side work. |

## Target Frameworks

The library targets the shared `CoreTargetFrameworks` configured for the repository:

- `net8.0`
- `net9.0`
- `net10.0`
- `net11.0`
- On Windows builds, `net462`, `net472`, `net48`, `net481`, and matching Windows TFMs are included.

Native AOT compatibility is enabled for compatible modern .NET target frameworks.

## Validation

The V5 dual package configuration has been verified locally with:

```shell
dotnet restore src\ReactiveList.sln
dotnet build src\ReactiveList.sln -c Release --no-restore -v quiet
dotnet test src\ReactiveList.sln -c Release --no-build --no-restore
```

The full TUnit run passed across the solution with 2,901 tests, 0 failed, and 0 skipped. The targeted MTP Cobertura coverage run reported 100.00% line coverage for the `ReactiveList` package.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
