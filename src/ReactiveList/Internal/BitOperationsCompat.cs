// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace CP.Reactive.Internal;

/// <summary>Provides bit operations used by pooled collection sizing.</summary>
internal static class BitOperationsCompat
{
    /// <summary>Returns the integer base-2 logarithm of a non-zero unsigned value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The integer base-2 logarithm.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Log2(uint value)
    {
#if NETFRAMEWORK
        var result = 0;
        while ((value >>= 1) != 0)
        {
            result++;
        }

        return result;
#else
        return System.Numerics.BitOperations.Log2(value);
#endif
    }

    /// <summary>Rounds a value up to the next power of two.</summary>
    /// <param name="value">The value to round.</param>
    /// <returns>The rounded power-of-two value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint RoundUpToPowerOf2(uint value)
    {
#if NETFRAMEWORK
        if (value <= 1)
        {
            return 1;
        }

        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
#else
        return System.Numerics.BitOperations.RoundUpToPowerOf2(value);
#endif
    }
}
