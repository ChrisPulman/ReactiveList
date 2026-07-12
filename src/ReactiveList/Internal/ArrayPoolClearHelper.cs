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
    public static bool IsReferenceOrContainsReferences<T>()
    {
#if NETFRAMEWORK
        return TypeCache<T>.ContainsReferences;
#else
        return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#endif
    }

#if NETFRAMEWORK
    /// <summary>Determines whether a type contains managed references.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="visited">The value types already visited in the current traversal.</param>
    /// <returns><see langword="true"/> when the type contains managed references; otherwise, <see langword="false"/>.</returns>
    private static bool ContainsReferencesCore(Type type, HashSet<Type> visited)
    {
        if (!type.IsValueType)
        {
            return !type.IsPointer && !type.IsByRef;
        }

        if (type.IsPrimitive || type.IsEnum || !visited.Add(type))
        {
            return false;
        }

        var fields = type.GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        for (var i = 0; i < fields.Length; i++)
        {
            if (ContainsReferencesCore(fields[i].FieldType, visited))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Caches the reflection result once for each closed generic type.</summary>
    /// <typeparam name="T">The type to inspect.</typeparam>
    private static class TypeCache<T>
    {
        public static readonly bool ContainsReferences = ContainsReferencesCore(typeof(T), []);
    }
#endif
}
