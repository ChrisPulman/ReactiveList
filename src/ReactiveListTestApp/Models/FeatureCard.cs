// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveListTestApp.Models;

/// <summary>Describes a ReactiveList feature and where the running app demonstrates it.</summary>
/// <param name="Name">The feature name.</param>
/// <param name="Category">The feature category.</param>
/// <param name="Description">The short feature description.</param>
/// <param name="LiveUse">How the running showcase uses the feature.</param>
internal sealed record FeatureCard(string Name, string Category, string Description, string LiveUse);
