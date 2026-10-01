// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveListTestApp.Tests;

/// <summary>Supplies a stable UTC timestamp for deterministic market projections.</summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    /// <summary>The timestamp shared by deterministic projections.</summary>
    private static readonly DateTimeOffset Timestamp = new(2026, 9, 30, 12, 34, 56, TimeSpan.Zero);

    /// <inheritdoc/>
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => Timestamp;
}
