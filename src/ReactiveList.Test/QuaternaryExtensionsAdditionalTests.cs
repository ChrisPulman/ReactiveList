// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CP.Reactive;
using CP.Reactive.Collections;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Additional comprehensive tests for QuaternaryExtensions covering dynamic secondary index views
/// using CreateViewBySecondaryIndex with observable keys.
/// </summary>
public class QuaternaryExtensionsAdditionalTests
{
    /// <summary>
    /// Tests that CreateViewBySecondaryIndex with observable keys rebuilds when keys change.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
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
        var simpleFilterSubject = new BehaviorSubject<Func<Employee, bool>>(e => e.Department == "Engineering");
        using var simpleView = new CP.Reactive.Views.DynamicReactiveView<Employee>(list, simpleFilterSubject, TimeSpan.Zero, ImmediateScheduler.Instance);
        simpleView.Items.Count.Should().Be(2, "DynamicReactiveView with simple filter should work");

        // Test DynamicReactiveView with ItemMatchesSecondaryIndex filter directly
        var indexFilterSubject = new BehaviorSubject<Func<Employee, bool>>(
            item => new HashSet<string>(["Engineering"]).Any(key => list.ItemMatchesSecondaryIndex("ByDepartment", item, key)));
        using var indexView = new CP.Reactive.Views.DynamicReactiveView<Employee>(list, indexFilterSubject, TimeSpan.Zero, ImmediateScheduler.Instance);
        indexView.Items.Count.Should().Be(2, "DynamicReactiveView with ItemMatchesSecondaryIndex filter should work");

        var departmentFilter = new BehaviorSubject<string[]>(new[] { "Engineering" });

        // Act
        using var view = list.CreateDynamicViewBySecondaryIndex("ByDepartment", departmentFilter, ImmediateScheduler.Instance, 0);
        await Task.Delay(50);

        // Initial state - only Engineering
        view.Items.Count.Should().Be(2);
        view.Items.All(e => e.Department == "Engineering").Should().BeTrue();

        // Change to Sales
        departmentFilter.OnNext(new[] { "Sales" });
        await Task.Delay(100);

        view.Items.Count.Should().Be(2);
        view.Items.All(e => e.Department == "Sales").Should().BeTrue();

        // Change to multiple departments
        departmentFilter.OnNext(new[] { "Engineering", "Marketing" });
        await Task.Delay(100);

        view.Items.Count.Should().Be(3);
    }

    /// <summary>
    /// Tests that CreateViewBySecondaryIndex with observable keys handles empty key array.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
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

        var departmentFilter = new BehaviorSubject<string[]>(new[] { "Engineering" });

        // Act
        using var view = list.CreateDynamicViewBySecondaryIndex("ByDepartment", departmentFilter, ImmediateScheduler.Instance, 0);
        await Task.Delay(50);

        view.Items.Count.Should().Be(1);

        // Change to empty array
        departmentFilter.OnNext(Array.Empty<string>());
        await Task.Delay(100);

        // Assert - no items match empty filter
        view.Items.Count.Should().Be(0);
    }

    /// <summary>
    /// Tests that CreateViewBySecondaryIndex throws for null list.
    /// </summary>
    [Fact]
    public void CreateViewBySecondaryIndex_ThrowsForNullList()
    {
        // Arrange
        QuaternaryList<Employee>? nullList = null;

        // Act & Assert
        var act = () => nullList!.CreateViewBySecondaryIndex("ByDepartment", "Engineering", ImmediateScheduler.Instance);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that CreateViewBySecondaryIndex throws for null index name.
    /// </summary>
    [Fact]
    public void CreateViewBySecondaryIndex_ThrowsForNullIndexName()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex("ByDepartment", e => e.Department);

        // Act & Assert
        var act = () => list.CreateViewBySecondaryIndex(null!, "Engineering", ImmediateScheduler.Instance);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that views handle rapid key changes gracefully.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
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

        var departmentFilter = new BehaviorSubject<string[]>(new[] { "Engineering" });

        using var view = list.CreateDynamicViewBySecondaryIndex("ByDepartment", departmentFilter, ImmediateScheduler.Instance, 10);
        await Task.Delay(50);

        // Act - rapid changes
        departmentFilter.OnNext(new[] { "Sales" });
        departmentFilter.OnNext(new[] { "Marketing" });
        departmentFilter.OnNext(new[] { "Engineering" });
        departmentFilter.OnNext(new[] { "Sales", "Marketing" });
        await Task.Delay(200);

        // Assert - final state should be Sales and Marketing
        view.Items.Count.Should().Be(2);
    }

    /// <summary>
    /// Tests a real-world scenario of filtering employees by multiple criteria.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
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
        var selectedDepartments = new BehaviorSubject<string[]>(new[] { "Engineering" });

        // Act - Create filtered view for UI
        using var filteredView = employees.CreateDynamicViewBySecondaryIndex(
            "ByDepartment",
            selectedDepartments,
            ImmediateScheduler.Instance,
            50);

        await Task.Delay(100);

        // Assert initial state
        filteredView.Items.Count.Should().Be(3);
        filteredView.Items.All(e => e.Department == "Engineering").Should().BeTrue();

        // User selects "Sales" department
        selectedDepartments.OnNext(new[] { "Sales" });
        await Task.Delay(150);

        filteredView.Items.Count.Should().Be(2);
        filteredView.Items.All(e => e.Department == "Sales").Should().BeTrue();

        // User selects multiple departments
        selectedDepartments.OnNext(new[] { "Engineering", "Marketing" });
        await Task.Delay(150);

        filteredView.Items.Count.Should().Be(5);
    }

    /// <summary>
    /// Tests dictionary CreateViewBySecondaryIndex with single key.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
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
        using var view = dict.CreateViewBySecondaryIndex("ByStatus", "Pending", ImmediateScheduler.Instance, 0);
        await Task.Delay(50);

        // Assert
        view.Items.Count.Should().Be(2);
        view.Items.All(order => order.Status == "Pending").Should().BeTrue();
    }

    /// <summary>
    /// Tests dictionary CreateViewBySecondaryIndex with multiple keys via extension method.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
    public async Task Dictionary_CreateViewBySecondaryIndex_HandlesMultipleIndexKeys()
    {
        // Arrange
        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex("ByStatus", o => o.Status);

        dict.Add("ORD001", new OrderInfo("ORD001", "Pending", 100m));
        dict.Add("ORD002", new OrderInfo("ORD002", "Shipped", 200m));
        dict.Add("ORD003", new OrderInfo("ORD003", "Delivered", 300m));

        // Act - extension method with array returns ReactiveView<KeyValuePair>
        using var view = QuaternaryExtensions.CreateViewBySecondaryIndex(dict, "ByStatus", new[] { "Pending", "Shipped" }, ImmediateScheduler.Instance, 0);
        await Task.Delay(50);

        // Assert
        view.Items.Count.Should().Be(2);
        view.Items.Select(kvp => kvp.Value.Status).Should().BeEquivalentTo(new[] { "Pending", "Shipped" });
    }

    /// <summary>
    /// Tests dictionary CreateViewBySecondaryIndex with observable keys.
    /// </summary>
    /// <returns>A task representing the async test.</returns>
    [Fact]
    public async Task Dictionary_CreateViewBySecondaryIndex_WithObservableKeys_RebuildsWhenKeysChange()
    {
        // Arrange
        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex("ByStatus", o => o.Status);

        dict.Add("ORD001", new OrderInfo("ORD001", "Pending", 100m));
        dict.Add("ORD002", new OrderInfo("ORD002", "Shipped", 200m));
        dict.Add("ORD003", new OrderInfo("ORD003", "Delivered", 300m));

        var statusFilter = new BehaviorSubject<string[]>(new[] { "Pending" });

        // Act - extension method with observable returns DynamicReactiveView<KeyValuePair>
        using var view = QuaternaryExtensions.CreateDynamicViewBySecondaryIndex(dict, "ByStatus", statusFilter, ImmediateScheduler.Instance, 0);
        await Task.Delay(50);

        view.Items.Count.Should().Be(1);

        // Change filter
        statusFilter.OnNext(new[] { "Shipped", "Delivered" });
        await Task.Delay(100);

        // Assert
        view.Items.Count.Should().Be(2);
    }

    /// <summary>
    /// Tests that DynamicSecondaryIndexReactiveView initializes correctly with direct construction.
    /// </summary>
    [Fact]
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
        var keysObservable = new BehaviorSubject<string[]>(["Engineering"]);

        using var view = new CP.Reactive.Views.DynamicSecondaryIndexReactiveView<Employee, string>(
            list,
            "ByDepartment",
            keysObservable,
            ImmediateScheduler.Instance,
            TimeSpan.Zero);

        // Assert - should have items immediately after construction
        view.Items.Count.Should().Be(2, "view should have 2 items immediately after construction");
    }

    /// <summary>
    /// Tests that CreateDynamicViewBySecondaryIndex extension method works same as direct construction.
    /// </summary>
    [Fact]
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
        var keysObservable = new BehaviorSubject<string[]>(["Engineering"]);
        using var extView = list.CreateDynamicViewBySecondaryIndex("ByDepartment", keysObservable, ImmediateScheduler.Instance, 0);

        // Assert - should have items immediately after construction
        extView.Items.Count.Should().Be(2, "extension method should produce view with 2 items");
    }

    private record Employee(string Name, string Department);

    private record OrderInfo(string OrderId, string Status, decimal Amount);
}
#endif
