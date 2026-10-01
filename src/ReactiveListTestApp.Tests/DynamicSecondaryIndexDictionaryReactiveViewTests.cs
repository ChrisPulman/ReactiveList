// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.Primitives.Collections;
using CP.Primitives.Views;
using ReactiveUI.Primitives.Signals;

namespace ReactiveListTestApp.Tests;

/// <summary>Verifies indexed dictionaries reconcile bursts without losing keys or updates.</summary>
public sealed class DynamicSecondaryIndexDictionaryReactiveViewTests
{
    /// <summary>The number of primary keys repeatedly replaced before dispatch.</summary>
    private const int KeyCount = 10;

    /// <summary>The number of mutations in one dispatch burst.</summary>
    private const int UpdateCount = 100;

    /// <summary>The divisor used by the secondary index.</summary>
    private const int ParityDivisor = 2;

    /// <summary>The registered secondary index name.</summary>
    private const string IndexName = "Parity";

    /// <summary>The quiet period used to coalesce dictionary invalidations.</summary>
    private static readonly TimeSpan QuietInterval = TimeSpan.FromMilliseconds(1);

    /// <summary>The maximum wait for a scheduled reconciliation.</summary>
    private static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Retains every final primary key and replacement across a queued mutation burst.</summary>
    /// <param name="throttled">Whether to coalesce dispatch using a nonzero quiet period.</param>
    /// <param name="cancellationToken">Cancels the bounded dispatch wait.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task QueuedBurst_PreservesAllCurrentKeysAndValues(bool throttled, CancellationToken cancellationToken)
    {
        using var source = new QuaternaryDictionary<int, int>();
        source.AddValueIndex(IndexName, static value => value % ParityDivisor);
        using var keys = new BehaviorSignal<int[]>([0]);
        var scheduler = new QueuedViewSequencer();
        using var view = DynamicSecondaryIndexDictionaryReactiveView<int, int>.Create(
            source,
            IndexName,
            keys,
            scheduler,
            throttled ? QuietInterval : TimeSpan.Zero);
        for (var index = 0; index < UpdateCount; index++)
        {
            source.AddOrUpdate(index % KeyCount, index * ParityDivisor);
        }

        await scheduler.WorkAvailable.WaitAsync(DispatchTimeout, cancellationToken);
        scheduler.RunAll();

        await Assert.That(view.Count).IsEqualTo(KeyCount);
        var seen = new HashSet<int>();
        foreach (var item in view.Items)
        {
            await Assert.That(seen.Add(item.Key)).IsTrue();
            await Assert.That(item.Value).IsEqualTo(source[item.Key]);
        }

        source.Clear();
        view.Refresh();
        scheduler.RunAll();
        await Assert.That(view.Items).IsEmpty();
    }
}
