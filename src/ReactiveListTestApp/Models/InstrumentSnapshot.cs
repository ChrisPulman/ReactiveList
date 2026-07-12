// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveListTestApp.Models;

/// <summary>Represents an immutable instrument projection published to reactive collections.</summary>
/// <param name="Sequence">The latest raw sequence included in the snapshot.</param>
/// <param name="InstrumentId">The numeric instrument identifier.</param>
/// <param name="Symbol">The display symbol.</param>
/// <param name="Sector">The instrument sector.</param>
/// <param name="Venue">The trading venue.</param>
/// <param name="Price">The latest aggregated price.</param>
/// <param name="ChangePercent">The change from the opening reference price.</param>
/// <param name="Volume">The accumulated volume.</param>
/// <param name="LatencyMilliseconds">The simulated processing latency.</param>
/// <param name="IsAlert">Whether the snapshot crosses an alert threshold.</param>
/// <param name="UpdatedAt">The projection timestamp.</param>
internal readonly record struct InstrumentSnapshot(
    long Sequence,
    int InstrumentId,
    string Symbol,
    string Sector,
    string Venue,
    double Price,
    double ChangePercent,
    long Volume,
    double LatencyMilliseconds,
    bool IsAlert,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Gets a text direction that complements the numeric change value.</summary>
    public string Direction => ChangePercent >= 0 ? "UP" : "DOWN";
}
