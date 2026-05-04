// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices;

/// <summary>
/// Indicates that local variables should not be zero-initialized.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Module)]
internal sealed class SkipLocalsInitAttribute : Attribute
{
}
#endif
