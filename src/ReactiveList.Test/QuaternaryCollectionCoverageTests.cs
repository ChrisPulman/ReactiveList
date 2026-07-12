// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using FluentAssertions;
using ReactiveUI.Primitives.Signals;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Covers quaternary list, dictionary, and base notification paths that are not reached by the public API tests.</summary>
public class QuaternaryCollectionCoverageTests
{
    private const int CollectionValueTwo = 2;

    private const int CollectionValueThree = 3;

    private const int CollectionValueFour = 4;

    private const int CollectionValueFive = 5;

    private const int CollectionValueSix = 6;

    private const int CollectionValueSeven = 7;

    private const int CollectionValueEight = 8;

    private const int ModuloBucketCount = 10;

    private const int CollectionValueEleven = 11;

    private const int ExpectedRemainingItems = 40;

    private const int ReplacementValue = 42;

    private const int ProcessorPollMilliseconds = 50;

    private const int MissingCollectionValue = 99;

    private const int FinalBatchSize = 100;

    private const int ExpectedParityItems = 150;

    private const int ParallelBatchSize = 300;

    private const int FinalBatchStart = 600;

    private const int MinimumRetainedItems = 700;

    private const int MissingKeyOrIndex = 999;

    private const int RemovedInitialKeys = 1060;

    private const int LastRetainedKey = 1099;

    private const int LargeBatchSize = 1100;

    private const int FirstRetainedKey = 1101;

    private const int DefinitelyMissingValue = 999_999;

    private const string ParityIndexName = "Parity";

    private const string LengthIndexName = "Length";

    private const string ThreeText = "three";

    private static readonly KeyValuePair<int, string>[] ReplacementPairs = [new(2, "TWO")];

    private static readonly int[] MissingKeys = [999];

    private static readonly int[] ReflectedAddRangeItems = [5, 6];

    private static readonly List<int> EmptyIntList = [];

    private static readonly List<KeyValuePair<int, string>> EmptyPairs = [];

    /// <summary>Provides a rename-safe marker for a deliberately absent secondary index.</summary>
    private enum Missing
    {
        /// <summary>Represents the absent index name.</summary>
        Marker
    }

    /// <summary>Verifies QuaternaryList empty, enumerable, list, and secondary-index batch paths.</summary>
    [Test]
    public void QuaternaryList_BatchOverloads_ShouldMaintainItemsIndexesAndVersion()
    {
        using var list = new QuaternaryList<int>();
        var initialVersion = list.Version;

        list.AddRange([]);
        list.Count.Should().Be(0);
        list.Version.Should().Be(initialVersion);

        list.AddIndex(ParityIndexName, item => item % CollectionValueTwo);
        list.AddRange(Yield(0, 1, CollectionValueTwo, CollectionValueThree, CollectionValueFour));
        list.RemoveRange(Yield(1, CollectionValueThree));
        list.AddRange([ModuloBucketCount, CollectionValueEleven]);
        list.AddRange(EmptyIntList);
        list.AddRange([]);
        list.RemoveRange([]);
        list.RemoveRange(EmptyIntList);
        list.RemoveRange([]);
        list.RemoveRange([CollectionValueTwo]);

        list.Count.Should().Be(CollectionValueFour);
        list.Should().BeEquivalentTo([0, CollectionValueFour, ModuloBucketCount, CollectionValueEleven]);
        list.GetItemsBySecondaryIndex(ParityIndexName, 0).Should().BeEquivalentTo([0, CollectionValueFour, ModuloBucketCount]);
        list.GetItemsBySecondaryIndex(ParityIndexName, 1).Should().ContainSingle().Which.Should().Be(CollectionValueEleven);
        list.Version.Should().BeGreaterThan(initialVersion);
    }

    /// <summary>Verifies QuaternaryList parallel array and list paths for large batch adds and removals.</summary>
    [Test]
    public void QuaternaryList_LargeBatchOverloads_ShouldUseParallelPaths()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod10", item => item % ModuloBucketCount);

        var firstBatch = Enumerable.Range(0, LargeBatchSize).ToArray();
        list.AddRange(firstBatch);
        list.RemoveRange(firstBatch.Where(item => item % CollectionValueThree == 0).ToArray());

        list.Count.Should().BeGreaterThan(MinimumRetainedItems);
        list.Contains(0).Should().BeFalse();
        list.Contains(1).Should().BeTrue();

        var secondBatch = Enumerable.Range(LargeBatchSize, LargeBatchSize).ToList();
        list.AddRange(secondBatch);
        list.RemoveRange(secondBatch.Where(item => item % CollectionValueTwo == 0).ToList());

        list.Contains(LargeBatchSize).Should().BeFalse();
        list.Contains(FirstRetainedKey).Should().BeTrue();
        list.GetItemsBySecondaryIndex("Mod10", 1).Should().Contain(FirstRetainedKey);

        var countBeforeMissingRemove = list.Count;
        list.RemoveRange([DefinitelyMissingValue]);
        list.Count.Should().Be(countBeforeMissingRemove);
    }

    /// <summary>Verifies QuaternaryList parallel paths when all items land in one shard, plus RemoveMany buffer growth.</summary>
    [Test]
    public void QuaternaryList_SingleShardParallelBatchesAndRemoveManyGrowth_ShouldMaintainIndexes()
    {
        using var list = new QuaternaryList<ConstantShardItem>();
        list.AddIndex(ParityIndexName, static item => item.Id % CollectionValueTwo);

        var arrayBatch = Enumerable.Range(0, ParallelBatchSize).Select(static id => new ConstantShardItem(id)).ToArray();
        list.AddRange(arrayBatch);
        list.GetItemsBySecondaryIndex(ParityIndexName, 0).Should().HaveCount(ExpectedParityItems);
        list.RemoveRange(arrayBatch);
        list.Count.Should().Be(0);

        var listBatch = Enumerable.Range(ParallelBatchSize, ParallelBatchSize).Select(static id => new ConstantShardItem(id)).ToList();
        list.AddRange(listBatch);
        list.GetItemsBySecondaryIndex(ParityIndexName, 1).Should().HaveCount(ExpectedParityItems);
        list.RemoveRange(listBatch);
        list.Count.Should().Be(0);

        list.AddRange(Enumerable.Range(FinalBatchStart, FinalBatchSize).Select(static id => new ConstantShardItem(id)).ToArray());
        list.RemoveMany(static _ => true).Should().Be(FinalBatchSize);
        list.Count.Should().Be(0);
        list.GetItemsBySecondaryIndex(ParityIndexName, 0).Should().BeEmpty();
    }

    /// <summary>Verifies QuaternaryList edit wrapper members and reflected members not exposed by ICollection.</summary>
    [Test]
    public void QuaternaryList_EditWrapper_ShouldExposeCollectionMembers()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex(ParityIndexName, item => item % CollectionValueTwo);
        list.AddRange([1, CollectionValueTwo, CollectionValueThree]);

        list.Edit(editor =>
        {
            editor.Count.Should().Be(CollectionValueThree);
            editor.IsReadOnly.Should().BeFalse();
            editor.Contains(CollectionValueTwo).Should().BeTrue();

            var copy = new int[3];
            editor.CopyTo(copy, 0);
            copy.Should().BeEquivalentTo([1, CollectionValueTwo, CollectionValueThree]);
            editor.Should().BeEquivalentTo([1, CollectionValueTwo, CollectionValueThree]);

            editor.Remove(1).Should().BeTrue();
            editor.Remove(MissingCollectionValue).Should().BeFalse();
            editor.Add(CollectionValueFour);

            var wrapperType = editor.GetType();
            var addRangeMethod = wrapperType.GetMethod("AddRange") ?? throw new MissingMethodException(wrapperType.FullName, "AddRange");
            var itemProperty = wrapperType.GetProperty("Item") ?? throw new MissingMemberException(wrapperType.FullName, "Item");
            addRangeMethod.Invoke(editor, [ReflectedAddRangeItems]);
            itemProperty.GetValue(editor, [0]).Should().NotBeNull();

            var editorEnumerator = editor.GetEnumerator();
            while (editorEnumerator.MoveNext())
            {
                _ = editorEnumerator.Current;
            }

            editorEnumerator.MoveNext().Should().BeFalse();
            ((IEnumerable)editor).GetEnumerator().MoveNext().Should().BeTrue();

            Action badIndex = () => itemProperty.GetValue(editor, [MissingKeyOrIndex]);
            Action replaceByIndex = () => itemProperty.SetValue(editor, CollectionValueSeven, [0]);

            badIndex.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentOutOfRangeException>();
            replaceByIndex.Should().Throw<TargetInvocationException>().WithInnerException<NotSupportedException>();
        });

        list.Should().BeEquivalentTo([CollectionValueTwo, CollectionValueThree, CollectionValueFour, CollectionValueFive, CollectionValueSix]);
        list.GetItemsBySecondaryIndex(ParityIndexName, 0).Should().BeEquivalentTo([CollectionValueTwo, CollectionValueFour, CollectionValueSix]);

        using var noIndexList = new QuaternaryList<int>();
        noIndexList.AddRange([1, CollectionValueTwo]);
        noIndexList.Edit(editor => editor.Remove(1).Should().BeTrue());
        noIndexList.Should().ContainSingle().Which.Should().Be(CollectionValueTwo);
    }

    /// <summary>Verifies QuaternaryList snapshot and index guard paths.</summary>
    [Test]
    public void QuaternaryList_SnapshotAndInvalidIndexes_ShouldBehaveAsExpected()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([0, CollectionValueFour, CollectionValueEight, 1]);

        list.Snapshot().Should().BeEquivalentTo(list.ToArray());
        using var enumerator = ((IEnumerable<int>)list).GetEnumerator();
        while (enumerator.MoveNext())
        {
            _ = enumerator.Current;
        }

        enumerator.MoveNext().Should().BeFalse();

        var nonGenericEnumerator = ((IEnumerable)list).GetEnumerator();
        while (nonGenericEnumerator.MoveNext())
        {
            _ = nonGenericEnumerator.Current;
        }

        nonGenericEnumerator.MoveNext().Should().BeFalse();

        Action negativeIndex = () => _ = list[-1];
        Action tooHighIndex = () => _ = list[MissingCollectionValue];
        Action setter = () => list[0] = ReplacementValue;

        negativeIndex.Should().Throw<ArgumentOutOfRangeException>();
        tooHighIndex.Should().Throw<ArgumentOutOfRangeException>();
        setter.Should().Throw<NotSupportedException>();

        list.Dispose();
        list.Dispose();
    }

    /// <summary>Verifies QuaternaryBase dispatches legacy collection changes through a captured synchronization context.</summary>
    [Test]
    public void QuaternaryBase_CollectionChanged_ShouldUseCapturedSynchronizationContext()
    {
        var previousContext = SynchronizationContext.Current;
        var context = new ImmediateSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(context);

        try
        {
            using var list = new QuaternaryList<int>();
            using var reset = new ManualResetEventSlim(false);
            NotifyCollectionChangedAction? action = null;

            list.CollectionChanged += (_, args) =>
            {
                action = args.Action;
                reset.Set();
            };

            list.Add(ReplacementValue);

            reset.Wait(TimeSpan.FromSeconds(CollectionValueTwo)).Should().BeTrue();
            context.PostCount.Should().BeGreaterThan(0);
            action.Should().Be(NotifyCollectionChangedAction.Reset);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    /// <summary>Verifies QuaternaryDictionary empty, enumerable, list, secondary-index, and view creation paths.</summary>
    [Test]
    public void QuaternaryDictionary_BatchOverloadsAndViews_ShouldMaintainIndexes()
    {
        using var dictionary = new QuaternaryDictionary<int, string>();
        var initialVersion = dictionary.Version;

        dictionary.AddRange([]);
        dictionary.Version.Should().Be(initialVersion);

        dictionary.AddRange(Yield(
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(CollectionValueTwo, "two"),
            new KeyValuePair<int, string>(CollectionValueThree, ThreeText)));
        dictionary.AddValueIndex(LengthIndexName, value => value.Length);
        dictionary.AddRange(EmptyPairs);

        using var view = dictionary.CreateViewBySecondaryIndex(LengthIndexName, CollectionValueThree, Sequencer.Immediate, throttleMs: 1);
        view.Count.Should().Be(CollectionValueTwo);

        dictionary.AddRange([
            new(CollectionValueFour, "four"),
            new(CollectionValueFive, "five")
        ]);
        dictionary.Add(new KeyValuePair<int, string>(CollectionValueSix, "six"));
        dictionary.AddRange([]);

        dictionary.RemoveKeys(Yield(1));
        dictionary.RemoveKeys(EmptyIntList);
        dictionary.RemoveKeys([]);
        dictionary.RemoveKeys([]);
        dictionary.RemoveKeys([CollectionValueFour]);
        dictionary.RemoveKeys(MissingKeys);

        dictionary.Count.Should().Be(CollectionValueFour);
        dictionary.ContainsKey(1).Should().BeFalse();
        dictionary.ContainsKey(CollectionValueFour).Should().BeFalse();
        dictionary.GetValuesBySecondaryIndex(LengthIndexName, CollectionValueThree).Should().BeEquivalentTo(["two", "six"]);
        dictionary.GetValuesBySecondaryIndex(LengthIndexName, CollectionValueFour).Should().ContainSingle().Which.Should().Be("five");
        dictionary.AddRange(ReplacementPairs);
        dictionary.AddRange([new(CollectionValueTwo, "deux")]);
        dictionary[CollectionValueTwo].Should().Be("deux");

        Action missingIndex = () => dictionary.CreateViewBySecondaryIndex(nameof(Missing), CollectionValueThree, Sequencer.Immediate);
        Action incompatibleIndex = () => dictionary.CreateViewBySecondaryIndex(LengthIndexName, ThreeText, Sequencer.Immediate);

        missingIndex.Should().Throw<InvalidOperationException>();
        incompatibleIndex.Should().Throw<InvalidOperationException>();
    }

    /// <summary>Verifies QuaternaryDictionary parallel array and list paths for large batch adds and key removals.</summary>
    [Test]
    public void QuaternaryDictionary_LargeBatchOverloads_ShouldUseParallelPaths()
    {
        using var dictionary = new QuaternaryDictionary<int, string>();
        dictionary.AddValueIndex(LengthIndexName, value => value.Length);

        var firstBatch = Enumerable.Range(0, LargeBatchSize)
            .Select(i => new KeyValuePair<int, string>(i, $"value-{i}"))
            .ToArray();

        dictionary.AddRange(firstBatch);
        dictionary.RemoveKeys(Enumerable.Range(0, RemovedInitialKeys).ToArray());

        dictionary.Count.Should().Be(ExpectedRemainingItems);
        dictionary.ContainsKey(0).Should().BeFalse();
        dictionary.ContainsKey(LastRetainedKey).Should().BeTrue();

        var secondBatch = Enumerable.Range(LargeBatchSize, LargeBatchSize)
            .Select(i => new KeyValuePair<int, string>(i, $"value-{i}"))
            .ToList();

        dictionary.AddRange(secondBatch);
        dictionary.RemoveKeys(secondBatch.Select(pair => pair.Key).Where(key => key % CollectionValueTwo == 0).ToList());

        dictionary.ContainsKey(LargeBatchSize).Should().BeFalse();
        dictionary.ContainsKey(FirstRetainedKey).Should().BeTrue();
        dictionary.GetValuesBySecondaryIndex(LengthIndexName, "value-1101".Length).Should().Contain("value-1101");
    }

    /// <summary>Verifies QuaternaryDictionary parallel paths when all keys land in one shard, plus RemoveMany buffer growth.</summary>
    [Test]
    public void QuaternaryDictionary_SingleShardParallelBatchesAndRemoveManyGrowth_ShouldMaintainIndexes()
    {
        using var dictionary = new QuaternaryDictionary<ConstantShardKey, string>();
        dictionary.AddValueIndex(LengthIndexName, static value => value.Length);

        var arrayBatch = Enumerable.Range(0, ParallelBatchSize)
            .Select(static id => new KeyValuePair<ConstantShardKey, string>(new ConstantShardKey(id), $"v{id}"))
            .ToArray();

        dictionary.AddRange(arrayBatch);
        dictionary.GetValuesBySecondaryIndex(LengthIndexName, CollectionValueTwo).Should().Contain("v0");
        dictionary.RemoveKeys(arrayBatch.Select(static pair => pair.Key).ToArray());
        dictionary.Count.Should().Be(0);

        var listBatch = Enumerable.Range(ParallelBatchSize, ParallelBatchSize)
            .Select(static id => new KeyValuePair<ConstantShardKey, string>(new ConstantShardKey(id), $"v{id}"))
            .ToList();

        dictionary.AddRange(listBatch);
        dictionary.GetValuesBySecondaryIndex(LengthIndexName, CollectionValueFour).Should().Contain("v300");
        dictionary.RemoveKeys(listBatch.ConvertAll(static pair => pair.Key));
        dictionary.Count.Should().Be(0);

        dictionary.AddRange(Enumerable.Range(FinalBatchStart, FinalBatchSize)
            .Select(static id => new KeyValuePair<ConstantShardKey, string>(new ConstantShardKey(id), $"v{id}"))
            .ToArray());

        dictionary.RemoveMany(static _ => true).Should().Be(FinalBatchSize);
        dictionary.Count.Should().Be(0);
        dictionary.GetValuesBySecondaryIndex(LengthIndexName, CollectionValueFour).Should().BeEmpty();

        dictionary.Add(new ConstantShardKey(MissingKeyOrIndex), "v999");
        dictionary.RemoveMany(static _ => false).Should().Be(0);
        dictionary.Count.Should().Be(1);
    }

    /// <summary>Verifies QuaternaryDictionary edit wrapper members and index maintenance.</summary>
    [Test]
    public void QuaternaryDictionary_EditWrapper_ShouldExposeDictionaryMembers()
    {
        using var dictionary = new QuaternaryDictionary<int, string>();
        dictionary.AddValueIndex(LengthIndexName, value => value.Length);
        dictionary.AddRange([
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(CollectionValueTwo, "two"),
            new KeyValuePair<int, string>(CollectionValueThree, ThreeText)
        ]);

        dictionary.Edit(editor =>
        {
            editor.Count.Should().Be(CollectionValueThree);
            editor.IsReadOnly.Should().BeFalse();
            editor.Keys.Should().BeEquivalentTo([1, CollectionValueTwo, CollectionValueThree]);
            editor.Values.Should().BeEquivalentTo(["one", "two", ThreeText]);
            editor[1].Should().Be("one");

            editor[1] = "ONE";
            editor[CollectionValueFour] = "four";
            editor.Add(CollectionValueFive, "five");
            editor.Add(new KeyValuePair<int, string>(CollectionValueSix, "six"));

            editor.ContainsKey(CollectionValueSix).Should().BeTrue();
            editor.TryGetValue(CollectionValueSix, out var six).Should().BeTrue();
            six.Should().Be("six");
            editor.Contains(new KeyValuePair<int, string>(CollectionValueSix, "six")).Should().BeTrue();

            var copy = new KeyValuePair<int, string>[editor.Count];
            editor.CopyTo(copy, 0);
            copy.Should().Contain(new KeyValuePair<int, string>(CollectionValueSix, "six"));

            ((IEnumerable)editor).GetEnumerator().MoveNext().Should().BeTrue();
            var editorEnumerator = editor.GetEnumerator();
            while (editorEnumerator.MoveNext())
            {
                _ = editorEnumerator.Current;
            }

            editorEnumerator.MoveNext().Should().BeFalse();
            editor.Remove(new KeyValuePair<int, string>(CollectionValueFive, "wrong")).Should().BeFalse();
            editor.Remove(new KeyValuePair<int, string>(CollectionValueFive, "five")).Should().BeTrue();
            editor.Remove(MissingCollectionValue).Should().BeFalse();

            Action missingKey = () => _ = editor[MissingCollectionValue];
            missingKey.Should().Throw<KeyNotFoundException>();
        });

        dictionary.Should().Contain(new KeyValuePair<int, string>(1, "ONE"));
        dictionary.Should().Contain(new KeyValuePair<int, string>(CollectionValueFour, "four"));
        dictionary.Should().Contain(new KeyValuePair<int, string>(CollectionValueSix, "six"));
        dictionary.ContainsKey(CollectionValueFive).Should().BeFalse();
        dictionary.GetValuesBySecondaryIndex(LengthIndexName, CollectionValueThree).Should().BeEquivalentTo(["ONE", "two", "six"]);
    }

    /// <summary>Verifies QuaternaryDictionary guard paths and legacy collection changed update notifications.</summary>
    [Test]
    public void QuaternaryDictionary_GuardsAndCollectionChanged_ShouldBehaveAsExpected()
    {
        using var dictionary = new QuaternaryDictionary<int, string> { { 1, "one" } };
        dictionary.Contains(new KeyValuePair<int, string>(1, "uno")).Should().BeFalse();
        dictionary.Remove(new KeyValuePair<int, string>(1, "uno")).Should().BeFalse();

        Action duplicate = () => dictionary.Add(1, "duplicate");
        Action missingIndexer = () => _ = dictionary[MissingCollectionValue];
        Action nullCopy = () => dictionary.CopyTo(null!, 0);
        Action nullRemoveKeys = () => dictionary.RemoveKeys(null!);
        Action nullRemoveMany = () => dictionary.RemoveMany(null!);
        Action nullEdit = () => dictionary.Edit(null!);

        duplicate.Should().Throw<ArgumentException>();
        missingIndexer.Should().Throw<KeyNotFoundException>();
        nullCopy.Should().Throw<ArgumentNullException>();
        nullRemoveKeys.Should().Throw<ArgumentNullException>();
        nullRemoveMany.Should().Throw<ArgumentNullException>();
        nullEdit.Should().Throw<ArgumentNullException>();

        using var reset = new ManualResetEventSlim(false);
        NotifyCollectionChangedAction? action = null;

        dictionary.CollectionChanged += (_, args) =>
        {
            action = args.Action;
            reset.Set();
        };

        dictionary[1] = "ONE";

        reset.Wait(TimeSpan.FromSeconds(CollectionValueTwo)).Should().BeTrue();
        action.Should().Be(NotifyCollectionChangedAction.Reset);
    }

    /// <summary>Verifies protected QuaternaryBase batch helpers and null guard paths through a minimal harness.</summary>
    [Test]
    public void QuaternaryBase_BatchHelpers_ShouldEmitAndValidateArguments()
    {
        using var harness = new QuaternaryBaseHarness();
        var received = new List<CacheNotify<int>>();
        using var subscription = harness.Stream.Subscribe(received.Add);

        harness.EmitDirect([1, CollectionValueTwo]);
        harness.EmitAddedFromList([CollectionValueThree, CollectionValueFour]);
        harness.EmitRemovedFromList([CollectionValueFive, CollectionValueSix]);
        SpinWait.SpinUntil(() => received.Count >= 3, TimeSpan.FromSeconds(CollectionValueTwo)).Should().BeTrue();

        received.Select(static notification => notification.Action)
            .Should().Equal(CacheAction.BatchOperation, CacheAction.BatchAdded, CacheAction.BatchRemoved);
        received.Select(static notification => notification.Batch!.Count)
            .Should().Equal(CollectionValueTwo, CollectionValueTwo, CollectionValueTwo);

        Action nullAdded = () => harness.EmitAddedFromList(null!);
        Action nullRemoved = () => harness.EmitRemovedFromList(null!);

        nullAdded.Should().Throw<ArgumentNullException>().WithParameterName("items");
        nullRemoved.Should().Throw<ArgumentNullException>().WithParameterName("items");

        foreach (var notification in received)
        {
            notification.Batch!.Dispose();
        }
    }

    /// <summary>Verifies QuaternaryBase no-observer fast paths and legacy collection changed mappings.</summary>
    [Test]
    public void QuaternaryBase_NoObserverAndLegacyCollectionChangedBranches_ShouldExecute()
    {
        using var noObserverHarness = new QuaternaryBaseHarness();
        NotifyCollectionChangedEventHandler? nullHandler = null;
        noObserverHarness.CollectionChanged += nullHandler;
        noObserverHarness.CollectionChanged -= nullHandler;

        noObserverHarness.EmitDirect([1, CollectionValueTwo]);
        noObserverHarness.EmitOwnedRemoved([CollectionValueThree, CollectionValueFour]);
        noObserverHarness.EmitRemovedFromList([CollectionValueFive, CollectionValueSix]);

        using var harness = new QuaternaryBaseHarness();
        var actions = new List<NotifyCollectionChangedAction>();
        harness.CollectionChanged += (_, args) => actions.Add(args.Action);

        harness.EmitDirect([1, CollectionValueTwo]);
        harness.EmitSingle(CacheAction.Added, CollectionValueThree);
        harness.EmitSingle(CacheAction.Removed, CollectionValueFour);
        harness.EmitSingle(CacheAction.Moved, CollectionValueFive);

        SpinWait.SpinUntil(() => actions.Count >= 4, TimeSpan.FromSeconds(CollectionValueTwo)).Should().BeTrue();
        actions.Should().Equal(
            NotifyCollectionChangedAction.Reset,
            NotifyCollectionChangedAction.Reset,
            NotifyCollectionChangedAction.Reset,
            NotifyCollectionChangedAction.Reset);
    }

    /// <summary>Verifies private event processor edge cases that have no stable public timing path.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task QuaternaryBase_PrivateEventProcessorEdges_ShouldExecute()
    {
        var baseType = typeof(QuaternaryBase<int, int>);
        var processEvents = baseType.GetMethod("ProcessEventsAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(baseType.FullName, "ProcessEventsAsync");
        var ensureStarted = baseType.GetMethod("EnsureEventProcessorStarted", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(baseType.FullName, "EnsureEventProcessorStarted");

        using (var nullStateHarness = new QuaternaryBaseHarness())
        {
            await (Task)(processEvents.Invoke(nullStateHarness, null)
                ?? throw new InvalidOperationException("The event processor did not return a task."));
        }

        using (var completedReaderHarness = new QuaternaryBaseHarness())
        {
            var completedChannel = Channel.CreateUnbounded<CacheNotify<int>>();
            completedChannel.Writer.Complete();
            SetPrivateField(completedReaderHarness, "_eventChannel", completedChannel);
            SetPrivateField(completedReaderHarness, "_pipeline", new Signal<CacheNotify<int>>());
            SetPrivateField(completedReaderHarness, "_cts", new CancellationTokenSource());

            await (Task)(processEvents.Invoke(completedReaderHarness, null)
                ?? throw new InvalidOperationException("The event processor did not return a task."));
        }

        using (var failedWriteHarness = new QuaternaryBaseHarness())
        {
            var completedChannel = Channel.CreateUnbounded<CacheNotify<int>>();
            completedChannel.Writer.Complete();
            SetPrivateField(failedWriteHarness, "_eventProcessorStarted", 1);
            SetPrivateField(failedWriteHarness, "_hasSubscribers", 1);
            SetPrivateField(failedWriteHarness, "_eventChannel", completedChannel);
            SetPrivateField(failedWriteHarness, "_pipeline", new Signal<CacheNotify<int>>());
            SetPrivateField(failedWriteHarness, "_cts", new CancellationTokenSource());

            failedWriteHarness.EmitDirect([1, CollectionValueTwo]);
        }

        using (var nullHandlerHarness = new QuaternaryBaseHarness())
        {
            InvokePrivate(
                nullHandlerHarness,
                "InvokeLegacyINCC",
                new CacheNotify<int>(CacheAction.BatchAdded, default, CreateBatch(1, CollectionValueTwo)));
        }

        using (var legacyHarness = new QuaternaryBaseHarness())
        {
            var actions = new List<NotifyCollectionChangedAction>();
            legacyHarness.CollectionChanged += (_, args) => actions.Add(args.Action);

            InvokePrivate(legacyHarness, "InvokeLegacyINCC", new CacheNotify<int>(CacheAction.Cleared, default));

            actions.Should().Contain(NotifyCollectionChangedAction.Reset);
        }

        using var startedRaceHarness = new QuaternaryBaseHarness();
        var gate = new object();
        SetPrivateField(startedRaceHarness, "_eventGate", gate);

        Task ensureTask;
        lock (gate)
        {
            ensureTask = Task.Run(() => ensureStarted.Invoke(startedRaceHarness, null));
            SpinWait.SpinUntil(() => ensureTask.IsCompleted, TimeSpan.FromMilliseconds(ProcessorPollMilliseconds));
            SetPrivateField(startedRaceHarness, "_eventProcessorStarted", 1);
        }

        await ensureTask;
    }

    /// <summary>Provides Yield.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <returns>The result.</returns>
    /// <param name="items">The items value.</param>
    private static IEnumerable<T> Yield<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }

    /// <summary>Provides CreateBatch.</summary>
    /// <param name="values">The values value.</param>
    /// <returns>The result.</returns>
    private static PooledBatch<int> CreateBatch(params int[] values)
    {
        var array = ArrayPool<int>.Shared.Rent(Math.Max(1, values.Length));
        Array.Copy(values, array, values.Length);
        return new PooledBatch<int>(array, values.Length);
    }

    /// <summary>Provides InvokePrivate.</summary>
    /// <param name="target">The target value.</param>
    /// <param name="methodName">The methodName value.</param>
    /// <param name="args">The args value.</param>
    /// <returns>The result.</returns>
    private static object? InvokePrivate(object target, string methodName, params object?[] args)
    {
        var baseType = target.GetType().BaseType ?? throw new InvalidOperationException("The test harness has no base type.");
        var method = baseType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(baseType.FullName, methodName);
        return method.Invoke(target, args);
    }

    /// <summary>Provides SetPrivateField.</summary>
    /// <param name="target">The target value.</param>
    /// <param name="fieldName">The fieldName value.</param>
    /// <param name="value">The value.</param>
    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        for (var type = target.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null)
            {
                field.SetValue(target, value);
                return;
            }
        }

        throw new MissingFieldException(target.GetType().FullName, fieldName);
    }

    /// <summary>Provides ImmediateSynchronizationContext.</summary>
    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        private int _postCount;

        /// <summary>Gets PostCount.</summary>
        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _postCount);
            d(state);
        }
    }

    /// <summary>Provides ConstantShardItem.</summary>
    private sealed class ConstantShardItem : IEquatable<ConstantShardItem>
    {
        /// <summary>Initializes a new instance of the <see cref="ConstantShardItem"/> class.</summary>
        /// <param name="id">The id value.</param>
        public ConstantShardItem(int id) => Id = id;

        /// <summary>Gets Id.</summary>
        public int Id { get; }

        /// <summary>Provides Equals.</summary>
        /// <param name="other">The other value.</param>
        /// <returns>The result.</returns>
        public bool Equals(ConstantShardItem? other) => other is not null && Id == other.Id;

        public override bool Equals(object? obj) => Equals(obj as ConstantShardItem);

        public override int GetHashCode() => 0;
    }

    /// <summary>Provides ConstantShardKey.</summary>
    private sealed class ConstantShardKey : IEquatable<ConstantShardKey>
    {
        /// <summary>Initializes a new instance of the <see cref="ConstantShardKey"/> class.</summary>
        /// <param name="id">The id value.</param>
        public ConstantShardKey(int id) => Id = id;

        /// <summary>Gets Id.</summary>
        public int Id { get; }

        /// <summary>Provides Equals.</summary>
        /// <param name="other">The other value.</param>
        /// <returns>The result.</returns>
        public bool Equals(ConstantShardKey? other) => other is not null && Id == other.Id;

        public override bool Equals(object? obj) => Equals(obj as ConstantShardKey);

        public override int GetHashCode() => 0;
    }

    /// <summary>Provides QuaternaryBaseHarness.</summary>
    private sealed class QuaternaryBaseHarness : QuaternaryBase<int, int>
    {
        protected override IReadOnlyList<IQuad<int>> BaseQuads { get; } =
        [
            new QuadList<int>(),
            new QuadList<int>(),
            new QuadList<int>(),
            new QuadList<int>()
        ];

        /// <summary>Provides EmitDirect.</summary>
        /// <param name="items">The items value.</param>
        public void EmitDirect(int[] items) => EmitBatchDirect(items, items.Length);

        /// <summary>Provides EmitAddedFromList.</summary>
        /// <param name="items">The items value.</param>
        public void EmitAddedFromList(IList<int> items) => EmitBatchAddedFromList(items, items?.Count ?? 0);

        /// <summary>Provides EmitRemovedFromList.</summary>
        /// <param name="items">The items value.</param>
        public void EmitRemovedFromList(IList<int> items) => EmitBatchRemovedFromList(items, items?.Count ?? 0);

        /// <summary>Provides EmitOwnedRemoved.</summary>
        /// <param name="items">The items value.</param>
        public void EmitOwnedRemoved(int[] items) => EmitOwnedBatchRemoved(items, items.Length);

        /// <summary>Provides EmitSingle.</summary>
        /// <param name="action">The action value.</param>
        /// <param name="item">The item value.</param>
        public void EmitSingle(CacheAction action, int item) => Emit(action, item);

        public override IEnumerator<int> GetEnumerator() => Enumerable.Empty<int>().GetEnumerator();
    }
}
#endif
