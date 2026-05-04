// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Specifies that an output may be null when the method returns the specified value.
/// </summary>
/// <param name="returnValue">The return value condition.</param>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class MaybeNullWhenAttribute(bool returnValue) : Attribute
{
    /// <summary>
    /// Gets a value indicating whether the attribute condition applies on true returns.
    /// </summary>
    public bool ReturnValue { get; } = returnValue;
}
#endif
