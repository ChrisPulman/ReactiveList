// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Views;
#else
namespace CP.Primitives.Views;
#endif

/// <summary>Retains structural changes before dispatch and maintains the consumed source snapshot.</summary>
/// <typeparam name="T">The source item type.</typeparam>
internal sealed class ViewChangeBuffer<T>
    where T : notnull
{
    /// <summary>Synchronizes notification producers with the consuming view.</summary>
    private readonly Lock _gate = new();

    /// <summary>The changes retained independently of coalesced dispatch signals.</summary>
    private readonly Queue<(ChangeSet<T> Changes, long Version)> _pending = [];

    /// <summary>The source revision already covered by an explicit snapshot.</summary>
    private long _coveredVersion;

    /// <summary>Initializes a new instance of the <see cref="ViewChangeBuffer{T}"/> class.</summary>
    /// <param name="source">The source to snapshot.</param>
    internal ViewChangeBuffer(IReactiveSource<T> source) => Reset(source);

    /// <summary>Gets the source items after consumed changes.</summary>
    internal List<T> Items { get; } = [];

    /// <summary>Gets whether dispatch-time changes remain to be consumed.</summary>
    internal bool HasPending
    {
        get
        {
            lock (_gate)
            {
                return _pending.Count > 0;
            }
        }
    }

    /// <summary>Finds an occurrence, preferring reference identity over value equality.</summary>
    /// <param name="items">The collection to search.</param>
    /// <param name="item">The occurrence to locate.</param>
    /// <returns>The matching index, or -1.</returns>
    internal static int FindIndex(IEnumerable<T> items, T item)
    {
        var index = 0;
        var equalIndex = -1;
        foreach (var candidate in items)
        {
            if (!typeof(T).IsValueType && ReferenceEquals(candidate, item))
            {
                return index;
            }

            if (equalIndex < 0 && EqualityComparer<T>.Default.Equals(candidate, item))
            {
                equalIndex = index;
            }

            index++;
        }

        return equalIndex;
    }

    /// <summary>Retains a stable change set before its dispatch signal is throttled.</summary>
    /// <param name="changes">The copied structural changes.</param>
    /// <param name="version">The emitting source revision.</param>
    /// <returns>A dispatch signal.</returns>
    internal bool Capture(ChangeSet<T> changes, long version)
    {
        lock (_gate)
        {
            _pending.Enqueue((changes, version));
        }

        return true;
    }

    /// <summary>Takes the next changes not already represented by an explicit snapshot.</summary>
    /// <param name="changes">The retained changes.</param>
    /// <returns>Whether a change set was available.</returns>
    internal bool TryTake(out ChangeSet<T> changes)
    {
        lock (_gate)
        {
            while (_pending.Count > 0)
            {
                var pending = _pending.Dequeue();
                if (pending.Version > _coveredVersion)
                {
                    changes = pending.Changes;
                    return true;
                }
            }
        }

        changes = default;
        return false;
    }

    /// <summary>Snapshots an explicit refresh and excludes already-covered queued changes.</summary>
    /// <param name="source">The source to snapshot.</param>
    internal void Reset(IReactiveSource<T> source)
    {
        var version = source.Version;
        List<T> snapshot = [.. source];
        while (version != source.Version)
        {
            version = source.Version;
            snapshot = [.. source];
        }

        lock (_gate)
        {
            _coveredVersion = version;
        }

        Items.Clear();
        Items.AddRange(snapshot);
    }

    /// <summary>Applies a structural change to the logical source order.</summary>
    /// <param name="change">The change to consume.</param>
    internal void Apply(Change<T> change)
    {
        if (change.Reason is ChangeReason.Add)
        {
            var index = change.CurrentIndex;
            Items.Insert(index >= 0 && index <= Items.Count ? index : Items.Count, change.Current);
        }
        else if (change.Reason is ChangeReason.Remove)
        {
            RemoveItem(change);
        }
        else if (change.Reason is ChangeReason.Update)
        {
            UpdateItem(change);
        }
        else if (change.Reason is ChangeReason.Move)
        {
            MoveItem(change);
        }
        else if (change.Reason is ChangeReason.Clear)
        {
            Items.Clear();
        }
    }

    /// <summary>Removes one occurrence from the source mirror.</summary>
    /// <param name="change">The removal to apply.</param>
    private void RemoveItem(Change<T> change)
    {
        var index = change.PreviousIndex;
        if (index < 0 || index >= Items.Count)
        {
            index = FindIndex(Items, change.Current);
        }

        if (index < 0)
        {
            return;
        }

        Items.RemoveAt(index);
    }

    /// <summary>Replaces an occurrence in the source mirror.</summary>
    /// <param name="change">The update to apply.</param>
    private void UpdateItem(Change<T> change)
    {
        var index = change.CurrentIndex;
        if (index < 0 || index >= Items.Count)
        {
            index = change.Previous is null ? -1 : FindIndex(Items, change.Previous);
        }

        if (index < 0)
        {
            return;
        }

        Items[index] = change.Current;
    }

    /// <summary>Moves an occurrence within the source mirror.</summary>
    /// <param name="change">The move to apply.</param>
    private void MoveItem(Change<T> change)
    {
        var previous = change.PreviousIndex;
        var current = change.CurrentIndex;
        if (previous < 0 || previous >= Items.Count || current < 0 || current >= Items.Count)
        {
            return;
        }

        var item = Items[previous];
        Items.RemoveAt(previous);
        Items.Insert(current, item);
    }
}
