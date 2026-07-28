// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;
#else
namespace CP.Primitives.Internal;
#endif
/// <summary>Provides target-framework-compatible array-pool clearing decisions.</summary>
internal static class ArrayPoolClearHelper
{
    /// <summary>Determines whether pooled arrays for <typeparamref name="T"/> should be cleared before return.</summary>
    /// <typeparam name="T">The array element type.</typeparam>
    /// <returns><see langword="true"/> when clearing is required; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsReferenceOrContainsReferences<T>() =>
#if NETFRAMEWORK
        TypeCache<T>.ContainsReferences;
#else
        RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#endif

#if NETFRAMEWORK
    /// <summary>Caches a conservative, type-safe clearing decision for each closed generic type.</summary>
    /// <remarks>
    /// Custom value types are cleared on .NET Framework because that runtime does not expose
    /// the generic runtime reference-inspection helper. This avoids inspecting private fields.
    /// </remarks>
    /// <typeparam name="T">The type to inspect.</typeparam>
    internal static class TypeCache<T>
    {
        /// <summary>Indicates whether arrays of <typeparamref name="T"/> should be cleared when returned.</summary>
        internal static readonly bool ContainsReferences =
            !typeof(T).IsValueType || Type.GetTypeCode(typeof(T)) == TypeCode.Object;
    }
#endif
}
