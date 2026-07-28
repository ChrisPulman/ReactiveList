// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if NETFRAMEWORK
namespace System.Runtime.CompilerServices;

/// <summary>Indicates that local variables should not be zero-initialized.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Module)]
internal sealed class SkipLocalsInitAttribute : Attribute;
#elif REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;

/// <summary>Marks the runtime-provided local-initialization compatibility path.</summary>
file enum SkipLocalsInitAttribute
{
    /// <summary>Indicates that the target runtime provides the attribute.</summary>
    RuntimeProvided,
}
#else
namespace CP.Primitives.Internal;

/// <summary>Marks the runtime-provided local-initialization compatibility path.</summary>
file enum SkipLocalsInitAttribute
{
    /// <summary>Indicates that the target runtime provides the attribute.</summary>
    RuntimeProvided,
}
#endif
