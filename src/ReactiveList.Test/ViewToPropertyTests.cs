// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Views;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for ToProperty methods across all reactive view types.</summary>
public class ViewToPropertyTests
{
    /// <summary>ReactiveView ToProperty with action setter should set property and return same instance.</summary>
    [Test]
    public void ReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        var subject = new Signal<CacheNotify<string>>();
        ReadOnlyObservableCollection<string>? capturedItems = null;

        using var view = new ReactiveView<string>(
            subject,
            ["test"],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            Sequencer.Immediate);

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>ReactiveView ToProperty with action setter should throw when setter is null.</summary>
    [Test]
    public void ReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            Sequencer.Immediate);

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<string>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>ReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void ReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var view = new ReactiveView<string>(
            subject,
            ["test"],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            Sequencer.Immediate);

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }

#if NET8_0_OR_GREATER || NETFRAMEWORK
    /// <summary>DynamicReactiveView ToProperty with action setter should set property and return same instance.</summary>
    [Test]
    public void DynamicReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new QuaternaryList<string>();
        list.Add("test");
        var filterSubject = new BehaviorSignal<Func<string, bool>>(_ => true);
        ReadOnlyObservableCollection<string>? capturedItems = null;

        using var view = new DynamicReactiveView<string>(
            list,
            filterSubject,
            TimeSpan.FromMilliseconds(10),
            Sequencer.Immediate);

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>DynamicReactiveView ToProperty with action setter should throw when setter is null.</summary>
    [Test]
    public void DynamicReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var list = new QuaternaryList<string>();
        var filterSubject = new BehaviorSignal<Func<string, bool>>(_ => true);

        using var view = new DynamicReactiveView<string>(
            list,
            filterSubject,
            TimeSpan.FromMilliseconds(10),
            Sequencer.Immediate);

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<string>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>DynamicReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void DynamicReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new QuaternaryList<string>();
        list.Add("test");
        var filterSubject = new BehaviorSignal<Func<string, bool>>(_ => true);

        using var view = new DynamicReactiveView<string>(
            list,
            filterSubject,
            TimeSpan.FromMilliseconds(10),
            Sequencer.Immediate);

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }
#endif

    /// <summary>SortedReactiveView ToProperty with action setter should set property and return same instance.</summary>
    [Test]
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
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
        capturedItems.Should().BeEquivalentTo([1, 2, 3], options => options.WithStrictOrdering());
    }

    /// <summary>SortedReactiveView ToProperty with action setter should throw when setter is null.</summary>
    [Test]
    public void SortedReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var list = new ReactiveList<int>();

        using var view = new SortedReactiveView<int>(
            list,
            Comparer<int>.Default,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<int>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>SortedReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void SortedReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.Add(3);
        list.Add(1);
        list.Add(2);

        using var view = new SortedReactiveView<int>(
            list,
            Comparer<int>.Default,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
        collection.Should().BeEquivalentTo([1, 2, 3], options => options.WithStrictOrdering());
    }

    /// <summary>FilteredReactiveView ToProperty with action setter should set property and return same instance.</summary>
    [Test]
    public void FilteredReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([1, 2, 3, 4, 5]);
        ReadOnlyObservableCollection<int>? capturedItems = null;

        using var view = new FilteredReactiveView<int>(
            list,
            x => x > 2,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
        capturedItems.Should().BeEquivalentTo([3, 4, 5]);
    }

    /// <summary>FilteredReactiveView ToProperty with action setter should throw when setter is null.</summary>
    [Test]
    public void FilteredReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var list = new ReactiveList<int>();

        using var view = new FilteredReactiveView<int>(
            list,
            _ => true,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<int>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>FilteredReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void FilteredReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([1, 2, 3, 4, 5]);

        using var view = new FilteredReactiveView<int>(
            list,
            x => x > 2,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
        collection.Should().BeEquivalentTo([3, 4, 5]);
    }

    /// <summary>GroupedReactiveView ToProperty with action setter should set property and return same instance.</summary>
    [Test]
    public void GroupedReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new ReactiveList<string>();
        list.AddRange(["apple", "banana", "apricot"]);
        ReadOnlyObservableCollection<ReactiveGroup<char, string>>? capturedGroups = null;

        using var view = new GroupedReactiveView<string, char>(
            list,
            s => s[0],
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(groups => capturedGroups = groups);

        result.Should().BeSameAs(view);
        capturedGroups.Should().BeSameAs(view.Groups);
        capturedGroups.Should().HaveCount(2);
    }

    /// <summary>GroupedReactiveView ToProperty with action setter should throw when setter is null.</summary>
    [Test]
    public void GroupedReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var list = new ReactiveList<string>();

        using var view = new GroupedReactiveView<string, char>(
            list,
            s => s[0],
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<ReactiveGroup<char, string>>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>GroupedReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void GroupedReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new ReactiveList<string>();
        list.AddRange(["apple", "banana", "apricot"]);

        using var view = new GroupedReactiveView<string, char>(
            list,
            s => s[0],
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Groups);
        collection.Should().HaveCount(2);
    }

    /// <summary>GroupedReactiveView Items property should be same as Groups property.</summary>
    [Test]
    public void GroupedReactiveView_Items_ShouldBeSameAsGroups()
    {
        using var list = new ReactiveList<string>();

        using var view = new GroupedReactiveView<string, char>(
            list,
            s => s[0],
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        view.Items.Should().BeSameAs(view.Groups);
    }

    /// <summary>DynamicFilteredReactiveView ToProperty with action setter should set property and return same instance.</summary>
    [Test]
    public void DynamicFilteredReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([1, 2, 3, 4, 5]);
        var filterSubject = new BehaviorSignal<Func<int, bool>>(x => x > 2);
        ReadOnlyObservableCollection<int>? capturedItems = null;

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filterSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>DynamicFilteredReactiveView ToProperty with action setter should throw when setter is null.</summary>
    [Test]
    public void DynamicFilteredReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var list = new ReactiveList<int>();
        var filterSubject = new BehaviorSignal<Func<int, bool>>(_ => true);

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filterSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<int>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>DynamicFilteredReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void DynamicFilteredReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([1, 2, 3, 4, 5]);
        var filterSubject = new BehaviorSignal<Func<int, bool>>(x => x > 2);

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filterSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }

#if NET8_0_OR_GREATER || NETFRAMEWORK
    /// <summary>SecondaryIndexReactiveView ToProperty with action setter should set property and return same instance.</summary>
    [Test]
    public void SecondaryIndexReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>("Category", p => p.Category);
        dict[1] = new TestPerson("Alice", "A");
        dict[2] = new TestPerson("Bob", "B");
        dict[3] = new TestPerson("Charlie", "A");
        ReadOnlyObservableCollection<TestPerson>? capturedItems = null;

        using var view = SecondaryIndexReactiveView<int, TestPerson>.Create<string>(
            dict,
            "Category",
            "A",
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
        capturedItems.Should().HaveCount(2);
    }

    /// <summary>SecondaryIndexReactiveView ToProperty with action setter should throw when setter is null.</summary>
    [Test]
    public void SecondaryIndexReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>("Category", p => p.Category);

        using var view = SecondaryIndexReactiveView<int, TestPerson>.Create<string>(
            dict,
            "Category",
            "A",
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<TestPerson>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>SecondaryIndexReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void SecondaryIndexReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>("Category", p => p.Category);
        dict[1] = new TestPerson("Alice", "A");
        dict[2] = new TestPerson("Bob", "B");

        using var view = SecondaryIndexReactiveView<int, TestPerson>.Create<string>(
            dict,
            "Category",
            "A",
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }

    /// <summary>DynamicSecondaryIndexReactiveView ToProperty with action setter should set property and return same instance.</summary>
    [Test]
    public void DynamicSecondaryIndexReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex<string>("Category", p => p.Category);
        list.Add(new TestPerson("Alice", "A"));
        list.Add(new TestPerson("Bob", "B"));
        var keysSubject = new BehaviorSignal<string[]>(["A"]);
        ReadOnlyObservableCollection<TestPerson>? capturedItems = null;

        using var view = new DynamicSecondaryIndexReactiveView<TestPerson, string>(
            list,
            "Category",
            keysSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>DynamicSecondaryIndexReactiveView ToProperty with action setter should throw when setter is null.</summary>
    [Test]
    public void DynamicSecondaryIndexReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex<string>("Category", p => p.Category);
        var keysSubject = new BehaviorSignal<string[]>(["A"]);

        using var view = new DynamicSecondaryIndexReactiveView<TestPerson, string>(
            list,
            "Category",
            keysSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<TestPerson>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>DynamicSecondaryIndexReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void DynamicSecondaryIndexReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex<string>("Category", p => p.Category);
        list.Add(new TestPerson("Alice", "A"));
        var keysSubject = new BehaviorSignal<string[]>(["A"]);

        using var view = new DynamicSecondaryIndexReactiveView<TestPerson, string>(
            list,
            "Category",
            keysSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }

    /// <summary>DynamicSecondaryIndexDictionaryReactiveView ToProperty with action setter should set property and return same instance.</summary>
    [Test]
    public void DynamicSecondaryIndexDictionaryReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>("Category", p => p.Category);
        dict[1] = new TestPerson("Alice", "A");
        dict[2] = new TestPerson("Bob", "B");
        var keysSubject = new BehaviorSignal<string[]>(["A"]);
        ReadOnlyObservableCollection<KeyValuePair<int, TestPerson>>? capturedItems = null;

        using var view = DynamicSecondaryIndexDictionaryReactiveView<int, TestPerson>.Create<string>(
            dict,
            "Category",
            keysSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>DynamicSecondaryIndexDictionaryReactiveView ToProperty with action setter should throw when setter is null.</summary>
    [Test]
    public void DynamicSecondaryIndexDictionaryReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>("Category", p => p.Category);
        var keysSubject = new BehaviorSignal<string[]>(["A"]);

        using var view = DynamicSecondaryIndexDictionaryReactiveView<int, TestPerson>.Create<string>(
            dict,
            "Category",
            keysSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<KeyValuePair<int, TestPerson>>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertySetter");
    }

    /// <summary>
    /// DynamicSecondaryIndexDictionaryReactiveView ToProperty with out parameter should set collection and return same instance.
    /// </summary>
    [Test]
    public void DynamicSecondaryIndexDictionaryReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>("Category", p => p.Category);
        dict[1] = new TestPerson("Alice", "A");
        var keysSubject = new BehaviorSignal<string[]>(["A"]);

        using var view = DynamicSecondaryIndexDictionaryReactiveView<int, TestPerson>.Create<string>(
            dict,
            "Category",
            keysSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }
#endif

    /// <summary>All views should implement IReactiveView interface.</summary>
    [Test]
    public void AllViews_ShouldImplementIReactiveViewInterface()
    {
        var subject = new Signal<CacheNotify<string>>();

        using var reactiveView = new ReactiveView<string>(
            subject,
            [],
            _ => true,
            TimeSpan.FromMilliseconds(10),
            Sequencer.Immediate);

        reactiveView.Should().BeAssignableTo<IReactiveView<ReactiveView<string>, string>>();
    }

    /// <summary>SortedReactiveView should implement IReactiveView interface.</summary>
    [Test]
    public void SortedReactiveView_ShouldImplementIReactiveViewInterface()
    {
        using var list = new ReactiveList<int>();

        using var view = new SortedReactiveView<int>(
            list,
            Comparer<int>.Default,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        view.Should().BeAssignableTo<IReactiveView<SortedReactiveView<int>, int>>();
    }

    /// <summary>FilteredReactiveView should implement IReactiveView interface.</summary>
    [Test]
    public void FilteredReactiveView_ShouldImplementIReactiveViewInterface()
    {
        using var list = new ReactiveList<int>();

        using var view = new FilteredReactiveView<int>(
            list,
            _ => true,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        view.Should().BeAssignableTo<IReactiveView<FilteredReactiveView<int>, int>>();
    }

    /// <summary>GroupedReactiveView should implement IReactiveView interface.</summary>
    [Test]
    public void GroupedReactiveView_ShouldImplementIReactiveViewInterface()
    {
        using var list = new ReactiveList<string>();

        using var view = new GroupedReactiveView<string, char>(
            list,
            s => s[0],
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        view.Should().BeAssignableTo<IReactiveView<GroupedReactiveView<string, char>, ReactiveGroup<char, string>>>();
    }

    /// <summary>DynamicFilteredReactiveView should implement IReactiveView interface.</summary>
    [Test]
    public void DynamicFilteredReactiveView_ShouldImplementIReactiveViewInterface()
    {
        using var list = new ReactiveList<int>();
        var filterSubject = new BehaviorSignal<Func<int, bool>>(_ => true);

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filterSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(10));

        view.Should().BeAssignableTo<IReactiveView<DynamicFilteredReactiveView<int>, int>>();
    }

#if NET8_0_OR_GREATER || NETFRAMEWORK
    /// <summary>DynamicReactiveView should implement IReactiveView interface.</summary>
    [Test]
    public void DynamicReactiveView_ShouldImplementIReactiveViewInterface()
    {
        using var list = new QuaternaryList<string>();
        var filterSubject = new BehaviorSignal<Func<string, bool>>(_ => true);

        using var view = new DynamicReactiveView<string>(
            list,
            filterSubject,
            TimeSpan.FromMilliseconds(10),
            Sequencer.Immediate);

        view.Should().BeAssignableTo<IReactiveView<DynamicReactiveView<string>, string>>();
    }

    /// <summary>Test helper record for testing person types.</summary>
    /// <param name="Name">The Name value.</param>
    /// <param name="Category">The Category value.</param>
    private sealed record TestPerson(string Name, string Category);
#endif
}
