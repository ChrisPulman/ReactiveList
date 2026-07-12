// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveListTestApp.Models;

/// <summary>Represents a display projection of one Reactive2DList row.</summary>
/// <param name="Lane">The logical venue lane.</param>
/// <param name="Values">The formatted row values.</param>
/// <param name="Average">The row average.</param>
/// <param name="Minimum">The row minimum.</param>
/// <param name="Maximum">The row maximum.</param>
internal sealed record MatrixRow(string Lane, string Values, double Average, double Minimum, double Maximum);
