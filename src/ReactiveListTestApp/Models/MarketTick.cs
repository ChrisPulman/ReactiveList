// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveListTestApp.Models;

/// <summary>Represents one allocation-free raw market event in the hot ingestion lane.</summary>
/// <param name="Sequence">The monotonically increasing event sequence.</param>
/// <param name="InstrumentId">The numeric instrument identifier.</param>
/// <param name="Price">The generated trade price.</param>
/// <param name="Volume">The generated trade volume.</param>
/// <param name="TimestampTicks">The high-resolution generation timestamp.</param>
/// <param name="IsBuy">Whether the event represents buyer-initiated flow.</param>
internal readonly record struct MarketTick(long Sequence, int InstrumentId, double Price, int Volume, long TimestampTicks, bool IsBuy);
