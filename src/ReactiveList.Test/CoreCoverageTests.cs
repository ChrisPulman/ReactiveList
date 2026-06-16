// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using CP.Reactive;
using CP.Reactive.Core;
using FluentAssertions;
using ReactiveUI.Primitives.Concurrency;
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
        var buffered = ObservableMixins.ToEnumerable(new[] { notification }.ToObservable()
                .BufferNotifications(TimeSpan.FromMilliseconds(1)))
            .ToList();
        var emptyBuffered = ObservableMixins.ToEnumerable(Array.Empty<CacheNotify<int>>().ToObservable()
                .BufferNotifications(TimeSpan.FromMilliseconds(1)))
            .ToList();
        var throttled = ObservableMixins.ToEnumerable(new[] { notification }.ToObservable()
                .ThrottleNotifications(TimeSpan.Zero))
            .ToList();
        var observed = ObservableMixins.ToEnumerable(new[] { notification }.ToObservable()
                .ObserveOnScheduler(Sequencer.Immediate))
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

        var changeSets = ObservableMixins.ToEnumerable(notifications.ToObservable()
                .ToChangeSets())
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
        defaultSet.Dispose();
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
        EditableListWrapperPool.Clear<string>();
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

        EditableListWrapperPool.Clear<string>();
    }

    /// <summary>
    /// Returned pooled wrappers should reject future access and dispose idempotently.
    /// </summary>
    [Test]
    public void PooledEditableListWrapper_WhenReturned_ShouldRejectAccessAndDisposeIdempotently()
    {
        EditableListWrapperPool.Clear<int>();
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
        var enumerator = ((IEnumerable)grouping).GetEnumerator();

        grouping.Key.Should().Be("numbers");
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be(1);
        ((IEnumerable)grouping).Cast<int>().Should().Equal(1, 2);
    }

    /// <summary>
    /// Internal observable factories should surface factory errors and event handler variants.
    /// </summary>
    [Test]
    public void InternalObservableFactories_ShouldCoverErrorAndEventBranches()
    {
        var factoryError = new InvalidOperationException("factory");
        var deferredObserver = new RecordingObserver<int>();
        using var deferredSubscription = Observable
            .Defer<int>(() => throw factoryError)
            .Subscribe(deferredObserver);

        deferredObserver.Error.Should().BeSameAs(factoryError);

        var successValues = new List<int>();
        using var successSubscription = Observable
            .Defer(() => new[] { 1, 2 }.ToObservable())
            .Subscribe(successValues.Add);

        successValues.Should().Equal(1, 2);

        var eventSource = new EventSource();
        var events = new List<EventPattern<EventArgs>>();
        using var eventSubscription = Observable
            .FromEventPattern<EventHandler<EventArgs>, EventArgs>(
                handler => eventSource.Raised += handler,
                handler => eventSource.Raised -= handler)
            .Subscribe(events.Add);

        eventSource.Raise();
        events.Should().ContainSingle();

        Action unsupported = () => Observable
            .FromEventPattern<Action, EventArgs>(_ => { }, _ => { })
            .Subscribe(new RecordingObserver<EventPattern<EventArgs>>());

        unsupported.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// Internal observable operators should cover error and completion branches.
    /// </summary>
    [Test]
    public void ObservableMixins_ShouldCoverErrorAndCompletionBranches()
    {
        var toEnumerableError = new InvalidOperationException("enumerable");
        var throwing = Signal.Create<int>(observer =>
        {
            observer.OnError(toEnumerableError);
            return ReactiveUI.Primitives.Disposables.Scope.Empty;
        });

        Action enumerate = () => ObservableMixins.ToEnumerable(throwing).ToList();
        enumerate.Should().Throw<InvalidOperationException>();

        using var bufferSource = new Signal<int>();
        var buffered = new RecordingObserver<IList<int>>();
        using var bufferSubscription = bufferSource.Buffer(TimeSpan.FromMilliseconds(1), Sequencer.Immediate).Subscribe(buffered);

        bufferSource.OnNext(1);
        bufferSource.OnCompleted();

        buffered.Values.SelectMany(static item => item).Should().Contain(1);
        buffered.Completed.Should().BeTrue();

        using var bufferErrorSource = new Signal<int>();
        var bufferErrorObserver = new RecordingObserver<IList<int>>();
        var bufferError = new InvalidOperationException("buffer");
        using var bufferErrorSubscription = bufferErrorSource.Buffer(TimeSpan.FromMilliseconds(1), Sequencer.Immediate).Subscribe(bufferErrorObserver);

        bufferErrorSource.OnError(bufferError);
        bufferErrorObserver.Error.Should().BeSameAs(bufferError);

        using var throttleSource = new Signal<int>();
        var throttled = new RecordingObserver<int>();
        using var throttleSubscription = throttleSource.Throttle(TimeSpan.FromMilliseconds(1), Sequencer.Immediate).Subscribe(throttled);

        throttleSource.OnNext(42);
        throttleSource.OnCompleted();

        throttled.Values.Should().Contain(42);
        throttled.Completed.Should().BeTrue();

        using var throttleErrorSource = new Signal<int>();
        var throttleErrorObserver = new RecordingObserver<int>();
        var throttleError = new InvalidOperationException("throttle");
        using var throttleErrorSubscription = throttleErrorSource.Throttle(TimeSpan.FromMilliseconds(1), Sequencer.Immediate).Subscribe(throttleErrorObserver);

        throttleErrorSource.OnError(throttleError);
        throttleErrorObserver.Error.Should().BeSameAs(throttleError);

        var manualBufferSequencer = new ManualSequencer();
        var completedBufferObserver = new RecordingObserver<IList<int>>();
        var completedThenValue = Signal.Create<int>(observer =>
        {
            observer.OnNext(7);
            observer.OnCompleted();
            observer.OnNext(8);
            return ReactiveUI.Primitives.Disposables.Scope.Empty;
        });

        using var completedBufferSubscription = completedThenValue
            .Buffer(TimeSpan.FromMilliseconds(1), manualBufferSequencer)
            .Subscribe(completedBufferObserver);

        completedBufferObserver.Values.Should().ContainSingle()
            .Which.Should().Equal(7);
        completedBufferObserver.Completed.Should().BeTrue();
        manualBufferSequencer.RunAll();

        using var emptyFlushSource = new Signal<int>();
        var duplicateBufferSequencer = new DuplicateSequencer();
        var emptyFlushObserver = new RecordingObserver<IList<int>>();
        using var emptyFlushSubscription = emptyFlushSource
            .Buffer(TimeSpan.FromMilliseconds(1), duplicateBufferSequencer)
            .Subscribe(emptyFlushObserver);

        emptyFlushSource.OnNext(11);
        duplicateBufferSequencer.RunAll();

        emptyFlushObserver.Values.Should().ContainSingle()
            .Which.Should().Equal(11);

        var manualThrottleSequencer = new ManualSequencer();
        var completedThrottleObserver = new RecordingObserver<int>();

        using var completedThrottleSubscription = completedThenValue
            .Throttle(TimeSpan.FromMilliseconds(1), manualThrottleSequencer)
            .Subscribe(completedThrottleObserver);

        completedThrottleObserver.Values.Should().ContainSingle()
            .Which.Should().Be(7);
        completedThrottleObserver.Completed.Should().BeTrue();
        manualThrottleSequencer.RunAll();

        using var postStopBufferSubscription = new ScriptedObservable<int>(observer =>
            {
                observer.OnCompleted();
                observer.OnNext(9);
            })
            .Buffer(TimeSpan.FromMilliseconds(1), new ManualSequencer())
            .Subscribe(new RecordingObserver<IList<int>>());

        using var postStopThrottleSubscription = new ScriptedObservable<int>(observer =>
            {
                observer.OnCompleted();
                observer.OnNext(9);
            })
            .Throttle(TimeSpan.FromMilliseconds(1), new ManualSequencer())
            .Subscribe(new RecordingObserver<int>());
    }

    /// <summary>
    /// ReactiveList extension guards and default dynamic filters should be covered.
    /// </summary>
    [Test]
    public void ReactiveListExtensions_ShouldCoverGuardAndDefaultFilterBranches()
    {
        IObservable<ChangeSet<int>> nullChangeSets = null!;
        Action nullGroupSource = () => nullChangeSets.GroupByChanges(static item => item);
        Action nullGroupingSource = () => nullChangeSets.GroupingByChanges(static item => item);
        Action nullRefreshSource = () => ReactiveListExtensions.AutoRefresh<NotifyItem>(null!, propertyName: null);

        nullGroupSource.Should().Throw<ArgumentNullException>().WithParameterName("source");
        nullGroupingSource.Should().Throw<ArgumentNullException>().WithParameterName("source");
        nullRefreshSource.Should().Throw<ArgumentNullException>().WithParameterName("source");

        using var changeSets = new Signal<ChangeSet<int>>();
        Action nullGroupSelector = () => changeSets.GroupByChanges<int, int>(null!);
        Action nullGroupingSelector = () => changeSets.GroupingByChanges<int, int>(null!);

        nullGroupSelector.Should().Throw<ArgumentNullException>().WithParameterName("keySelector");
        nullGroupingSelector.Should().Throw<ArgumentNullException>().WithParameterName("keySelector");

        using var stream = new Signal<CacheNotify<int>>();
        using var filters = new Signal<Func<int, bool>>();
        var received = new List<CacheNotify<int>>();
        using var subscription = stream.FilterDynamic(filters).Subscribe(received.Add);

        stream.OnNext(new CacheNotify<int>(CacheAction.Added, 10));
        stream.OnNext(new CacheNotify<int>(CacheAction.Removed, 20));

        received.Select(static item => item.Item).Should().Equal(10, 20);

        using var pairStream = new Signal<CacheNotify<KeyValuePair<int, string>>>();
        using var pairFilters = new Signal<Func<KeyValuePair<int, string>, bool>>();
        var pairReceived = new List<CacheNotify<KeyValuePair<int, string>>>();
        using var pairSubscription = pairStream.FilterDynamic(pairFilters).Subscribe(pairReceived.Add);

        pairStream.OnNext(new CacheNotify<KeyValuePair<int, string>>(CacheAction.Added, new KeyValuePair<int, string>(1, "one")));
        pairStream.OnNext(new CacheNotify<KeyValuePair<int, string>>(CacheAction.Removed, new KeyValuePair<int, string>(2, "two")));

        pairReceived.Select(static item => item.Item.Key).Should().Equal(1, 2);

        using var noMatchBatch = CreateBatch(1, 2);
        var noMatchNotification = new CacheNotify<int>(CacheAction.BatchAdded, default, noMatchBatch);
        ReactiveListExtensions.FilterBatchByPredicate(noMatchNotification, static item => item > 10).Should().BeNull();
        ReactiveListExtensions.FilterBatch(noMatchNotification, new HashSet<int> { 99 }).Should().BeNull();
    }

    /// <summary>
    /// GroupBy should propagate upstream errors to active groups and to the outer subscriber.
    /// </summary>
    [Test]
    public void GroupBy_ShouldPropagateErrorsToGroupsAndOuterSubscriber()
    {
        using var source = new Signal<ChangeSet<int>>();
        var groupErrors = new List<Exception>();
        var outerObserver = new RecordingObserver<IGroupedObservable<int, int>>();
        var upstreamError = new InvalidOperationException("group failure");

        using var subscription = ReactiveListExtensions
            .GroupByChanges<int, int>(source, static value => value % 2)
            .Subscribe(
                group =>
                {
                    group.Key.Should().Be(1);
                    group.Subscribe(_ => { }, groupErrors.Add, () => { });
                },
                outerObserver.OnError,
                outerObserver.OnCompleted);

        using var changes = new ChangeSet<int>(Change<int>.CreateAdd(1));
        source.OnNext(changes);
        source.OnError(upstreamError);

        groupErrors.Should().ContainSingle()
            .Which.Should().BeSameAs(upstreamError);
        outerObserver.Error.Should().BeSameAs(upstreamError);
    }

    /// <summary>
    /// SelectChanges should return the shared empty changeset when the input contains no changes.
    /// </summary>
    [Test]
    public void SelectChanges_ShouldReturnEmptyChangeSetForEmptyInput()
    {
        var source = new[] { ChangeSet<int>.Empty }.ToObservable();
        var results = ObservableMixins.ToEnumerable(
                ReactiveListExtensions.SelectChanges<int, string>(
                    source,
                    (Func<int, string>)(static value => value.ToString())))
            .ToList();

        results.Should().ContainSingle()
            .Which.Count.Should().Be(0);
    }

    /// <summary>
    /// The ReactiveUI.Primitives R3 bridge generated attribute should be constructible.
    /// </summary>
    [Test]
    public void ReactivePrimitivesGeneratedBridgeAttribute_ShouldBeConstructible()
    {
        var attributeType = typeof(CP.Reactive.Collections.ReactiveList<int>).Assembly.GetType(
            "ReactiveUI.Primitives.R3Bridge.Generated.PrimitivesR3BridgeGeneratedAttribute");

        attributeType.Should().NotBeNull();
        var attribute = Activator.CreateInstance(
            attributeType!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: ["ReactiveList"],
            culture: null);

        attribute.Should().NotBeNull();
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

    private sealed record NotifyItem(int Value) : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public void Raise(string? propertyName = null) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }

    private sealed class EventSource
    {
        public event EventHandler<EventArgs>? Raised;

        public void Raise() => Raised?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RecordingObserver<T> : IObserver<T>
    {
        public List<T> Values { get; } = [];

        public Exception? Error { get; private set; }

        public bool Completed { get; private set; }

        public void OnCompleted() => Completed = true;

        public void OnError(Exception error) => Error = error;

        public void OnNext(T value) => Values.Add(value);
    }

    private sealed class ScriptedObservable<T>(Action<IObserver<T>> script) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            script(observer);
            return ReactiveUI.Primitives.Disposables.Scope.Empty;
        }
    }

    private sealed class ManualSequencer : ISequencer
    {
        private readonly Queue<IWorkItem> _workItems = new();

        public DateTimeOffset Now => DateTimeOffset.UtcNow;

        public long Timestamp => DateTimeOffset.UtcNow.Ticks;

        public void Schedule(IWorkItem item) => _workItems.Enqueue(item);

        public void Schedule(IWorkItem item, long dueTime) => _workItems.Enqueue(item);

        public void RunAll()
        {
            while (_workItems.Count > 0)
            {
                _workItems.Dequeue().Execute();
            }
        }
    }

    private sealed class DuplicateSequencer : ISequencer
    {
        private readonly Queue<IWorkItem> _workItems = new();

        public DateTimeOffset Now => DateTimeOffset.UtcNow;

        public long Timestamp => DateTimeOffset.UtcNow.Ticks;

        public void Schedule(IWorkItem item)
        {
            _workItems.Enqueue(item);
            _workItems.Enqueue(item);
        }

        public void Schedule(IWorkItem item, long dueTime) => Schedule(item);

        public void RunAll()
        {
            while (_workItems.Count > 0)
            {
                _workItems.Dequeue().Execute();
            }
        }
    }
}
