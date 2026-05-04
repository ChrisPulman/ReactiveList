// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices;

/// <summary>
/// Indicates that a method is a module initializer.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ModuleInitializerAttribute : Attribute
{
}
#endif
