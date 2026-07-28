// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;
#else
namespace CP.Primitives.Internal;
#endif
/// <summary>Represents a change token for tracking collection modifications with minimal allocations.</summary>
internal readonly record struct ChangeToken
{
    /// <summary>Initializes a new instance of the <see cref="ChangeToken"/> struct.</summary>
    /// <param name="version">The version number.</param>
    /// <param name="count">The item count.</param>
    internal ChangeToken(long version, int count)
    {
        Version = version;
        Count = count;
    }

    /// <summary>Gets the version number of the collection when this token was created.</summary>
    internal long Version { get; }

    /// <summary>Gets the count of items when this token was created.</summary>
    internal int Count { get; }

    /// <summary>Determines whether the collection has changed since this token was created.</summary>
    /// <param name="currentVersion">The current version of the collection.</param>
    /// <returns>true if the collection has changed; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool HasChanged(long currentVersion) => Version != currentVersion;
}
