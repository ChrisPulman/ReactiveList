// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveListTestApp.Models;

/// <summary>Represents one bounded cache-stream diagnostic entry.</summary>
/// <param name="Sequence">The diagnostic sequence.</param>
/// <param name="Time">The formatted event time.</param>
/// <param name="Source">The collection source.</param>
/// <param name="Action">The cache action.</param>
/// <param name="Count">The affected item count.</param>
/// <param name="Detail">Additional diagnostic detail.</param>
internal sealed record StreamEvent(long Sequence, string Time, string Source, string Action, int Count, string Detail);
