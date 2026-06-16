// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK

using System.Runtime.CompilerServices;

namespace CP.Reactive.Internal;

/// <summary>Represents a change token for tracking collection modifications with minimal allocations.</summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
internal readonly record struct ChangeToken<T>
{
    /// <summary>Initializes a new instance of the <see cref="ChangeToken{T}"/> struct.</summary>
    /// <param name="version">The version number.</param>
    /// <param name="count">The item count.</param>
    public ChangeToken(long version, int count)
    {
        Version = version;
        Count = count;
    }

    /// <summary>Gets the version number of the collection when this token was created.</summary>
    public long Version { get; }

    /// <summary>Gets the count of items when this token was created.</summary>
    public int Count { get; }

    /// <summary>Determines whether the collection has changed since this token was created.</summary>
    /// <param name="currentVersion">The current version of the collection.</param>
    /// <returns>true if the collection has changed; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool HasChanged(long currentVersion) => Version != currentVersion;
}
#endif
