// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if NETFRAMEWORK
namespace System.Runtime.CompilerServices;

/// <summary>Indicates that a method is a module initializer.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ModuleInitializerAttribute : Attribute;
#endif
