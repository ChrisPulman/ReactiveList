// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using CP.Primitives.Views;
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
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
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
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<string>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.PropertySetterFieldName);
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
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
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
        using var list = new QuaternaryList<string> { "test" };
        var filterSubject = new BehaviorSignal<Func<string, bool>>(_ => true);
        ReadOnlyObservableCollection<string>? capturedItems = null;

        using var view = new DynamicReactiveView<string>(
            list,
            filterSubject,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
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
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<string>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.PropertySetterFieldName);
    }

    /// <summary>DynamicReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void DynamicReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new QuaternaryList<string> { "test" };
        var filterSubject = new BehaviorSignal<Func<string, bool>>(_ => true);

        using var view = new DynamicReactiveView<string>(
            list,
            filterSubject,
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
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
        using var list = new ReactiveList<int> { TestData.TestValueThree };
        list.Add(1);
        list.Add(TestData.TestValueTwo);
        ReadOnlyObservableCollection<int>? capturedItems = null;

        using var view = new SortedReactiveView<int>(
            list,
            Comparer<int>.Default,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
        capturedItems.Should().BeEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree], options => options.WithStrictOrdering());
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
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<int>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.PropertySetterFieldName);
    }

    /// <summary>SortedReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void SortedReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new ReactiveList<int> { TestData.TestValueThree };
        list.Add(1);
        list.Add(TestData.TestValueTwo);

        using var view = new SortedReactiveView<int>(
            list,
            Comparer<int>.Default,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
        collection.Should().BeEquivalentTo([1, TestData.TestValueTwo, TestData.TestValueThree], options => options.WithStrictOrdering());
    }

    /// <summary>FilteredReactiveView ToProperty with action setter should set property and return same instance.</summary>
    [Test]
    public void FilteredReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
        ReadOnlyObservableCollection<int>? capturedItems = null;

        using var view = new FilteredReactiveView<int>(
            list,
            x => x > TestData.TestValueTwo,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
        capturedItems.Should().BeEquivalentTo([TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
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
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<int>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.PropertySetterFieldName);
    }

    /// <summary>FilteredReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void FilteredReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);

        using var view = new FilteredReactiveView<int>(
            list,
            x => x > TestData.TestValueTwo,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
        collection.Should().BeEquivalentTo([TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
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
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var result = view.ToProperty(groups => capturedGroups = groups);

        result.Should().BeSameAs(view);
        capturedGroups.Should().BeSameAs(view.Groups);
        capturedGroups.Should().HaveCount(TestData.TestValueTwo);
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
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<ReactiveGroup<char, string>>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.PropertySetterFieldName);
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
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Groups);
        collection.Should().HaveCount(TestData.TestValueTwo);
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
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        view.Items.Should().BeSameAs(view.Groups);
    }

    /// <summary>DynamicFilteredReactiveView ToProperty with action setter should set property and return same instance.</summary>
    [Test]
    public void DynamicFilteredReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
        var filterSubject = new BehaviorSignal<Func<int, bool>>(x => x > TestData.TestValueTwo);
        ReadOnlyObservableCollection<int>? capturedItems = null;

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filterSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

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
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<int>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.PropertySetterFieldName);
    }

    /// <summary>DynamicFilteredReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void DynamicFilteredReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new ReactiveList<int>();
        list.AddRange([1, TestData.TestValueTwo, TestData.TestValueThree, TestData.TestValueFour, TestData.TestValueFive]);
        var filterSubject = new BehaviorSignal<Func<int, bool>>(x => x > TestData.TestValueTwo);

        using var view = new DynamicFilteredReactiveView<int>(
            list,
            filterSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

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
        dict.AddValueIndex<string>(TestData.CategoryPropertyName, p => p.Category);
        dict[1] = new(TestData.AliceName, "A");
        dict[TestData.TestValueTwo] = new("Bob", "B");
        dict[TestData.TestValueThree] = new("Charlie", "A");
        ReadOnlyObservableCollection<TestPerson>? capturedItems = null;

        using var view = SecondaryIndexReactiveView<int, TestPerson>.Create<string>(
            dict,
            TestData.CategoryPropertyName,
            "A",
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
        capturedItems.Should().HaveCount(TestData.TestValueTwo);
    }

    /// <summary>SecondaryIndexReactiveView ToProperty with action setter should throw when setter is null.</summary>
    [Test]
    public void SecondaryIndexReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>(TestData.CategoryPropertyName, p => p.Category);

        using var view = SecondaryIndexReactiveView<int, TestPerson>.Create<string>(
            dict,
            TestData.CategoryPropertyName,
            "A",
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<TestPerson>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.PropertySetterFieldName);
    }

    /// <summary>SecondaryIndexReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void SecondaryIndexReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>(TestData.CategoryPropertyName, p => p.Category);
        dict[1] = new(TestData.AliceName, "A");
        dict[TestData.TestValueTwo] = new("Bob", "B");

        using var view = SecondaryIndexReactiveView<int, TestPerson>.Create<string>(
            dict,
            TestData.CategoryPropertyName,
            "A",
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }

    /// <summary>DynamicSecondaryIndexReactiveView ToProperty with action setter should set property and return same instance.</summary>
    [Test]
    public void DynamicSecondaryIndexReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex<string>(TestData.CategoryPropertyName, p => p.Category);
        list.Add(new TestPerson(TestData.AliceName, "A"));
        list.Add(new TestPerson("Bob", "B"));
        var keysSubject = new BehaviorSignal<string[]>(["A"]);
        ReadOnlyObservableCollection<TestPerson>? capturedItems = null;

        using var view = new DynamicSecondaryIndexReactiveView<TestPerson, string>(
            list,
            TestData.CategoryPropertyName,
            keysSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>DynamicSecondaryIndexReactiveView ToProperty with action setter should throw when setter is null.</summary>
    [Test]
    public void DynamicSecondaryIndexReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex<string>(TestData.CategoryPropertyName, p => p.Category);
        var keysSubject = new BehaviorSignal<string[]>(["A"]);

        using var view = new DynamicSecondaryIndexReactiveView<TestPerson, string>(
            list,
            TestData.CategoryPropertyName,
            keysSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<TestPerson>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.PropertySetterFieldName);
    }

    /// <summary>DynamicSecondaryIndexReactiveView ToProperty with out parameter should set collection and return same instance.</summary>
    [Test]
    public void DynamicSecondaryIndexReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex<string>(TestData.CategoryPropertyName, p => p.Category);
        list.Add(new TestPerson(TestData.AliceName, "A"));
        var keysSubject = new BehaviorSignal<string[]>(["A"]);

        using var view = new DynamicSecondaryIndexReactiveView<TestPerson, string>(
            list,
            TestData.CategoryPropertyName,
            keysSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var result = view.ToProperty(out var collection);

        result.Should().BeSameAs(view);
        collection.Should().BeSameAs(view.Items);
    }

    /// <summary>DynamicSecondaryIndexDictionaryReactiveView ToProperty with action setter should set property and return same instance.</summary>
    [Test]
    public void DynamicSecondaryIndexDictionaryReactiveView_ToPropertyAction_ShouldSetPropertyAndReturnSameInstance()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>(TestData.CategoryPropertyName, p => p.Category);
        dict[1] = new(TestData.AliceName, "A");
        dict[TestData.TestValueTwo] = new("Bob", "B");
        var keysSubject = new BehaviorSignal<string[]>(["A"]);
        ReadOnlyObservableCollection<KeyValuePair<int, TestPerson>>? capturedItems = null;

        using var view = DynamicSecondaryIndexDictionaryReactiveView<int, TestPerson>.Create<string>(
            dict,
            TestData.CategoryPropertyName,
            keysSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var result = view.ToProperty(items => capturedItems = items);

        result.Should().BeSameAs(view);
        capturedItems.Should().BeSameAs(view.Items);
    }

    /// <summary>DynamicSecondaryIndexDictionaryReactiveView ToProperty with action setter should throw when setter is null.</summary>
    [Test]
    public void DynamicSecondaryIndexDictionaryReactiveView_ToPropertyAction_WithNullSetter_ShouldThrow()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>(TestData.CategoryPropertyName, p => p.Category);
        var keysSubject = new BehaviorSignal<string[]>(["A"]);

        using var view = DynamicSecondaryIndexDictionaryReactiveView<int, TestPerson>.Create<string>(
            dict,
            TestData.CategoryPropertyName,
            keysSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

        var act = () => view.ToProperty((Action<ReadOnlyObservableCollection<KeyValuePair<int, TestPerson>>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(TestData.PropertySetterFieldName);
    }

    /// <summary>
    /// DynamicSecondaryIndexDictionaryReactiveView ToProperty with out parameter should set collection and return same instance.
    /// </summary>
    [Test]
    public void DynamicSecondaryIndexDictionaryReactiveView_ToPropertyOut_ShouldSetCollectionAndReturnSameInstance()
    {
        using var dict = new QuaternaryDictionary<int, TestPerson>();
        dict.AddValueIndex<string>(TestData.CategoryPropertyName, p => p.Category);
        dict[1] = new(TestData.AliceName, "A");
        var keysSubject = new BehaviorSignal<string[]>(["A"]);

        using var view = DynamicSecondaryIndexDictionaryReactiveView<int, TestPerson>.Create<string>(
            dict,
            TestData.CategoryPropertyName,
            keysSubject,
            Sequencer.Immediate,
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

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
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
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
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

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
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

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
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

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
            TimeSpan.FromMilliseconds(TestData.TestValueTen));

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
            TimeSpan.FromMilliseconds(TestData.TestValueTen),
            Sequencer.Immediate);

        view.Should().BeAssignableTo<IReactiveView<DynamicReactiveView<string>, string>>();
    }

    /// <summary>Test helper record for testing person types.</summary>
    /// <param name="Name">The Name value.</param>
    /// <param name="Category">The Category value.</param>
    private sealed record TestPerson(string Name, string Category);
#endif
}
