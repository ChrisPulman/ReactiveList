// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET8_0_OR_GREATER

using System.Runtime.CompilerServices;

namespace CP.Reactive.Quaternary.Internal;

/// <summary>
/// Represents a change token for tracking collection modifications with minimal allocations.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
internal readonly record struct ChangeToken<T>
{
    /// <summary>
    /// Gets the version number of the collection when this token was created.
    /// </summary>
    public readonly long Version;

    /// <summary>
    /// Gets the count of items when this token was created.
    /// </summary>
    public readonly int Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeToken{T}"/> struct.
    /// </summary>
    /// <param name="version">The version number.</param>
    /// <param name="count">The item count.</param>
    public ChangeToken(long version, int count)
    {
        Version = version;
        Count = count;
    }

    /// <summary>
    /// Determines whether the collection has changed since this token was created.
    /// </summary>
    /// <param name="currentVersion">The current version of the collection.</param>
    /// <returns>true if the collection has changed; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool HasChanged(long currentVersion) => Version != currentVersion;
}
#endif
