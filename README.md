# ReactiveList

A lightweight reactive list with fine-grained change tracking built on DynamicData and System.Reactive.

The list exposes reactive streams for what changed (Added/Removed/Changed), and a stream of the current items snapshot, while also implementing common list interfaces and change notifications for easy UI binding.

Targets: .NET Standard 2.0, .NET 8, .NET 9, .NET 10


## Why use ReactiveList?
- Reactive: Subscribe to changes as they happen.
- UI-friendly: Implements `INotifyCollectionChanged` and `INotifyPropertyChanged`.
- Easy binding: Exposes a `ReadOnlyObservableCollection<T>` for the current items.
- Granular change info: Access the last Added/Removed/Changed batch via collections and/or observables.
- Familiar API: Implements `IList<T>`, `IList`, `IReadOnlyList<T>`, and `ICancelable`.


## Getting started

Create a reactive list and subscribe to changes.

```csharp
using CP.Reactive;

var list = new ReactiveList<string>();

// React to items added in the last change
var addedSub = list.Added.Subscribe(added =>
{
    Console.WriteLine($"Added: {string.Join(", ", added)}");
});

// React to items removed in the last change
var removedSub = list.Removed.Subscribe(removed =>
{
    Console.WriteLine($"Removed: {string.Join(", ", removed)}");
});

// React to any items changed (add/remove/replace) in the last change
var changedSub = list.Changed.Subscribe(changed =>
{
    Console.WriteLine($"Changed: {string.Join(", ", changed)}");
});

// Observe the current items (snapshot) whenever the count changes
var currentSub = list.CurrentItems.Subscribe(items =>
{
    Console.WriteLine($"Current: [{string.Join(", ", items)}]");
});

// Work with the list just like a normal list
list.Add("one");
list.AddRange(["two", "three"]);
list.Insert(1, "two-point-five");
list.Remove("two");
list.RemoveAt(0);
list.RemoveRange(0, 1);

// Replace all items in a single operation
list.ReplaceAll(["a", "b", "c"]);

// Update an item (replace a specific value)
list.Update("b", "B");

// Access the read-only view of items (UI binding friendly)
var items = list.Items; // ReadOnlyObservableCollection<string>

// Cleanup
addedSub.Dispose();
removedSub.Dispose();
changedSub.Dispose();
currentSub.Dispose();
list.Dispose();
```


### WPF/WinUI binding

Because `ReactiveList<T>` implements `INotifyCollectionChanged`, `INotifyPropertyChanged`, and `IEnumerable<T>`, you can bind directly.

```csharp
public sealed class MyViewModel
{
    public IReactiveList<string> Items { get; } = new ReactiveList<string>(new[] { "One", "Two" });
}
```

```xml
<ListBox ItemsSource="{Binding Items}" />
```

Alternatively, bind to `Items` for an explicit `ReadOnlyObservableCollection<T>`:

```xml
<ListBox ItemsSource="{Binding Items.Items}" />
```


## Behavior notes

- Observables run on `Scheduler.Immediate` inside the list; if you update UI from subscriptions, dispatch to your UI thread.
- `ItemsAdded`, `ItemsRemoved`, `ItemsChanged` are snapshots of the last change batch only (not cumulative).
- `ReplaceAll(newItems)` raises a clear + add-range under the hood. After `ReplaceAll`:
  - `ItemsRemoved` contains the cleared items.
  - `ItemsAdded` contains the new items.
  - `ItemsChanged` reflects the clear operation (the removed range).
  - A `Reset` collection change notification is raised.


## API quick reference

Interfaces implemented:
- `IList<T>`, `IList`, `IReadOnlyList<T>`
- `INotifyCollectionChanged`, `INotifyPropertyChanged`
- `ICancelable` (`IsDisposed`)

Properties:
- `ReadOnlyObservableCollection<T> Items` — current items for binding.
- `ReadOnlyObservableCollection<T> ItemsAdded` — items added in the last change.
- `ReadOnlyObservableCollection<T> ItemsRemoved` — items removed in the last change.
- `ReadOnlyObservableCollection<T> ItemsChanged` — items changed (add/remove/replace) in the last change.
- `IObservable<IEnumerable<T>> Added` — stream of items added each change.
- `IObservable<IEnumerable<T>> Removed` — stream of items removed each change.
- `IObservable<IEnumerable<T>> Changed` — stream of items changed each change.
- `IObservable<IEnumerable<T>> CurrentItems` — current items snapshot on count changes.
- `int Count`, `bool IsDisposed`.

Indexers:
- `T this[int index] { get; set; }`
- `object? IList.this[int index] { get; set; }`

Events:
- `event NotifyCollectionChangedEventHandler? CollectionChanged`
- `event PropertyChangedEventHandler? PropertyChanged`

Operations:
- `void Add(T item)`
- `void AddRange(IEnumerable<T> items)`
- `void Insert(int index, T item)`
- `void InsertRange(int index, IEnumerable<T> items)`
- `bool Remove(T item)` / `void Remove(IEnumerable<T> items)` / `void RemoveAt(int index)` / `void RemoveRange(int index, int count)`
- `void Clear()`
- `void ReplaceAll(IEnumerable<T> items)`
- `void Update(T item, T newValue)`
- `int IndexOf(T item)` / `bool Contains(T item)`
- `void CopyTo(T[] array, int arrayIndex)` / `void CopyTo(Array array, int index)`
- `IDisposable Subscribe(IObserver<IEnumerable<T>> observer)` (subscribes to `CurrentItems`)
- `void Dispose()`


## Examples

ReplaceAll semantics:

```csharp
var list = new ReactiveList<string>(["one", "two"]);
// At this point:
// list.ItemsAdded.Count == 2
// list.ItemsChanged.Count == 2
// list.ItemsRemoved.Count == 0

list.ReplaceAll(["three", "four", "five"]);

// After ReplaceAll:
// list.ItemsAdded.Count == 3           // new items
// list.ItemsRemoved.Count == 2         // old items cleared
// list.ItemsChanged.Count == 2         // clear change set (removed range)
```

Subscribe to snapshots of the current items:

```csharp
var list = new ReactiveList<int>();
list.CurrentItems.Subscribe(items =>
{
    // Runs when Count changes
    var sum = items.Sum();
    Console.WriteLine($"Sum: {sum}");
});

list.AddRange([1, 2, 3]); // triggers CurrentItems
list.Remove(2);           // triggers CurrentItems
```

---

## Reactive2DList

`Reactive2DList<T>` is a reactive list of reactive lists: `Reactive2DList<T> : ReactiveList<ReactiveList<T>>`.
Use it for grid- or table-like data structures where rows are dynamic and each row's items are also dynamic.
All `ReactiveList` behavior applies at the outer level (rows). Each inner row is its own `ReactiveList<T>`.

### Constructing a Reactive2DList

```csharp
using CP.Reactive;

// Empty 2D list
var grid = new Reactive2DList<int>();

// With rows from IEnumerable<IEnumerable<T>>
var grid2 = new Reactive2DList<int>(new[]
{
    new[] { 1, 2, 3 },
    new[] { 4, 5 },
});

// With existing reactive rows
var rowA = new ReactiveList<int>(new[] { 10, 11 });
var rowB = new ReactiveList<int>(new[] { 12 });
var grid3 = new Reactive2DList<int>(new[] { rowA, rowB });

// With IEnumerable<T>: creates a grid with one single-element row per item
var grid4 = new Reactive2DList<int>(new[] { 7, 8, 9 });
// grid4 == [ [7], [8], [9] ]

// With a single row
var grid5 = new Reactive2DList<int>(new ReactiveList<int>(new[] { 1, 2, 3 }));

// With a single value (one row, one item)
var grid6 = new Reactive2DList<int>(42);
```

### Adding rows

```csharp
var grid = new Reactive2DList<string>();

// Add multiple rows (each inner IEnumerable becomes a new row)
grid.AddRange(new[]
{
    new[] { "a1", "a2" },
    new[] { "b1" },
});

// Add one row per item (each item becomes a single-element row)
grid.AddRange(new[] { "x", "y" }); // => rows: [ ["x"], ["y"] ]
```

### Inserting

```csharp
var grid = new Reactive2DList<string>(new[]
{
    new[] { "r0c0", "r0c1" },
    new[] { "r1c0" },
});

// Insert a new row at index 1
grid.Insert(1, new[] { "new", "row" });

// Insert a single-element row at index 0
grid.Insert(0, "solo");

// Insert items into an existing row (index 2), starting at innerIndex 1
grid.Insert(2, new[] { "mid1", "mid2" }, innerIndex: 1);
```

### Observing changes

You can subscribe to row-level changes on the outer list, and item-level changes on each inner row.

```csharp
var grid = new Reactive2DList<string>();

grid.Added.Subscribe(rows =>
{
    Console.WriteLine($"Rows added: {rows.Count()}");

    // Subscribe to each newly added row's changes
    foreach (var row in rows)
    {
        row.Added.Subscribe(items => Console.WriteLine($"Row added items: {string.Join(", ", items)}"));
        row.Removed.Subscribe(items => Console.WriteLine($"Row removed items: {string.Join(", ", items)}"));
        row.Changed.Subscribe(items => Console.WriteLine($"Row changed items: {string.Join(", ", items)}"));
    }
});

// Add a row and then modify it
var row0 = new ReactiveList<string>(new[] { "a", "b" });
grid.Add(row0);
row0.Add("c");
row0.Remove("a");
```

### Binding (nested ItemsControl)

```xml
<ItemsControl ItemsSource="{Binding Grid}">
  <ItemsControl.ItemTemplate>
    <DataTemplate>
      <ItemsControl ItemsSource="{Binding}">
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <TextBlock Text="{Binding}"/>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```


## Building locally

- Open the solution and build. Projects target `netstandard2.0` (library) and modern .NET versions for tests/apps.
- Dependencies: `DynamicData`, `System.Reactive`.

---

## License

MIT

---
**ReactiveList** - Empowering Industrial Automation with Reactive Technology ⚡🏭
