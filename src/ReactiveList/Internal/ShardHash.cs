// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;
#else
namespace CP.Primitives.Internal;
#endif
/// <summary>Provides optimized hash code calculation for sharding.</summary>
internal static class ShardHash
{
    private const int HashBitCount = 32;

    /// <summary>Computes a shard index using optimized bit manipulation.</summary>
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
        return (int)((uint)(hash * 0x9E3779B9) >> (HashBitCount - BitCount(shardCount)));
    }

    /// <summary>Computes a shard index for a known 4-shard configuration.</summary>
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

    /// <summary>Gets the bit count required to represent the shard count as an unsigned power-of-two range.</summary>
    /// <param name="n">The input shard count.</param>
    /// <returns>The bit count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BitCount(int n) => BitOperationsCompat.Log2((uint)n);
}
