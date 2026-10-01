// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive;
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Views;
#else
using CP.Primitives;
using CP.Primitives.Collections;
using CP.Primitives.Core;
using CP.Primitives.Views;
#endif
using ReactiveUI.Primitives.Signals;
using TUnit.Assertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>
/// Additional comprehensive tests for QuaternaryExtensions covering dynamic secondary index views
/// using CreateViewBySecondaryIndex with observable keys.
/// </summary>
public class QuaternaryExtensionsAdditionalTests
{
    /// <summary>The expected number of items in a pair.</summary>
    private const int ExpectedPairCount = 2;

    /// <summary>The expected number of items in a triple.</summary>
    private const int ExpectedTripleCount = 3;

    /// <summary>The expected number of items in five-item test data.</summary>
    private const int ExpectedFiveItems = 5;

    /// <summary>The throttle interval used when constructing test views.</summary>
    private const int ViewThrottleMilliseconds = 10;

    /// <summary>The delay used to allow an initial view update to complete.</summary>
    private const int InitialViewDelayMilliseconds = 50;

    /// <summary>The delay used to allow a filter update to complete.</summary>
    private const int FilterUpdateDelayMilliseconds = 100;

    /// <summary>The delay used to allow a selection update to complete.</summary>
    private const int SelectionUpdateDelayMilliseconds = 150;

    /// <summary>The delay used to allow throttled changes to coalesce.</summary>
    private const int CoalescingDelayMilliseconds = 200;

    /// <summary>The amount assigned to the first test order.</summary>
    private const decimal FirstOrderAmount = 100M;

    /// <summary>The amount assigned to the third test order.</summary>
    private const decimal ThirdOrderAmount = 150M;

    /// <summary>The amount assigned to the second test order.</summary>
    private const decimal SecondOrderAmount = 200M;

    /// <summary>The amount assigned to the fourth test order.</summary>
    private const decimal FourthOrderAmount = 300M;

    /// <summary>The first employee name used by the test data.</summary>
    private const string AliceName = "Alice";

    /// <summary>The third employee name used by the test data.</summary>
    private const string CharlieName = "Charlie";

    /// <summary>The name of the secondary index that groups employees by department.</summary>
    private const string DepartmentIndexName = "ByDepartment";

    /// <summary>The engineering department value used by employee tests.</summary>
    private const string EngineeringDepartment = "Engineering";

    /// <summary>The sales department value used by employee tests.</summary>
    private const string SalesDepartment = "Sales";

    /// <summary>The marketing department value used by employee tests.</summary>
    private const string MarketingDepartment = "Marketing";

    /// <summary>The name of the secondary index that groups orders by status.</summary>
    private const string StatusIndexName = "ByStatus";

    /// <summary>The pending status used by order tests.</summary>
    private const string PendingStatus = "Pending";

    /// <summary>The shipped status used by order tests.</summary>
    private const string ShippedStatus = "Shipped";

    /// <summary>The delivered status used by order tests.</summary>
    private const string DeliveredStatus = "Delivered";

    /// <summary>The identifier of the first test order.</summary>
    private const string FirstOrderId = "ORD001";

    /// <summary>The identifier of the second test order.</summary>
    private const string SecondOrderId = "ORD002";

    /// <summary>The identifier of the third test order.</summary>
    private const string ThirdOrderId = "ORD003";

    /// <summary>Tests that CreateViewBySecondaryIndex with observable keys rebuilds when keys change.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_WithObservableKeys_RebuildsWhenKeysChange()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, static e => e.Department);
        list.AddRange(
        [
            new Employee(AliceName, EngineeringDepartment),
            new Employee("Bob", SalesDepartment),
            new Employee(CharlieName, EngineeringDepartment),
            new Employee("Diana", MarketingDepartment),
            new Employee("Eve", SalesDepartment)
        ]);

        // Verify index works directly first
        var directLookup = new List<Employee>(list.GetItemsBySecondaryIndex(DepartmentIndexName, EngineeringDepartment));
        await Assert.That(directLookup.Count).IsEqualTo(ExpectedPairCount).Because("direct index lookup should find 2 Engineering employees");

        // Verify ItemMatchesSecondaryIndex works
        var alice = FindEmployeeByName(list, AliceName);
        await Assert.That(list.ItemMatchesSecondaryIndex(DepartmentIndexName, alice, EngineeringDepartment)).IsTrue().Because("Alice should match Engineering");
        await Assert.That(list.ItemMatchesSecondaryIndex(DepartmentIndexName, alice, SalesDepartment)).IsFalse().Because("Alice should not match Sales");

        // Verify filter logic works directly on list
        var keysToMatch = new HashSet<string>([EngineeringDepartment]);
        var filteredByFilter = FilterBySecondaryIndex(list, keysToMatch);
        await Assert.That(filteredByFilter.Count).IsEqualTo(ExpectedPairCount).Because("filter applied to list should find 2 Engineering employees");

        // Test DynamicReactiveView with a simple direct filter first
        var simpleFilterSubject = new BehaviorSignal<Func<Employee, bool>>(static e => e.Department == EngineeringDepartment);
        using var simpleView = new DynamicReactiveView<Employee>(list, simpleFilterSubject, TimeSpan.Zero, Sequencer.Immediate);
        await Assert.That(simpleView.Items.Count).IsEqualTo(ExpectedPairCount).Because("DynamicReactiveView with simple filter should work");

        // Test DynamicReactiveView with ItemMatchesSecondaryIndex filter directly
        var indexFilterSubject = new BehaviorSignal<Func<Employee, bool>>(
            item => list.ItemMatchesSecondaryIndex(DepartmentIndexName, item, EngineeringDepartment));
        using var indexView = new DynamicReactiveView<Employee>(list, indexFilterSubject, TimeSpan.Zero, Sequencer.Immediate);
        await Assert.That(indexView.Items.Count).IsEqualTo(ExpectedPairCount).Because("DynamicReactiveView with ItemMatchesSecondaryIndex filter should work");

        var departmentFilter = new BehaviorSignal<string[]>([EngineeringDepartment]);

        // Act
        using var view = list.CreateDynamicViewBySecondaryIndex(DepartmentIndexName, departmentFilter, Sequencer.Immediate, 0);
        await Task.Delay(InitialViewDelayMilliseconds);

        // Initial state - only Engineering
        await Assert.That(view.Items.Count).IsEqualTo(ExpectedPairCount);
        await Assert.That(AllEmployeesBelongTo(view.Items, EngineeringDepartment)).IsTrue();

        // Change to Sales
        departmentFilter.OnNext([SalesDepartment]);
        await Task.Delay(FilterUpdateDelayMilliseconds);

        await Assert.That(view.Items.Count).IsEqualTo(ExpectedPairCount);
        await Assert.That(AllEmployeesBelongTo(view.Items, SalesDepartment)).IsTrue();

        // Change to multiple departments
        departmentFilter.OnNext([EngineeringDepartment, MarketingDepartment]);
        await Task.Delay(FilterUpdateDelayMilliseconds);

        await Assert.That(view.Items.Count).IsEqualTo(ExpectedTripleCount);
    }

    /// <summary>Tests that CreateViewBySecondaryIndex with observable keys handles empty key array.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_WithObservableKeys_HandlesEmptyKeyArray()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, static e => e.Department);
        list.AddRange(
        [
            new Employee(AliceName, EngineeringDepartment),
            new Employee("Bob", SalesDepartment)
        ]);

        var departmentFilter = new BehaviorSignal<string[]>([EngineeringDepartment]);

        // Act
        using var view = list.CreateDynamicViewBySecondaryIndex(DepartmentIndexName, departmentFilter, Sequencer.Immediate, 0);
        await Task.Delay(InitialViewDelayMilliseconds);

        await Assert.That(view.Items.Count).IsEqualTo(1);

        // Change to empty array
        departmentFilter.OnNext([]);
        await Task.Delay(FilterUpdateDelayMilliseconds);

        // Assert - no items match empty filter
        await Assert.That(view.Items.Count).IsEqualTo(0);
    }

    /// <summary>Tests that CreateViewBySecondaryIndex throws for null list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_ThrowsForNullList()
    {
        // Arrange
        QuaternaryList<Employee>? nullList = null;

        // Act & Assert
        var act = () => nullList!.CreateViewBySecondaryIndex(DepartmentIndexName, EngineeringDepartment, Sequencer.Immediate);
        await Assert.That(act).Throws<ArgumentNullException>();
    }

    /// <summary>Tests that CreateViewBySecondaryIndex throws for null index name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_ThrowsForNullIndexName()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, static e => e.Department);

        // Act & Assert
        var act = () => list.CreateViewBySecondaryIndex(null!, EngineeringDepartment, Sequencer.Immediate);
        await Assert.That(act).Throws<ArgumentNullException>();
    }

    /// <summary>Tests that views handle rapid key changes gracefully.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_HandlesRapidKeyChanges()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, static e => e.Department);
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
        await Assert.That(view.Items.Count).IsEqualTo(ExpectedPairCount);
    }

    /// <summary>Tests a real-world scenario of filtering employees by multiple criteria.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task RealWorldScenario_EmployeeFilteringByDepartment()
    {
        // Arrange - Company employee directory
        using var employees = new QuaternaryList<Employee>();
        employees.AddIndex(DepartmentIndexName, static e => e.Department);

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
        await Assert.That(filteredView.Items.Count).IsEqualTo(ExpectedTripleCount);
        await Assert.That(AllEmployeesBelongTo(filteredView.Items, EngineeringDepartment)).IsTrue();

        // User selects SalesDepartment department
        selectedDepartments.OnNext([SalesDepartment]);
        await Task.Delay(SelectionUpdateDelayMilliseconds);

        await Assert.That(filteredView.Items.Count).IsEqualTo(ExpectedPairCount);
        await Assert.That(AllEmployeesBelongTo(filteredView.Items, SalesDepartment)).IsTrue();

        // User selects multiple departments
        selectedDepartments.OnNext([EngineeringDepartment, MarketingDepartment]);
        await Task.Delay(SelectionUpdateDelayMilliseconds);

        await Assert.That(filteredView.Items.Count).IsEqualTo(ExpectedFiveItems);
    }

    /// <summary>Tests dictionary CreateViewBySecondaryIndex with single key.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task Dictionary_CreateViewBySecondaryIndex_FiltersByValueIndexKey()
    {
        // Arrange
        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex(StatusIndexName, static o => o.Status);

        dict.Add(FirstOrderId, new(FirstOrderId, PendingStatus, FirstOrderAmount));
        dict.Add(SecondOrderId, new(SecondOrderId, ShippedStatus, SecondOrderAmount));
        dict.Add(ThirdOrderId, new(ThirdOrderId, PendingStatus, ThirdOrderAmount));
        dict.Add("ORD004", new("ORD004", DeliveredStatus, FourthOrderAmount));

        // Act - instance method returns SecondaryIndexReactiveView where Items are TValue directly
        using var view = dict.CreateViewBySecondaryIndex(StatusIndexName, PendingStatus, Sequencer.Immediate, 0);
        await Task.Delay(InitialViewDelayMilliseconds);

        // Assert
        await Assert.That(view.Items.Count).IsEqualTo(ExpectedPairCount);
        await Assert.That(AllOrdersHaveStatus(view.Items, PendingStatus)).IsTrue();
    }

    /// <summary>Tests dictionary CreateViewBySecondaryIndex with multiple keys via extension method.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task Dictionary_CreateViewBySecondaryIndex_HandlesMultipleIndexKeys()
    {
        // Arrange
        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex(StatusIndexName, static o => o.Status);

        dict.Add(FirstOrderId, new(FirstOrderId, PendingStatus, FirstOrderAmount));
        dict.Add(SecondOrderId, new(SecondOrderId, ShippedStatus, SecondOrderAmount));
        dict.Add(ThirdOrderId, new(ThirdOrderId, DeliveredStatus, FourthOrderAmount));

        // Act - extension method with array returns ReactiveView<KeyValuePair>
        using var view = QuaternaryExtensions.CreateViewBySecondaryIndex(dict, StatusIndexName, [PendingStatus, ShippedStatus], Sequencer.Immediate, 0);
        await Task.Delay(InitialViewDelayMilliseconds);

        // Assert
        await Assert.That(view.Items.Count).IsEqualTo(ExpectedPairCount);
        await Assert.That(GetOrderStatuses(view.Items)).IsEquivalentTo([PendingStatus, ShippedStatus]);
    }

    /// <summary>Tests dictionary CreateViewBySecondaryIndex with observable keys.</summary>
    /// <returns>A task representing the async test.</returns>
    [Test]
    public async Task Dictionary_CreateViewBySecondaryIndex_WithObservableKeys_RebuildsWhenKeysChange()
    {
        // Arrange
        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex(StatusIndexName, static o => o.Status);

        dict.Add(FirstOrderId, new(FirstOrderId, PendingStatus, FirstOrderAmount));
        dict.Add(SecondOrderId, new(SecondOrderId, ShippedStatus, SecondOrderAmount));
        dict.Add(ThirdOrderId, new(ThirdOrderId, DeliveredStatus, FourthOrderAmount));

        var statusFilter = new BehaviorSignal<string[]>([PendingStatus]);

        // Act - extension method with observable returns DynamicReactiveView<KeyValuePair>
        using var view = QuaternaryExtensions.CreateDynamicViewBySecondaryIndex(dict, StatusIndexName, statusFilter, Sequencer.Immediate, 0);
        await Task.Delay(InitialViewDelayMilliseconds);

        await Assert.That(view.Items.Count).IsEqualTo(1);

        // Change filter
        statusFilter.OnNext([ShippedStatus, DeliveredStatus]);
        await Task.Delay(FilterUpdateDelayMilliseconds);

        // Assert
        await Assert.That(view.Items.Count).IsEqualTo(ExpectedPairCount);
    }

    /// <summary>Tests that DynamicSecondaryIndexReactiveView initializes correctly with direct construction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DynamicSecondaryIndexReactiveView_DirectConstruction_InitializesCorrectly()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, static e => e.Department);
        list.AddRange(
        [
            new Employee(AliceName, EngineeringDepartment),
            new Employee("Bob", SalesDepartment),
            new Employee(CharlieName, EngineeringDepartment),
        ]);

        // Verify direct lookup works
        var directLookup = new List<Employee>(list.GetItemsBySecondaryIndex(DepartmentIndexName, EngineeringDepartment));
        await Assert.That(directLookup.Count).IsEqualTo(ExpectedPairCount).Because("direct index lookup should find 2 Engineering employees");

        // Create the view directly (not through extension method)
        var keysObservable = new BehaviorSignal<string[]>([EngineeringDepartment]);

        using var view = new DynamicSecondaryIndexReactiveView<Employee, string>(
            list,
            DepartmentIndexName,
            keysObservable,
            Sequencer.Immediate,
            TimeSpan.Zero);

        // Assert - should have items immediately after construction
        await Assert.That(view.Items.Count).IsEqualTo(ExpectedPairCount).Because("view should have 2 items immediately after construction");
    }

    /// <summary>Tests that CreateDynamicViewBySecondaryIndex extension method works same as direct construction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateDynamicViewBySecondaryIndex_ExtensionMethod_WorksCorrectly()
    {
        // Arrange
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, static e => e.Department);
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
        await Assert.That(extView.Items.Count).IsEqualTo(ExpectedPairCount).Because("extension method should produce view with 2 items");
    }

    /// <summary>Tests that secondary-index stream filters keep clear notifications for view reset semantics.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FilterBySecondaryIndex_ClearNotifications_ShouldPassThroughAllOverloads()
    {
        using var list = new QuaternaryList<Employee>();
        list.AddIndex(DepartmentIndexName, static employee => employee.Department);
        list.Add(new(AliceName, EngineeringDepartment));
        using var listStream = new Signal<CacheNotify<Employee>>();
        var listSingle = new List<CacheNotify<Employee>>();
        var listMultiple = new List<CacheNotify<Employee>>();
        using var listSingleSubscription = listStream
            .FilterBySecondaryIndex(list, DepartmentIndexName, EngineeringDepartment)
            .Subscribe(listSingle.Add);
        using var listMultipleSubscription = listStream
            .FilterBySecondaryIndex(list, DepartmentIndexName, EngineeringDepartment, SalesDepartment)
            .Subscribe(listMultiple.Add);

        listStream.OnNext(new(CacheAction.Cleared, default!));

        await Assert.That((await Assert.That(listSingle).HasSingleItem()).Action).IsEqualTo(CacheAction.Cleared);
        await Assert.That((await Assert.That(listMultiple).HasSingleItem()).Action).IsEqualTo(CacheAction.Cleared);

        using var dict = new QuaternaryDictionary<string, OrderInfo>();
        dict.AddValueIndex(StatusIndexName, static order => order.Status);
        dict.Add(FirstOrderId, new(FirstOrderId, PendingStatus, FirstOrderAmount));
        using var dictStream = new Signal<CacheNotify<KeyValuePair<string, OrderInfo>>>();
        var dictSingle = new List<CacheNotify<KeyValuePair<string, OrderInfo>>>();
        var dictMultiple = new List<CacheNotify<KeyValuePair<string, OrderInfo>>>();
        using var dictSingleSubscription = dictStream
            .FilterBySecondaryIndex(dict, StatusIndexName, PendingStatus)
            .Subscribe(dictSingle.Add);
        using var dictMultipleSubscription = dictStream
            .FilterBySecondaryIndex(dict, StatusIndexName, PendingStatus, ShippedStatus)
            .Subscribe(dictMultiple.Add);

        dictStream.OnNext(new(CacheAction.Cleared, default));

        await Assert.That((await Assert.That(dictSingle).HasSingleItem()).Action).IsEqualTo(CacheAction.Cleared);
        await Assert.That((await Assert.That(dictMultiple).HasSingleItem()).Action).IsEqualTo(CacheAction.Cleared);
    }

    /// <summary>Finds an employee by name without allocating a LINQ iterator.</summary>
    /// <param name="employees">The employees to search.</param>
    /// <param name="name">The employee name to find.</param>
    /// <returns>The matching employee.</returns>
    private static Employee FindEmployeeByName(IEnumerable<Employee> employees, string name)
    {
        foreach (var employee in employees)
        {
            if (employee.Name == name)
            {
                return employee;
            }
        }

        throw new InvalidOperationException($"Employee '{name}' was not found.");
    }

    /// <summary>Filters employees by one or more secondary-index keys using explicit iteration.</summary>
    /// <param name="list">The indexed employee list.</param>
    /// <param name="keys">The secondary-index keys to match.</param>
    /// <returns>The matching employees.</returns>
    private static List<Employee> FilterBySecondaryIndex(QuaternaryList<Employee> list, IEnumerable<string> keys)
    {
        var matches = new List<Employee>();
        foreach (var employee in list)
        {
            foreach (var key in keys)
            {
                if (!list.ItemMatchesSecondaryIndex(DepartmentIndexName, employee, key))
                {
                    continue;
                }

                matches.Add(employee);
                break;
            }
        }

        return matches;
    }

    /// <summary>Determines whether all employees belong to the requested department.</summary>
    /// <param name="employees">The employees to inspect.</param>
    /// <param name="department">The expected department.</param>
    /// <returns><see langword="true"/> when every employee belongs to the department; otherwise, <see langword="false"/>.</returns>
    private static bool AllEmployeesBelongTo(IEnumerable<Employee> employees, string department)
    {
        foreach (var employee in employees)
        {
            if (employee.Department != department)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether all orders have the requested status.</summary>
    /// <param name="orders">The orders to inspect.</param>
    /// <param name="status">The expected status.</param>
    /// <returns><see langword="true"/> when every order has the status; otherwise, <see langword="false"/>.</returns>
    private static bool AllOrdersHaveStatus(IEnumerable<OrderInfo> orders, string status)
    {
        foreach (var order in orders)
        {
            if (order.Status != status)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Collects order statuses from dictionary entries using explicit iteration.</summary>
    /// <param name="orders">The dictionary entries to inspect.</param>
    /// <returns>The order statuses.</returns>
    private static List<string> GetOrderStatuses(IEnumerable<KeyValuePair<string, OrderInfo>> orders)
    {
        var statuses = new List<string>();
        foreach (var order in orders)
        {
            statuses.Add(order.Value.Status);
        }

        return statuses;
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
