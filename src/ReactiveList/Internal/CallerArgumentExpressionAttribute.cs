// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices;

/// <summary>
/// Indicates that a parameter captures the expression passed for another parameter.
/// </summary>
/// <param name="parameterName">The source parameter name.</param>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute
{
    /// <summary>
    /// Gets the source parameter name.
    /// </summary>
    public string ParameterName { get; } = parameterName;
}
#endif
