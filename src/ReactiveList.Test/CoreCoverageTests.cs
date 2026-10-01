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
#if REACTIVELIST_REACTIVE
using CP.Reactive;
using CP.Reactive.Core;
using GroupedIntObservable = CP.Reactive.Core.IGroupedObservable<int, int>;
using Observable = CP.Reactive.Internal.Observable;
using ReactiveListType = CP.Reactive.Collections.ReactiveList<int>;
#else
using CP.Primitives;
using CP.Primitives.Core;
using ReactiveUI.Primitives.Concurrency;
using GroupedIntObservable = CP.Primitives.Core.IGroupedObservable<int, int>;
using ReactiveListType = CP.Primitives.Collections.ReactiveList<int>;
#endif
using TUnit.Assertions;
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
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CacheNotifyExtensions_ShouldFilterProjectAndCountNotifications()
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

        await Assert.That((await Assert.That(whereAction).HasSingleItem()).Item).IsEqualTo("one");
        await Assert.That(Project(whereAdded, static notification => notification.Action)).IsEquivalentTo([CacheAction.Added, CacheAction.BatchAdded], CollectionOrdering.Matching);
        await Assert.That(Project(whereRemoved, static notification => notification.Action)).IsEquivalentTo([CacheAction.Removed, CacheAction.BatchRemoved], CollectionOrdering.Matching);
        await Assert.That(selectedItems).IsEquivalentTo(["one", "six", UpdatedTextItem, MovedTextItem], CollectionOrdering.Matching);
        await Assert.That(allItems).IsEquivalentTo(["one", "two", "four", "six", RemovedBatchFirstItem, "ten", UpdatedTextItem, MovedTextItem], CollectionOrdering.Matching);
        await Assert.That(addedItems).IsEquivalentTo(["one", "two", "four"], CollectionOrdering.Matching);
        await Assert.That(removedItems).IsEquivalentTo(["six", RemovedBatchFirstItem, "ten"], CollectionOrdering.Matching);
        await Assert.That(updatedItems).IsEquivalentTo([UpdatedTextItem], CollectionOrdering.Matching);
        await Assert.That(movedItems).IsEquivalentTo([(MovedTextItem, CoverageValueTwo, CoverageValueFour)], CollectionOrdering.Matching);
        await Assert.That(cleared).HasSingleItem();
        await Assert.That(transformed).IsEquivalentTo(["ONE", "TWO", "FOUR", "SIX", "EIGHT", "TEN", "TWELVE", "FOURTEEN"], CollectionOrdering.Matching);
        await Assert.That(filtered).IsEquivalentTo(["one", "two", "four", MovedTextItem], CollectionOrdering.Matching);
        await Assert.That(Project(counts, static item => item.Count)).IsEquivalentTo([1, CoverageValueTwo, 1, CoverageValueTwo, 1, 1, 0, 0], CollectionOrdering.Matching);

        addedBatch.Dispose();
        removedBatch.Dispose();
    }

    /// <summary>Time and scheduler extensions should buffer, throttle, observe, and dispose batches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CacheNotifyExtensions_ShouldBufferThrottleObserveAndDisposeBatches()
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

        await Assert.That(await Assert.That(await Assert.That(buffered).HasSingleItem()).HasSingleItem()).IsSameReferenceAs(notification);
        await Assert.That(emptyBuffered).IsEmpty();
        await Assert.That(await Assert.That(throttled).HasSingleItem()).IsSameReferenceAs(notification);
        await Assert.That(await Assert.That(observed).HasSingleItem()).IsSameReferenceAs(notification);

        using var subject = new Signal<CacheNotify<int>>();
        var autoDisposed = new List<CacheNotify<int>>();
        using var subscription = subject.AutoDisposeBatches().Subscribe(autoDisposed.Add);
        var batch = CreateBatch(CoverageValueFortyTwo);

        subject.OnNext(new(CacheAction.BatchAdded, default, batch));
        Action disposeAgain = batch.Dispose;

        await Assert.That(autoDisposed).HasSingleItem();
        await Assert.That(disposeAgain).ThrowsNothing();
    }

    /// <summary>CacheNotifyExtensions should validate null arguments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CacheNotifyExtensions_ShouldValidateNullArguments()
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
            await Assert.That(action).Throws<ArgumentNullException>().WithParameterName(SourceParameterName);
        }

        Action observeNullScheduler = () => valid.ObserveOnScheduler(null!);
        Action transformNullSelector = () => valid.TransformItems<int, int>(null!);
        Action filterNullPredicate = () => valid.FilterItems(null!);

        await Assert.That(observeNullScheduler).Throws<ArgumentNullException>().WithParameterName("scheduler");
        await Assert.That(transformNullSelector).Throws<ArgumentNullException>().WithParameterName("selector");
        await Assert.That(filterNullPredicate).Throws<ArgumentNullException>().WithParameterName("predicate");
    }

    /// <summary>ToChange should map single notifications and ignore unsupported ones.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ToChange_ShouldMapSingleNotifications()
    {
        CacheNotify<int> nullNotification = null!;

        await Assert.That(nullNotification.ToChange()).IsNull();
        await Assert.That(new CacheNotify<int>(CacheAction.Added, 1, CurrentIndex: 2).ToChange()).IsEqualTo(Change<int>.CreateAdd(1, CoverageValueTwo));
        await Assert.That(new CacheNotify<int>(CacheAction.Removed, CoverageValueThree, CurrentIndex: 4).ToChange()).IsEqualTo(Change<int>.CreateRemove(CoverageValueThree, CoverageValueFour));
        await Assert.That(new CacheNotify<int>(CacheAction.Updated, CoverageValueFive, CurrentIndex: 6, Previous: 4).ToChange())
            .IsEqualTo(Change<int>.CreateUpdate(CoverageValueFive, CoverageValueFour, CoverageValueSix));
        await Assert.That(new CacheNotify<int>(CacheAction.Moved, CoverageValueSeven, CurrentIndex: 8, PreviousIndex: 9).ToChange())
            .IsEqualTo(Change<int>.CreateMove(CoverageValueSeven, CoverageValueEight, CoverageValueNine));
        await Assert.That(new CacheNotify<int>(CacheAction.Refreshed, CoverageValueTen, CurrentIndex: 11).ToChange()).IsEqualTo(Change<int>.CreateRefresh(CoverageValueTen, CoverageValueEleven));

        var clearChange = new CacheNotify<int>(CacheAction.Cleared, default).ToChange();
        await Assert.That(clearChange).IsNotNull();
        await Assert.That(clearChange!.Value.Reason).IsEqualTo(ChangeReason.Clear);

        await Assert.That(new CacheNotify<int>(CacheAction.BatchAdded, default).ToChange()).IsNull();
        await Assert.That(new CacheNotify<string>(CacheAction.Added, default).ToChange()).IsNull();
    }

    /// <summary>ToChangeSets should expand batches, singles, and filter empty changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ToChangeSets_ShouldExpandBatchesSinglesAndFilterEmptyChanges()
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

        await Assert.That(changeSets).Count().IsEqualTo(CoverageValueFive);
        await Assert.That(changeSets[0]).Count().IsEqualTo(CoverageValueTwo);
        await Assert.That(Project(changeSets[0], static change => change.Reason)).All(static item => item.Equals(ChangeReason.Add));
        await Assert.That(changeSets[0][0].CurrentIndex).IsEqualTo(CoverageValueTen);
        await Assert.That(Project(changeSets[1], static change => change.Reason)).All(static item => item.Equals(ChangeReason.Remove));
        await Assert.That((await Assert.That(changeSets[CoverageValueTwo]).HasSingleItem()).Reason).IsEqualTo(ChangeReason.Remove);
        await Assert.That((await Assert.That(changeSets[CoverageValueThree]).HasSingleItem()).Reason).IsEqualTo(ChangeReason.Refresh);
        await Assert.That(await Assert.That(changeSets[CoverageValueFour]).HasSingleItem()).IsEqualTo(Change<int>.CreateAdd(CoverageValueSeven, CoverageValueTwenty));

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
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChangeSet_ShouldExposeCountsEnumeratorsAndValidation()
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

        await Assert.That(set.Count).IsEqualTo(CoverageValueFour);
        await Assert.That(set.Adds).IsEqualTo(1);
        await Assert.That(set.Removes).IsEqualTo(1);
        await Assert.That(set.Updates).IsEqualTo(1);
        await Assert.That(set.Moves).IsEqualTo(1);
        await Assert.That(set[0]).IsEqualTo(changes[0]);
        await Assert.That(await Assert.That(single).HasSingleItem()).IsEqualTo(Change<int>.CreateRefresh(CoverageValueFive, CoverageValueFour));
        await Assert.That(defaultSet.Adds).IsEqualTo(0);
        defaultSet.Dispose();
        await Assert.That(set.Equals(set)).IsTrue();
        await Assert.That(set.Equals(comparison)).IsFalse();
        await Assert.That(set.GetHashCode()).IsNotEqualTo(0);

        await Assert.That(Project((IEnumerable<Change<int>>)set, static change => change.Current))
            .IsEquivalentTo([1, CoverageValueTwo, CoverageValueThree, CoverageValueFour], CollectionOrdering.Matching);

        var enumerator = ((IEnumerable)set).GetEnumerator();
        await Assert.That(enumerator.MoveNext()).IsTrue();
        await Assert.That(enumerator.Current).IsEqualTo(changes[0]);
        enumerator.Reset();
        await Assert.That(enumerator.MoveNext()).IsTrue();
        await Assert.That(enumerator.Current).IsEqualTo(changes[0]);

        Action getNegative = () => _ = set[-1];
        Action getPastEnd = () => _ = set[set.Count];
        Action createNull = static () => _ = new ChangeSet<int>(null!);

        await Assert.That(getNegative).Throws<ArgumentOutOfRangeException>().WithParameterName("index");
        await Assert.That(getPastEnd).Throws<ArgumentOutOfRangeException>().WithParameterName("index");
        await Assert.That(createNull).Throws<ArgumentNullException>().WithParameterName(nameof(changes));

        using var spanSet = new ChangeSet<int>(changes.AsSpan());
        await Assert.That(spanSet.AsSpan().ToArray()).IsEquivalentTo(changes, CollectionOrdering.Matching);

        await Assert.That(ChangeSet<int>.Empty.AsSpan().IsEmpty).IsTrue();
    }

    /// <summary>Disposing one struct copy should not invalidate another copy.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChangeSet_DisposedCopy_ShouldRetainSharedValues()
    {
        var changeSet = new ChangeSet<string>(Change<string>.CreateAdd(UpdatedTextItem));
        var copy = changeSet;

        changeSet.Dispose();

        await Assert.That((await Assert.That(copy).HasSingleItem()).Current).IsEqualTo(UpdatedTextItem);
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
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PooledEditableListWrapper_ShouldSynchronizeOperationsAndValidateMoves()
    {
        EditableListWrapperPool<string>.Clear();
        var list = new List<string> { "one", "two" };
        var observable = new ObservableCollection<string>(list);
        using var wrapper = new PooledEditableListWrapper<string>(list, observable);

        await Assert.That(wrapper.IsReadOnly).IsFalse();
        await Assert.That(wrapper[1]).IsEqualTo("two");
        wrapper[1] = "deux";
        wrapper.AddRange([ThirdTextItem, "four"]);
        wrapper.Insert(1, InsertedTextItem);
        wrapper.Move(0, CoverageValueTwo);
        wrapper.Move(CoverageValueTwo, CoverageValueTwo);

        await Assert.That(list).IsEquivalentTo([InsertedTextItem, "deux", "one", ThirdTextItem, "four"], CollectionOrdering.Matching);
        await Assert.That(observable).IsEquivalentTo(list, CollectionOrdering.Matching);
        await Assert.That(wrapper.Contains(ThirdTextItem)).IsTrue();
        await Assert.That(wrapper.IndexOf(ThirdTextItem)).IsEqualTo(CoverageValueThree);

        var copied = new string[wrapper.Count];
        wrapper.CopyTo(copied, 0);
        await Assert.That(copied).IsEquivalentTo(list, CollectionOrdering.Matching);
        await Assert.That(Collect(wrapper)).IsEquivalentTo(list, CollectionOrdering.Matching);
        await Assert.That(CollectNonGeneric<string>(wrapper)).IsEquivalentTo(list, CollectionOrdering.Matching);
        var wrapperEnumerator = ((IEnumerable)wrapper).GetEnumerator();
        await Assert.That(wrapperEnumerator.MoveNext()).IsTrue();
        await Assert.That(wrapperEnumerator.Current).IsEqualTo(InsertedTextItem);

        await Assert.That(wrapper.Remove("missing")).IsFalse();
        await Assert.That(wrapper.Remove("deux")).IsTrue();
        wrapper.RemoveAt(0);
        wrapper.Clear();

        await Assert.That(list).IsEmpty();
        await Assert.That(observable).IsEmpty();

        wrapper.Initialize(["a", "b"], null);
        await Assert.That(wrapper.Count).IsEqualTo(CoverageValueTwo);

        Action badOldIndex = () => wrapper.Move(-1, 0);
        Action badNewIndex = () => wrapper.Move(0, CoverageValueTwo);

        await Assert.That(badOldIndex).Throws<ArgumentOutOfRangeException>().WithParameterName("oldIndex");
        await Assert.That(badNewIndex).Throws<ArgumentOutOfRangeException>().WithParameterName("newIndex");

        EditableListWrapperPool<string>.Clear();
    }

    /// <summary>Returned pooled wrappers should reject future access and dispose idempotently.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PooledEditableListWrapper_WhenReturned_ShouldRejectAccessAndDisposeIdempotently()
    {
        EditableListWrapperPool<int>.Clear();
        var wrapper = new PooledEditableListWrapper<int>([]);

        ((IResettable)wrapper).Reset();
        wrapper.Dispose();

        await Assert.That(wrapper.Count).IsEqualTo(0);
        Action useReturned = () => wrapper.Add(1);

        await Assert.That(useReturned).Throws<ObjectDisposedException>();
    }

    /// <summary>ReactiveGroup should expose grouping data and forward collection change events.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReactiveGroup_ShouldExposeItemsAndForwardCollectionChanges()
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

        await Assert.That(group.Key).IsEqualTo("letters");
        await Assert.That(group.Count).IsEqualTo(CoverageValueTwo);
        await Assert.That(group.Items).IsEquivalentTo(["one", "two"], CollectionOrdering.Matching);
        await Assert.That(group).IsEquivalentTo(["one", "two"], CollectionOrdering.Matching);
        await Assert.That(CollectNonGeneric<string>(group)).IsEquivalentTo(["one", "two"], CollectionOrdering.Matching);
        var groupEnumerator = ((IEnumerable)group).GetEnumerator();
        await Assert.That(groupEnumerator.MoveNext()).IsTrue();
        await Assert.That(groupEnumerator.Current).IsEqualTo("one");
        await Assert.That(events).Count().IsEqualTo(CoverageValueTwo);
        await Assert.That(Project(events, static args => args.Action)).IsEquivalentTo([NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add], CollectionOrdering.Matching);
        await Assert.That(Project(collectionSenders, sender => ReferenceEquals(sender, group))).IsEquivalentTo([true, true], CollectionOrdering.Matching);
        await Assert.That(properties).IsEquivalentTo(new string?[] { nameof(group.Count), nameof(group.Count), "Item[]", "Item[]" }, CollectionOrdering.Matching);
        await Assert.That(Project(propertySenders, sender => ReferenceEquals(sender, group))).IsEquivalentTo([true, true, true, true], CollectionOrdering.Matching);

        group.CollectionChanged -= collectionHandler;
        group.PropertyChanged -= propertyHandler;
        source.Add("three");

        await Assert.That(events).Count().IsEqualTo(CoverageValueThree);
        await Assert.That(properties).Count().IsEqualTo(CoverageValueSix);

        group.CollectionChanged -= collectionHandler;
        group.PropertyChanged -= propertyHandler;
        source.Add("four");

        await Assert.That(events).Count().IsEqualTo(CoverageValueThree);
        await Assert.That(properties).Count().IsEqualTo(CoverageValueSix);

        var silentSource = new ObservableCollection<int>();
        _ = new ReactiveGroup<string, int>(NumbersGroupKey, silentSource);
        Action addWithoutSubscriber = () => silentSource.Add(1);

        await Assert.That(addWithoutSubscriber).ThrowsNothing();
    }

    /// <summary>SecondaryIndex should reject keys of the wrong type in MatchesKey.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SecondaryIndex_MatchesKey_ShouldRejectWrongKeyType()
    {
        var index = new SecondaryIndex<Person, string>(static person => person.Department);
        var person = new Person(1, "Ada", "Engineering");

        await Assert.That(index.MatchesKey(person, "Engineering")).IsTrue();
        await Assert.That(index.MatchesKey(person, CoverageValueOneHundredTwentyThree)).IsFalse();
    }

    /// <summary>Internal grouping should expose its non-generic enumerator.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChangeGrouping_ShouldExposeNonGenericEnumerator()
    {
        var grouping = new ChangeGrouping<string, int>(NumbersGroupKey, [1, CoverageValueTwo]);
        var enumerator = ((IEnumerable)grouping).GetEnumerator();

        await Assert.That(grouping.Key).IsEqualTo(NumbersGroupKey);
        await Assert.That(enumerator.MoveNext()).IsTrue();
        await Assert.That(enumerator.Current).IsEqualTo(1);
        await Assert.That(CollectNonGeneric<int>(grouping)).IsEquivalentTo([1, CoverageValueTwo], CollectionOrdering.Matching);
    }

    /// <summary>Internal observable factories should surface factory errors and event handler variants.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InternalObservableFactories_ShouldCoverErrorAndEventBranches()
    {
        var factoryError = new InvalidOperationException("factory");
        var deferredObserver = new RecordingObserver<int>();
        using var deferredSubscription = Observable
            .Defer<int>(() => throw factoryError)
            .Subscribe(deferredObserver);

        await Assert.That(deferredObserver.Error).IsSameReferenceAs(factoryError);

        var successValues = new List<int>();
        using var successSubscription = Observable
            .Defer(ObservableFactoryValues.ToObservable)
            .Subscribe(successValues.Add);

        await Assert.That(successValues).IsEquivalentTo([1, CoverageValueTwo], CollectionOrdering.Matching);

        var eventSource = new EventSource();
        var events = new List<EventPattern<EventArgs>>();
        using var eventSubscription = Observable
            .FromEventPattern<EventHandler<EventArgs>, EventArgs>(
                handler => eventSource.Raised += handler,
                handler => eventSource.Raised -= handler)
            .Subscribe(events.Add);

        eventSource.Raise();
        await Assert.That(events).HasSingleItem();

        Action unsupported = static () => Observable
            .FromEventPattern<Action, EventArgs>(static _ => { }, static _ => { })
            .Subscribe(new RecordingObserver<EventPattern<EventArgs>>());

        await Assert.That(unsupported).Throws<NotSupportedException>();
    }

    /// <summary>Internal observable operators should cover error and completion branches.</summary>
    /// <returns>A task that completes when the asynchronous assertions finish.</returns>
    [Test]
    public async Task ObservableMixins_ShouldCoverErrorAndCompletionBranches()
    {
        var toEnumerableError = new InvalidOperationException("enumerable");
#if REACTIVELIST_REACTIVE
        var throwing = System.Reactive.Linq.Observable.Create<int>(observer =>
#else
        var throwing = Signal.Create<int>(observer =>
#endif
        {
            observer.OnError(toEnumerableError);
            return ReactiveUI.Primitives.Disposables.Scope.Empty;
        });

        Action enumerate = () => _ = Collect(ObservableMixins.ToEnumerable(throwing));
        await Assert.That(enumerate).Throws<InvalidOperationException>();

#if !REACTIVELIST_REACTIVE
        using var bufferSource = new Signal<int>();
        var buffered = new RecordingObserver<IList<int>>();
        using var bufferSubscription = bufferSource.Buffer(TimeSpan.FromMilliseconds(1), Sequencer.Immediate).Subscribe(buffered);

        bufferSource.OnNext(1);
        bufferSource.OnCompleted();

        await Assert.That(Flatten(buffered.Values)).Contains(1);
        await Assert.That(buffered.Completed).IsTrue();

        using var bufferErrorSource = new Signal<int>();
        var bufferErrorObserver = new RecordingObserver<IList<int>>();
        var bufferError = new InvalidOperationException("buffer");
        using var bufferErrorSubscription = bufferErrorSource.Buffer(TimeSpan.FromMilliseconds(1), Sequencer.Immediate).Subscribe(bufferErrorObserver);

        bufferErrorSource.OnError(bufferError);
        await Assert.That(bufferErrorObserver.Error).IsSameReferenceAs(bufferError);

        await VerifyBufferCompletionBranches();
        await VerifyThrottleBranches();
#endif
    }

    /// <summary>ReactiveList extension guards and default dynamic filters should be covered.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReactiveListExtensions_ShouldCoverGuardAndDefaultFilterBranches()
    {
        IObservable<ChangeSet<int>> nullChangeSets = null!;
        Action nullGroupSource = () => nullChangeSets.GroupByChanges(static item => item);
        Action nullGroupingSource = () => nullChangeSets.GroupingByChanges(static item => item);
        Action nullRefreshSource = static () => ReactiveListExtensions.AutoRefresh<NotifyItem>(null!, propertyName: null);
        var notifyItem = new NotifyItem(1);
        notifyItem.Raise(nameof(NotifyItem.Value));
        await Assert.That(notifyItem.Value).IsEqualTo(1);

        await Assert.That(nullGroupSource).Throws<ArgumentNullException>().WithParameterName(SourceParameterName);
        await Assert.That(nullGroupingSource).Throws<ArgumentNullException>().WithParameterName(SourceParameterName);
        await Assert.That(nullRefreshSource).Throws<ArgumentNullException>().WithParameterName(SourceParameterName);

        using var changeSets = new Signal<ChangeSet<int>>();
        Action nullGroupSelector = () => changeSets.GroupByChanges<int, int>(null!);
        Action nullGroupingSelector = () => changeSets.GroupingByChanges<int, int>(null!);

        await Assert.That(nullGroupSelector).Throws<ArgumentNullException>().WithParameterName("keySelector");
        await Assert.That(nullGroupingSelector).Throws<ArgumentNullException>().WithParameterName("keySelector");

        using var stream = new Signal<CacheNotify<int>>();
        using var filters = new Signal<Func<int, bool>>();
        var received = new List<CacheNotify<int>>();
        using var subscription = stream.FilterDynamic(filters).Subscribe(received.Add);

        stream.OnNext(new(CacheAction.Added, CoverageValueTen));
        stream.OnNext(new(CacheAction.Removed, CoverageValueTwenty));

        await Assert.That(received.ConvertAll(static item => item.Item)).IsEquivalentTo([CoverageValueTen, CoverageValueTwenty], CollectionOrdering.Matching);

        using var pairStream = new Signal<CacheNotify<KeyValuePair<int, string>>>();
        using var pairFilters = new Signal<Func<KeyValuePair<int, string>, bool>>();
        var pairReceived = new List<CacheNotify<KeyValuePair<int, string>>>();
        using var pairSubscription = pairStream.FilterDynamic(pairFilters).Subscribe(pairReceived.Add);

        pairStream.OnNext(new(CacheAction.Added, new(1, "one")));
        pairStream.OnNext(new(CacheAction.Removed, new(CoverageValueTwo, "two")));

        await Assert.That(pairReceived.ConvertAll(static item => item.Item.Key)).IsEquivalentTo([1, CoverageValueTwo], CollectionOrdering.Matching);

        using var noMatchBatch = CreateBatch(1, CoverageValueTwo);
        var noMatchNotification = new CacheNotify<int>(CacheAction.BatchAdded, default, noMatchBatch);
        await Assert.That(ReactiveListExtensions.FilterBatchByPredicate(noMatchNotification, static item => item > CoverageValueTen)).IsNull();
        await Assert.That(ReactiveListExtensions.FilterBatch(noMatchNotification, [CoverageValueNinetyNine])).IsNull();
    }

    /// <summary>GroupBy should propagate upstream errors to active groups and to the outer subscriber.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GroupBy_ShouldPropagateErrorsToGroupsAndOuterSubscriber()
    {
        using var source = new Signal<ChangeSet<int>>();
        var groupErrors = new List<Exception>();
        var outerObserver = new RecordingObserver<GroupedIntObservable>();
        var upstreamError = new InvalidOperationException("group failure");
        var groupKeys = new List<int>();

        using var subscription = ReactiveListExtensions
            .GroupByChanges(source, static value => value % CoverageValueTwo)
            .Subscribe(
                group =>
                {
                    groupKeys.Add(group.Key);
                    _ = group.Subscribe(static _ => { }, groupErrors.Add, static () => { });
                },
                outerObserver.OnError,
                outerObserver.OnCompleted);

        using var changes = new ChangeSet<int>(Change<int>.CreateAdd(1));
        source.OnNext(changes);
        source.OnError(upstreamError);

        await Assert.That(groupKeys).IsEquivalentTo([1], CollectionOrdering.Matching);
        await Assert.That(await Assert.That(groupErrors).HasSingleItem()).IsSameReferenceAs(upstreamError);
        await Assert.That(outerObserver.Error).IsSameReferenceAs(upstreamError);
    }

    /// <summary>SelectChanges should return the shared empty changeset when the input contains no changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SelectChanges_ShouldReturnEmptyChangeSetForEmptyInput()
    {
        var source = new[] { ChangeSet<int>.Empty }.ToObservable();
        var results = Collect(ObservableMixins.ToEnumerable(
            ReactiveListExtensions.SelectChanges(
                source,
                (Func<int, string>)(static value => value.ToString()))));

        await Assert.That((await Assert.That(results).HasSingleItem()).Count).IsEqualTo(0);
    }

    /// <summary>The ReactiveUI.Primitives R3 bridge marker should be absent when the consumer does not reference R3.</summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Test]
    public async Task ReactivePrimitivesGeneratedBridgeAttribute_ShouldBeAbsentWithoutR3()
    {
        var attributeType = typeof(ReactiveListType).Assembly.GetType(
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

#if !REACTIVELIST_REACTIVE
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
    /// <returns>A task representing the asynchronous verification.</returns>
    private static async Task VerifyBufferCompletionBranches()
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

        await Assert.That(await Assert.That(completedBufferObserver.Values).HasSingleItem()).IsEquivalentTo([CoverageValueSeven], CollectionOrdering.Matching);
        await Assert.That(completedBufferObserver.Completed).IsTrue();
        manualBufferSequencer.RunAll();

        using var emptyFlushSource = new Signal<int>();
        var duplicateBufferSequencer = new DuplicateSequencer();
        var emptyFlushObserver = new RecordingObserver<IList<int>>();
        using var emptyFlushSubscription = emptyFlushSource
            .Buffer(TimeSpan.FromMilliseconds(1), duplicateBufferSequencer)
            .Subscribe(emptyFlushObserver);

        emptyFlushSource.OnNext(CoverageValueEleven);
        duplicateBufferSequencer.RunAll();

        await Assert.That(await Assert.That(emptyFlushObserver.Values).HasSingleItem()).IsEquivalentTo([CoverageValueEleven], CollectionOrdering.Matching);

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

        await Assert.That(throttled.Values).Contains(CoverageValueFortyTwo);
        await Assert.That(throttled.Completed).IsTrue();

        using var throttleErrorSource = new Signal<int>();
        var throttleErrorObserver = new RecordingObserver<int>();
        var throttleError = new InvalidOperationException("throttle");
        using var throttleErrorSubscription = throttleErrorSource.Throttle(TimeSpan.FromMilliseconds(1), Sequencer.Immediate).Subscribe(throttleErrorObserver);

        throttleErrorSource.OnError(throttleError);
        await Assert.That(throttleErrorObserver.Error).IsSameReferenceAs(throttleError);

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
#endif

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

#if !REACTIVELIST_REACTIVE
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
#endif

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
