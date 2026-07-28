// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if NETFRAMEWORK
namespace System.Runtime.CompilerServices;

/// <summary>Indicates that a parameter captures the expression passed for another parameter.</summary>
/// <param name="parameterName">The source parameter name.</param>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute
{
    /// <summary>Gets the source parameter name.</summary>
    internal string ParameterName { get; } = parameterName;
}
#elif REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;

/// <summary>Marks the runtime-provided caller-argument-expression compatibility path.</summary>
file enum CallerArgumentExpressionAttribute
{
    /// <summary>Indicates that the target runtime provides the attribute.</summary>
    RuntimeProvided,
}
#else
namespace CP.Primitives.Internal;

/// <summary>Marks the runtime-provided caller-argument-expression compatibility path.</summary>
file enum CallerArgumentExpressionAttribute
{
    /// <summary>Indicates that the target runtime provides the attribute.</summary>
    RuntimeProvided,
}
#endif
