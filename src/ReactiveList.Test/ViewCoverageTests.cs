// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Views;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Tests;

/// <summary>
/// Coverage tests for reactive view implementations.
/// </summary>
public class ViewCoverageTests
{
    /// <summary>
    /// Filtered views should track update transitions and refreshes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task FilteredReactiveView_UpdateTransitions_ShouldAddRemoveReplaceAndRefresh()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 2, 3, 4 });

        using var view = new FilteredReactiveView<int>(
            list,
            static item => item % 2 == 0,
            ImmediateScheduler.Instance,
            TimeSpan.Zero);

        view.Items.Should().Equal(2, 4);
        view[0].Should().Be(2);

        list.Update(2, 5);
        await WaitForPipeline();
        view.Items.Should().Equal(4);

        list.Update(3, 6);
        await WaitForPipeline();
        view.Items.Should().Equal(4, 6);

        list.Update(4, 8);
        await WaitForPipeline();
        view.Items.Should().Equal(8, 6);

        list.Move(2, 0);
        await WaitForPipeline();
        view.Items.Should().Equal(8, 6);

        view.Refresh();
        view.ToArray().Should().Equal(8, 6);

        list.Remove(6);
        await WaitForPipeline();
        view.Items.Should().Equal(8);

        list.Clear();
        await WaitForPipeline();
        view.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Sorted views should maintain comparer order through source changes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SortedReactiveView_Changes_ShouldKeepItemsSorted()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 3, 1 });

        using var view = new SortedReactiveView<int>(
            list,
            Comparer<int>.Default,
            ImmediateScheduler.Instance,
            TimeSpan.Zero);

        view.Items.Should().Equal(1, 3);
        view[1].Should().Be(3);

        list.Add(2);
        await WaitForPipeline();
        view.Items.Should().Equal(1, 2, 3);

        list.Update(3, 0);
        await WaitForPipeline();
        view.Items.Should().Equal(0, 1, 2);

        list.Move(0, 2);
        await WaitForPipeline();
        view.Items.Should().Equal(0, 1, 2);

        list.Remove(1);
        await WaitForPipeline();
        view.Items.Should().Equal(0, 2);

        view.Refresh();
        view.Items.Should().Equal(0, 2);
    }

    /// <summary>
    /// Grouped views should expose dictionary members and update group membership.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task GroupedReactiveView_DictionarySurfaceAndUpdates_ShouldTrackGroups()
    {
        using var list = new ReactiveList<ViewItem>();
        var north = new ViewItem(1, "north");
        var south = new ViewItem(2, "south");
        list.AddRange(new[] { north, south });

        using var view = new GroupedReactiveView<ViewItem, string>(
            list,
            static item => item.Region,
            ImmediateScheduler.Instance,
            TimeSpan.Zero);

        view.Keys.Should().BeEquivalentTo(new[] { "north", "south" });
        view.Values.SelectMany(static group => group).Should().BeEquivalentTo(new[] { north, south });
        view["north"].Should().ContainSingle().Which.Should().Be(north);
        view.TryGetValue("north", out var northGroup).Should().BeTrue();
        northGroup.Should().ContainSingle().Which.Should().Be(north);
        view.TryGetValue("missing", out var missing).Should().BeFalse();
        missing.Should().BeEmpty();
        ((IEnumerable)view).Cast<KeyValuePair<string, IReadOnlyList<ViewItem>>>()
            .Should().HaveCount(2);
        view.Refresh();

        var changedScore = north with { Score = 10 };
        list.Update(north, changedScore);
        await WaitForPipeline();
        view["north"].Should().ContainSingle().Which.Should().Be(changedScore);

        var movedRegion = changedScore with { Region = "south" };
        list.Update(changedScore, movedRegion);
        await WaitForPipeline();
        view.ContainsKey("north").Should().BeFalse();
        view["south"].Should().BeEquivalentTo(new[] { south, movedRegion });

        list.Remove(south);
        await WaitForPipeline();
        view["south"].Should().ContainSingle().Which.Should().Be(movedRegion);

        list.Remove(movedRegion);
        await WaitForPipeline();
        view.Should().BeEmpty();

        list.Add(north);
        await WaitForPipeline();
        list.Clear();
        await WaitForPipeline();
        view.Should().BeEmpty();
    }

    /// <summary>
    /// Dynamic filtered views should rebuild on filter changes and track source changes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DynamicFilteredReactiveView_FilterAndSourceChanges_ShouldRebuildAndTrackTransitions()
    {
        using var list = new ReactiveList<int>();
        list.AddRange(new[] { 1, 2, 3 });
        using var filters = new BehaviorSubject<Func<int, bool>>(static item => item >= 2);

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filters,
            ImmediateScheduler.Instance,
            TimeSpan.Zero);

        await WaitForPipeline();
        view.Items.Should().Equal(2, 3);
        view[0].Should().Be(2);

        filters.OnNext(null!);
        await WaitForPipeline();
        view.Items.Should().Equal(1, 2, 3);

        filters.OnNext(static item => item % 2 == 0);
        await WaitForPipeline();
        view.Items.Should().Equal(2);

        list.Add(4);
        await WaitForPipeline();
        view.Items.Should().Equal(2, 4);

        list.Update(2, 5);
        await WaitForPipeline();
        view.Items.Should().Equal(4);

        list.Update(1, 6);
        await WaitForPipeline();
        view.Items.Should().Equal(4, 6);

        view.Refresh();
        view.Items.Should().Equal(6, 4);

        list.Update(4, 8);
        await WaitForPipeline();
        view.Items.Should().Equal(6, 8);

        list.Remove(6);
        await WaitForPipeline();
        view.Items.Should().Equal(8);

        list.Move(2, 0);
        await WaitForPipeline();
        view.Items.Should().Equal(8);

        list.Clear();
        await WaitForPipeline();
        view.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Dynamic reactive views should apply single and batch stream actions.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DynamicReactiveView_StreamActions_ShouldApplyCurrentFilterAndBatches()
    {
        using var source = new ReactiveSourceHarness<int>(new[] { 1, 2, 3 });
        using var filters = new BehaviorSubject<Func<int, bool>>(static item => item % 2 == 0);

        using var view = new DynamicReactiveView<int>(
            source,
            filters,
            TimeSpan.Zero,
            ImmediateScheduler.Instance);

        view.Items.Should().Equal(2);

        source.AddItem(4);
        source.Emit(new CacheNotify<int>(CacheAction.Added, 4));
        source.AddItem(5);
        source.Emit(new CacheNotify<int>(CacheAction.Added, 5));
        await WaitForPipeline();
        view.Items.Should().Equal(2, 4);

        source.RemoveItem(2);
        source.Emit(new CacheNotify<int>(CacheAction.Removed, 2));
        await WaitForPipeline();
        view.Items.Should().Equal(4);

        source.AddItems(new[] { 6, 7 });
        source.Emit(new CacheNotify<int>(CacheAction.BatchAdded, default, CreateBatch(6, 7)));
        await WaitForPipeline();
        view.Items.Should().Equal(4, 6);

        source.RemoveItems(new[] { 4, 6 });
        source.Emit(new CacheNotify<int>(CacheAction.BatchRemoved, default, CreateBatch(4, 6)));
        await WaitForPipeline();
        view.Items.Should().BeEmpty();

        source.ClearItems();
        source.Emit(new CacheNotify<int>(CacheAction.Cleared, default));
        await WaitForPipeline();
        view.Items.Should().BeEmpty();

        filters.OnNext(static _ => true);
        await WaitForPipeline();
        source.AddItem(9);
        source.Emit(new CacheNotify<int>(CacheAction.Added, 9));
        await WaitForPipeline();
        view.Items.Should().Equal(9);
    }

#if NET8_0_OR_GREATER || NETFRAMEWORK
    /// <summary>
    /// Secondary-index dictionary views should remove values when updates leave the index.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SecondaryIndexReactiveView_DictionaryUpdates_ShouldRemoveValuesThatLeaveTheIndex()
    {
        using var dictionary = new QuaternaryDictionary<int, ViewItem>();
        var north = new ViewItem(1, "north");
        dictionary.Add(1, north);
        dictionary.Add(2, new ViewItem(2, "south"));
        dictionary.AddValueIndex("region", static item => item.Region);

        using var view = new SecondaryIndexReactiveView<int, ViewItem, string>(
            dictionary,
            "region",
            "north",
            ImmediateScheduler.Instance,
            TimeSpan.Zero);

        view.Items.Should().ContainSingle().Which.Should().Be(north);
        view.Count.Should().Be(1);
        view[0].Should().Be(north);
        view.ToProperty(out var outCollection).Should().BeSameAs(view);
        outCollection.Should().BeSameAs(view.Items);
        view.ToProperty(collection => collection.Should().BeSameAs(view.Items)).Should().BeSameAs(view);
        view.Refresh();
        view.GetEnumerator().MoveNext().Should().BeTrue();

        dictionary.AddOrUpdate(1, north with { Region = "south" });
        await WaitForPipeline();
        view.Items.Should().BeEmpty();

        var newNorth = new ViewItem(3, "north");
        dictionary.AddOrUpdate(3, newNorth);
        await WaitForPipeline();
        view.Items.Should().ContainSingle().Which.Should().Be(newNorth);

        dictionary.Remove(3);
        await WaitForPipeline();
        view.Items.Should().BeEmpty();

        dictionary.AddOrUpdate(4, new ViewItem(4, "north"));
        await WaitForPipeline();
        dictionary.Clear();
        await WaitForPipeline();
        view.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Dynamic secondary-index views should track key changes and dictionary updates.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DynamicSecondaryIndexViews_KeyChangesAndDictionaryUpdates_ShouldTrackCurrentKeys()
    {
        using var list = new QuaternaryList<ViewItem>();
        var north = new ViewItem(1, "north");
        var south = new ViewItem(2, "south");
        list.Add(north);
        list.Add(south);
        list.AddIndex("region", static item => item.Region);
        using var listKeys = new BehaviorSubject<string[]>(new[] { "north" });

        using var listView = new DynamicSecondaryIndexReactiveView<ViewItem, string>(
            list,
            "region",
            listKeys,
            ImmediateScheduler.Instance,
            TimeSpan.Zero);

        listView.Items.Should().ContainSingle().Which.Should().Be(north);
        listView.Count.Should().Be(1);
        listView[0].Should().Be(north);
        listView.ToProperty(out var listOutCollection).Should().BeSameAs(listView);
        listOutCollection.Should().BeSameAs(listView.Items);
        listView.ToProperty(collection => collection.Should().BeSameAs(listView.Items)).Should().BeSameAs(listView);
        listView.Refresh();
        listView.GetEnumerator().MoveNext().Should().BeTrue();

        list.Remove(north);
        await WaitForPipeline();
        listView.Items.Should().BeEmpty();

        list.Add(north);
        await WaitForPipeline();
        listView.Items.Should().ContainSingle().Which.Should().Be(north);

        listKeys.OnNext(new[] { "south" });
        await WaitForPipeline();
        listView.Items.Should().ContainSingle().Which.Should().Be(south);

        var secondSouth = new ViewItem(3, "south");
        list.Add(secondSouth);
        await WaitForPipeline();
        listView.Items.Should().BeEquivalentTo(new[] { south, secondSouth });

        list.ReplaceAll(new[] { north });
        await WaitForPipeline();
        listView.Items.Should().BeEmpty();

        using var dictionary = new QuaternaryDictionary<int, ViewItem>();
        dictionary.Add(1, north);
        dictionary.Add(2, south);
        dictionary.AddValueIndex("region", static item => item.Region);
        using var dictionaryKeys = new BehaviorSubject<string[]>(new[] { "north" });

        using var dictionaryView = new DynamicSecondaryIndexDictionaryReactiveView<int, ViewItem, string>(
            dictionary,
            "region",
            dictionaryKeys,
            ImmediateScheduler.Instance,
            TimeSpan.Zero);

        dictionaryView.Items.Should().ContainSingle().Which.Should().Be(new KeyValuePair<int, ViewItem>(1, north));
        dictionaryView.Count.Should().Be(1);
        dictionaryView[0].Key.Should().Be(1);
        dictionaryView.ToProperty(out var dictionaryOutCollection).Should().BeSameAs(dictionaryView);
        dictionaryOutCollection.Should().BeSameAs(dictionaryView.Items);
        dictionaryView.ToProperty(collection => collection.Should().BeSameAs(dictionaryView.Items)).Should().BeSameAs(dictionaryView);
        dictionaryView.Refresh();
        dictionaryView.GetEnumerator().MoveNext().Should().BeTrue();

        dictionary.AddOrUpdate(1, north with { Region = "south" });
        await WaitForPipeline();
        dictionaryView.Items.Should().BeEmpty();

        dictionary.AddOrUpdate(4, new ViewItem(4, "north"));
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().Contain(4);

        dictionary.AddOrUpdate(4, new ViewItem(4, "north", Score: 10));
        await WaitForPipeline();
        dictionaryView.Items.Single(static item => item.Key == 4).Value.Score.Should().Be(10);

        dictionaryKeys.OnNext(new[] { "south" });
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().BeEquivalentTo(new[] { 1, 2 });

        dictionary.AddOrUpdate(5, new ViewItem(5, "north"));
        await WaitForPipeline();
        dictionary.AddOrUpdate(5, new ViewItem(5, "south"));
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().Contain(5);

        var thirdSouth = new ViewItem(3, "south");
        dictionary.AddOrUpdate(3, thirdSouth);
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().Contain(3);

        dictionary.Remove(1);
        await WaitForPipeline();
        dictionaryView.Items.Select(static item => item.Key).Should().NotContain(1);

        dictionary.Clear();
        await WaitForPipeline();
        dictionaryView.Items.Should().BeEmpty();
    }
#endif

    private static async Task WaitForPipeline() => await Task.Delay(30);

    private static PooledBatch<T> CreateBatch<T>(params T[] items)
    {
        var array = ArrayPool<T>.Shared.Rent(items.Length);
        Array.Copy(items, array, items.Length);
        return new PooledBatch<T>(array, items.Length);
    }

    private sealed record ViewItem(int Id, string Region, int Score = 0);

    private sealed class ReactiveSourceHarness<T> : IReactiveSource<T>
        where T : notnull
    {
        private readonly List<T> _items;
        private readonly Subject<CacheNotify<T>> _stream = new();

        public ReactiveSourceHarness(IEnumerable<T> items) => _items = new List<T>(items);

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public int Count => _items.Count;

        public bool IsDisposed { get; private set; }

        public bool IsReadOnly => false;

        public IObservable<CacheNotify<T>> Stream => _stream.AsObservable();

        public long Version { get; private set; }

        public void AddItem(T item)
        {
            _items.Add(item);
            Version++;
        }

        public void AddItems(IEnumerable<T> items)
        {
            _items.AddRange(items);
            Version++;
        }

        public void ClearItems()
        {
            _items.Clear();
            Version++;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            _stream.Dispose();
        }

        public void Emit(CacheNotify<T> notification) => _stream.OnNext(notification);

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        public void RemoveItem(T item)
        {
            _items.Remove(item);
            Version++;
        }

        public void RemoveItems(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                _items.Remove(item);
            }

            Version++;
        }

        public T[] ToArray() => _items.ToArray();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void RaiseReset() =>
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
