// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using CP.Primitives;
using CP.Primitives.Core;
using FluentAssertions;
using ReactiveUI.Primitives.Concurrency;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Additional coverage tests for core reactive primitives.</summary>
public class CoreCoverageTests
{
    /// <summary>The coverage value two.</summary>
    private const int CoverageValueTwo = 2;

    /// <summary>The coverage value three.</summary>
    private const int CoverageValueThree = 3;

    /// <summary>The coverage value four.</summary>
    private const int CoverageValueFour = 4;

    /// <summary>The coverage value five.</summary>
    private const int CoverageValueFive = 5;

    /// <summary>The coverage value six.</summary>
    private const int CoverageValueSix = 6;

    /// <summary>The coverage value seven.</summary>
    private const int CoverageValueSeven = 7;

    /// <summary>The coverage value eight.</summary>
    private const int CoverageValueEight = 8;

    /// <summary>The coverage value nine.</summary>
    private const int CoverageValueNine = 9;

    /// <summary>The coverage value ten.</summary>
    private const int CoverageValueTen = 10;

    /// <summary>The coverage value eleven.</summary>
    private const int CoverageValueEleven = 11;

    /// <summary>The coverage value twenty.</summary>
    private const int CoverageValueTwenty = 20;

    /// <summary>The coverage value forty two.</summary>
    private const int CoverageValueFortyTwo = 42;

    /// <summary>The coverage value ninety nine.</summary>
    private const int CoverageValueNinetyNine = 99;

    /// <summary>The coverage value one hundred twenty three.</summary>
    private const int CoverageValueOneHundredTwentyThree = 123;

    /// <summary>The removed batch first item.</summary>
    private const string RemovedBatchFirstItem = "eight";

    /// <summary>The updated text item.</summary>
    private const string UpdatedTextItem = "twelve";

    /// <summary>The moved text item.</summary>
    private const string MovedTextItem = "fourteen";

    /// <summary>The source parameter name.</summary>
    private const string SourceParameterName = "source";

    /// <summary>The third text item.</summary>
    private const string ThirdTextItem = "three";

    /// <summary>The inserted text item.</summary>
    private const string InsertedTextItem = "inserted";

    /// <summary>The numbers group key.</summary>
    private const string NumbersGroupKey = "numbers";

    /// <summary>The values emitted by observable factory coverage tests.</summary>
    private static readonly int[] ObservableFactoryValues = [1, 2];

    /// <summary>Cache notification stream extensions should filter, project, and count notifications.</summary>
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
#if NETFRAMEWORK
        Func<string, bool> containsLowercaseO = static item => item.IndexOf('o') >= 0;
#else
        Func<string, bool> containsLowercaseO = static item => item.Contains('o');
#endif

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
        using var transformedSubscription = subject.TransformItems(static item => item.ToUpperInvariant()).Subscribe(transformed.Add);
        using var filteredSubscription = subject.FilterItems(containsLowercaseO).Subscribe(filtered.Add);
        using var countSubscription = subject.CountByAction().Subscribe(counts.Add);

        var addedBatch = CreateStringBatch("two", "four");
        var removedBatch = CreateStringBatch(RemovedBatchFirstItem, "ten");

        subject.OnNext(new(CacheAction.Added, "one", CurrentIndex: 0));
        subject.OnNext(new(CacheAction.BatchAdded, default, addedBatch, CurrentIndex: 1));
        subject.OnNext(new(CacheAction.Removed, "six", CurrentIndex: 2));
        subject.OnNext(new(CacheAction.BatchRemoved, default, removedBatch));
        subject.OnNext(new(CacheAction.Updated, UpdatedTextItem, CurrentIndex: 3, Previous: "eleven"));
        subject.OnNext(new(CacheAction.Moved, MovedTextItem, CurrentIndex: 4, PreviousIndex: 2));
        subject.OnNext(new(CacheAction.Cleared, default));
        subject.OnNext(new(CacheAction.BatchOperation, default));

        _ = whereAction.Should().ContainSingle()
            .Which.Item.Should().Be("one");
        _ = Project(whereAdded, static notification => notification.Action).Should().Equal(CacheAction.Added, CacheAction.BatchAdded);
        _ = Project(whereRemoved, static notification => notification.Action).Should().Equal(CacheAction.Removed, CacheAction.BatchRemoved);
        _ = selectedItems.Should().Equal("one", "six", UpdatedTextItem, MovedTextItem);
        _ = allItems.Should().Equal("one", "two", "four", "six", RemovedBatchFirstItem, "ten", UpdatedTextItem, MovedTextItem);
        _ = addedItems.Should().Equal("one", "two", "four");
        _ = removedItems.Should().Equal("six", RemovedBatchFirstItem, "ten");
        _ = updatedItems.Should().Equal(UpdatedTextItem);
        _ = movedItems.Should().Equal((MovedTextItem, CoverageValueTwo, CoverageValueFour));
        _ = cleared.Should().ContainSingle();
        _ = transformed.Should().Equal("ONE", "TWO", "FOUR", "SIX", "EIGHT", "TEN", "TWELVE", "FOURTEEN");
        _ = filtered.Should().Equal("one", "two", "four", MovedTextItem);
        _ = Project(counts, static item => item.Count).Should().Equal(1, CoverageValueTwo, 1, CoverageValueTwo, 1, 1, 0, 0);

        addedBatch.Dispose();
        removedBatch.Dispose();
    }

    /// <summary>Time and scheduler extensions should buffer, throttle, observe, and dispose batches.</summary>
    [Test]
    public void CacheNotifyExtensions_ShouldBufferThrottleObserveAndDisposeBatches()
    {
        var notification = new CacheNotify<int>(CacheAction.Added, 1);
        var buffered = Collect(ObservableMixins.ToEnumerable(new[] { notification }.ToObservable()
            .BufferNotifications(TimeSpan.FromMilliseconds(1))));
        var emptyBuffered = Collect(ObservableMixins.ToEnumerable(Array.Empty<CacheNotify<int>>().ToObservable()
            .BufferNotifications(TimeSpan.FromMilliseconds(1))));
        var throttled = Collect(ObservableMixins.ToEnumerable(new[] { notification }.ToObservable()
            .ThrottleNotifications(TimeSpan.Zero)));
        var observed = Collect(ObservableMixins.ToEnumerable(new[] { notification }.ToObservable()
            .ObserveOnScheduler(Sequencer.Immediate)));

        _ = buffered.Should().ContainSingle()
            .Which.Should().ContainSingle()
            .Which.Should().BeSameAs(notification);
        _ = emptyBuffered.Should().BeEmpty();
        _ = throttled.Should().ContainSingle()
            .Which.Should().BeSameAs(notification);
        _ = observed.Should().ContainSingle()
            .Which.Should().BeSameAs(notification);

        using var subject = new Signal<CacheNotify<int>>();
        var autoDisposed = new List<CacheNotify<int>>();
        using var subscription = subject.AutoDisposeBatches().Subscribe(autoDisposed.Add);
        var batch = CreateBatch(CoverageValueFortyTwo);

        subject.OnNext(new(CacheAction.BatchAdded, default, batch));
        Action disposeAgain = batch.Dispose;

        _ = autoDisposed.Should().ContainSingle();
        _ = disposeAgain.Should().NotThrow();
    }

    /// <summary>CacheNotifyExtensions should validate null arguments.</summary>
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
            () => source.TransformItems(static item => item),
            () => source.FilterItems(static item => true),
            () => source.AutoDisposeBatches(),
            () => source.CountByAction(),
            () => source.ToChangeSets()
        ];

        foreach (var action in sourceActions)
        {
            _ = action.Should().Throw<ArgumentNullException>()
                .WithParameterName(SourceParameterName);
        }

        Action observeNullScheduler = () => valid.ObserveOnScheduler(null!);
        Action transformNullSelector = () => valid.TransformItems<int, int>(null!);
        Action filterNullPredicate = () => valid.FilterItems(null!);

        _ = observeNullScheduler.Should().Throw<ArgumentNullException>()
            .WithParameterName("scheduler");
        _ = transformNullSelector.Should().Throw<ArgumentNullException>()
            .WithParameterName("selector");
        _ = filterNullPredicate.Should().Throw<ArgumentNullException>()
            .WithParameterName("predicate");
    }

    /// <summary>ToChange should map single notifications and ignore unsupported ones.</summary>
    [Test]
    public void ToChange_ShouldMapSingleNotifications()
    {
        CacheNotify<int> nullNotification = null!;

        _ = nullNotification.ToChange().Should().BeNull();
        _ = new CacheNotify<int>(CacheAction.Added, 1, CurrentIndex: 2).ToChange().Should().Be(
            Change<int>.CreateAdd(1, CoverageValueTwo));
        _ = new CacheNotify<int>(CacheAction.Removed, CoverageValueThree, CurrentIndex: 4).ToChange().Should().Be(
            Change<int>.CreateRemove(CoverageValueThree, CoverageValueFour));
        _ = new CacheNotify<int>(CacheAction.Updated, CoverageValueFive, CurrentIndex: 6, Previous: 4).ToChange().Should().Be(
            Change<int>.CreateUpdate(CoverageValueFive, CoverageValueFour, CoverageValueSix));
        _ = new CacheNotify<int>(CacheAction.Moved, CoverageValueSeven, CurrentIndex: 8, PreviousIndex: 9).ToChange().Should().Be(
            Change<int>.CreateMove(CoverageValueSeven, CoverageValueEight, CoverageValueNine));
        _ = new CacheNotify<int>(CacheAction.Refreshed, CoverageValueTen, CurrentIndex: 11).ToChange().Should().Be(
            Change<int>.CreateRefresh(CoverageValueTen, CoverageValueEleven));

        var clearChange = new CacheNotify<int>(CacheAction.Cleared, default).ToChange();
        _ = clearChange.Should().NotBeNull();
        _ = clearChange!.Value.Reason.Should().Be(ChangeReason.Clear);

        _ = new CacheNotify<int>(CacheAction.BatchAdded, default).ToChange().Should().BeNull();
        _ = new CacheNotify<string>(CacheAction.Added, default).ToChange().Should().BeNull();
    }

    /// <summary>ToChangeSets should expand batches, singles, and filter empty changes.</summary>
    [Test]
    public void ToChangeSets_ShouldExpandBatchesSinglesAndFilterEmptyChanges()
    {
        var addedBatch = CreateBatch(1, CoverageValueTwo);
        var removedBatch = CreateBatch(CoverageValueThree, CoverageValueFour);
        var clearedBatch = CreateBatch(CoverageValueFive);
        var refreshBatch = CreateBatch(CoverageValueSix);
        var emptyBatch = new PooledBatch<int>(ArrayPool<int>.Shared.Rent(1), 0);

        var notifications = new[]
        {
            new CacheNotify<int>(CacheAction.BatchAdded, default, addedBatch, CurrentIndex: 10),
            new CacheNotify<int>(CacheAction.BatchRemoved, default, removedBatch),
            new CacheNotify<int>(CacheAction.Cleared, default, clearedBatch),
            new CacheNotify<int>(CacheAction.BatchOperation, default, refreshBatch),
            new CacheNotify<int>(CacheAction.Cleared, default),
            new CacheNotify<int>(CacheAction.BatchAdded, default, emptyBatch),
            new CacheNotify<int>(CacheAction.Added, CoverageValueSeven, CurrentIndex: 20),
            new CacheNotify<int>(CacheAction.BatchRemoved, default)
        };

        var changeSets = Collect(ObservableMixins.ToEnumerable(notifications.ToObservable()
            .ToChangeSets()));

        _ = changeSets.Should().HaveCount(CoverageValueFive);
        _ = changeSets[0].Should().HaveCount(CoverageValueTwo);
        _ = Project(changeSets[0], static change => change.Reason).Should().AllBeEquivalentTo(ChangeReason.Add);
        _ = changeSets[0][0].CurrentIndex.Should().Be(CoverageValueTen);
        _ = Project(changeSets[1], static change => change.Reason).Should().AllBeEquivalentTo(ChangeReason.Remove);
        _ = changeSets[CoverageValueTwo].Should().ContainSingle()
            .Which.Reason.Should().Be(ChangeReason.Remove);
        _ = changeSets[CoverageValueThree].Should().ContainSingle()
            .Which.Reason.Should().Be(ChangeReason.Refresh);
        _ = changeSets[CoverageValueFour].Should().ContainSingle()
            .Which.Should().Be(Change<int>.CreateAdd(CoverageValueSeven, CoverageValueTwenty));

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

    /// <summary>ChangeSet should expose counts, spans, enumerators, and validation.</summary>
    [Test]
    public void ChangeSet_ShouldExposeCountsEnumeratorsAndValidation()
    {
        var changes = new[]
        {
            Change<int>.CreateAdd(1, 0),
            Change<int>.CreateRemove(CoverageValueTwo, 1),
            Change<int>.CreateUpdate(CoverageValueThree, CoverageValueTwo, CoverageValueTwo),
            Change<int>.CreateMove(CoverageValueFour, CoverageValueThree, 0)
        };

        using var set = new ChangeSet<int>(changes);
        using var single = new ChangeSet<int>(Change<int>.CreateRefresh(CoverageValueFive, CoverageValueFour));
        using var comparison = new ChangeSet<int>([.. changes]);
        var defaultSet = default(ChangeSet<int>);

        _ = set.Count.Should().Be(CoverageValueFour);
        _ = set.Adds.Should().Be(1);
        _ = set.Removes.Should().Be(1);
        _ = set.Updates.Should().Be(1);
        _ = set.Moves.Should().Be(1);
        _ = set[0].Should().Be(changes[0]);
        _ = single.Should().ContainSingle()
            .Which.Should().Be(Change<int>.CreateRefresh(CoverageValueFive, CoverageValueFour));
        _ = defaultSet.Adds.Should().Be(0);
        defaultSet.Dispose();
        _ = set.Equals(set).Should().BeTrue();
        _ = set.Equals(comparison).Should().BeFalse();
        _ = set.GetHashCode().Should().NotBe(0);

        _ = Project((IEnumerable<Change<int>>)set, static change => change.Current).Should().Equal(1, CoverageValueTwo, CoverageValueThree, CoverageValueFour);

        var enumerator = ((IEnumerable)set).GetEnumerator();
        _ = enumerator.MoveNext().Should().BeTrue();
        _ = enumerator.Current.Should().Be(changes[0]);
        enumerator.Reset();
        _ = enumerator.MoveNext().Should().BeTrue();
        _ = enumerator.Current.Should().Be(changes[0]);

        Action getNegative = () => _ = set[-1];
        Action getPastEnd = () => _ = set[set.Count];
        Action createNull = static () => _ = new ChangeSet<int>(null!);

        _ = getNegative.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("index");
        _ = getPastEnd.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("index");
        _ = createNull.Should().Throw<ArgumentNullException>()
            .WithParameterName("changes");

        using var spanSet = new ChangeSet<int>(changes.AsSpan());
        _ = spanSet.AsSpan().ToArray().Should().Equal(changes);

        _ = ChangeSet<int>.Empty.AsSpan().IsEmpty.Should().BeTrue();
    }

    /// <summary>Disposing one struct copy should not invalidate another copy.</summary>
    [Test]
    public void ChangeSet_DisposedCopy_ShouldRetainSharedValues()
    {
        var changeSet = new ChangeSet<string>(Change<string>.CreateAdd(UpdatedTextItem));
        var copy = changeSet;

        changeSet.Dispose();

        _ = copy.Should().ContainSingle()
            .Which.Current.Should().Be(UpdatedTextItem);
        copy.Dispose();
    }

    /// <summary>Array-pool clearing detection should include references nested inside value types.</summary>
    /// <returns>A task that completes when all assertions have run.</returns>
    [Test]
    public async Task ArrayPoolClearHelper_ShouldDetectNestedReferences()
    {
        await TUnit.Assertions.Assert.That(ArrayPoolClearHelper.IsReferenceOrContainsReferences<int>()).IsFalse();
        await TUnit.Assertions.Assert.That(ArrayPoolClearHelper.IsReferenceOrContainsReferences<string>()).IsTrue();
        await TUnit.Assertions.Assert.That(ArrayPoolClearHelper.IsReferenceOrContainsReferences<ValueWithReference>()).IsTrue();
    }

    /// <summary>PooledEditableListWrapper should synchronize all list operations.</summary>
    [Test]
    public void PooledEditableListWrapper_ShouldSynchronizeOperationsAndValidateMoves()
    {
        EditableListWrapperPool<string>.Clear();
        var list = new List<string> { "one", "two" };
        var observable = new ObservableCollection<string>(list);
        using var wrapper = new PooledEditableListWrapper<string>(list, observable);

        _ = wrapper.IsReadOnly.Should().BeFalse();
        _ = wrapper[1].Should().Be("two");
        wrapper[1] = "deux";
        wrapper.AddRange([ThirdTextItem, "four"]);
        wrapper.Insert(1, InsertedTextItem);
        wrapper.Move(0, CoverageValueTwo);
        wrapper.Move(CoverageValueTwo, CoverageValueTwo);

        _ = list.Should().Equal(InsertedTextItem, "deux", "one", ThirdTextItem, "four");
        _ = observable.Should().Equal(list);
        _ = wrapper.Contains(ThirdTextItem).Should().BeTrue();
        _ = wrapper.IndexOf(ThirdTextItem).Should().Be(CoverageValueThree);

        var copied = new string[wrapper.Count];
        wrapper.CopyTo(copied, 0);
        _ = copied.Should().Equal(list);
        _ = Collect(wrapper).Should().Equal(list);
        _ = CollectNonGeneric<string>(wrapper).Should().Equal(list);
        var wrapperEnumerator = ((IEnumerable)wrapper).GetEnumerator();
        _ = wrapperEnumerator.MoveNext().Should().BeTrue();
        _ = wrapperEnumerator.Current.Should().Be(InsertedTextItem);

        _ = wrapper.Remove("missing").Should().BeFalse();
        _ = wrapper.Remove("deux").Should().BeTrue();
        wrapper.RemoveAt(0);
        wrapper.Clear();

        _ = list.Should().BeEmpty();
        _ = observable.Should().BeEmpty();

        wrapper.Initialize(["a", "b"], null);
        _ = wrapper.Count.Should().Be(CoverageValueTwo);

        Action badOldIndex = () => wrapper.Move(-1, 0);
        Action badNewIndex = () => wrapper.Move(0, CoverageValueTwo);

        _ = badOldIndex.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("oldIndex");
        _ = badNewIndex.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("newIndex");

        EditableListWrapperPool<string>.Clear();
    }

    /// <summary>Returned pooled wrappers should reject future access and dispose idempotently.</summary>
    [Test]
    public void PooledEditableListWrapper_WhenReturned_ShouldRejectAccessAndDisposeIdempotently()
    {
        EditableListWrapperPool<int>.Clear();
        var wrapper = new PooledEditableListWrapper<int>([]);

        ((IResettable)wrapper).Reset();
        wrapper.Dispose();

        _ = wrapper.Count.Should().Be(0);
        Action useReturned = () => wrapper.Add(1);

        _ = useReturned.Should().Throw<ObjectDisposedException>();
    }

    /// <summary>ReactiveGroup should expose grouping data and forward collection change events.</summary>
    [Test]
    public void ReactiveGroup_ShouldExposeItemsAndForwardCollectionChanges()
    {
        var source = new ObservableCollection<string>(["one"]);
        var group = new ReactiveGroup<string, string>("letters", source);
        var events = new List<NotifyCollectionChangedEventArgs>();
        var collectionSenders = new List<object?>();
        var propertySenders = new List<object?>();
        var properties = new List<string?>();
        NotifyCollectionChangedEventHandler collectionHandler = (sender, args) =>
        {
            collectionSenders.Add(sender);
            events.Add(args);
        };
        System.ComponentModel.PropertyChangedEventHandler propertyHandler = (sender, args) =>
        {
            propertySenders.Add(sender);
            properties.Add(args.PropertyName);
        };
        group.CollectionChanged += collectionHandler;
        group.CollectionChanged += collectionHandler;
        group.PropertyChanged += propertyHandler;
        group.PropertyChanged += propertyHandler;

        source.Add("two");

        _ = group.Key.Should().Be("letters");
        _ = group.Count.Should().Be(CoverageValueTwo);
        _ = group.Items.Should().Equal("one", "two");
        _ = group.Should().Equal("one", "two");
        _ = CollectNonGeneric<string>(group).Should().Equal("one", "two");
        var groupEnumerator = ((IEnumerable)group).GetEnumerator();
        _ = groupEnumerator.MoveNext().Should().BeTrue();
        _ = groupEnumerator.Current.Should().Be("one");
        _ = events.Should().HaveCount(CoverageValueTwo);
        _ = Project(events, static args => args.Action).Should().Equal(
            NotifyCollectionChangedAction.Add,
            NotifyCollectionChangedAction.Add);
        _ = Project(collectionSenders, sender => ReferenceEquals(sender, group)).Should().Equal(true, true);
        _ = properties.Should().Equal(nameof(group.Count), nameof(group.Count), "Item[]", "Item[]");
        _ = Project(propertySenders, sender => ReferenceEquals(sender, group)).Should().Equal(true, true, true, true);

        group.CollectionChanged -= collectionHandler;
        group.PropertyChanged -= propertyHandler;
        source.Add("three");

        _ = events.Should().HaveCount(CoverageValueThree);
        _ = properties.Should().HaveCount(CoverageValueSix);

        group.CollectionChanged -= collectionHandler;
        group.PropertyChanged -= propertyHandler;
        source.Add("four");

        _ = events.Should().HaveCount(CoverageValueThree);
        _ = properties.Should().HaveCount(CoverageValueSix);

        var silentSource = new ObservableCollection<int>();
        _ = new ReactiveGroup<string, int>(NumbersGroupKey, silentSource);
        Action addWithoutSubscriber = () => silentSource.Add(1);

        _ = addWithoutSubscriber.Should().NotThrow();
    }

    /// <summary>SecondaryIndex should reject keys of the wrong type in MatchesKey.</summary>
    [Test]
    public void SecondaryIndex_MatchesKey_ShouldRejectWrongKeyType()
    {
        var index = new SecondaryIndex<Person, string>(static person => person.Department);
        var person = new Person(1, "Ada", "Engineering");

        _ = index.MatchesKey(person, "Engineering").Should().BeTrue();
        _ = index.MatchesKey(person, CoverageValueOneHundredTwentyThree).Should().BeFalse();
    }

    /// <summary>Internal grouping should expose its non-generic enumerator.</summary>
    [Test]
    public void ChangeGrouping_ShouldExposeNonGenericEnumerator()
    {
        var grouping = new ChangeGrouping<string, int>(NumbersGroupKey, [1, CoverageValueTwo]);
        var enumerator = ((IEnumerable)grouping).GetEnumerator();

        _ = grouping.Key.Should().Be(NumbersGroupKey);
        _ = enumerator.MoveNext().Should().BeTrue();
        _ = enumerator.Current.Should().Be(1);
        _ = CollectNonGeneric<int>(grouping).Should().Equal(1, CoverageValueTwo);
    }

    /// <summary>Internal observable factories should surface factory errors and event handler variants.</summary>
    [Test]
    public void InternalObservableFactories_ShouldCoverErrorAndEventBranches()
    {
        var factoryError = new InvalidOperationException("factory");
        var deferredObserver = new RecordingObserver<int>();
        using var deferredSubscription = Observable
            .Defer<int>(() => throw factoryError)
            .Subscribe(deferredObserver);

        _ = deferredObserver.Error.Should().BeSameAs(factoryError);

        var successValues = new List<int>();
        using var successSubscription = Observable
            .Defer(ObservableFactoryValues.ToObservable)
            .Subscribe(successValues.Add);

        _ = successValues.Should().Equal(1, CoverageValueTwo);

        var eventSource = new EventSource();
        var events = new List<EventPattern<EventArgs>>();
        using var eventSubscription = Observable
            .FromEventPattern<EventHandler<EventArgs>, EventArgs>(
                handler => eventSource.Raised += handler,
                handler => eventSource.Raised -= handler)
            .Subscribe(events.Add);

        eventSource.Raise();
        _ = events.Should().ContainSingle();

        Action unsupported = static () => Observable
            .FromEventPattern<Action, EventArgs>(static _ => { }, static _ => { })
            .Subscribe(new RecordingObserver<EventPattern<EventArgs>>());

        _ = unsupported.Should().Throw<NotSupportedException>();
    }

    /// <summary>Internal observable operators should cover error and completion branches.</summary>
    /// <returns>A task that completes when the asynchronous assertions finish.</returns>
    [Test]
    public async Task ObservableMixins_ShouldCoverErrorAndCompletionBranches()
    {
        var toEnumerableError = new InvalidOperationException("enumerable");
        var throwing = Signal.Create<int>(observer =>
        {
            observer.OnError(toEnumerableError);
            return ReactiveUI.Primitives.Disposables.Scope.Empty;
        });

        Action enumerate = () => _ = Collect(ObservableMixins.ToEnumerable(throwing));
        _ = enumerate.Should().Throw<InvalidOperationException>();

        using var bufferSource = new Signal<int>();
        var buffered = new RecordingObserver<IList<int>>();
        using var bufferSubscription = bufferSource.Buffer(TimeSpan.FromMilliseconds(1), Sequencer.Immediate).Subscribe(buffered);

        bufferSource.OnNext(1);
        bufferSource.OnCompleted();

        _ = Flatten(buffered.Values).Should().Contain(1);
        _ = buffered.Completed.Should().BeTrue();

        using var bufferErrorSource = new Signal<int>();
        var bufferErrorObserver = new RecordingObserver<IList<int>>();
        var bufferError = new InvalidOperationException("buffer");
        using var bufferErrorSubscription = bufferErrorSource.Buffer(TimeSpan.FromMilliseconds(1), Sequencer.Immediate).Subscribe(bufferErrorObserver);

        bufferErrorSource.OnError(bufferError);
        _ = bufferErrorObserver.Error.Should().BeSameAs(bufferError);

        VerifyBufferCompletionBranches();
        await VerifyThrottleBranches();
    }

    /// <summary>ReactiveList extension guards and default dynamic filters should be covered.</summary>
    [Test]
    public void ReactiveListExtensions_ShouldCoverGuardAndDefaultFilterBranches()
    {
        IObservable<ChangeSet<int>> nullChangeSets = null!;
        Action nullGroupSource = () => nullChangeSets.GroupByChanges(static item => item);
        Action nullGroupingSource = () => nullChangeSets.GroupingByChanges(static item => item);
        Action nullRefreshSource = static () => ReactiveListExtensions.AutoRefresh<NotifyItem>(null!, propertyName: null);
        var notifyItem = new NotifyItem(1);
        notifyItem.Raise(nameof(NotifyItem.Value));
        _ = notifyItem.Value.Should().Be(1);

        _ = nullGroupSource.Should().Throw<ArgumentNullException>().WithParameterName(SourceParameterName);
        _ = nullGroupingSource.Should().Throw<ArgumentNullException>().WithParameterName(SourceParameterName);
        _ = nullRefreshSource.Should().Throw<ArgumentNullException>().WithParameterName(SourceParameterName);

        using var changeSets = new Signal<ChangeSet<int>>();
        Action nullGroupSelector = () => changeSets.GroupByChanges<int, int>(null!);
        Action nullGroupingSelector = () => changeSets.GroupingByChanges<int, int>(null!);

        _ = nullGroupSelector.Should().Throw<ArgumentNullException>().WithParameterName("keySelector");
        _ = nullGroupingSelector.Should().Throw<ArgumentNullException>().WithParameterName("keySelector");

        using var stream = new Signal<CacheNotify<int>>();
        using var filters = new Signal<Func<int, bool>>();
        var received = new List<CacheNotify<int>>();
        using var subscription = stream.FilterDynamic(filters).Subscribe(received.Add);

        stream.OnNext(new(CacheAction.Added, CoverageValueTen));
        stream.OnNext(new(CacheAction.Removed, CoverageValueTwenty));

        _ = received.ConvertAll(static item => item.Item).Should().Equal(CoverageValueTen, CoverageValueTwenty);

        using var pairStream = new Signal<CacheNotify<KeyValuePair<int, string>>>();
        using var pairFilters = new Signal<Func<KeyValuePair<int, string>, bool>>();
        var pairReceived = new List<CacheNotify<KeyValuePair<int, string>>>();
        using var pairSubscription = pairStream.FilterDynamic(pairFilters).Subscribe(pairReceived.Add);

        pairStream.OnNext(new(CacheAction.Added, new(1, "one")));
        pairStream.OnNext(new(CacheAction.Removed, new(CoverageValueTwo, "two")));

        _ = pairReceived.ConvertAll(static item => item.Item.Key).Should().Equal(1, CoverageValueTwo);

        using var noMatchBatch = CreateBatch(1, CoverageValueTwo);
        var noMatchNotification = new CacheNotify<int>(CacheAction.BatchAdded, default, noMatchBatch);
        _ = ReactiveListExtensions.FilterBatchByPredicate(noMatchNotification, static item => item > CoverageValueTen).Should().BeNull();
        _ = ReactiveListExtensions.FilterBatch(noMatchNotification, [CoverageValueNinetyNine]).Should().BeNull();
    }

    /// <summary>GroupBy should propagate upstream errors to active groups and to the outer subscriber.</summary>
    [Test]
    public void GroupBy_ShouldPropagateErrorsToGroupsAndOuterSubscriber()
    {
        using var source = new Signal<ChangeSet<int>>();
        var groupErrors = new List<Exception>();
        var outerObserver = new RecordingObserver<IGroupedObservable<int, int>>();
        var upstreamError = new InvalidOperationException("group failure");

        using var subscription = ReactiveListExtensions
            .GroupByChanges(source, static value => value % CoverageValueTwo)
            .Subscribe(
                group =>
                {
                    _ = group.Key.Should().Be(1);
                    _ = group.Subscribe(static _ => { }, groupErrors.Add, static () => { });
                },
                outerObserver.OnError,
                outerObserver.OnCompleted);

        using var changes = new ChangeSet<int>(Change<int>.CreateAdd(1));
        source.OnNext(changes);
        source.OnError(upstreamError);

        _ = groupErrors.Should().ContainSingle()
            .Which.Should().BeSameAs(upstreamError);
        _ = outerObserver.Error.Should().BeSameAs(upstreamError);
    }

    /// <summary>SelectChanges should return the shared empty changeset when the input contains no changes.</summary>
    [Test]
    public void SelectChanges_ShouldReturnEmptyChangeSetForEmptyInput()
    {
        var source = new[] { ChangeSet<int>.Empty }.ToObservable();
        var results = Collect(ObservableMixins.ToEnumerable(
            ReactiveListExtensions.SelectChanges(
                source,
                (Func<int, string>)(static value => value.ToString()))));

        _ = results.Should().ContainSingle()
            .Which.Count.Should().Be(0);
    }

    /// <summary>The ReactiveUI.Primitives R3 bridge marker should be absent when the consumer does not reference R3.</summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Test]
    public async Task ReactivePrimitivesGeneratedBridgeAttribute_ShouldBeAbsentWithoutR3()
    {
        var attributeType = typeof(CP.Primitives.Collections.ReactiveList<int>).Assembly.GetType(
            "ReactiveUI.Primitives.R3Bridge.Generated.PrimitivesR3BridgeGeneratedAttribute");

        await TUnit.Assertions.Assert.That(attributeType).IsNull();
    }

    /// <summary>Provides CreateBatch.</summary>
    /// <param name="values">The values value.</param>
    /// <returns>The result.</returns>
    private static PooledBatch<int> CreateBatch(params int[] values)
    {
        var array = ArrayPool<int>.Shared.Rent(Math.Max(1, values.Length));
        Array.Copy(values, array, values.Length);
        return new(array, values.Length);
    }

    /// <summary>Provides CreateStringBatch.</summary>
    /// <param name="values">The values value.</param>
    /// <returns>The result.</returns>
    private static PooledBatch<string> CreateStringBatch(params string[] values)
    {
        var array = ArrayPool<string>.Shared.Rent(Math.Max(1, values.Length));
        Array.Copy(values, array, values.Length);
        return new(array, values.Length);
    }

    /// <summary>Materializes a sequence through explicit iteration.</summary>
    /// <typeparam name="T">The sequence item type.</typeparam>
    /// <param name="source">The sequence to materialize.</param>
    /// <returns>The materialized items.</returns>
    private static List<T> Collect<T>(IEnumerable<T> source) => new(source);

    /// <summary>Materializes a non-generic sequence through explicit iteration.</summary>
    /// <typeparam name="T">The expected sequence item type.</typeparam>
    /// <param name="source">The sequence to materialize.</param>
    /// <returns>The materialized items.</returns>
    private static List<T> CollectNonGeneric<T>(IEnumerable source)
    {
        var result = new List<T>();
        foreach (var item in source)
        {
            result.Add((T)item);
        }

        return result;
    }

    /// <summary>Projects a sequence through explicit iteration.</summary>
    /// <typeparam name="TSource">The source item type.</typeparam>
    /// <typeparam name="TResult">The result item type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="selector">The projection applied to each item.</param>
    /// <returns>The projected items.</returns>
    private static List<TResult> Project<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector)
    {
        var result = new List<TResult>();
        foreach (var item in source)
        {
            result.Add(selector(item));
        }

        return result;
    }

    /// <summary>Flattens nested sequences through explicit iteration.</summary>
    /// <typeparam name="T">The nested item type.</typeparam>
    /// <param name="source">The nested sequences.</param>
    /// <returns>The flattened items.</returns>
    private static List<T> Flatten<T>(IEnumerable<IEnumerable<T>> source)
    {
        var result = new List<T>();
        foreach (var sequence in source)
        {
            result.AddRange(sequence);
        }

        return result;
    }

    /// <summary>Exercises the completion, delayed flush, and post-stop buffer branches.</summary>
    private static void VerifyBufferCompletionBranches()
    {
        var manualBufferSequencer = new ManualSequencer();
        var completedBufferObserver = new RecordingObserver<IList<int>>();
        var completedThenValue = Signal.Create<int>(static observer =>
        {
            observer.OnNext(CoverageValueSeven);
            observer.OnCompleted();
            observer.OnNext(CoverageValueEight);
            return ReactiveUI.Primitives.Disposables.Scope.Empty;
        });

        using var completedBufferSubscription = completedThenValue
            .Buffer(TimeSpan.FromMilliseconds(1), manualBufferSequencer)
            .Subscribe(completedBufferObserver);

        _ = completedBufferObserver.Values.Should().ContainSingle()
            .Which.Should().Equal(CoverageValueSeven);
        _ = completedBufferObserver.Completed.Should().BeTrue();
        manualBufferSequencer.RunAll();

        using var emptyFlushSource = new Signal<int>();
        var duplicateBufferSequencer = new DuplicateSequencer();
        var emptyFlushObserver = new RecordingObserver<IList<int>>();
        using var emptyFlushSubscription = emptyFlushSource
            .Buffer(TimeSpan.FromMilliseconds(1), duplicateBufferSequencer)
            .Subscribe(emptyFlushObserver);

        emptyFlushSource.OnNext(CoverageValueEleven);
        duplicateBufferSequencer.RunAll();

        _ = emptyFlushObserver.Values.Should().ContainSingle()
            .Which.Should().Equal(CoverageValueEleven);

        using var postStopBufferSubscription = new ScriptedObservable<int>(static observer =>
            {
                observer.OnCompleted();
                observer.OnNext(CoverageValueNine);
            })
            .Buffer(TimeSpan.FromMilliseconds(1), new ManualSequencer())
            .Subscribe(new RecordingObserver<IList<int>>());
    }

    /// <summary>Exercises the regular, error, completion, and post-stop throttle branches.</summary>
    /// <returns>A task that completes when the asynchronous assertions finish.</returns>
    private static async Task VerifyThrottleBranches()
    {
        using var throttleSource = new Signal<int>();
        var throttled = new RecordingObserver<int>();
        using var throttleSubscription = throttleSource.Throttle(TimeSpan.FromMilliseconds(1), Sequencer.Immediate).Subscribe(throttled);

        throttleSource.OnNext(CoverageValueFortyTwo);
        throttleSource.OnCompleted();

        _ = throttled.Values.Should().Contain(CoverageValueFortyTwo);
        _ = throttled.Completed.Should().BeTrue();

        using var throttleErrorSource = new Signal<int>();
        var throttleErrorObserver = new RecordingObserver<int>();
        var throttleError = new InvalidOperationException("throttle");
        using var throttleErrorSubscription = throttleErrorSource.Throttle(TimeSpan.FromMilliseconds(1), Sequencer.Immediate).Subscribe(throttleErrorObserver);

        throttleErrorSource.OnError(throttleError);
        _ = throttleErrorObserver.Error.Should().BeSameAs(throttleError);

        await VerifyThrottleCompletionBranches();
    }

    /// <summary>Exercises the completion flush and post-stop throttle branches.</summary>
    /// <returns>A task that completes when the asynchronous assertions finish.</returns>
    private static async Task VerifyThrottleCompletionBranches()
    {
        var completedThenValue = Signal.Create<int>(static observer =>
        {
            observer.OnNext(CoverageValueSeven);
            observer.OnCompleted();
            observer.OnNext(CoverageValueEight);
            return ReactiveUI.Primitives.Disposables.Scope.Empty;
        });
        var manualThrottleSequencer = new ManualSequencer();
        var completedThrottleObserver = new RecordingObserver<int>();

        using var completedThrottleSubscription = completedThenValue
            .Throttle(TimeSpan.FromMilliseconds(1), manualThrottleSequencer)
            .Subscribe(completedThrottleObserver);

        await TUnit.Assertions.Assert.That(completedThrottleObserver.Values.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(completedThrottleObserver.Values[0]).IsEqualTo(CoverageValueSeven);
        await TUnit.Assertions.Assert.That(completedThrottleObserver.Completed).IsTrue();

        manualThrottleSequencer.RunAll();

        await TUnit.Assertions.Assert.That(completedThrottleObserver.Values.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(completedThrottleObserver.Values[0]).IsEqualTo(CoverageValueSeven);

        using var postStopThrottleSubscription = new ScriptedObservable<int>(static observer =>
            {
                observer.OnCompleted();
                observer.OnNext(CoverageValueNine);
            })
            .Throttle(TimeSpan.FromMilliseconds(1), new ManualSequencer())
            .Subscribe(new RecordingObserver<int>());
    }

    /// <summary>Represents a value type that contains a managed reference.</summary>
    /// <param name="Text">The managed text reference.</param>
    private readonly record struct ValueWithReference(string Text);

    /// <summary>Provides EventSource.</summary>
    private sealed class EventSource
    {
        /// <summary>Raised when the event source is triggered.</summary>
        public event EventHandler<EventArgs>? Raised;

        /// <summary>Provides Raise.</summary>
        public void Raise() => Raised?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Provides RecordingObserver.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    private sealed class RecordingObserver<T> : IObserver<T>
    {
        /// <summary>Gets Values.</summary>
        public List<T> Values { get; } = [];

        /// <summary>Gets Error.</summary>
        public Exception? Error { get; private set; }

        /// <summary>Gets Completed.</summary>
        public bool Completed { get; private set; }

        /// <summary>Provides OnCompleted.</summary>
        public void OnCompleted() => Completed = true;

        /// <summary>Provides OnError.</summary>
        /// <param name="error">The error value.</param>
        public void OnError(Exception error) => Error = error;

        /// <summary>Provides OnNext.</summary>
        /// <param name="value">The value.</param>
        public void OnNext(T value) => Values.Add(value);
    }

    /// <summary>Provides ScriptedObservable.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="script">The script value.</param>
    private sealed class ScriptedObservable<T>(Action<IObserver<T>> script) : IObservable<T>
    {
        /// <summary>Provides Subscribe.</summary>
        /// <param name="observer">The observer value.</param>
        /// <returns>The result.</returns>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            script(observer);
            return ReactiveUI.Primitives.Disposables.Scope.Empty;
        }
    }

    /// <summary>Provides ManualSequencer.</summary>
    private sealed class ManualSequencer : ISequencer
    {
        /// <summary>The scheduled work items awaiting execution.</summary>
        private readonly Queue<IWorkItem> _workItems = new();

        /// <summary>Gets Now.</summary>
        public DateTimeOffset Now => Sequencer.Immediate.Now;

        /// <summary>Gets Timestamp.</summary>
        public long Timestamp => Sequencer.Immediate.Timestamp;

        /// <summary>Provides Schedule.</summary>
        /// <param name="item">The item value.</param>
        public void Schedule(IWorkItem item) => _workItems.Enqueue(item);

        /// <summary>Provides Schedule.</summary>
        /// <param name="item">The item value.</param>
        /// <param name="dueTimestamp">The dueTimestamp value.</param>
        public void Schedule(IWorkItem item, long dueTimestamp) => _workItems.Enqueue(item);

        /// <summary>Provides RunAll.</summary>
        public void RunAll()
        {
            while (_workItems.Count > 0)
            {
                _workItems.Dequeue().Execute();
            }
        }
    }

    /// <summary>Provides DuplicateSequencer.</summary>
    private sealed class DuplicateSequencer : ISequencer
    {
        /// <summary>The scheduled work items awaiting execution.</summary>
        private readonly Queue<IWorkItem> _workItems = new();

        /// <summary>Gets Now.</summary>
        public DateTimeOffset Now => Sequencer.Immediate.Now;

        /// <summary>Gets Timestamp.</summary>
        public long Timestamp => Sequencer.Immediate.Timestamp;

        /// <summary>Provides Schedule.</summary>
        /// <param name="item">The item value.</param>
        public void Schedule(IWorkItem item)
        {
            _workItems.Enqueue(item);
            _workItems.Enqueue(item);
        }

        /// <summary>Provides Schedule.</summary>
        /// <param name="item">The item value.</param>
        /// <param name="dueTimestamp">The dueTimestamp value.</param>
        public void Schedule(IWorkItem item, long dueTimestamp) => Schedule(item);

        /// <summary>Provides RunAll.</summary>
        public void RunAll()
        {
            while (_workItems.Count > 0)
            {
                _workItems.Dequeue().Execute();
            }
        }
    }

    /// <summary>Provides Person.</summary>
    /// <param name="Id">The Id value.</param>
    /// <param name="Name">The Name value.</param>
    /// <param name="Department">The Department value.</param>
    private sealed record Person(int Id, string Name, string Department);

    /// <summary>Provides NotifyItem.</summary>
    /// <param name="Value">The Value.</param>
    private sealed record NotifyItem(int Value) : System.ComponentModel.INotifyPropertyChanged
    {
        /// <summary>Raised when a property value changes.</summary>
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Provides Raise.</summary>
        /// <param name="propertyName">The propertyName value.</param>
        public void Raise(string? propertyName = null) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
