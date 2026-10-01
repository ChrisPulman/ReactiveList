// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CP.Reactive;
using CP.Reactive.Collections;

namespace ReactiveList.Reactive.Test;

/// <summary>Verifies native Rx query subjects and secondary-index projections.</summary>
public sealed class QuaternaryListTests
{
    /// <summary>The named parity index shared by list projections.</summary>
    private const string ParityIndex = "Parity";

    /// <summary>The divisor shared by parity-index queries.</summary>
    private const int ParityDivisor = 2;

    /// <summary>The third test value and initial dictionary length key.</summary>
    private const int ThirdValue = 3;

    /// <summary>The highest test value and replacement length key.</summary>
    private const int FourthValue = 4;

    /// <summary>The indexed sample items.</summary>
    private static readonly int[] InitialValues = [1, ParityDivisor, ThirdValue, FourthValue];

    /// <summary>The values included by the initial minimum query.</summary>
    private static readonly int[] MinimumValues = [ParityDivisor, ThirdValue, FourthValue];

    /// <summary>The values included by the highest minimum query.</summary>
    private static readonly int[] HighestValues = [FourthValue];

    /// <summary>The values selected by the even key.</summary>
    private static readonly int[] EvenValues = [ParityDivisor, FourthValue];

    /// <summary>The values selected by the odd key.</summary>
    private static readonly int[] OddValues = [1, ThirdValue];

    /// <summary>The secondary-index keys selecting every item.</summary>
    private static readonly int[] AllKeys = [0, 1];

    /// <summary>Uses an Rx query observable to rebuild quaternary source projections.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateView_QuerySubject_SelectsMatchingItems()
    {
        using var list = new QuaternaryList<int>();
        list.AddRange(InitialValues);
        using var query = new BehaviorSubject<int>(ParityDivisor);
        using var view = list.CreateView(query, static (minimum, item) => item >= minimum, ImmediateScheduler.Instance, 0);
        await Assert.That(view.Items).IsEquivalentTo(MinimumValues);
        query.OnNext(FourthValue);
        await Assert.That(view.Items).IsEquivalentTo(HighestValues);
    }

    /// <summary>Changes included secondary-index keys using a native Rx BehaviorSubject.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateDynamicViewBySecondaryIndex_RxKeys_RebuildsListSelection()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex(ParityIndex, static value => value % ParityDivisor);
        list.AddRange(InitialValues);
        using var keys = new BehaviorSubject<int[]>([0]);
        using var view = list.CreateDynamicViewBySecondaryIndex(ParityIndex, keys, ImmediateScheduler.Instance, 0);
        await Assert.That(view.Items).IsEquivalentTo(EvenValues);
        keys.OnNext([1]);
        await Assert.That(view.Items).IsEquivalentTo(OddValues);
        keys.OnNext([]);
        await Assert.That(view.Count).IsEqualTo(0);
    }

    /// <summary>Filters dictionary values by dynamic native Rx index keys.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateDynamicViewBySecondaryIndex_RxKeys_RebuildsDictionarySelection()
    {
        using var dictionary = new QuaternaryDictionary<int, string>();
        dictionary.AddValueIndex("Length", static value => value.Length);
        dictionary.AddOrUpdate(1, "one");
        dictionary.AddOrUpdate(ParityDivisor, "four");
        dictionary.AddOrUpdate(ThirdValue, "two");
        using var keys = new BehaviorSubject<int[]>([ThirdValue]);
        using var view = dictionary.CreateDynamicViewBySecondaryIndex("Length", keys, ImmediateScheduler.Instance, 0);
        var selectedKeys = new int[view.Count];
        for (var i = 0; i < selectedKeys.Length; i++)
        {
            selectedKeys[i] = view[i].Key;
        }

        await Assert.That(selectedKeys).IsEquivalentTo(OddValues);
        keys.OnNext([FourthValue]);
        await Assert.That(view.Count).IsEqualTo(1);
        await Assert.That(view[0].Value).IsEqualTo("four");
        keys.OnNext([]);
        await Assert.That(view.Count).IsEqualTo(0);
    }

    /// <summary>Accepts Rx schedulers for static single-key and multi-key index views.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateViewBySecondaryIndex_RxScheduler_ProjectsStaticKeys()
    {
        using var list = new QuaternaryList<int>();
        list.AddIndex(ParityIndex, static value => value % ParityDivisor);
        list.AddRange(InitialValues);
        using var even = list.CreateViewBySecondaryIndex(ParityIndex, 0, ImmediateScheduler.Instance, 0);
        using var all = list.CreateViewBySecondaryIndex(ParityIndex, AllKeys, ImmediateScheduler.Instance, 0);
        await Assert.That(even.Items).IsEquivalentTo(EvenValues);
        await Assert.That(all.Items).IsEquivalentTo(InitialValues);
    }
}
