# ReactiveList
A Reactive List giving observables for changes, and current list items.
Based on DynamicData.

# IReactiveList
Inherits : 
- IList of T
- IList
- IReadOnlyList of T
- INotifyCollectionChanged
- INotifyPropertyChanged
- ICancelable


## Features
- **Reactive**: The list is reactive, and will notify observers of changes.
-----------------------------------------------------------------------------
- **CurrentItems**: Observe the current list of items.
- **Changed**: Observe changes to the list.
- **Added**: Observe changes Added to the list.
- **Removed**: Observe changes Removed from the list.
-----------------------------------------------------------------------------
- **Add**: Add an item to the list.
- **AddRange**: Add a range of items to the list.
- **CopyTo**: Copy the list to an array.
- **Contains**: Check if the list contains an item.
- **Count**: Get the count of items in the list.
- **Clear**: Clear the list of all items.
- **Dispose**: Dispose of the list.
- **GetEnumerator: Get an enumerator for the list.
- **Insert**: Insert an item at a specific index.
- **IndexOf**: Get an item from the current list of items.
- **Remove**: Remove an item from the list.
- **RemoveAt**: Remove an item at a specific index.
- **RemoveRange**: Remove a range of items from the list.
- **ReplaceAll**: Replace the current list of items with a new list of items.
- **Subscribe**: Subscribe to the list.
- **Update**: Update an item in the list.
- **Updated**: Observe changes Updated in the list.
