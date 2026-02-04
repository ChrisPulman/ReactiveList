// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Views;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Tests for ToProperty methods across all reactive view types.
/// </summary>
public class ViewToPropertyTests
{
    /// <summary>
    /// ReactiveView ToProperty with action setter should set property and return same instance.
    /// </summary>
    [Fact]
    public void ReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        var subject = new Subject<CacheNotify<string>>();
        ReadOnlyObservableCollection<string>? capturedItems = null;

        using var view = new ReactiveView<string>(
            subject,
            ["test"],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>
    /// ReactiveView ToProperty with action setter should throw when setter is null.
    /// </summary>
    [Fact]
    public void ReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<string>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>
    /// ReactiveView ToProperty with out parameter should set collection and return same instance.
    /// </summary>
    [Fact]
    public void ReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["test"],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// DynamicReactiveView ToProperty with action setter should set property and return same instance.
    /// </summary>
    [Fact]
    public void DynamicReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new QuaternaryList<string>();
        list.Add("test");
        var filterSubject = new BehaviorSubject<Func<string, bool>>(_ => true);
        ReadOnlyObservableCollection<string>? capturedItems = null;

        using var view = new DynamicReactiveView<string>(
            list,
            filterSubject,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>
    /// DynamicReactiveView ToProperty with action setter should throw when setter is null.
    /// </summary>
    [Fact]
    public void DynamicReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var list = new QuaternaryList<string>();
        var filterSubject = new BehaviorSubject<Func<string, bool>>(_ => true);

        using var view = new DynamicReactiveView<string>(
            list,
            filterSubject,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<string>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>
    /// DynamicReactiveView ToProperty with out parameter should set collection and return same instance.
    /// </summary>
    [Fact]
    public void DynamicReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new QuaternaryList<string>();
        list.Add("test");
        var filterSubject = new BehaviorSubject<Func<string, bool>>(_ => true);

        using var view = new DynamicReactiveView<string>(
            list,
            filterSubject,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }
#endif

    /// <summary>
    /// SortedReactiveView ToProperty with action setter should set property and return same instance.
    /// </summary>
    [Fact]
    public void SortedReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.Add(3);
        list.Add(1);
        list.Add(2);
        ReadOnlyObservableCollection<int>? capturedItems = null;

        using var view = new SortedReactiveView<int>(
            list,
            Comparer<int>.Default,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
        capturedItems.Should().BeEquivalentTo([1, 2, 3], options => options.WithStrictOrdering());
    }

    /// <summary>
    /// SortedReactiveView ToProperty with action setter should throw when setter is null.
    /// </summary>
    [Fact]
    public void SortedReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var list = new ReactiveList<int>();

        using var view = new SortedReactiveView<int>(
            list,
            Comparer<int>.Default,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<int>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>
    /// SortedReactiveView ToProperty with out parameter should set collection and return same instance.
    /// </summary>
    [Fact]
    public void SortedReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.Add(3);
        list.Add(1);
        list.Add(2);

        using var view = new SortedReactiveView<int>(
            list,
            Comparer<int>.Default,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
        collection.Should().BeEquivalentTo([1, 2, 3], options => options.WithStrictOrdering());
    }

    /// <summary>
    /// FilteredReactiveView ToProperty with action setter should set property and return same instance.
    /// </summary>
    [Fact]
    public void FilteredReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([1, 2, 3, 4, 5]);
        ReadOnlyObservableCollection<int>? capturedItems = null;

        using var view = new FilteredReactiveView<int>(
            list,
            x => x > 2,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
        capturedItems.Should().BeEquivalentTo([3, 4, 5]);
    }

    /// <summary>
    /// FilteredReactiveView ToProperty with action setter should throw when setter is null.
    /// </summary>
    [Fact]
    public void FilteredReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var list = new ReactiveList<int>();

        using var view = new FilteredReactiveView<int>(
            list,
            _ => true,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<int>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>
    /// FilteredReactiveView ToProperty with out parameter should set collection and return same instance.
    /// </summary>
    [Fact]
    public void FilteredReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([1, 2, 3, 4, 5]);

        using var view = new FilteredReactiveView<int>(
            list,
            x => x > 2,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
        collection.Should().BeEquivalentTo([3, 4, 5]);
    }

    /// <summary>
    /// GroupedReactiveView ToProperty with action setter should set property and return same instance.
    /// </summary>
    [Fact]
    public void GroupedReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new ReactiveList<string>();
        list.AddRange(["apple", "banana", "apricot"]);
        ReadOnlyObservableCollection<ReactiveGroup<char, string>>? capturedGroups = null;

        using var view = new GroupedReactiveView<string, char>(
            list,
            s => s[0],
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(groups => capturedGroups = groups);

        result.Should().BeSameAs(view);
        capturedGroups.Should().BeSameAs(view.Groups);
        capturedGroups.Should().HaveCount(2);
    }

    /// <summary>
    /// GroupedReactiveView ToProperty with action setter should throw when setter is null.
    /// </summary>
    [Fact]
    public void GroupedReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var list = new ReactiveList<string>();

        using var view = new GroupedReactiveView<string, char>(
            list,
            s => s[0],
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<ReactiveGroup<char, string>>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>
    /// GroupedReactiveView ToProperty with out parameter should set collection and return same instance.
    /// </summary>
    [Fact]
    public void GroupedReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new ReactiveList<string>();
        list.AddRange(["apple", "banana", "apricot"]);

        using var view = new GroupedReactiveView<string, char>(
            list,
            s => s[0],
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Groups);
        collection.Should().HaveCount(2);
    }

    /// <summary>
    /// GroupedReactiveView Items property should be same as Groups property.
    /// </summary>
    [Fact]
    public void GroupedReactiveView_Items_ShouldBeSameAsGroups()
    {
        using var list = new ReactiveList<string>();

        using var view = new GroupedReactiveView<string, char>(
            list,
            s => s[0],
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        view.Items.Should().BeSameAs(view.Groups);
    }

    /// <summary>
    /// DynamicFilteredReactiveView ToProperty with action setter should set property and return same instance.
    /// </summary>
    [Fact]
    public void DynamicFilteredReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([1, 2, 3, 4, 5]);
        var filterSubject = new BehaviorSubject<Func<int, bool>>(x => x > 2);
        ReadOnlyObservableCollection<int>? capturedItems = null;

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filterSubject,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>
    /// DynamicFilteredReactiveView ToProperty with action setter should throw when setter is null.
    /// </summary>
    [Fact]
    public void DynamicFilteredReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var list = new ReactiveList<int>();
        var filterSubject = new BehaviorSubject<Func<int, bool>>(_ => true);

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filterSubject,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<int>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>
    /// DynamicFilteredReactiveView ToProperty with out parameter should set collection and return same instance.
    /// </summary>
    [Fact]
    public void DynamicFilteredReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([1, 2, 3, 4, 5]);
        var filterSubject = new BehaviorSubject<Func<int, bool>>(x => x > 2);

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filterSubject,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// SecondaryIndexReactiveView ToProperty with action setter should set property and return same instance.
    /// </summary>
    [Fact]
    public void SecondaryIndexReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>("Category", p => p.Category);
        dict[1] = new TestPerson("Alice", "A");
        dict[2] = new TestPerson("Bob", "B");
        dict[3] = new TestPerson("Charlie", "A");
        ReadOnlyObservableCollection<TestPerson>? capturedItems = null;

        using var view = new SecondaryIndexReactiveView<int, TestPerson, string>(
            dict,
            "Category",
            "A",
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
        capturedItems.Should().HaveCount(2);
    }

    /// <summary>
    /// SecondaryIndexReactiveView ToProperty with action setter should throw when setter is null.
    /// </summary>
    [Fact]
    public void SecondaryIndexReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>("Category", p => p.Category);

        using var view = new SecondaryIndexReactiveView<int, TestPerson, string>(
            dict,
            "Category",
            "A",
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<TestPerson>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>
    /// SecondaryIndexReactiveView ToProperty with out parameter should set collection and return same instance.
    /// </summary>
    [Fact]
    public void SecondaryIndexReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>("Category", p => p.Category);
        dict[1] = new TestPerson("Alice", "A");
        dict[2] = new TestPerson("Bob", "B");

        using var view = new SecondaryIndexReactiveView<int, TestPerson, string>(
            dict,
            "Category",
            "A",
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }

    /// <summary>
    /// DynamicSecondaryIndexReactiveView ToProperty with action setter should set property and return same instance.
    /// </summary>
    [Fact]
    public void DynamicSecondaryIndexReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex<string>("Category", p => p.Category);
        list.Add(new TestPerson("Alice", "A"));
        list.Add(new TestPerson("Bob", "B"));
        var keysSubject = new BehaviorSubject<string[]>(["A"]);
        ReadOnlyObservableCollection<TestPerson>? capturedItems = null;

        using var view = new DynamicSecondaryIndexReactiveView<TestPerson, string>(
            list,
            "Category",
            keysSubject,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>
    /// DynamicSecondaryIndexReactiveView ToProperty with action setter should throw when setter is null.
    /// </summary>
    [Fact]
    public void DynamicSecondaryIndexReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex<string>("Category", p => p.Category);
        var keysSubject = new BehaviorSubject<string[]>(["A"]);

        using var view = new DynamicSecondaryIndexReactiveView<TestPerson, string>(
            list,
            "Category",
            keysSubject,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<TestPerson>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>
    /// DynamicSecondaryIndexReactiveView ToProperty with out parameter should set collection and return same instance.
    /// </summary>
    [Fact]
    public void DynamicSecondaryIndexReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex<string>("Category", p => p.Category);
        list.Add(new TestPerson("Alice", "A"));
        var keysSubject = new BehaviorSubject<string[]>(["A"]);

        using var view = new DynamicSecondaryIndexReactiveView<TestPerson, string>(
            list,
            "Category",
            keysSubject,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }

    /// <summary>
    /// DynamicSecondaryIndexDictionaryReactiveView ToProperty with action setter should set property and return same instance.
    /// </summary>
    [Fact]
    public void DynamicSecondaryIndexDictionaryReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>("Category", p => p.Category);
        dict[1] = new TestPerson("Alice", "A");
        dict[2] = new TestPerson("Bob", "B");
        var keysSubject = new BehaviorSubject<string[]>(["A"]);
        ReadOnlyObservableCollection<KeyValuePair<int, TestPerson>>? capturedItems = null;

        using var view = new DynamicSecondaryIndexDictionaryReactiveView<int, TestPerson, string>(
            dict,
            "Category",
            keysSubject,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>
    /// DynamicSecondaryIndexDictionaryReactiveView ToProperty with action setter should throw when setter is null.
    /// </summary>
    [Fact]
    public void DynamicSecondaryIndexDictionaryReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>("Category", p => p.Category);
        var keysSubject = new BehaviorSubject<string[]>(["A"]);

        using var view = new DynamicSecondaryIndexDictionaryReactiveView<int, TestPerson, string>(
            dict,
            "Category",
            keysSubject,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<KeyValuePair<int, TestPerson>>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>
    /// DynamicSecondaryIndexDictionaryReactiveView ToProperty with out parameter should set collection and return same instance.
    /// </summary>
    [Fact]
    public void DynamicSecondaryIndexDictionaryReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>("Category", p => p.Category);
        dict[1] = new TestPerson("Alice", "A");
        var keysSubject = new BehaviorSubject<string[]>(["A"]);

        using var view = new DynamicSecondaryIndexDictionaryReactiveView<int, TestPerson, string>(
            dict,
            "Category",
            keysSubject,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }
#endif

    /// <summary>
    /// All views should implement IReactiveView interface.
    /// </summary>
    [Fact]
    public void AllViews_ShouldImplementIReactiveViewInterface()
    {
        var subject = new Subject<CacheNotify<string>>();

        using var reactiveView = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        reactiveView.Should().BeAssignableTo<IReactiveView<ReactiveView<string>, string>>();
    }

    /// <summary>
    /// SortedReactiveView should implement IReactiveView interface.
    /// </summary>
    [Fact]
    public void SortedReactiveView_ShouldImplementIReactiveViewInterface()
    {
        using var list = new ReactiveList<int>();

        using var view = new SortedReactiveView<int>(
            list,
            Comparer<int>.Default,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        view.Should().BeAssignableTo<IReactiveView<SortedReactiveView<int>, int>>();
    }

    /// <summary>
    /// FilteredReactiveView should implement IReactiveView interface.
    /// </summary>
    [Fact]
    public void FilteredReactiveView_ShouldImplementIReactiveViewInterface()
    {
        using var list = new ReactiveList<int>();

        using var view = new FilteredReactiveView<int>(
            list,
            _ => true,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        view.Should().BeAssignableTo<IReactiveView<FilteredReactiveView<int>, int>>();
    }

    /// <summary>
    /// GroupedReactiveView should implement IReactiveView interface.
    /// </summary>
    [Fact]
    public void GroupedReactiveView_ShouldImplementIReactiveViewInterface()
    {
        using var list = new ReactiveList<string>();

        using var view = new GroupedReactiveView<string, char>(
            list,
            s => s[0],
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        view.Should().BeAssignableTo<IReactiveView<GroupedReactiveView<string, char>, ReactiveGroup<char, string>>>();
    }

    /// <summary>
    /// DynamicFilteredReactiveView should implement IReactiveView interface.
    /// </summary>
    [Fact]
    public void DynamicFilteredReactiveView_ShouldImplementIReactiveViewInterface()
    {
        using var list = new ReactiveList<int>();
        var filterSubject = new BehaviorSubject<Func<int, bool>>(_ => true);

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filterSubject,
            ImmediateScheduler.Instance,
            TimeSpan.FromMilliseconds(10));

        view.Should().BeAssignableTo<IReactiveView<DynamicFilteredReactiveView<int>, int>>();
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// DynamicReactiveView should implement IReactiveView interface.
    /// </summary>
    [Fact]
    public void DynamicReactiveView_ShouldImplementIReactiveViewInterface()
    {
        using var list = new QuaternaryList<string>();
        var filterSubject = new BehaviorSubject<Func<string, bool>>(_ => true);

        using var view = new DynamicReactiveView<string>(
            list,
            filterSubject,
            TimeSpan.FromMilliseconds(10),
            ImmediateScheduler.Instance);

        view.Should().BeAssignableTo<IReactiveView<DynamicReactiveView<string>, string>>();
    }

    /// <summary>
    /// Test helper record for testing person types.
    /// </summary>
    private record TestPerson(string Name, string Category);
#endif
}
