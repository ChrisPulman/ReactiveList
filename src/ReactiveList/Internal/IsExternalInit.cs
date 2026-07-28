// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if NETFRAMEWORK
namespace System.Runtime.CompilerServices;

/// <summary>Reserved for compiler support.</summary>
internal sealed class IsExternalInit
{
    /// <summary>Initializes a new instance of the <see cref="IsExternalInit"/> class.</summary>
    internal IsExternalInit()
    {
    }
}
#elif REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;

/// <summary>Marks the runtime-provided init-only compatibility path.</summary>
file enum IsExternalInit
{
    /// <summary>Indicates that the target runtime provides init-only support.</summary>
    RuntimeProvided,
}
#else
namespace CP.Primitives.Internal;

/// <summary>Marks the runtime-provided init-only compatibility path.</summary>
file enum IsExternalInit
{
    /// <summary>Indicates that the target runtime provides init-only support.</summary>
    RuntimeProvided,
}
#endif
