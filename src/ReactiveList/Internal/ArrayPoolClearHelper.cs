// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace CP.Reactive.Internal;

/// <summary>Provides target-framework-compatible array-pool clearing decisions.</summary>
internal static class ArrayPoolClearHelper
{
    /// <summary>Determines whether pooled arrays for <typeparamref name="T"/> should be cleared before return.</summary>
    /// <typeparam name="T">The array element type.</typeparam>
    /// <returns><see langword="true"/> when clearing is required; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsReferenceOrContainsReferences<T>()
    {
#if NETFRAMEWORK
        return !typeof(T).IsValueType;
#else
        return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#endif
    }
}
