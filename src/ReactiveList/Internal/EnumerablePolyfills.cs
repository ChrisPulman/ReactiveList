// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if NET462
using System.Collections.Generic;

namespace System.Linq;

/// <summary>Provides LINQ helpers missing on .NET Framework.</summary>
internal static class EnumerablePolyfills
{
    /// <summary>Creates a hash set from a sequence.</summary>
    /// <typeparam name="TSource">The element type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <returns>A hash set containing the source elements.</returns>
    public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source) => new(source);
}
#endif
