// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK
using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive;
using CP.Reactive.Collections;
#else
using CP.Primitives;
using CP.Primitives.Collections;
#endif
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Contains unit tests for the QuaternaryExtensions class.</summary>
public class QuaternaryExtensionsTests
{
    /// <summary>The second integer value used by collection tests.</summary>
    private const int SecondCollectionValue = 2;

    /// <summary>The third integer value used by collection tests.</summary>
    private const int ThirdCollectionValue = 3;

    /// <summary>The fourth integer value used by collection tests.</summary>
    private const int FourthCollectionValue = 4;

    /// <summary>The fifth integer value used by collection tests.</summary>
    private const int FifthCollectionValue = 5;

    /// <summary>The integer value added during reactive-view tests.</summary>
    private const int AddedCollectionValue = 42;

    /// <summary>The delay used to allow throttled view updates to complete.</summary>
    private const int ViewUpdateDelayMilliseconds = 200;

    /// <summary>The first person name used by the test data.</summary>
    private const string AliceName = "Alice";

    /// <summary>The name of the secondary index that groups people by city.</summary>
    private const string CityIndexName = "ByCity";

    /// <summary>Verifies that CreateView returns a view with all items when no filter is applied.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateView_WithoutFilter_ShouldContainAllItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue]);

        using var view = list.CreateView(Sequencer.Default, throttleMs: 10);

        await Assert.That(view.Items.Count).IsEqualTo(FifthCollectionValue);
    }

    /// <summary>Verifies that CreateView with filter returns only matching items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateView_WithFilter_ShouldContainOnlyMatchingItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue]);

        using var view = list.CreateView(static x => x % SecondCollectionValue == 0, Sequencer.Default, throttleMs: 10);

        await Assert.That(view.Items.Count).IsEqualTo(SecondCollectionValue);
        await Assert.That(view.Items).Contains(SecondCollectionValue);
        await Assert.That(view.Items).Contains(FourthCollectionValue);
    }

    /// <summary>Verifies that CreateViewBySecondaryIndex filters items by the secondary index key.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_ShouldFilterByKey()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex(CityIndexName, static p => p.City);
        list.AddRange([
            new TestPerson(AliceName, "NYC"),
            new TestPerson("Bob", "LA"),
            new TestPerson("Charlie", "NYC")
        ]);

        using var view = list.CreateViewBySecondaryIndex(CityIndexName, "NYC", Sequencer.Default, throttleMs: 10);

        await Assert.That(view.Items.Count).IsEqualTo(SecondCollectionValue);
        await Assert.That(view.Items).All(static p => p.City == "NYC");
    }

    /// <summary>Verifies that CreateViewBySecondaryIndex with multiple keys includes items matching any key.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_WithMultipleKeys_ShouldIncludeAllMatches()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex(CityIndexName, static p => p.City);
        list.AddRange([
            new TestPerson(AliceName, "NYC"),
            new TestPerson("Bob", "LA"),
            new TestPerson("Charlie", "Chicago"),
            new TestPerson("Diana", "NYC")
        ]);

        using var view = list.CreateViewBySecondaryIndex(CityIndexName, ["NYC", "LA"], Sequencer.Default, throttleMs: 10);

        await Assert.That(view.Items.Count).IsEqualTo(ThirdCollectionValue);
    }

    /// <summary>Verifies that ToProperty sets the property correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ToProperty_ShouldSetProperty()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue]);

        System.Collections.ObjectModel.ReadOnlyObservableCollection<int>? result = null;
        using var view = list.CreateView(Sequencer.Default, throttleMs: 10)
            .ToProperty(x => result = x);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Count).IsEqualTo(ThirdCollectionValue);
    }

    /// <summary>Verifies that ReactiveView updates when items are added to the source list.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task ReactiveView_ShouldUpdateOnAdd()
    {
        using var list = new QuaternaryList<int>();
        using var view = list.CreateView(Sequencer.Default, throttleMs: 50);

        list.Add(AddedCollectionValue);

        // Wait for throttle + processing
        await Task.Delay(ViewUpdateDelayMilliseconds);

        await Assert.That(view.Items).HasSingleItem();
        await Assert.That(view.Items).Contains(AddedCollectionValue);
    }

    /// <summary>Verifies that ReactiveView updates when items are removed from the source list.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task ReactiveView_ShouldUpdateOnRemove()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue]);

        using var view = list.CreateView(Sequencer.Default, throttleMs: 50);

        // Initial state
        await Assert.That(view.Items.Count).IsEqualTo(ThirdCollectionValue);

        _ = list.Remove(SecondCollectionValue);

        // Wait for throttle + processing
        await Task.Delay(ViewUpdateDelayMilliseconds);

        await Assert.That(view.Items.Count).IsEqualTo(SecondCollectionValue);
        await Assert.That(view.Items).DoesNotContain(SecondCollectionValue);
    }

    /// <summary>Verifies that ReactiveView updates when RemoveRange is called.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task ReactiveView_ShouldUpdateOnRemoveRange()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange([1, SecondCollectionValue, ThirdCollectionValue, FourthCollectionValue, FifthCollectionValue]);

        using var view = list.CreateView(Sequencer.Default, throttleMs: 50);

        // Initial state
        await Assert.That(view.Items.Count).IsEqualTo(FifthCollectionValue);

        list.RemoveRange([SecondCollectionValue, FourthCollectionValue]);

        // Wait for throttle + processing
        await Task.Delay(ViewUpdateDelayMilliseconds);

        await Assert.That(view.Items.Count).IsEqualTo(ThirdCollectionValue);
        await Assert.That(view.Items).DoesNotContain(SecondCollectionValue);
        await Assert.That(view.Items).DoesNotContain(FourthCollectionValue);
    }

    /// <summary>Verifies that CreateViewBySecondaryIndex updates when new matching items are added.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_ShouldUpdateOnAdd()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex(CityIndexName, static p => p.City);
        list.Add(new(AliceName, "NYC"));

        using var view = list.CreateViewBySecondaryIndex(CityIndexName, "NYC", Sequencer.Default, throttleMs: 50);

        await Assert.That(view.Items).HasSingleItem();

        list.Add(new("Bob", "NYC"));

        // Wait for throttle + processing
        await Task.Delay(ViewUpdateDelayMilliseconds);

        await Assert.That(view.Items.Count).IsEqualTo(SecondCollectionValue);
    }

    /// <summary>Verifies that CreateViewBySecondaryIndex doesn't include non-matching items when added.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_ShouldNotIncludeNonMatchingItems()
    {
        using var list = new QuaternaryList<TestPerson>();
        list.AddIndex(CityIndexName, static p => p.City);
        list.Add(new(AliceName, "NYC"));

        using var view = list.CreateViewBySecondaryIndex(CityIndexName, "NYC", Sequencer.Default, throttleMs: 50);

        await Assert.That(view.Items).HasSingleItem();

        list.Add(new("Bob", "LA"));

        // Wait for throttle + processing
        await Task.Delay(ViewUpdateDelayMilliseconds);

        // Should still be only 1 item (Alice from NYC)
        await Assert.That(view.Items).HasSingleItem();
    }

    /// <summary>Provides TestPerson.</summary>
    /// <param name="Name">The Name value.</param>
    /// <param name="City">The City value.</param>
    private sealed record TestPerson(string Name, string City);
}
#endif
