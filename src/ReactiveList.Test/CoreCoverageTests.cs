// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CP.Reactive.Core;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>
/// Additional coverage tests for core reactive primitives.
/// </summary>
public class CoreCoverageTests
{
    /// <summary>
    /// Cache notification stream extensions should filter, project, and count notifications.
    /// </summary>
    [Test]
    public void CacheNotifyExtensions_ShouldFilterProjectAndCountNotifications()
    {
        using var subject = new Signal<CacheNotify<string>>();
        var whereAction = new List<CacheNotify<string>>();
        var whereAdded = new List<CacheNotify<string>>();
        var whereRemoved = new List<CacheNotify<string>>();
        var selectedItems = new List<string>();
        var allItems = new List<string>();
        var addedItems = new List<string>();
        var removedItems = new List<string>();
        var updatedItems = new List<string>();
        var movedItems = new List<(string Item, int OldIndex, int NewIndex)>();
        var cleared = new List<CacheNotify<string>>();
        var transformed = new List<string>();
        var filtered = new List<string>();
        var counts = new List<(CacheAction Action, int Count)>();

        using var whereActionSubscription = subject.WhereAction(CacheAction.Added).Subscribe(whereAction.Add);
        using var whereAddedSubscription = subject.WhereAdded().Subscribe(whereAdded.Add);
        using var whereRemovedSubscription = subject.WhereRemoved().Subscribe(whereRemoved.Add);
        using var selectedSubscription = subject.SelectItems().Subscribe(selectedItems.Add);
        using var allSubscription = subject.SelectAllItems().Subscribe(allItems.Add);
        using var addedSubscription = subject.OnItemAdded().Subscribe(addedItems.Add);
        using var removedSubscription = subject.OnItemRemoved().Subscribe(removedItems.Add);
        using var updatedSubscription = subject.OnItemUpdated().Subscribe(updatedItems.Add);
        using var movedSubscription = subject.OnItemMoved().Subscribe(movedItems.Add);
        using var clearedSubscription = subject.OnCleared().Subscribe(cleared.Add);
        using var transformedSubscription = subject.TransformItems(item => item.ToUpperInvariant()).Subscribe(transformed.Add);
        using var filteredSubscription = subject.FilterItems(item => item.Contains("o")).Subscribe(filtered.Add);
        using var countSubscription = subject.CountByAction().Subscribe(counts.Add);

        var addedBatch = CreateStringBatch("two", "four");
        var removedBatch = CreateStringBatch("eight", "ten");

        subject.OnNext(new CacheNotify<string>(CacheAction.Added, "one", CurrentIndex: 0));
        subject.OnNext(new CacheNotify<string>(CacheAction.BatchAdded, default, addedBatch, CurrentIndex: 1));
        subject.OnNext(new CacheNotify<string>(CacheAction.Removed, "six", CurrentIndex: 2));
        subject.OnNext(new CacheNotify<string>(CacheAction.BatchRemoved, default, removedBatch));
        subject.OnNext(new CacheNotify<string>(CacheAction.Updated, "twelve", CurrentIndex: 3, Previous: "eleven"));
        subject.OnNext(new CacheNotify<string>(CacheAction.Moved, "fourteen", CurrentIndex: 4, PreviousIndex: 2));
        subject.OnNext(new CacheNotify<string>(CacheAction.Cleared, default));
        subject.OnNext(new CacheNotify<string>(CacheAction.BatchOperation, default));

        whereAction.Should().ContainSingle()
            .Which.Item.Should().Be("one");
        whereAdded.Select(notification => notification.Action).Should().Equal(CacheAction.Added, CacheAction.BatchAdded);
        whereRemoved.Select(notification => notification.Action).Should().Equal(CacheAction.Removed, CacheAction.BatchRemoved);
        selectedItems.Should().Equal("one", "six", "twelve", "fourteen");
        allItems.Should().Equal("one", "two", "four", "six", "eight", "ten", "twelve", "fourteen");
        addedItems.Should().Equal("one", "two", "four");
        removedItems.Should().Equal("six", "eight", "ten");
        updatedItems.Should().Equal("twelve");
        movedItems.Should().Equal(("fourteen", 2, 4));
        cleared.Should().ContainSingle();
        transformed.Should().Equal("ONE", "TWO", "FOUR", "SIX", "EIGHT", "TEN", "TWELVE", "FOURTEEN");
        filtered.Should().Equal("one", "two", "four", "fourteen");
        counts.Select(item => item.Count).Should().Equal(1, 2, 1, 2, 1, 1, 0, 0);

        addedBatch.Dispose();
        removedBatch.Dispose();
    }

    /// <summary>
    /// Time and scheduler extensions should buffer, throttle, observe, and dispose batches.
    /// </summary>
    [Test]
    public void CacheNotifyExtensions_ShouldBufferThrottleObserveAndDisposeBatches()
    {
        var notification = new CacheNotify<int>(CacheAction.Added, 1);
        var buffered = new[] { notification }.ToObservable()
            .BufferNotifications(TimeSpan.FromMilliseconds(1))
            .ToEnumerable()
            .ToList();
        var emptyBuffered = Array.Empty<CacheNotify<int>>().ToObservable()
            .BufferNotifications(TimeSpan.FromMilliseconds(1))
            .ToEnumerable()
            .ToList();
        var throttled = new[] { notification }.ToObservable()
            .ThrottleNotifications(TimeSpan.Zero)
            .ToEnumerable()
            .ToList();
        var observed = new[] { notification }.ToObservable()
            .ObserveOnScheduler(Sequencer.Immediate)
            .ToEnumerable()
            .ToList();

        buffered.Should().ContainSingle()
            .Which.Should().ContainSingle()
            .Which.Should().BeSameAs(notification);
        emptyBuffered.Should().BeEmpty();
        throttled.Should().ContainSingle()
            .Which.Should().BeSameAs(notification);
        observed.Should().ContainSingle()
            .Which.Should().BeSameAs(notification);

        using var subject = new Signal<CacheNotify<int>>();
        var autoDisposed = new List<CacheNotify<int>>();
        using var subscription = subject.AutoDisposeBatches().Subscribe(autoDisposed.Add);
        var batch = CreateBatch(42);

        subject.OnNext(new CacheNotify<int>(CacheAction.BatchAdded, default, batch));
        Action disposeAgain = () => batch.Dispose();

        autoDisposed.Should().ContainSingle();
        disposeAgain.Should().NotThrow();
    }

    /// <summary>
    /// CacheNotifyExtensions should validate null arguments.
    /// </summary>
    [Test]
    public void CacheNotifyExtensions_ShouldValidateNullArguments()
    {
        IObservable<CacheNotify<int>> source = null!;
        var valid = new[] { new CacheNotify<int>(CacheAction.Added, 1) }.ToObservable();

        Action[] sourceActions =
        [
            () => source.WhereAction(CacheAction.Added),
            () => source.WhereAdded(),
            () => source.WhereRemoved(),
            () => source.SelectItems(),
            () => source.SelectAllItems(),
            () => source.OnItemMoved(),
            () => source.BufferNotifications(TimeSpan.FromMilliseconds(1)),
            () => source.ThrottleNotifications(TimeSpan.FromMilliseconds(1)),
            () => source.ObserveOnScheduler(Sequencer.Immediate),
            () => source.TransformItems(item => item),
            () => source.FilterItems(item => true),
            () => source.AutoDisposeBatches(),
            () => source.CountByAction(),
            () => source.ToChangeSets()
        ];

        foreach (var action in sourceActions)
        {
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("source");
        }

        Action observeNullScheduler = () => valid.ObserveOnScheduler(null!);
        Action transformNullSelector = () => valid.TransformItems<int, int>(null!);
        Action filterNullPredicate = () => valid.FilterItems(null!);

        observeNullScheduler.Should().Throw<ArgumentNullException>()
            .WithParameterName("scheduler");
        transformNullSelector.Should().Throw<ArgumentNullException>()
            .WithParameterName("selector");
        filterNullPredicate.Should().Throw<ArgumentNullException>()
            .WithParameterName("predicate");
    }

    /// <summary>
    /// ToChange should map single notifications and ignore unsupported ones.
    /// </summary>
    [Test]
    public void ToChange_ShouldMapSingleNotifications()
    {
        CacheNotify<int> nullNotification = null!;

        nullNotification.ToChange().Should().BeNull();
        new CacheNotify<int>(CacheAction.Added, 1, CurrentIndex: 2).ToChange().Should().Be(
            Change<int>.CreateAdd(1, 2));
        new CacheNotify<int>(CacheAction.Removed, 3, CurrentIndex: 4).ToChange().Should().Be(
            Change<int>.CreateRemove(3, 4));
        new CacheNotify<int>(CacheAction.Updated, 5, CurrentIndex: 6, Previous: 4).ToChange().Should().Be(
            Change<int>.CreateUpdate(5, 4, 6));
        new CacheNotify<int>(CacheAction.Moved, 7, CurrentIndex: 8, PreviousIndex: 9).ToChange().Should().Be(
            Change<int>.CreateMove(7, 8, 9));
        new CacheNotify<int>(CacheAction.Refreshed, 10, CurrentIndex: 11).ToChange().Should().Be(
            Change<int>.CreateRefresh(10, 11));

        var clearChange = new CacheNotify<int>(CacheAction.Cleared, default).ToChange();
        clearChange.Should().NotBeNull();
        clearChange!.Value.Reason.Should().Be(ChangeReason.Clear);

        new CacheNotify<int>(CacheAction.BatchAdded, default).ToChange().Should().BeNull();
        new CacheNotify<string>(CacheAction.Added, default).ToChange().Should().BeNull();
    }

    /// <summary>
    /// ToChangeSets should expand batches, singles, and filter empty changes.
    /// </summary>
    [Test]
    public void ToChangeSets_ShouldExpandBatchesSinglesAndFilterEmptyChanges()
    {
        var addedBatch = CreateBatch(1, 2);
        var removedBatch = CreateBatch(3, 4);
        var clearedBatch = CreateBatch(5);
        var refreshBatch = CreateBatch(6);
        var emptyBatch = new PooledBatch<int>(ArrayPool<int>.Shared.Rent(1), 0);

        var notifications = new[]
        {
            new CacheNotify<int>(CacheAction.BatchAdded, default, addedBatch, CurrentIndex: 10),
            new CacheNotify<int>(CacheAction.BatchRemoved, default, removedBatch),
            new CacheNotify<int>(CacheAction.Cleared, default, clearedBatch),
            new CacheNotify<int>(CacheAction.BatchOperation, default, refreshBatch),
            new CacheNotify<int>(CacheAction.Cleared, default),
            new CacheNotify<int>(CacheAction.BatchAdded, default, emptyBatch),
            new CacheNotify<int>(CacheAction.Added, 7, CurrentIndex: 20),
            new CacheNotify<int>(CacheAction.BatchRemoved, default)
        };

        var changeSets = notifications.ToObservable()
            .ToChangeSets()
            .ToEnumerable()
            .ToList();

        changeSets.Should().HaveCount(5);
        changeSets[0].Should().HaveCount(2);
        changeSets[0].Select(change => change.Reason).Should().AllBeEquivalentTo(ChangeReason.Add);
        changeSets[0][0].CurrentIndex.Should().Be(10);
        changeSets[1].Select(change => change.Reason).Should().AllBeEquivalentTo(ChangeReason.Remove);
        changeSets[2].Should().ContainSingle()
            .Which.Reason.Should().Be(ChangeReason.Remove);
        changeSets[3].Should().ContainSingle()
            .Which.Reason.Should().Be(ChangeReason.Refresh);
        changeSets[4].Should().ContainSingle()
            .Which.Should().Be(Change<int>.CreateAdd(7, 20));

        foreach (var changeSet in changeSets)
        {
            changeSet.Dispose();
        }

        addedBatch.Dispose();
        removedBatch.Dispose();
        clearedBatch.Dispose();
        refreshBatch.Dispose();
        emptyBatch.Dispose();
    }

    /// <summary>
    /// ChangeSet should expose counts, spans, enumerators, and validation.
    /// </summary>
    [Test]
    public void ChangeSet_ShouldExposeCountsEnumeratorsAndValidation()
    {
        var changes = new[]
        {
            Change<int>.CreateAdd(1, 0),
            Change<int>.CreateRemove(2, 1),
            Change<int>.CreateUpdate(3, 2, 2),
            Change<int>.CreateMove(4, 3, 0)
        };

        using var set = new ChangeSet<int>(changes);
        using var single = new ChangeSet<int>(Change<int>.CreateRefresh(5, 4));
        using var comparison = new ChangeSet<int>(changes.ToArray());
        var defaultSet = default(ChangeSet<int>);

        set.Count.Should().Be(4);
        set.Adds.Should().Be(1);
        set.Removes.Should().Be(1);
        set.Updates.Should().Be(1);
        set.Moves.Should().Be(1);
        set[0].Should().Be(changes[0]);
        single.Should().ContainSingle()
            .Which.Should().Be(Change<int>.CreateRefresh(5, 4));
        defaultSet.Adds.Should().Be(0);
        set.Equals(set).Should().BeTrue();
        set.Equals(comparison).Should().BeFalse();
        set.GetHashCode().Should().NotBe(0);

        ((IEnumerable<Change<int>>)set).Select(change => change.Current).Should().Equal(1, 2, 3, 4);

        var enumerator = ((IEnumerable)set).GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be(changes[0]);
        enumerator.Reset();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be(changes[0]);

        Action getNegative = () => _ = set[-1];
        Action getPastEnd = () => _ = set[set.Count];
        Action createNull = () => _ = new ChangeSet<int>(null!);

        getNegative.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("index");
        getPastEnd.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("index");
        createNull.Should().Throw<ArgumentNullException>()
            .WithParameterName("changes");

#if NET6_0_OR_GREATER
        using var spanSet = new ChangeSet<int>(changes.AsSpan());
        spanSet.AsSpan().ToArray().Should().Equal(changes);
#endif
    }

    /// <summary>
    /// PooledEditableListWrapper should synchronize all list operations.
    /// </summary>
    [Test]
    public void PooledEditableListWrapper_ShouldSynchronizeOperationsAndValidateMoves()
    {
        EditableListWrapperPool<string>.Clear();
        var list = new List<string> { "one", "two" };
        var observable = new ObservableCollection<string>(list);
        using var wrapper = new PooledEditableListWrapper<string>(list, observable);

        wrapper.IsReadOnly.Should().BeFalse();
        wrapper[1].Should().Be("two");
        wrapper[1] = "deux";
        wrapper.AddRange(["three", "four"]);
        wrapper.Insert(1, "inserted");
        wrapper.Move(0, 2);
        wrapper.Move(2, 2);

        list.Should().Equal("inserted", "deux", "one", "three", "four");
        observable.Should().Equal(list);
        wrapper.Contains("three").Should().BeTrue();
        wrapper.IndexOf("three").Should().Be(3);

        var copied = new string[wrapper.Count];
        wrapper.CopyTo(copied, 0);
        copied.Should().Equal(list);
        wrapper.ToArray().Should().Equal(list);
        ((IEnumerable)wrapper).Cast<string>().Should().Equal(list);
        var wrapperEnumerator = ((IEnumerable)wrapper).GetEnumerator();
        wrapperEnumerator.MoveNext().Should().BeTrue();
        wrapperEnumerator.Current.Should().Be("inserted");

        wrapper.Remove("missing").Should().BeFalse();
        wrapper.Remove("deux").Should().BeTrue();
        wrapper.RemoveAt(0);
        wrapper.Clear();

        list.Should().BeEmpty();
        observable.Should().BeEmpty();

        wrapper.Initialize(new List<string> { "a", "b" }, null);
        wrapper.Count.Should().Be(2);

        Action badOldIndex = () => wrapper.Move(-1, 0);
        Action badNewIndex = () => wrapper.Move(0, 2);

        badOldIndex.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("oldIndex");
        badNewIndex.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("newIndex");

        EditableListWrapperPool<string>.Clear();
    }

    /// <summary>
    /// Returned pooled wrappers should reject future access and dispose idempotently.
    /// </summary>
    [Test]
    public void PooledEditableListWrapper_WhenReturned_ShouldRejectAccessAndDisposeIdempotently()
    {
        EditableListWrapperPool<int>.Clear();
        var wrapper = new PooledEditableListWrapper<int>([]);

        ((IResettable)wrapper).Reset();
        wrapper.Dispose();

        wrapper.Count.Should().Be(0);
        Action useReturned = () => wrapper.Add(1);

        useReturned.Should().Throw<ObjectDisposedException>();
    }

    /// <summary>
    /// ReactiveGroup should expose grouping data and forward collection change events.
    /// </summary>
    [Test]
    public void ReactiveGroup_ShouldExposeItemsAndForwardCollectionChanges()
    {
        var source = new ObservableCollection<string>(["one"]);
        var group = new ReactiveGroup<string, string>("letters", source);
        var events = new List<NotifyCollectionChangedEventArgs>();
        group.CollectionChanged += (sender, args) => events.Add(args);

        source.Add("two");

        group.Key.Should().Be("letters");
        group.Count.Should().Be(2);
        group.Items.Should().Equal("one", "two");
        group.Should().Equal("one", "two");
        ((IEnumerable)group).Cast<string>().Should().Equal("one", "two");
        var groupEnumerator = ((IEnumerable)group).GetEnumerator();
        groupEnumerator.MoveNext().Should().BeTrue();
        groupEnumerator.Current.Should().Be("one");
        events.Should().ContainSingle()
            .Which.Action.Should().Be(NotifyCollectionChangedAction.Add);

        var silentSource = new ObservableCollection<int>();
        _ = new ReactiveGroup<string, int>("numbers", silentSource);
        Action addWithoutSubscriber = () => silentSource.Add(1);

        addWithoutSubscriber.Should().NotThrow();
    }

    /// <summary>
    /// SecondaryIndex should reject keys of the wrong type in MatchesKey.
    /// </summary>
    [Test]
    public void SecondaryIndex_MatchesKey_ShouldRejectWrongKeyType()
    {
        var index = new SecondaryIndex<Person, string>(person => person.Department);
        var person = new Person(1, "Ada", "Engineering");

        index.MatchesKey(person, "Engineering").Should().BeTrue();
        index.MatchesKey(person, 123).Should().BeFalse();
    }

    /// <summary>
    /// Internal grouping should expose its non-generic enumerator.
    /// </summary>
    [Test]
    public void ChangeGrouping_ShouldExposeNonGenericEnumerator()
    {
        var grouping = new ChangeGrouping<string, int>("numbers", new[] { 1, 2 });

        grouping.Key.Should().Be("numbers");
        ((IEnumerable)grouping).Cast<int>().Should().Equal(1, 2);
    }

    private static PooledBatch<int> CreateBatch(params int[] values)
    {
        var array = ArrayPool<int>.Shared.Rent(Math.Max(1, values.Length));
        Array.Copy(values, array, values.Length);
        return new PooledBatch<int>(array, values.Length);
    }

    private static PooledBatch<string> CreateStringBatch(params string[] values)
    {
        var array = ArrayPool<string>.Shared.Rent(Math.Max(1, values.Length));
        Array.Copy(values, array, values.Length);
        return new PooledBatch<string>(array, values.Length);
    }

    private sealed record Person(int Id, string Name, string Department);
}
