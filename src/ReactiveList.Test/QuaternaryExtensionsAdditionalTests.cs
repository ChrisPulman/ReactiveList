// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
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
    private const int ExpectedPairCount = 2;

    private const int ExpectedTripleCount = 3;

    private const int ExpectedFiveItems = 5;

    private const int ViewThrottleMilliseconds = 10;

    private const int InitialViewDelayMilliseconds = 50;

    private const int FilterUpdateDelayMilliseconds = 100;

    private const int SelectionUpdateDelayMilliseconds = 150;

    private const int CoalescingDelayMilliseconds = 200;

    private const decimal FirstOrderAmount = 100m;

    private const decimal ThirdOrderAmount = 150m;

    private const decimal SecondOrderAmount = 200m;

    private const decimal FourthOrderAmount = 300m;

    private const string AliceName = "Alice";

    private const string CharlieName = "Charlie";

    private const string DepartmentIndexName = "ByDepartment";

    private const string EngineeringDepartment = "Engineering";

    private const string SalesDepartment = "Sales";

    private const string MarketingDepartment = "Marketing";

    private const string StatusIndexName = "ByStatus";

    private const string PendingStatus = "Pending";

    private const string ShippedStatus = "Shipped";

    private const string DeliveredStatus = "Delivered";

    private const string FirstOrderId = "ORD001";

    private const string SecondOrderId = "ORD002";

    private const string ThirdOrderId = "ORD003";

    /// <summary>Tests that CreateViewBySecondaryIndex with observable keys rebuilds when keys change.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_WithObservableKeys_RebuildsWhenKeysChange()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, e => e.Department);
        list.AddRange(
        [
            new Employee(AliceName, EngineeringDepartment),
            new Employee("Bob", SalesDepartment),
            new Employee(CharlieName, EngineeringDepartment),
            new Employee("Diana", MarketingDepartment),
            new Employee("Eve", SalesDepartment)
        ]);

        // Verify index works directly first
        var directLookup = list.GetItemsBySecondaryIndex(DepartmentIndexName, EngineeringDepartment).ToList();
        directLookup.Count.Should().Be(ExpectedPairCount, "direct index lookup should find 2 Engineering employees");

        // Verify ItemMatchesSecondaryIndex works
        var alice = list.First(e => e.Name == AliceName);
        list.ItemMatchesSecondaryIndex(DepartmentIndexName, alice, EngineeringDepartment).Should().BeTrue("Alice should match Engineering");
        list.ItemMatchesSecondaryIndex(DepartmentIndexName, alice, SalesDepartment).Should().BeFalse("Alice should not match Sales");

        // Verify filter logic works directly on list
        var keysToMatch = new HashSet<string>([EngineeringDepartment]);
        var filteredByFilter = list.Where(item => keysToMatch.Any(key => list.ItemMatchesSecondaryIndex(DepartmentIndexName, item, key))).ToList();
        filteredByFilter.Count.Should().Be(ExpectedPairCount, "filter applied to list should find 2 Engineering employees");

        // Test DynamicReactiveView with a simple direct filter first
        var simpleFilterSubject = new BehaviorSignal<Func<Employee, bool>>(e => e.Department == EngineeringDepartment);
        using var simpleView = new CP.Primitives.Views.DynamicReactiveView<Employee>(list, simpleFilterSubject, TimeSpan.Zero, Sequencer.Immediate);
        simpleView.Items.Count.Should().Be(ExpectedPairCount, "DynamicReactiveView with simple filter should work");

        // Test DynamicReactiveView with ItemMatchesSecondaryIndex filter directly
        var indexFilterSubject = new BehaviorSignal<Func<Employee, bool>>(
            item => new HashSet<string>([EngineeringDepartment]).Any(key => list.ItemMatchesSecondaryIndex(DepartmentIndexName, item, key)));
        using var indexView = new CP.Primitives.Views.DynamicReactiveView<Employee>(list, indexFilterSubject, TimeSpan.Zero, Sequencer.Immediate);
        indexView.Items.Count.Should().Be(ExpectedPairCount, "DynamicReactiveView with ItemMatchesSecondaryIndex filter should work");

        var departmentFilter = new BehaviorSignal<string[]>([EngineeringDepartment]);

        // Act
        using var view = list.CreateDynamicViewBySecondaryIndex(DepartmentIndexName, departmentFilter, Sequencer.Immediate, 0);
        await Task.Delay(InitialViewDelayMilliseconds);

        // Initial state - only Engineering
        view.Items.Count.Should().Be(ExpectedPairCount);
        view.Items.All(e => e.Department == EngineeringDepartment).Should().BeTrue();

        // Change to Sales
        departmentFilter.OnNext([SalesDepartment]);
        await Task.Delay(FilterUpdateDelayMilliseconds);

        view.Items.Count.Should().Be(ExpectedPairCount);
        view.Items.All(e => e.Department == SalesDepartment).Should().BeTrue();

        // Change to multiple departments
        departmentFilter.OnNext([EngineeringDepartment, MarketingDepartment]);
        await Task.Delay(FilterUpdateDelayMilliseconds);

        view.Items.Count.Should().Be(ExpectedTripleCount);
    }

    /// <summary>Tests that CreateViewBySecondaryIndex with observable keys handles empty key array.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_WithObservableKeys_HandlesEmptyKeyArray()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, e => e.Department);
        list.AddRange(
        [
            new Employee(AliceName, EngineeringDepartment),
            new Employee("Bob", SalesDepartment)
        ]);

        var departmentFilter = new BehaviorSignal<string[]>([EngineeringDepartment]);

        // Act
        using var view = list.CreateDynamicViewBySecondaryIndex(DepartmentIndexName, departmentFilter, Sequencer.Immediate, 0);
        await Task.Delay(InitialViewDelayMilliseconds);

        view.Items.Count.Should().Be(1);

        // Change to empty array
        departmentFilter.OnNext([]);
        await Task.Delay(FilterUpdateDelayMilliseconds);

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
        var act = () => nullList!.CreateViewBySecondaryIndex(DepartmentIndexName, EngineeringDepartment, Sequencer.Immediate);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>Tests that CreateViewBySecondaryIndex throws for null index name.</summary>
    [Test]
    public void CreateViewBySecondaryIndex_ThrowsForNullIndexName()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, e => e.Department);

        // Act & Assert
        var act = () => list.CreateViewBySecondaryIndex(null!, EngineeringDepartment, Sequencer.Immediate);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>Tests that views handle rapid key changes gracefully.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_HandlesRapidKeyChanges()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, e => e.Department);
        list.AddRange(
        [
            new Employee(AliceName, EngineeringDepartment),
            new Employee("Bob", SalesDepartment),
            new Employee(CharlieName, MarketingDepartment)
        ]);

        var departmentFilter = new BehaviorSignal<string[]>([EngineeringDepartment]);

        using var view = list.CreateDynamicViewBySecondaryIndex(DepartmentIndexName, departmentFilter, Sequencer.Immediate, ViewThrottleMilliseconds);
        await Task.Delay(InitialViewDelayMilliseconds);

        // Act - rapid changes
        departmentFilter.OnNext([SalesDepartment]);
        departmentFilter.OnNext([MarketingDepartment]);
        departmentFilter.OnNext([EngineeringDepartment]);
        departmentFilter.OnNext([SalesDepartment, MarketingDepartment]);
        await Task.Delay(CoalescingDelayMilliseconds);

        // Assert - final state should be Sales and Marketing
        view.Items.Count.Should().Be(ExpectedPairCount);
    }

    /// <summary>Tests a real-world scenario of filtering employees by multiple criteria.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task RealWorldScenario_EmployeeFilteringByDepartment()
    {
        // Arrange - Company employee directory
        using var employees = new QuaternaryList<Employee>();
        employees.AddIndex(DepartmentIndexName, e => e.Department);

        // Add initial employees
        employees.AddRange(
        [
            new Employee("Alice Smith", EngineeringDepartment),
            new Employee("Bob Johnson", SalesDepartment),
            new Employee("Carol Williams", EngineeringDepartment),
            new Employee("David Brown", MarketingDepartment),
            new Employee("Eve Davis", EngineeringDepartment),
            new Employee("Frank Miller", SalesDepartment),
            new Employee("Grace Wilson", "HR"),
            new Employee("Henry Moore", MarketingDepartment)
        ]);

        // UI filter selection (simulating user changing department filter)
        var selectedDepartments = new BehaviorSignal<string[]>([EngineeringDepartment]);

        // Act - Create filtered view for UI
        using var filteredView = employees.CreateDynamicViewBySecondaryIndex(
            DepartmentIndexName,
            selectedDepartments,
            Sequencer.Immediate,
            InitialViewDelayMilliseconds);

        await Task.Delay(FilterUpdateDelayMilliseconds);

        // Assert initial state
        filteredView.Items.Count.Should().Be(ExpectedTripleCount);
        filteredView.Items.All(e => e.Department == EngineeringDepartment).Should().BeTrue();

        // User selects SalesDepartment department
        selectedDepartments.OnNext([SalesDepartment]);
        await Task.Delay(SelectionUpdateDelayMilliseconds);

        filteredView.Items.Count.Should().Be(ExpectedPairCount);
        filteredView.Items.All(e => e.Department == SalesDepartment).Should().BeTrue();

        // User selects multiple departments
        selectedDepartments.OnNext([EngineeringDepartment, MarketingDepartment]);
        await Task.Delay(SelectionUpdateDelayMilliseconds);

        filteredView.Items.Count.Should().Be(ExpectedFiveItems);
    }

    /// <summary>Tests dictionary CreateViewBySecondaryIndex with single key.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task Dictionary_CreateViewBySecondaryIndex_FiltersByValueIndexKey()
    {
        // Arrange
        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex(StatusIndexName, o => o.Status);

        dict.Add(FirstOrderId, new OrderInfo(FirstOrderId, PendingStatus, FirstOrderAmount));
        dict.Add(SecondOrderId, new OrderInfo(SecondOrderId, ShippedStatus, SecondOrderAmount));
        dict.Add(ThirdOrderId, new OrderInfo(ThirdOrderId, PendingStatus, ThirdOrderAmount));
        dict.Add("ORD004", new OrderInfo("ORD004", DeliveredStatus, FourthOrderAmount));

        // Act - instance method returns SecondaryIndexReactiveView where Items are TValue directly
        using var view = dict.CreateViewBySecondaryIndex(StatusIndexName, PendingStatus, Sequencer.Immediate, 0);
        await Task.Delay(InitialViewDelayMilliseconds);

        // Assert
        view.Items.Count.Should().Be(ExpectedPairCount);
        view.Items.All(order => order.Status == PendingStatus).Should().BeTrue();
    }

    /// <summary>Tests dictionary CreateViewBySecondaryIndex with multiple keys via extension method.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task Dictionary_CreateViewBySecondaryIndex_HandlesMultipleIndexKeys()
    {
        // Arrange
        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex(StatusIndexName, o => o.Status);

        dict.Add(FirstOrderId, new OrderInfo(FirstOrderId, PendingStatus, FirstOrderAmount));
        dict.Add(SecondOrderId, new OrderInfo(SecondOrderId, ShippedStatus, SecondOrderAmount));
        dict.Add(ThirdOrderId, new OrderInfo(ThirdOrderId, DeliveredStatus, FourthOrderAmount));

        // Act - extension method with array returns ReactiveView<KeyValuePair>
        using var view = QuaternaryExtensions.CreateViewBySecondaryIndex(dict, StatusIndexName, [PendingStatus, ShippedStatus], Sequencer.Immediate, 0);
        await Task.Delay(InitialViewDelayMilliseconds);

        // Assert
        view.Items.Count.Should().Be(ExpectedPairCount);
        view.Items.Select(kvp => kvp.Value.Status).Should().BeEquivalentTo([PendingStatus, ShippedStatus]);
    }

    /// <summary>Tests dictionary CreateViewBySecondaryIndex with observable keys.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task Dictionary_CreateViewBySecondaryIndex_WithObservableKeys_RebuildsWhenKeysChange()
    {
        // Arrange
        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex(StatusIndexName, o => o.Status);

        dict.Add(FirstOrderId, new OrderInfo(FirstOrderId, PendingStatus, FirstOrderAmount));
        dict.Add(SecondOrderId, new OrderInfo(SecondOrderId, ShippedStatus, SecondOrderAmount));
        dict.Add(ThirdOrderId, new OrderInfo(ThirdOrderId, DeliveredStatus, FourthOrderAmount));

        var statusFilter = new BehaviorSignal<string[]>([PendingStatus]);

        // Act - extension method with observable returns DynamicReactiveView<KeyValuePair>
        using var view = QuaternaryExtensions.CreateDynamicViewBySecondaryIndex(dict, StatusIndexName, statusFilter, Sequencer.Immediate, 0);
        await Task.Delay(InitialViewDelayMilliseconds);

        view.Items.Count.Should().Be(1);

        // Change filter
        statusFilter.OnNext([ShippedStatus, DeliveredStatus]);
        await Task.Delay(FilterUpdateDelayMilliseconds);

        // Assert
        view.Items.Count.Should().Be(ExpectedPairCount);
    }

    /// <summary>Tests that DynamicSecondaryIndexReactiveView initializes correctly with direct construction.</summary>
    [Test]
    public void DynamicSecondaryIndexReactiveView_DirectConstruction_InitializesCorrectly()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, e => e.Department);
        list.AddRange(
        [
            new Employee(AliceName, EngineeringDepartment),
            new Employee("Bob", SalesDepartment),
            new Employee(CharlieName, EngineeringDepartment),
        ]);

        // Verify direct lookup works
        var directLookup = list.GetItemsBySecondaryIndex(DepartmentIndexName, EngineeringDepartment).ToList();
        directLookup.Count.Should().Be(ExpectedPairCount, "direct index lookup should find 2 Engineering employees");

        // Create the view directly (not through extension method)
        var keysObservable = new BehaviorSignal<string[]>([EngineeringDepartment]);

        using var view = new CP.Primitives.Views.DynamicSecondaryIndexReactiveView<Employee, string>(
            list,
            DepartmentIndexName,
            keysObservable,
            Sequencer.Immediate,
            TimeSpan.Zero);

        // Assert - should have items immediately after construction
        view.Items.Count.Should().Be(ExpectedPairCount, "view should have 2 items immediately after construction");
    }

    /// <summary>Tests that CreateDynamicViewBySecondaryIndex extension method works same as direct construction.</summary>
    [Test]
    public void CreateDynamicViewBySecondaryIndex_ExtensionMethod_WorksCorrectly()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, e => e.Department);
        list.AddRange(
        [
            new Employee(AliceName, EngineeringDepartment),
            new Employee("Bob", SalesDepartment),
            new Employee(CharlieName, EngineeringDepartment),
        ]);

        // Use fresh observable for extension method
        var keysObservable = new BehaviorSignal<string[]>([EngineeringDepartment]);
        using var extView = list.CreateDynamicViewBySecondaryIndex(DepartmentIndexName, keysObservable, Sequencer.Immediate, 0);

        // Assert - should have items immediately after construction
        extView.Items.Count.Should().Be(ExpectedPairCount, "extension method should produce view with 2 items");
    }

    /// <summary>Tests that secondary-index stream filters keep clear notifications for view reset semantics.</summary>
    [Test]
    public void FilterBySecondaryIndex_ClearNotifications_ShouldPassThroughAllOverloads()
    {
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, static employee => employee.Department);
        list.Add(new Employee(AliceName, EngineeringDepartment));
        using var listStream = new Signal<CacheNotify<Employee>>();
        var listSingle = new List<CacheNotify<Employee>>();
        var listMultiple = new List<CacheNotify<Employee>>();
        using var listSingleSubscription = listStream
            .FilterBySecondaryIndex(list, DepartmentIndexName, EngineeringDepartment)
            .Subscribe(listSingle.Add);
        using var listMultipleSubscription = listStream
            .FilterBySecondaryIndex(list, DepartmentIndexName, EngineeringDepartment, SalesDepartment)
            .Subscribe(listMultiple.Add);

        listStream.OnNext(new CacheNotify<Employee>(CacheAction.Cleared, default!));

        listSingle.Should().ContainSingle().Which.Action.Should().Be(CacheAction.Cleared);
        listMultiple.Should().ContainSingle().Which.Action.Should().Be(CacheAction.Cleared);

        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex(StatusIndexName, static order => order.Status);
        dict.Add(FirstOrderId, new OrderInfo(FirstOrderId, PendingStatus, FirstOrderAmount));
        using var dictStream = new Signal<CacheNotify<KeyValuePair<string, OrderInfo>>>();
        var dictSingle = new List<CacheNotify<KeyValuePair<string, OrderInfo>>>();
        var dictMultiple = new List<CacheNotify<KeyValuePair<string, OrderInfo>>>();
        using var dictSingleSubscription = dictStream
            .FilterBySecondaryIndex(dict, StatusIndexName, PendingStatus)
            .Subscribe(dictSingle.Add);
        using var dictMultipleSubscription = dictStream
            .FilterBySecondaryIndex(dict, StatusIndexName, PendingStatus, ShippedStatus)
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
