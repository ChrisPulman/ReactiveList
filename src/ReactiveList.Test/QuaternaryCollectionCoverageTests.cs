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
using CP.Reactive.Collections;
using CP.Reactive.Core;
using FluentAssertions;
using ReactiveUI.Primitives.Signals;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>
/// Covers quaternary list, dictionary, and base notification paths that are not reached by the public API tests.
/// </summary>
public class QuaternaryCollectionCoverageTests
{
    /// <summary>
    /// Verifies QuaternaryList empty, enumerable, list, and secondary-index batch paths.
    /// </summary>
    [Test]
    public void QuaternaryList_BatchOverloads_ShouldMaintainItemsIndexesAndVersion()
    {
        using var list = new QuaternaryList<int>();
        var initialVersion = list.Version;

        list.AddRange(Array.Empty<int>());
        list.Count.Should().Be(0);
        list.Version.Should().Be(initialVersion);

        list.AddIndex("Parity", item => item % 2);
        list.AddRange(Yield(0, 1, 2, 3, 4));
        list.RemoveRange(Yield(1, 3));
        list.AddRange(new List<int> { 10, 11 });
        list.AddRange(new List<int>());
        list.RemoveRange(Array.Empty<int>());
        list.RemoveRange(new List<int>());
        list.RemoveRange(new List<int> { 2 });

        list.Count.Should().Be(4);
        list.Should().BeEquivalentTo(new[] { 0, 4, 10, 11 });
        list.GetItemsBySecondaryIndex("Parity", 0).Should().BeEquivalentTo(new[] { 0, 4, 10 });
        list.GetItemsBySecondaryIndex("Parity", 1).Should().ContainSingle().Which.Should().Be(11);
        list.Version.Should().BeGreaterThan(initialVersion);
    }

    /// <summary>
    /// Verifies QuaternaryList parallel array and list paths for large batch adds and removals.
    /// </summary>
    [Test]
    public void QuaternaryList_LargeBatchOverloads_ShouldUseParallelPaths()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Mod10", item => item % 10);

        var firstBatch = Enumerable.Range(0, 1100).ToArray();
        list.AddRange(firstBatch);
        list.RemoveRange(firstBatch.Where(item => item % 3 == 0).ToArray());

        list.Count.Should().BeGreaterThan(700);
        list.Contains(0).Should().BeFalse();
        list.Contains(1).Should().BeTrue();

        var secondBatch = Enumerable.Range(1100, 1100).ToList();
        list.AddRange(secondBatch);
        list.RemoveRange(secondBatch.Where(item => item % 2 == 0).ToList());

        list.Contains(1100).Should().BeFalse();
        list.Contains(1101).Should().BeTrue();
        list.GetItemsBySecondaryIndex("Mod10", 1).Should().Contain(1101);

        var countBeforeMissingRemove = list.Count;
        list.RemoveRange(new[] { 999_999 });
        list.Count.Should().Be(countBeforeMissingRemove);
    }

    /// <summary>
    /// Verifies QuaternaryList parallel paths when all items land in one shard, plus RemoveMany buffer growth.
    /// </summary>
    [Test]
    public void QuaternaryList_SingleShardParallelBatchesAndRemoveManyGrowth_ShouldMaintainIndexes()
    {
        using var list = new QuaternaryList<ConstantShardItem>();
        list.AddIndex("Parity", static item => item.Id % 2);

        var arrayBatch = Enumerable.Range(0, 300).Select(static id => new ConstantShardItem(id)).ToArray();
        list.AddRange(arrayBatch);
        list.GetItemsBySecondaryIndex("Parity", 0).Should().HaveCount(150);
        list.RemoveRange(arrayBatch);
        list.Count.Should().Be(0);

        var listBatch = Enumerable.Range(300, 300).Select(static id => new ConstantShardItem(id)).ToList();
        list.AddRange(listBatch);
        list.GetItemsBySecondaryIndex("Parity", 1).Should().HaveCount(150);
        list.RemoveRange(listBatch);
        list.Count.Should().Be(0);

        list.AddRange(Enumerable.Range(600, 100).Select(static id => new ConstantShardItem(id)).ToArray());
        list.RemoveMany(static _ => true).Should().Be(100);
        list.Count.Should().Be(0);
        list.GetItemsBySecondaryIndex("Parity", 0).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies QuaternaryList edit wrapper members and reflected members not exposed by ICollection.
    /// </summary>
    [Test]
    public void QuaternaryList_EditWrapper_ShouldExposeCollectionMembers()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex("Parity", item => item % 2);
        list.AddRange(new[] { 1, 2, 3 });

        list.Edit(editor =>
        {
            editor.Count.Should().Be(3);
            editor.IsReadOnly.Should().BeFalse();
            editor.Contains(2).Should().BeTrue();

            var copy = new int[3];
            editor.CopyTo(copy, 0);
            copy.Should().BeEquivalentTo(new[] { 1, 2, 3 });
            editor.Should().BeEquivalentTo(new[] { 1, 2, 3 });

            editor.Remove(1).Should().BeTrue();
            editor.Remove(99).Should().BeFalse();
            editor.Add(4);

            var wrapperType = editor.GetType();
            wrapperType.GetMethod("AddRange")!.Invoke(editor, new object[] { new[] { 5, 6 } });
            wrapperType.GetProperty("Item")!.GetValue(editor, new object[] { 0 }).Should().NotBeNull();

            var editorEnumerator = editor.GetEnumerator();
            while (editorEnumerator.MoveNext())
            {
            }

            editorEnumerator.MoveNext().Should().BeFalse();
            ((IEnumerable)editor).GetEnumerator().MoveNext().Should().BeTrue();

            Action badIndex = () => wrapperType.GetProperty("Item")!.GetValue(editor, new object[] { 999 });
            Action replaceByIndex = () => wrapperType.GetProperty("Item")!.SetValue(editor, 7, new object[] { 0 });

            badIndex.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentOutOfRangeException>();
            replaceByIndex.Should().Throw<TargetInvocationException>().WithInnerException<NotSupportedException>();
        });

        list.Should().BeEquivalentTo(new[] { 2, 3, 4, 5, 6 });
        list.GetItemsBySecondaryIndex("Parity", 0).Should().BeEquivalentTo(new[] { 2, 4, 6 });

        using var noIndexList = new QuaternaryList<int>();
        noIndexList.AddRange(new[] { 1, 2 });
        noIndexList.Edit(editor => editor.Remove(1).Should().BeTrue());
        noIndexList.Should().ContainSingle().Which.Should().Be(2);
    }

    /// <summary>
    /// Verifies QuaternaryList snapshot and index guard paths.
    /// </summary>
    [Test]
    public void QuaternaryList_SnapshotAndInvalidIndexes_ShouldBehaveAsExpected()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(new[] { 0, 4, 8, 1 });

        list.Snapshot().Should().BeEquivalentTo(list.ToArray());
        using var enumerator = ((IEnumerable<int>)list).GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        enumerator.MoveNext().Should().BeFalse();

        var nonGenericEnumerator = ((IEnumerable)list).GetEnumerator();
        while (nonGenericEnumerator.MoveNext())
        {
        }

        nonGenericEnumerator.MoveNext().Should().BeFalse();

        Action negativeIndex = () => _ = list[-1];
        Action tooHighIndex = () => _ = list[99];
        Action setter = () => list[0] = 42;

        negativeIndex.Should().Throw<ArgumentOutOfRangeException>();
        tooHighIndex.Should().Throw<ArgumentOutOfRangeException>();
        setter.Should().Throw<NotSupportedException>();

        list.Dispose();
        list.Dispose();
    }

    /// <summary>
    /// Verifies QuaternaryBase dispatches legacy collection changes through a captured synchronization context.
    /// </summary>
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

            list.Add(42);

            reset.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
            context.PostCount.Should().BeGreaterThan(0);
            action.Should().Be(NotifyCollectionChangedAction.Reset);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    /// <summary>
    /// Verifies QuaternaryDictionary empty, enumerable, list, secondary-index, and view creation paths.
    /// </summary>
    [Test]
    public void QuaternaryDictionary_BatchOverloadsAndViews_ShouldMaintainIndexes()
    {
        using var dictionary = new QuaternaryDictionary<int, string>();
        var initialVersion = dictionary.Version;

        dictionary.AddRange(Array.Empty<KeyValuePair<int, string>>());
        dictionary.Version.Should().Be(initialVersion);

        dictionary.AddRange(Yield(
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three")));
        dictionary.AddValueIndex("Length", value => value.Length);

        using var view = dictionary.CreateViewBySecondaryIndex("Length", 3, Sequencer.Immediate, throttleMs: 1);
        view.Count.Should().Be(2);

        dictionary.AddRange(new List<KeyValuePair<int, string>>
        {
            new(4, "four"),
            new(5, "five")
        });
        dictionary.Add(new KeyValuePair<int, string>(6, "six"));
        dictionary.AddRange(new List<KeyValuePair<int, string>>());

        dictionary.RemoveKeys(Yield(1));
        dictionary.RemoveKeys(Array.Empty<int>());
        dictionary.RemoveKeys(new List<int>());
        dictionary.RemoveKeys(new List<int> { 4 });
        dictionary.RemoveKeys(new[] { 999 });

        dictionary.Count.Should().Be(4);
        dictionary.ContainsKey(1).Should().BeFalse();
        dictionary.ContainsKey(4).Should().BeFalse();
        dictionary.GetValuesBySecondaryIndex("Length", 3).Should().BeEquivalentTo(new[] { "two", "six" });
        dictionary.GetValuesBySecondaryIndex("Length", 4).Should().ContainSingle().Which.Should().Be("five");
        dictionary.AddRange(new[]
        {
            new KeyValuePair<int, string>(2, "TWO")
        });
        dictionary.AddRange(new List<KeyValuePair<int, string>>
        {
            new(2, "deux")
        });
        dictionary[2].Should().Be("deux");

        Action missingIndex = () => dictionary.CreateViewBySecondaryIndex("Missing", 3, Sequencer.Immediate);
        Action incompatibleIndex = () => dictionary.CreateViewBySecondaryIndex("Length", "three", Sequencer.Immediate);

        missingIndex.Should().Throw<InvalidOperationException>();
        incompatibleIndex.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies QuaternaryDictionary parallel array and list paths for large batch adds and key removals.
    /// </summary>
    [Test]
    public void QuaternaryDictionary_LargeBatchOverloads_ShouldUseParallelPaths()
    {
        using var dictionary = new QuaternaryDictionary<int, string>();
        dictionary.AddValueIndex("Length", value => value.Length);

        var firstBatch = Enumerable.Range(0, 1100)
            .Select(i => new KeyValuePair<int, string>(i, $"value-{i}"))
            .ToArray();

        dictionary.AddRange(firstBatch);
        dictionary.RemoveKeys(Enumerable.Range(0, 1060).ToArray());

        dictionary.Count.Should().Be(40);
        dictionary.ContainsKey(0).Should().BeFalse();
        dictionary.ContainsKey(1099).Should().BeTrue();

        var secondBatch = Enumerable.Range(1100, 1100)
            .Select(i => new KeyValuePair<int, string>(i, $"value-{i}"))
            .ToList();

        dictionary.AddRange(secondBatch);
        dictionary.RemoveKeys(secondBatch.Select(pair => pair.Key).Where(key => key % 2 == 0).ToList());

        dictionary.ContainsKey(1100).Should().BeFalse();
        dictionary.ContainsKey(1101).Should().BeTrue();
        dictionary.GetValuesBySecondaryIndex("Length", "value-1101".Length).Should().Contain("value-1101");
    }

    /// <summary>
    /// Verifies QuaternaryDictionary parallel paths when all keys land in one shard, plus RemoveMany buffer growth.
    /// </summary>
    [Test]
    public void QuaternaryDictionary_SingleShardParallelBatchesAndRemoveManyGrowth_ShouldMaintainIndexes()
    {
        using var dictionary = new QuaternaryDictionary<ConstantShardKey, string>();
        dictionary.AddValueIndex("Length", static value => value.Length);

        var arrayBatch = Enumerable.Range(0, 300)
            .Select(static id => new KeyValuePair<ConstantShardKey, string>(new ConstantShardKey(id), $"v{id}"))
            .ToArray();

        dictionary.AddRange(arrayBatch);
        dictionary.GetValuesBySecondaryIndex("Length", 2).Should().Contain("v0");
        dictionary.RemoveKeys(arrayBatch.Select(static pair => pair.Key).ToArray());
        dictionary.Count.Should().Be(0);

        var listBatch = Enumerable.Range(300, 300)
            .Select(static id => new KeyValuePair<ConstantShardKey, string>(new ConstantShardKey(id), $"v{id}"))
            .ToList();

        dictionary.AddRange(listBatch);
        dictionary.GetValuesBySecondaryIndex("Length", 4).Should().Contain("v300");
        dictionary.RemoveKeys(listBatch.ConvertAll(static pair => pair.Key));
        dictionary.Count.Should().Be(0);

        dictionary.AddRange(Enumerable.Range(600, 100)
            .Select(static id => new KeyValuePair<ConstantShardKey, string>(new ConstantShardKey(id), $"v{id}"))
            .ToArray());

        dictionary.RemoveMany(static _ => true).Should().Be(100);
        dictionary.Count.Should().Be(0);
        dictionary.GetValuesBySecondaryIndex("Length", 4).Should().BeEmpty();

        dictionary.Add(new ConstantShardKey(999), "v999");
        dictionary.RemoveMany(static _ => false).Should().Be(0);
        dictionary.Count.Should().Be(1);
    }

    /// <summary>
    /// Verifies QuaternaryDictionary edit wrapper members and index maintenance.
    /// </summary>
    [Test]
    public void QuaternaryDictionary_EditWrapper_ShouldExposeDictionaryMembers()
    {
        using var dictionary = new QuaternaryDictionary<int, string>();
        dictionary.AddValueIndex("Length", value => value.Length);
        dictionary.AddRange(new[]
        {
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three")
        });

        dictionary.Edit(editor =>
        {
            editor.Count.Should().Be(3);
            editor.IsReadOnly.Should().BeFalse();
            editor.Keys.Should().BeEquivalentTo(new[] { 1, 2, 3 });
            editor.Values.Should().BeEquivalentTo(new[] { "one", "two", "three" });
            editor[1].Should().Be("one");

            editor[1] = "ONE";
            editor[4] = "four";
            editor.Add(5, "five");
            editor.Add(new KeyValuePair<int, string>(6, "six"));

            editor.ContainsKey(6).Should().BeTrue();
            editor.TryGetValue(6, out var six).Should().BeTrue();
            six.Should().Be("six");
            editor.Contains(new KeyValuePair<int, string>(6, "six")).Should().BeTrue();

            var copy = new KeyValuePair<int, string>[editor.Count];
            editor.CopyTo(copy, 0);
            copy.Should().Contain(new KeyValuePair<int, string>(6, "six"));

            ((IEnumerable)editor).GetEnumerator().MoveNext().Should().BeTrue();
            var editorEnumerator = editor.GetEnumerator();
            while (editorEnumerator.MoveNext())
            {
            }

            editorEnumerator.MoveNext().Should().BeFalse();
            editor.Remove(new KeyValuePair<int, string>(5, "wrong")).Should().BeFalse();
            editor.Remove(new KeyValuePair<int, string>(5, "five")).Should().BeTrue();
            editor.Remove(99).Should().BeFalse();

            Action missingKey = () => _ = editor[99];
            missingKey.Should().Throw<KeyNotFoundException>();
        });

        dictionary.Should().Contain(new KeyValuePair<int, string>(1, "ONE"));
        dictionary.Should().Contain(new KeyValuePair<int, string>(4, "four"));
        dictionary.Should().Contain(new KeyValuePair<int, string>(6, "six"));
        dictionary.ContainsKey(5).Should().BeFalse();
        dictionary.GetValuesBySecondaryIndex("Length", 3).Should().BeEquivalentTo(new[] { "ONE", "two", "six" });
    }

    /// <summary>
    /// Verifies QuaternaryDictionary guard paths and legacy collection changed update notifications.
    /// </summary>
    [Test]
    public void QuaternaryDictionary_GuardsAndCollectionChanged_ShouldBehaveAsExpected()
    {
        using var dictionary = new QuaternaryDictionary<int, string>();

        dictionary.Add(1, "one");
        dictionary.Contains(new KeyValuePair<int, string>(1, "uno")).Should().BeFalse();
        dictionary.Remove(new KeyValuePair<int, string>(1, "uno")).Should().BeFalse();

        Action duplicate = () => dictionary.Add(1, "duplicate");
        Action missingIndexer = () => _ = dictionary[99];
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

        reset.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        action.Should().Be(NotifyCollectionChangedAction.Reset);
    }

    /// <summary>
    /// Verifies protected QuaternaryBase batch helpers and null guard paths through a minimal harness.
    /// </summary>
    [Test]
    public void QuaternaryBase_BatchHelpers_ShouldEmitAndValidateArguments()
    {
        using var harness = new QuaternaryBaseHarness();
        var received = new List<CacheNotify<int>>();
        using var subscription = harness.Stream.Subscribe(received.Add);

        harness.EmitDirect(new[] { 1, 2 });
        harness.EmitAddedFromList(new List<int> { 3, 4 });
        harness.EmitRemovedFromList(new List<int> { 5, 6 });
        SpinWait.SpinUntil(() => received.Count >= 3, TimeSpan.FromSeconds(2)).Should().BeTrue();

        received.Select(static notification => notification.Action)
            .Should().Equal(CacheAction.BatchOperation, CacheAction.BatchAdded, CacheAction.BatchRemoved);
        received.Select(static notification => notification.Batch!.Count)
            .Should().Equal(2, 2, 2);

        Action nullAdded = () => harness.EmitAddedFromList(null!);
        Action nullRemoved = () => harness.EmitRemovedFromList(null!);

        nullAdded.Should().Throw<ArgumentNullException>().WithParameterName("items");
        nullRemoved.Should().Throw<ArgumentNullException>().WithParameterName("items");

        foreach (var notification in received)
        {
            notification.Batch!.Dispose();
        }
    }

    /// <summary>
    /// Verifies QuaternaryBase no-observer fast paths and legacy collection changed mappings.
    /// </summary>
    [Test]
    public void QuaternaryBase_NoObserverAndLegacyCollectionChangedBranches_ShouldExecute()
    {
        using var noObserverHarness = new QuaternaryBaseHarness();
        NotifyCollectionChangedEventHandler? nullHandler = null;
        noObserverHarness.CollectionChanged += nullHandler;
        noObserverHarness.CollectionChanged -= nullHandler;

        noObserverHarness.EmitDirect(new[] { 1, 2 });
        noObserverHarness.EmitOwnedRemoved(new[] { 3, 4 });
        noObserverHarness.EmitRemovedFromList(new List<int> { 5, 6 });

        using var harness = new QuaternaryBaseHarness();
        var actions = new List<NotifyCollectionChangedAction>();
        harness.CollectionChanged += (_, args) => actions.Add(args.Action);

        harness.EmitDirect(new[] { 1, 2 });
        harness.EmitSingle(CacheAction.Added, 3);
        harness.EmitSingle(CacheAction.Removed, 4);
        harness.EmitSingle(CacheAction.Moved, 5);

        SpinWait.SpinUntil(() => actions.Count >= 4, TimeSpan.FromSeconds(2)).Should().BeTrue();
        actions.Should().Equal(
            NotifyCollectionChangedAction.Reset,
            NotifyCollectionChangedAction.Reset,
            NotifyCollectionChangedAction.Reset,
            NotifyCollectionChangedAction.Reset);
    }

    /// <summary>
    /// Verifies private event processor edge cases that have no stable public timing path.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task QuaternaryBase_PrivateEventProcessorEdges_ShouldExecute()
    {
        var baseType = typeof(QuaternaryBase<int, int>);
        var processEvents = baseType.GetMethod("ProcessEventsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var ensureStarted = baseType.GetMethod("EnsureEventProcessorStarted", BindingFlags.Instance | BindingFlags.NonPublic)!;

        using (var nullStateHarness = new QuaternaryBaseHarness())
        {
            await (Task)processEvents.Invoke(nullStateHarness, null)!;
        }

        using (var completedReaderHarness = new QuaternaryBaseHarness())
        {
            var completedChannel = Channel.CreateUnbounded<CacheNotify<int>>();
            completedChannel.Writer.Complete();
            SetPrivateField(completedReaderHarness, "_eventChannel", completedChannel);
            SetPrivateField(completedReaderHarness, "_pipeline", new Signal<CacheNotify<int>>());
            SetPrivateField(completedReaderHarness, "_cts", new CancellationTokenSource());

            await (Task)processEvents.Invoke(completedReaderHarness, null)!;
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

            failedWriteHarness.EmitDirect(new[] { 1, 2 });
        }

        using (var nullHandlerHarness = new QuaternaryBaseHarness())
        {
            InvokePrivate(
                nullHandlerHarness,
                "InvokeLegacyINCC",
                new CacheNotify<int>(CacheAction.BatchAdded, default, CreateBatch(1, 2)));
        }

        using (var legacyHarness = new QuaternaryBaseHarness())
        {
            var actions = new List<NotifyCollectionChangedAction>();
            legacyHarness.CollectionChanged += (_, args) => actions.Add(args.Action);

            InvokePrivate(legacyHarness, "InvokeLegacyINCC", new CacheNotify<int>(CacheAction.Cleared, default));

            actions.Should().Contain(NotifyCollectionChangedAction.Reset);
        }

        using (var startedRaceHarness = new QuaternaryBaseHarness())
        {
            var gate = new object();
            SetPrivateField(startedRaceHarness, "_eventGate", gate);

            Task ensureTask;
            lock (gate)
            {
                ensureTask = Task.Run(() => ensureStarted.Invoke(startedRaceHarness, null));
                Thread.Sleep(50);
                SetPrivateField(startedRaceHarness, "_eventProcessorStarted", 1);
            }

            await ensureTask;
        }
    }

    private static IEnumerable<T> Yield<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }

    private static PooledBatch<int> CreateBatch(params int[] values)
    {
        var array = ArrayPool<int>.Shared.Rent(Math.Max(1, values.Length));
        Array.Copy(values, array, values.Length);
        return new PooledBatch<int>(array, values.Length);
    }

    private static object? InvokePrivate(object target, string methodName, params object?[] args) =>
        target.GetType().BaseType!.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
        }

        throw new MissingFieldException(target.GetType().FullName, fieldName);
    }

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        private int _postCount;

        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _postCount);
            d(state);
        }
    }

    private sealed class ConstantShardItem : IEquatable<ConstantShardItem>
    {
        public ConstantShardItem(int id) => Id = id;

        public int Id { get; }

        public bool Equals(ConstantShardItem? other) => other is not null && Id == other.Id;

        public override bool Equals(object? obj) => Equals(obj as ConstantShardItem);

        public override int GetHashCode() => 0;
    }

    private sealed class ConstantShardKey : IEquatable<ConstantShardKey>
    {
        public ConstantShardKey(int id) => Id = id;

        public int Id { get; }

        public bool Equals(ConstantShardKey? other) => other is not null && Id == other.Id;

        public override bool Equals(object? obj) => Equals(obj as ConstantShardKey);

        public override int GetHashCode() => 0;
    }

    private sealed class QuaternaryBaseHarness : QuaternaryBase<int, int>
    {
        private readonly QuadList<int>[] _quads =
        [
            new QuadList<int>(),
            new QuadList<int>(),
            new QuadList<int>(),
            new QuadList<int>()
        ];

        protected override IReadOnlyList<IQuad<int>> BaseQuads => _quads;

        public void EmitDirect(int[] items) => EmitBatchDirect(items, items.Length);

        public void EmitAddedFromList(IList<int> items) => EmitBatchAddedFromList(items, items?.Count ?? 0);

        public void EmitRemovedFromList(IList<int> items) => EmitBatchRemovedFromList(items, items?.Count ?? 0);

        public void EmitOwnedRemoved(int[] items) => EmitOwnedBatchRemoved(items, items.Length);

        public void EmitSingle(CacheAction action, int item) => Emit(action, item);

        public override IEnumerator<int> GetEnumerator() => Enumerable.Empty<int>().GetEnumerator();
    }
}
#endif
