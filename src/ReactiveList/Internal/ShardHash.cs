// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Runtime.CompilerServices;

namespace CP.Reactive.Internal;

/// <summary>
/// Provides optimized hash code calculation for sharding.
/// </summary>
internal static class ShardHash
{
    /// <summary>
    /// Computes a shard index using optimized bit manipulation.
    /// </summary>
    /// <typeparam name="T">The type of the key.</typeparam>
    /// <param name="key">The key to hash.</param>
    /// <param name="shardCount">The number of shards (must be power of 2).</param>
    /// <returns>The shard index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetShardIndex<T>(T key, int shardCount)
    {
        // Use unsigned shift to ensure positive result
        // Multiply by golden ratio to improve distribution
        var hash = key?.GetHashCode() ?? 0;
        return (int)((uint)(hash * 0x9E3779B9) >> (32 - BitCount(shardCount)));
    }

    /// <summary>
    /// Computes a shard index for a known 4-shard configuration.
    /// </summary>
    /// <typeparam name="T">The type of the key.</typeparam>
    /// <param name="key">The key to hash.</param>
    /// <returns>The shard index (0-3).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetShardIndex4<T>(T key)
    {
        // Optimized for 4 shards (2 bits needed)
        var hash = key?.GetHashCode() ?? 0;
        return (int)((uint)(hash * 0x9E3779B9) >> 30);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BitCount(int n) => System.Numerics.BitOperations.Log2((uint)n);
}
#endif
