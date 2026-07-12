// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveListTestApp.Models;

/// <summary>Contains one bounded projection frame produced from a full-rate raw batch.</summary>
/// <param name="Sequence">The frame sequence.</param>
/// <param name="EventCount">The raw event count.</param>
/// <param name="TotalEvents">The cumulative event count.</param>
/// <param name="GenerationTime">The raw generation duration.</param>
/// <param name="AllocatedBytes">The bytes allocated while generating the frame.</param>
/// <param name="Snapshots">The immutable instrument projections.</param>
/// <param name="Samples">A bounded raw-event sample.</param>
/// <param name="CreatedAt">The frame creation time.</param>
internal sealed record MarketFrame(
    long Sequence,
    int EventCount,
    long TotalEvents,
    TimeSpan GenerationTime,
    long AllocatedBytes,
    InstrumentSnapshot[] Snapshots,
    MarketTick[] Samples,
    DateTimeOffset CreatedAt);
