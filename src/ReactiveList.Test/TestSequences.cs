// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace ReactiveList.Test;

/// <summary>Provides ordered sequence matching for notification tests.</summary>
internal static class TestSequences
{
    /// <summary>Determines whether the expected items occur in order within the source.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="expected">The expected subsequence.</param>
    /// <returns>Whether every expected item occurs in order.</returns>
    internal static bool ContainsInOrder<T>(IEnumerable<T> source, T[] expected)
    {
        using var enumerator = source.GetEnumerator();
        foreach (var item in expected)
        {
            var found = false;
            while (enumerator.MoveNext())
            {
                if (EqualityComparer<T>.Default.Equals(enumerator.Current, item))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }
}
