// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveListTestApp.Models;

namespace ReactiveListTestApp.Tests;

/// <summary>Verifies direction labels at the zero-change boundary.</summary>
public sealed class InstrumentSnapshotTests
{
    /// <summary>The reference price carried by the snapshot.</summary>
    private const double ReferencePrice = 70;

    /// <summary>The simulated latency carried by the snapshot.</summary>
    private const double Latency = 0.1;

    /// <summary>Treats unchanged prices as upward and negative changes as downward.</summary>
    /// <param name="change">The signed change percentage.</param>
    /// <param name="direction">The expected display label.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(-0.01, "DOWN")]
    [Arguments(0D, "UP")]
    [Arguments(0.01, "UP")]
    public async Task Direction_ChangePercent_SelectsLabel(double change, string direction)
    {
        var snapshot = new InstrumentSnapshot(1, 0, "RL01", "Energy", "LSE", ReferencePrice, change, 1, Latency, false, DateTimeOffset.UnixEpoch);
        await Assert.That(snapshot.Direction).IsEqualTo(direction);
    }
}
