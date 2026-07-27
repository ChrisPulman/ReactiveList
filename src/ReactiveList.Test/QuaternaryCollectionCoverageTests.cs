// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
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
    /// <summary>The second collection value used by test data.</summary>
    private const int CollectionValueTwo = 2;

    /// <summary>The third collection value used by test data.</summary>
    private const int CollectionValueThree = 3;

    /// <summary>The fourth collection value used by test data.</summary>
    private const int CollectionValueFour = 4;

    /// <summary>The fifth collection value used by test data.</summary>
    private const int CollectionValueFive = 5;

    /// <summary>The sixth collection value used by test data.</summary>
    private const int CollectionValueSix = 6;

    /// <summary>The eighth collection value used by test data.</summary>
    private const int CollectionValueEight = 8;

    /// <summary>The number of modulo buckets used by secondary-index tests.</summary>
    private const int ModuloBucketCount = 10;

    /// <summary>The eleventh collection value used by test data.</summary>
    private const int CollectionValueEleven = 11;

    /// <summary>The expected number of remaining items after removal.</summary>
    private const int ExpectedRemainingItems = 40;

    /// <summary>The value used by replacement tests.</summary>
    private const int ReplacementValue = 42;

    /// <summary>The interval used when polling the event processor.</summary>
    private const int ProcessorPollMilliseconds = 50;

    /// <summary>A value deliberately absent from test collections.</summary>
    private const int MissingCollectionValue = 99;

    /// <summary>The size of the final batch used by parallel-path tests.</summary>
    private const int FinalBatchSize = 100;

    /// <summary>The expected number of items in each parity bucket.</summary>
    private const int ExpectedParityItems = 150;

    /// <summary>The size of batches used to exercise parallel paths.</summary>
    private const int ParallelBatchSize = 300;

    /// <summary>The first identifier in the final batch.</summary>
    private const int FinalBatchStart = 600;

    /// <summary>The minimum number of retained items expected after removals.</summary>
    private const int MinimumRetainedItems = 700;

    /// <summary>A key or index deliberately outside the populated range.</summary>
    private const int MissingKeyOrIndex = 999;

    /// <summary>The number of initial keys removed from the large batch.</summary>
    private const int RemovedInitialKeys = 1060;

    /// <summary>The last key expected to remain from the initial batch.</summary>
    private const int LastRetainedKey = 1099;

    /// <summary>The size of the large batch used by parallel-path tests.</summary>
    private const int LargeBatchSize = 1100;

    /// <summary>The first retained key after the second removal pass.</summary>
    private const int FirstRetainedKey = 1101;

    /// <summary>A value guaranteed not to occur in test collections.</summary>
    private const int DefinitelyMissingValue = 999_999;

    /// <summary>The name of the parity secondary index.</summary>
    private const string ParityIndexName = "Parity";

    /// <summary>The name of the string-length secondary index.</summary>
    private const string LengthIndexName = "Length";

    /// <summary>The textual representation used for the number three.</summary>
    private const string ThreeText = "three";

    /// <summary>The replacement key-value pairs used by dictionary tests.</summary>
    private static readonly KeyValuePair<int, string>[] ReplacementPairs = [new(2, "TWO")];

    /// <summary>The keys deliberately absent from the test dictionary.</summary>
    private static readonly int[] MissingKeys = [999];

    /// <summary>An empty integer list used to exercise list overloads.</summary>
    private static readonly List<int> EmptyIntList = [];

    /// <summary>An empty pair list used to exercise dictionary overloads.</summary>
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
        _ = list.Count.Should().Be(0);
        _ = list.Version.Should().Be(initialVersion);

        list.AddIndex(ParityIndexName, static item => item % CollectionValueTwo);
        list.AddRange(Yield(0, 1, CollectionValueTwo, CollectionValueThree, CollectionValueFour));
        list.RemoveRange(Yield(1, CollectionValueThree));
        list.AddRange([ModuloBucketCount, CollectionValueEleven]);
        list.AddRange(EmptyIntList);
        list.AddRange([]);
        list.RemoveRange([]);
        list.RemoveRange(EmptyIntList);
        list.RemoveRange([]);
        list.RemoveRange([CollectionValueTwo]);

        _ = list.Count.Should().Be(CollectionValueFour);
        _ = list.Should().BeEquivalentTo([0, CollectionValueFour, ModuloBucketCount, CollectionValueEleven]);
        _ = list.GetItemsBySecondaryIndex(ParityIndexName, 0).Should().BeEquivalentTo([0, CollectionValueFour, ModuloBucketCount]);
        _ = list.GetItemsBySecondaryIndex(ParityIndexName, 1).Should().ContainSingle().Which.Should().Be(CollectionValueEleven);
        _ = list.Version.Should().BeGreaterThan(initialVersion);
    }

    /// <summary>Verifies QuaternaryList parallel array and list paths for large batch adds and removals.</summary>
    [Test]
    public void QuaternaryList_LargeBatchOverloads_ShouldUseParallelPaths()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod10", static item => item % ModuloBucketCount);

        var firstBatch = CreateRangeArray(0, LargeBatchSize);
        list.AddRange(firstBatch);
        list.RemoveRange(FilterToArray(firstBatch, static item => item % CollectionValueThree == 0));

        _ = list.Count.Should().BeGreaterThan(MinimumRetainedItems);
        _ = list.Contains(0).Should().BeFalse();
        _ = list.Contains(1).Should().BeTrue();

        var secondBatch = CreateRangeList(LargeBatchSize, LargeBatchSize);
        list.AddRange(secondBatch);
        list.RemoveRange(FilterToList(secondBatch, static item => item % CollectionValueTwo == 0));

        _ = list.Contains(LargeBatchSize).Should().BeFalse();
        _ = list.Contains(FirstRetainedKey).Should().BeTrue();
        _ = list.GetItemsBySecondaryIndex("Mod10", 1).Should().Contain(FirstRetainedKey);

        var countBeforeMissingRemove = list.Count;
        list.RemoveRange([DefinitelyMissingValue]);
        _ = list.Count.Should().Be(countBeforeMissingRemove);
    }

    /// <summary>Verifies QuaternaryList parallel paths when all items land in one shard, plus RemoveMany buffer growth.</summary>
    [Test]
    public void QuaternaryList_SingleShardParallelBatchesAndRemoveManyGrowth_ShouldMaintainIndexes()
    {
        using var list = new QuaternaryList<ConstantShardItem>();
        list.AddIndex(ParityIndexName, static item => item.Id % CollectionValueTwo);

        var arrayBatch = CreateConstantShardItems(0, ParallelBatchSize);
        list.AddRange(arrayBatch);
        _ = list.GetItemsBySecondaryIndex(ParityIndexName, 0).Should().HaveCount(ExpectedParityItems);
        list.RemoveRange(arrayBatch);
        _ = list.Count.Should().Be(0);

        var listBatch = new List<ConstantShardItem>(CreateConstantShardItems(ParallelBatchSize, ParallelBatchSize));
        list.AddRange(listBatch);
        _ = list.GetItemsBySecondaryIndex(ParityIndexName, 1).Should().HaveCount(ExpectedParityItems);
        list.RemoveRange(listBatch);
        _ = list.Count.Should().Be(0);

        list.AddRange(CreateConstantShardItems(FinalBatchStart, FinalBatchSize));
        _ = list.RemoveMany(static _ => true).Should().Be(FinalBatchSize);
        _ = list.Count.Should().Be(0);
        _ = list.GetItemsBySecondaryIndex(ParityIndexName, 0).Should().BeEmpty();
    }

    /// <summary>Verifies the collection contract exposed by QuaternaryList batch edits.</summary>
    [Test]
    public void QuaternaryList_EditWrapper_ShouldExposeCollectionMembers()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex(ParityIndexName, static item => item % CollectionValueTwo);
        list.AddRange([1, CollectionValueTwo, CollectionValueThree]);

        list.Edit(static editor =>
        {
            _ = editor.Count.Should().Be(CollectionValueThree);
            _ = editor.IsReadOnly.Should().BeFalse();
            _ = editor.Contains(CollectionValueTwo).Should().BeTrue();

            var copy = new int[3];
            editor.CopyTo(copy, 0);
            _ = copy.Should().BeEquivalentTo([1, CollectionValueTwo, CollectionValueThree]);
            _ = editor.Should().BeEquivalentTo([1, CollectionValueTwo, CollectionValueThree]);

            _ = editor.Remove(1).Should().BeTrue();
            _ = editor.Remove(MissingCollectionValue).Should().BeFalse();
            editor.Add(CollectionValueFour);

            editor.Add(CollectionValueFive);
            editor.Add(CollectionValueSix);

            var editorEnumerator = editor.GetEnumerator();
            while (editorEnumerator.MoveNext())
            {
                _ = editorEnumerator.Current;
            }

            _ = editorEnumerator.MoveNext().Should().BeFalse();
            _ = ((IEnumerable)editor).GetEnumerator().MoveNext().Should().BeTrue();
        });

        _ = list.Should().BeEquivalentTo([CollectionValueTwo, CollectionValueThree, CollectionValueFour, CollectionValueFive, CollectionValueSix]);
        _ = list.GetItemsBySecondaryIndex(ParityIndexName, 0).Should().BeEquivalentTo([CollectionValueTwo, CollectionValueFour, CollectionValueSix]);

        using var noIndexList = new QuaternaryList<int>();
        noIndexList.AddRange([1, CollectionValueTwo]);
        noIndexList.Edit(static editor => _ = editor.Remove(1).Should().BeTrue());
        _ = noIndexList.Should().ContainSingle().Which.Should().Be(CollectionValueTwo);
    }

    /// <summary>Verifies QuaternaryList snapshot and index guard paths.</summary>
    [Test]
    public void QuaternaryList_SnapshotAndInvalidIndexes_ShouldBehaveAsExpected()
    {
        var list = new QuaternaryList<int>();
        list.AddRange([0, CollectionValueFour, CollectionValueEight, 1]);

        _ = list.Snapshot().Should().BeEquivalentTo(list.ToArray());
        using var enumerator = ((IEnumerable<int>)list).GetEnumerator();
        while (enumerator.MoveNext())
        {
            _ = enumerator.Current;
        }

        _ = enumerator.MoveNext().Should().BeFalse();

        var nonGenericEnumerator = ((IEnumerable)list).GetEnumerator();
        while (nonGenericEnumerator.MoveNext())
        {
            _ = nonGenericEnumerator.Current;
        }

        _ = nonGenericEnumerator.MoveNext().Should().BeFalse();

        Action negativeIndex = () => _ = list[-1];
        Action tooHighIndex = () => _ = list[MissingCollectionValue];
        Action setter = () => list[0] = ReplacementValue;

        _ = negativeIndex.Should().Throw<ArgumentOutOfRangeException>();
        _ = tooHighIndex.Should().Throw<ArgumentOutOfRangeException>();
        _ = setter.Should().Throw<NotSupportedException>();

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

            _ = reset.Wait(TimeSpan.FromSeconds(CollectionValueTwo)).Should().BeTrue();
            _ = context.PostCount.Should().BeGreaterThan(0);
            _ = action.Should().Be(NotifyCollectionChangedAction.Reset);
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
        _ = dictionary.Version.Should().Be(initialVersion);

        dictionary.AddRange(Yield<KeyValuePair<int, string>>(
            new(1, "one"),
            new(CollectionValueTwo, "two"),
            new(CollectionValueThree, ThreeText)));
        dictionary.AddValueIndex(LengthIndexName, static value => value.Length);
        dictionary.AddRange(EmptyPairs);

        using var view = dictionary.CreateViewBySecondaryIndex(LengthIndexName, CollectionValueThree, Sequencer.Immediate, throttleMs: 1);
        _ = view.Count.Should().Be(CollectionValueTwo);

        dictionary.AddRange([
            new(CollectionValueFour, "four"),
            new(CollectionValueFive, "five")
        ]);
        dictionary.Add(new(CollectionValueSix, "six"));
        dictionary.AddRange([]);

        dictionary.RemoveKeys(Yield(1));
        dictionary.RemoveKeys(EmptyIntList);
        dictionary.RemoveKeys([]);
        dictionary.RemoveKeys([]);
        dictionary.RemoveKeys([CollectionValueFour]);
        dictionary.RemoveKeys(MissingKeys);

        _ = dictionary.Count.Should().Be(CollectionValueFour);
        _ = dictionary.ContainsKey(1).Should().BeFalse();
        _ = dictionary.ContainsKey(CollectionValueFour).Should().BeFalse();
        _ = dictionary.GetValuesBySecondaryIndex(LengthIndexName, CollectionValueThree).Should().BeEquivalentTo(["two", "six"]);
        _ = dictionary.GetValuesBySecondaryIndex(LengthIndexName, CollectionValueFour).Should().ContainSingle().Which.Should().Be("five");
        dictionary.AddRange(ReplacementPairs);
        dictionary.AddRange([new(CollectionValueTwo, "deux")]);
        _ = dictionary[CollectionValueTwo].Should().Be("deux");

        Action missingIndex = () => dictionary.CreateViewBySecondaryIndex(nameof(Missing), CollectionValueThree, Sequencer.Immediate);
        Action incompatibleIndex = () => dictionary.CreateViewBySecondaryIndex(LengthIndexName, ThreeText, Sequencer.Immediate);

        _ = missingIndex.Should().Throw<InvalidOperationException>();
        _ = incompatibleIndex.Should().Throw<InvalidOperationException>();
    }

    /// <summary>Verifies QuaternaryDictionary parallel array and list paths for large batch adds and key removals.</summary>
    [Test]
    public void QuaternaryDictionary_LargeBatchOverloads_ShouldUseParallelPaths()
    {
        using var dictionary = new QuaternaryDictionary<int, string>();
        dictionary.AddValueIndex(LengthIndexName, static value => value.Length);

        var firstBatch = CreateIntStringPairs(0, LargeBatchSize);

        dictionary.AddRange(firstBatch);
        dictionary.RemoveKeys(CreateRangeArray(0, RemovedInitialKeys));

        _ = dictionary.Count.Should().Be(ExpectedRemainingItems);
        _ = dictionary.ContainsKey(0).Should().BeFalse();
        _ = dictionary.ContainsKey(LastRetainedKey).Should().BeTrue();

        var secondBatch = new List<KeyValuePair<int, string>>(CreateIntStringPairs(LargeBatchSize, LargeBatchSize));

        dictionary.AddRange(secondBatch);
        dictionary.RemoveKeys(ExtractEvenKeys(secondBatch));

        _ = dictionary.ContainsKey(LargeBatchSize).Should().BeFalse();
        _ = dictionary.ContainsKey(FirstRetainedKey).Should().BeTrue();
        _ = dictionary.GetValuesBySecondaryIndex(LengthIndexName, "value-1101".Length).Should().Contain("value-1101");
    }

    /// <summary>Verifies QuaternaryDictionary parallel paths when all keys land in one shard, plus RemoveMany buffer growth.</summary>
    [Test]
    public void QuaternaryDictionary_SingleShardParallelBatchesAndRemoveManyGrowth_ShouldMaintainIndexes()
    {
        using var dictionary = new QuaternaryDictionary<ConstantShardKey, string>();
        dictionary.AddValueIndex(LengthIndexName, static value => value.Length);

        var arrayBatch = CreateConstantShardPairs(0, ParallelBatchSize);

        dictionary.AddRange(arrayBatch);
        _ = dictionary.GetValuesBySecondaryIndex(LengthIndexName, CollectionValueTwo).Should().Contain("v0");
        dictionary.RemoveKeys(ExtractKeys(arrayBatch));
        _ = dictionary.Count.Should().Be(0);

        var listBatch = new List<KeyValuePair<ConstantShardKey, string>>(CreateConstantShardPairs(ParallelBatchSize, ParallelBatchSize));

        dictionary.AddRange(listBatch);
        _ = dictionary.GetValuesBySecondaryIndex(LengthIndexName, CollectionValueFour).Should().Contain("v300");
        dictionary.RemoveKeys(listBatch.ConvertAll(static pair => pair.Key));
        _ = dictionary.Count.Should().Be(0);

        dictionary.AddRange(CreateConstantShardPairs(FinalBatchStart, FinalBatchSize));

        _ = dictionary.RemoveMany(static _ => true).Should().Be(FinalBatchSize);
        _ = dictionary.Count.Should().Be(0);
        _ = dictionary.GetValuesBySecondaryIndex(LengthIndexName, CollectionValueFour).Should().BeEmpty();

        dictionary.Add(new(MissingKeyOrIndex), "v999");
        _ = dictionary.RemoveMany(static _ => false).Should().Be(0);
        _ = dictionary.Count.Should().Be(1);
    }

    /// <summary>Verifies QuaternaryDictionary edit wrapper members and index maintenance.</summary>
    [Test]
    public void QuaternaryDictionary_EditWrapper_ShouldExposeDictionaryMembers()
    {
        using var dictionary = new QuaternaryDictionary<int, string>();
        dictionary.AddValueIndex(LengthIndexName, static value => value.Length);
        dictionary.AddRange([
            new(1, "one"),
            new(CollectionValueTwo, "two"),
            new(CollectionValueThree, ThreeText)
        ]);

        dictionary.Edit(editor =>
        {
            _ = editor.Count.Should().Be(CollectionValueThree);
            _ = editor.IsReadOnly.Should().BeFalse();
            _ = editor.Keys.Should().BeEquivalentTo([1, CollectionValueTwo, CollectionValueThree]);
            _ = editor.Values.Should().BeEquivalentTo(["one", "two", ThreeText]);
            _ = editor[1].Should().Be("one");

            editor[1] = "ONE";
            editor[CollectionValueFour] = "four";
            editor.Add(CollectionValueFive, "five");
            editor.Add(new(CollectionValueSix, "six"));

            _ = editor.ContainsKey(CollectionValueSix).Should().BeTrue();
            _ = editor.TryGetValue(CollectionValueSix, out var six).Should().BeTrue();
            _ = six.Should().Be("six");
            _ = editor.Contains(Pair(CollectionValueSix, "six")).Should().BeTrue();

            var copy = new KeyValuePair<int, string>[editor.Count];
            editor.CopyTo(copy, 0);
            _ = copy.Should().Contain(Pair(CollectionValueSix, "six"));

            _ = ((IEnumerable)editor).GetEnumerator().MoveNext().Should().BeTrue();
            var editorEnumerator = editor.GetEnumerator();
            while (editorEnumerator.MoveNext())
            {
                _ = editorEnumerator.Current;
            }

            _ = editorEnumerator.MoveNext().Should().BeFalse();
            _ = editor.Remove(Pair(CollectionValueFive, "wrong")).Should().BeFalse();
            _ = editor.Remove(Pair(CollectionValueFive, "five")).Should().BeTrue();
            _ = editor.Remove(MissingCollectionValue).Should().BeFalse();

            Action missingKey = () => _ = editor[MissingCollectionValue];
            _ = missingKey.Should().Throw<KeyNotFoundException>();
        });

        _ = dictionary.Should().Contain(Pair(1, "ONE"));
        _ = dictionary.Should().Contain(Pair(CollectionValueFour, "four"));
        _ = dictionary.Should().Contain(Pair(CollectionValueSix, "six"));
        _ = dictionary.ContainsKey(CollectionValueFive).Should().BeFalse();
        _ = dictionary.GetValuesBySecondaryIndex(LengthIndexName, CollectionValueThree).Should().BeEquivalentTo(["ONE", "two", "six"]);
    }

    /// <summary>Verifies QuaternaryDictionary guard paths and legacy collection changed update notifications.</summary>
    [Test]
    public void QuaternaryDictionary_GuardsAndCollectionChanged_ShouldBehaveAsExpected()
    {
        using var dictionary = new QuaternaryDictionary<int, string> { { 1, "one" } };
        _ = dictionary.Contains(Pair(1, "uno")).Should().BeFalse();
        _ = dictionary.Remove(Pair(1, "uno")).Should().BeFalse();

        Action duplicate = () => dictionary.Add(1, "duplicate");
        Action missingIndexer = () => _ = dictionary[MissingCollectionValue];
        Action nullCopy = () => dictionary.CopyTo(null!, 0);
        Action nullRemoveKeys = () => dictionary.RemoveKeys(null!);
        Action nullRemoveMany = () => dictionary.RemoveMany(null!);
        Action nullEdit = () => dictionary.Edit(null!);

        _ = duplicate.Should().Throw<ArgumentException>();
        _ = missingIndexer.Should().Throw<KeyNotFoundException>();
        _ = nullCopy.Should().Throw<ArgumentNullException>();
        _ = nullRemoveKeys.Should().Throw<ArgumentNullException>();
        _ = nullRemoveMany.Should().Throw<ArgumentNullException>();
        _ = nullEdit.Should().Throw<ArgumentNullException>();

        using var reset = new ManualResetEventSlim(false);
        NotifyCollectionChangedAction? action = null;

        dictionary.CollectionChanged += (_, args) =>
        {
            action = args.Action;
            reset.Set();
        };

        dictionary[1] = "ONE";

        _ = reset.Wait(TimeSpan.FromSeconds(CollectionValueTwo)).Should().BeTrue();
        _ = action.Should().Be(NotifyCollectionChangedAction.Reset);
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
        _ = SpinWait.SpinUntil(() => received.Count >= 3, TimeSpan.FromSeconds(CollectionValueTwo)).Should().BeTrue();

        _ = ExtractActions(received)
            .Should().Equal(CacheAction.BatchOperation, CacheAction.BatchAdded, CacheAction.BatchRemoved);
        _ = ExtractBatchCounts(received)
            .Should().Equal(CollectionValueTwo, CollectionValueTwo, CollectionValueTwo);

        Action nullAdded = () => harness.EmitAddedFromList(null!);
        Action nullRemoved = () => harness.EmitRemovedFromList(null!);

        _ = nullAdded.Should().Throw<ArgumentNullException>().WithParameterName("items");
        _ = nullRemoved.Should().Throw<ArgumentNullException>().WithParameterName("items");

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

        _ = SpinWait.SpinUntil(() => actions.Count >= 4, TimeSpan.FromSeconds(CollectionValueTwo)).Should().BeTrue();
        _ = actions.Should().Equal(
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
        var processEvents = FindInstanceMethod(baseType, "ProcessEventsAsync");
        var ensureStarted = FindInstanceMethod(baseType, "EnsureEventProcessorStarted");

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
            _ = InvokePrivate(
                nullHandlerHarness,
                "InvokeLegacyINCC",
                new CacheNotify<int>(CacheAction.BatchAdded, default, CreateBatch(1, CollectionValueTwo)));
        }

        using (var legacyHarness = new QuaternaryBaseHarness())
        {
            var actions = new List<NotifyCollectionChangedAction>();
            legacyHarness.CollectionChanged += (_, args) => actions.Add(args.Action);

            _ = InvokePrivate(legacyHarness, "InvokeLegacyINCC", new CacheNotify<int>(CacheAction.Cleared, default));

            _ = actions.Should().Contain(NotifyCollectionChangedAction.Reset);
        }

        using var startedRaceHarness = new QuaternaryBaseHarness();
        var gate = new object();
        SetPrivateField(startedRaceHarness, "_eventGate", gate);

        Task ensureTask;
        lock (gate)
        {
            ensureTask = Task.Run(() => ensureStarted.Invoke(startedRaceHarness, null));
            _ = SpinWait.SpinUntil(() => ensureTask.IsCompleted, TimeSpan.FromMilliseconds(ProcessorPollMilliseconds));
            SetPrivateField(startedRaceHarness, "_eventProcessorStarted", 1);
        }

        await ensureTask;
    }

    /// <summary>Creates an integer-string key-value pair.</summary>
    /// <param name="key">The pair key.</param>
    /// <param name="value">The pair value.</param>
    /// <returns>The created pair.</returns>
    private static KeyValuePair<int, string> Pair(int key, string value) => new(key, value);

    /// <summary>Creates an array containing a contiguous integer range.</summary>
    /// <param name="start">The first value.</param>
    /// <param name="count">The number of values.</param>
    /// <returns>The populated array.</returns>
    private static int[] CreateRangeArray(int start, int count)
    {
        var values = new int[count];
        for (var index = 0; index < count; index++)
        {
            values[index] = start + index;
        }

        return values;
    }

    /// <summary>Creates a list containing a contiguous integer range.</summary>
    /// <param name="start">The first value.</param>
    /// <param name="count">The number of values.</param>
    /// <returns>The populated list.</returns>
    private static List<int> CreateRangeList(int start, int count) => new(CreateRangeArray(start, count));

    /// <summary>Copies matching values to a new array.</summary>
    /// <param name="values">The values to filter.</param>
    /// <param name="predicate">The predicate that selects values.</param>
    /// <returns>An array containing the selected values.</returns>
    private static int[] FilterToArray(IEnumerable<int> values, Func<int, bool> predicate)
    {
        var filtered = new List<int>();
        foreach (var value in values)
        {
            if (predicate(value))
            {
                filtered.Add(value);
            }
        }

        return filtered.ToArray();
    }

    /// <summary>Copies matching values to a new list.</summary>
    /// <param name="values">The values to filter.</param>
    /// <param name="predicate">The predicate that selects values.</param>
    /// <returns>A list containing the selected values.</returns>
    private static List<int> FilterToList(IEnumerable<int> values, Func<int, bool> predicate) =>
        new(FilterToArray(values, predicate));

    /// <summary>Creates constant-shard items for the requested identifier range.</summary>
    /// <param name="start">The first identifier.</param>
    /// <param name="count">The number of items.</param>
    /// <returns>The populated array.</returns>
    private static ConstantShardItem[] CreateConstantShardItems(int start, int count)
    {
        var items = new ConstantShardItem[count];
        for (var index = 0; index < count; index++)
        {
            items[index] = new(start + index);
        }

        return items;
    }

    /// <summary>Creates integer-string pairs for the requested key range.</summary>
    /// <param name="start">The first key.</param>
    /// <param name="count">The number of pairs.</param>
    /// <returns>The populated array.</returns>
    private static KeyValuePair<int, string>[] CreateIntStringPairs(int start, int count)
    {
        var pairs = new KeyValuePair<int, string>[count];
        for (var index = 0; index < count; index++)
        {
            var key = start + index;
            pairs[index] = new(key, $"value-{key}");
        }

        return pairs;
    }

    /// <summary>Extracts even keys from the supplied pairs.</summary>
    /// <param name="pairs">The pairs to inspect.</param>
    /// <returns>The selected keys.</returns>
    private static List<int> ExtractEvenKeys(IEnumerable<KeyValuePair<int, string>> pairs)
    {
        var keys = new List<int>();
        foreach (var pair in pairs)
        {
            if (pair.Key % CollectionValueTwo == 0)
            {
                keys.Add(pair.Key);
            }
        }

        return keys;
    }

    /// <summary>Creates constant-shard key-value pairs for the requested identifier range.</summary>
    /// <param name="start">The first identifier.</param>
    /// <param name="count">The number of pairs.</param>
    /// <returns>The populated array.</returns>
    private static KeyValuePair<ConstantShardKey, string>[] CreateConstantShardPairs(int start, int count)
    {
        var pairs = new KeyValuePair<ConstantShardKey, string>[count];
        for (var index = 0; index < count; index++)
        {
            var id = start + index;
            pairs[index] = new(new(id), $"v{id}");
        }

        return pairs;
    }

    /// <summary>Extracts keys from constant-shard pairs.</summary>
    /// <param name="pairs">The pairs to inspect.</param>
    /// <returns>The extracted keys.</returns>
    private static ConstantShardKey[] ExtractKeys(IReadOnlyList<KeyValuePair<ConstantShardKey, string>> pairs)
    {
        var keys = new ConstantShardKey[pairs.Count];
        for (var index = 0; index < pairs.Count; index++)
        {
            keys[index] = pairs[index].Key;
        }

        return keys;
    }

    /// <summary>Extracts actions from received cache notifications.</summary>
    /// <param name="notifications">The notifications to inspect.</param>
    /// <returns>The extracted actions.</returns>
    private static CacheAction[] ExtractActions(List<CacheNotify<int>> notifications)
    {
        var actions = new CacheAction[notifications.Count];
        for (var index = 0; index < notifications.Count; index++)
        {
            actions[index] = notifications[index].Action;
        }

        return actions;
    }

    /// <summary>Extracts batch counts from received cache notifications.</summary>
    /// <param name="notifications">The notifications to inspect.</param>
    /// <returns>The extracted batch counts.</returns>
    private static int[] ExtractBatchCounts(List<CacheNotify<int>> notifications)
    {
        var counts = new int[notifications.Count];
        for (var index = 0; index < notifications.Count; index++)
        {
            counts[index] = notifications[index].Batch!.Count;
        }

        return counts;
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
        return new(array, values.Length);
    }

    /// <summary>Provides InvokePrivate.</summary>
    /// <param name="target">The target value.</param>
    /// <param name="methodName">The methodName value.</param>
    /// <param name="args">The args value.</param>
    /// <returns>The result.</returns>
    private static object? InvokePrivate(object target, string methodName, params object?[] args)
    {
        var baseType = target.GetType().BaseType ?? throw new InvalidOperationException("The test harness has no base type.");
        var method = FindInstanceMethod(baseType, methodName);
        return method.Invoke(target, args);
    }

    /// <summary>Finds an instance method, including an internal implementation detail required by the coverage harness.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="methodName">The required method name.</param>
    /// <returns>The matching instance method.</returns>
    private static MethodInfo FindInstanceMethod(Type type, string methodName)
    {
        foreach (var method in type.GetRuntimeMethods())
        {
            if (!method.IsStatic && method.Name == methodName)
            {
                return method;
            }
        }

        throw new MissingMethodException(type.FullName, methodName);
    }

    /// <summary>Provides SetPrivateField.</summary>
    /// <param name="target">The target value.</param>
    /// <param name="fieldName">The fieldName value.</param>
    /// <param name="value">The value.</param>
    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        for (var type = target.GetType(); type is not null; type = type.BaseType)
        {
            foreach (var field in type.GetRuntimeFields())
            {
                if (!field.IsStatic && field.Name == fieldName)
                {
                    field.SetValue(target, value);
                    return;
                }
            }
        }

        throw new MissingFieldException(target.GetType().FullName, fieldName);
    }

    /// <summary>Provides ImmediateSynchronizationContext.</summary>
    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        /// <summary>The number of callbacks posted through this context.</summary>
        private int _postCount;

        /// <summary>Gets PostCount.</summary>
        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback d, object? state)
        {
            _ = Interlocked.Increment(ref _postCount);
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

        public override IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)Array.Empty<int>()).GetEnumerator();
    }
}
#endif
