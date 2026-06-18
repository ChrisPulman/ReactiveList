// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using FluentAssertions;
using ReactiveUI.Primitives.Signals;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>
/// Additional comprehensive tests for QuaternaryExtensions covering dynamic secondary index views
/// using CreateViewBySecondaryIndex with observable keys.
/// </summary>
public class QuaternaryExtensionsAdditionalTests
{
    /// <summary>Tests that CreateViewBySecondaryIndex with observable keys rebuilds when keys change.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_WithObservableKeys_RebuildsWhenKeysChange()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex("ByDepartment", e => e.Department);
        list.AddRange(
        [
            new Employee("Alice", "Engineering"),
            new Employee("Bob", "Sales"),
            new Employee("Charlie", "Engineering"),
            new Employee("Diana", "Marketing"),
            new Employee("Eve", "Sales")
        ]);

        // Verify index works directly first
        var directLookup = list.GetItemsBySecondaryIndex("ByDepartment", "Engineering").ToList();
        directLookup.Count.Should().Be(2, "direct index lookup should find 2 Engineering employees");

        // Verify ItemMatchesSecondaryIndex works
        var alice = list.First(e => e.Name == "Alice");
        list.ItemMatchesSecondaryIndex("ByDepartment", alice, "Engineering").Should().BeTrue("Alice should match Engineering");
        list.ItemMatchesSecondaryIndex("ByDepartment", alice, "Sales").Should().BeFalse("Alice should not match Sales");

        // Verify filter logic works directly on list
        var keysToMatch = new HashSet<string>(["Engineering"]);
        var filteredByFilter = list.Where(item => keysToMatch.Any(key => list.ItemMatchesSecondaryIndex("ByDepartment", item, key))).ToList();
        filteredByFilter.Count.Should().Be(2, "filter applied to list should find 2 Engineering employees");

        // Test DynamicReactiveView with a simple direct filter first
        var simpleFilterSubject = new BehaviorSignal<Func<Employee, bool>>(e => e.Department == "Engineering");
        using var simpleView = new CP.Reactive.Views.DynamicReactiveView<Employee>(list, simpleFilterSubject, TimeSpan.Zero, Sequencer.Immediate);
        simpleView.Items.Count.Should().Be(2, "DynamicReactiveView with simple filter should work");

        // Test DynamicReactiveView with ItemMatchesSecondaryIndex filter directly
        var indexFilterSubject = new BehaviorSignal<Func<Employee, bool>>(
            item => new HashSet<string>(["Engineering"]).Any(key => list.ItemMatchesSecondaryIndex("ByDepartment", item, key)));
        using var indexView = new CP.Reactive.Views.DynamicReactiveView<Employee>(list, indexFilterSubject, TimeSpan.Zero, Sequencer.Immediate);
        indexView.Items.Count.Should().Be(2, "DynamicReactiveView with ItemMatchesSecondaryIndex filter should work");

        var departmentFilter = new BehaviorSignal<string[]>(["Engineering"]);

        // Act
        using var view = list.CreateDynamicViewBySecondaryIndex("ByDepartment", departmentFilter, Sequencer.Immediate, 0);
        await Task.Delay(50);

        // Initial state - only Engineering
        view.Items.Count.Should().Be(2);
        view.Items.All(e => e.Department == "Engineering").Should().BeTrue();

        // Change to Sales
        departmentFilter.OnNext(["Sales"]);
        await Task.Delay(100);

        view.Items.Count.Should().Be(2);
        view.Items.All(e => e.Department == "Sales").Should().BeTrue();

        // Change to multiple departments
        departmentFilter.OnNext(["Engineering", "Marketing"]);
        await Task.Delay(100);

        view.Items.Count.Should().Be(3);
    }

    /// <summary>Tests that CreateViewBySecondaryIndex with observable keys handles empty key array.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_WithObservableKeys_HandlesEmptyKeyArray()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex("ByDepartment", e => e.Department);
        list.AddRange(
        [
            new Employee("Alice", "Engineering"),
            new Employee("Bob", "Sales")
        ]);

        var departmentFilter = new BehaviorSignal<string[]>(["Engineering"]);

        // Act
        using var view = list.CreateDynamicViewBySecondaryIndex("ByDepartment", departmentFilter, Sequencer.Immediate, 0);
        await Task.Delay(50);

        view.Items.Count.Should().Be(1);

        // Change to empty array
        departmentFilter.OnNext([]);
        await Task.Delay(100);

        // Assert - no items match empty filter
        view.Items.Count.Should().Be(0);
    }

    /// <summary>Tests that CreateViewBySecondaryIndex throws for null list.</summary>
    [Test]
    public void CreateViewBySecondaryIndex_ThrowsForNullList()
    {
        // Arrange
        QuaternaryList<Employee>? nullList = null;

        // Act & Assert
        var act = () => nullList!.CreateViewBySecondaryIndex("ByDepartment", "Engineering", Sequencer.Immediate);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>Tests that CreateViewBySecondaryIndex throws for null index name.</summary>
    [Test]
    public void CreateViewBySecondaryIndex_ThrowsForNullIndexName()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex("ByDepartment", e => e.Department);

        // Act & Assert
        var act = () => list.CreateViewBySecondaryIndex(null!, "Engineering", Sequencer.Immediate);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>Tests that views handle rapid key changes gracefully.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_HandlesRapidKeyChanges()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex("ByDepartment", e => e.Department);
        list.AddRange(
        [
            new Employee("Alice", "Engineering"),
            new Employee("Bob", "Sales"),
            new Employee("Charlie", "Marketing")
        ]);

        var departmentFilter = new BehaviorSignal<string[]>(["Engineering"]);

        using var view = list.CreateDynamicViewBySecondaryIndex("ByDepartment", departmentFilter, Sequencer.Immediate, 10);
        await Task.Delay(50);

        // Act - rapid changes
        departmentFilter.OnNext(["Sales"]);
        departmentFilter.OnNext(["Marketing"]);
        departmentFilter.OnNext(["Engineering"]);
        departmentFilter.OnNext(["Sales", "Marketing"]);
        await Task.Delay(200);

        // Assert - final state should be Sales and Marketing
        view.Items.Count.Should().Be(2);
    }

    /// <summary>Tests a real-world scenario of filtering employees by multiple criteria.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task RealWorldScenario_EmployeeFilteringByDepartment()
    {
        // Arrange - Company employee directory
        using var employees = new QuaternaryList<Employee>();
        employees.AddIndex("ByDepartment", e => e.Department);

        // Add initial employees
        employees.AddRange(
        [
            new Employee("Alice Smith", "Engineering"),
            new Employee("Bob Johnson", "Sales"),
            new Employee("Carol Williams", "Engineering"),
            new Employee("David Brown", "Marketing"),
            new Employee("Eve Davis", "Engineering"),
            new Employee("Frank Miller", "Sales"),
            new Employee("Grace Wilson", "HR"),
            new Employee("Henry Moore", "Marketing")
        ]);

        // UI filter selection (simulating user changing department filter)
        var selectedDepartments = new BehaviorSignal<string[]>(["Engineering"]);

        // Act - Create filtered view for UI
        using var filteredView = employees.CreateDynamicViewBySecondaryIndex(
            "ByDepartment",
            selectedDepartments,
            Sequencer.Immediate,
            50);

        await Task.Delay(100);

        // Assert initial state
        filteredView.Items.Count.Should().Be(3);
        filteredView.Items.All(e => e.Department == "Engineering").Should().BeTrue();

        // User selects "Sales" department
        selectedDepartments.OnNext(["Sales"]);
        await Task.Delay(150);

        filteredView.Items.Count.Should().Be(2);
        filteredView.Items.All(e => e.Department == "Sales").Should().BeTrue();

        // User selects multiple departments
        selectedDepartments.OnNext(["Engineering", "Marketing"]);
        await Task.Delay(150);

        filteredView.Items.Count.Should().Be(5);
    }

    /// <summary>Tests dictionary CreateViewBySecondaryIndex with single key.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task Dictionary_CreateViewBySecondaryIndex_FiltersByValueIndexKey()
    {
        // Arrange
        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex("ByStatus", o => o.Status);

        dict.Add("ORD001", new OrderInfo("ORD001", "Pending", 100m));
        dict.Add("ORD002", new OrderInfo("ORD002", "Shipped", 200m));
        dict.Add("ORD003", new OrderInfo("ORD003", "Pending", 150m));
        dict.Add("ORD004", new OrderInfo("ORD004", "Delivered", 300m));

        // Act - instance method returns SecondaryIndexReactiveView where Items are TValue directly
        using var view = dict.CreateViewBySecondaryIndex("ByStatus", "Pending", Sequencer.Immediate, 0);
        await Task.Delay(50);

        // Assert
        view.Items.Count.Should().Be(2);
        view.Items.All(order => order.Status == "Pending").Should().BeTrue();
    }

    /// <summary>Tests dictionary CreateViewBySecondaryIndex with multiple keys via extension method.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task Dictionary_CreateViewBySecondaryIndex_HandlesMultipleIndexKeys()
    {
        // Arrange
        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex("ByStatus", o => o.Status);

        dict.Add("ORD001", new OrderInfo("ORD001", "Pending", 100m));
        dict.Add("ORD002", new OrderInfo("ORD002", "Shipped", 200m));
        dict.Add("ORD003", new OrderInfo("ORD003", "Delivered", 300m));

        // Act - extension method with array returns ReactiveView<KeyValuePair>
        using var view = QuaternaryExtensions.CreateViewBySecondaryIndex(dict, "ByStatus", ["Pending", "Shipped"], Sequencer.Immediate, 0);
        await Task.Delay(50);

        // Assert
        view.Items.Count.Should().Be(2);
        view.Items.Select(kvp => kvp.Value.Status).Should().BeEquivalentTo(["Pending", "Shipped"]);
    }

    /// <summary>Tests dictionary CreateViewBySecondaryIndex with observable keys.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task Dictionary_CreateViewBySecondaryIndex_WithObservableKeys_RebuildsWhenKeysChange()
    {
        // Arrange
        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex("ByStatus", o => o.Status);

        dict.Add("ORD001", new OrderInfo("ORD001", "Pending", 100m));
        dict.Add("ORD002", new OrderInfo("ORD002", "Shipped", 200m));
        dict.Add("ORD003", new OrderInfo("ORD003", "Delivered", 300m));

        var statusFilter = new BehaviorSignal<string[]>(["Pending"]);

        // Act - extension method with observable returns DynamicReactiveView<KeyValuePair>
        using var view = QuaternaryExtensions.CreateDynamicViewBySecondaryIndex(dict, "ByStatus", statusFilter, Sequencer.Immediate, 0);
        await Task.Delay(50);

        view.Items.Count.Should().Be(1);

        // Change filter
        statusFilter.OnNext(["Shipped", "Delivered"]);
        await Task.Delay(100);

        // Assert
        view.Items.Count.Should().Be(2);
    }

    /// <summary>Tests that DynamicSecondaryIndexReactiveView initializes correctly with direct construction.</summary>
    [Test]
    public void DynamicSecondaryIndexReactiveView_DirectConstruction_InitializesCorrectly()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex("ByDepartment", e => e.Department);
        list.AddRange(
        [
            new Employee("Alice", "Engineering"),
            new Employee("Bob", "Sales"),
            new Employee("Charlie", "Engineering"),
        ]);

        // Verify direct lookup works
        var directLookup = list.GetItemsBySecondaryIndex("ByDepartment", "Engineering").ToList();
        directLookup.Count.Should().Be(2, "direct index lookup should find 2 Engineering employees");

        // Create the view directly (not through extension method)
        var keysObservable = new BehaviorSignal<string[]>(["Engineering"]);

        using var view = new CP.Reactive.Views.DynamicSecondaryIndexReactiveView<Employee, string>(
            list,
            "ByDepartment",
            keysObservable,
            Sequencer.Immediate,
            TimeSpan.Zero);

        // Assert - should have items immediately after construction
        view.Items.Count.Should().Be(2, "view should have 2 items immediately after construction");
    }

    /// <summary>Tests that CreateDynamicViewBySecondaryIndex extension method works same as direct construction.</summary>
    [Test]
    public void CreateDynamicViewBySecondaryIndex_ExtensionMethod_WorksCorrectly()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex("ByDepartment", e => e.Department);
        list.AddRange(
        [
            new Employee("Alice", "Engineering"),
            new Employee("Bob", "Sales"),
            new Employee("Charlie", "Engineering"),
        ]);

        // Use fresh observable for extension method
        var keysObservable = new BehaviorSignal<string[]>(["Engineering"]);
        using var extView = list.CreateDynamicViewBySecondaryIndex("ByDepartment", keysObservable, Sequencer.Immediate, 0);

        // Assert - should have items immediately after construction
        extView.Items.Count.Should().Be(2, "extension method should produce view with 2 items");
    }

    /// <summary>Tests that secondary-index stream filters keep clear notifications for view reset semantics.</summary>
    [Test]
    public void FilterBySecondaryIndex_ClearNotifications_ShouldPassThroughAllOverloads()
    {
        using var list = new QuaternaryList<Employee>();
        list.AddIndex("ByDepartment", static employee => employee.Department);
        list.Add(new Employee("Alice", "Engineering"));
        using var listStream = new Signal<CacheNotify<Employee>>();
        var listSingle = new List<CacheNotify<Employee>>();
        var listMultiple = new List<CacheNotify<Employee>>();
        using var listSingleSubscription = listStream
            .FilterBySecondaryIndex(list, "ByDepartment", "Engineering")
            .Subscribe(listSingle.Add);
        using var listMultipleSubscription = listStream
            .FilterBySecondaryIndex(list, "ByDepartment", "Engineering", "Sales")
            .Subscribe(listMultiple.Add);

        listStream.OnNext(new CacheNotify<Employee>(CacheAction.Cleared, default!));

        listSingle.Should().ContainSingle().Which.Action.Should().Be(CacheAction.Cleared);
        listMultiple.Should().ContainSingle().Which.Action.Should().Be(CacheAction.Cleared);

        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex("ByStatus", static order => order.Status);
        dict.Add("ORD001", new OrderInfo("ORD001", "Pending", 100m));
        using var dictStream = new Signal<CacheNotify<KeyValuePair<string, OrderInfo>>>();
        var dictSingle = new List<CacheNotify<KeyValuePair<string, OrderInfo>>>();
        var dictMultiple = new List<CacheNotify<KeyValuePair<string, OrderInfo>>>();
        using var dictSingleSubscription = dictStream
            .FilterBySecondaryIndex(dict, "ByStatus", "Pending")
            .Subscribe(dictSingle.Add);
        using var dictMultipleSubscription = dictStream
            .FilterBySecondaryIndex(dict, "ByStatus", "Pending", "Shipped")
            .Subscribe(dictMultiple.Add);

        dictStream.OnNext(new CacheNotify<KeyValuePair<string, OrderInfo>>(CacheAction.Cleared, default));

        dictSingle.Should().ContainSingle().Which.Action.Should().Be(CacheAction.Cleared);
        dictMultiple.Should().ContainSingle().Which.Action.Should().Be(CacheAction.Cleared);
    }

    /// <summary>Provides Employee.</summary>
    /// <param name="Name">The Name value.</param>
    /// <param name="Department">The Department value.</param>
    private sealed record Employee(string Name, string Department);

    /// <summary>Provides OrderInfo.</summary>
    /// <param name="OrderId">The OrderId value.</param>
    /// <param name="Status">The Status value.</param>
    /// <param name="Amount">The Amount value.</param>
    private sealed record OrderInfo(string OrderId, string Status, decimal Amount);
}
#endif
