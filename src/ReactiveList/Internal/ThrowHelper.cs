// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace CP.Reactive.Internal;

/// <summary>
/// Provides target-framework-compatible guard helpers.
/// </summary>
internal static class ThrowHelper
{
    /// <summary>
    /// Throws when the supplied argument is null.
    /// </summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="argument">The argument value.</param>
    /// <param name="paramName">The parameter name, supplied by the compiler.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull<T>(
        T? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : class
    {
#if NETFRAMEWORK
        if (argument is null)
        {
            throw new ArgumentNullException(paramName);
        }
#else
        ArgumentNullException.ThrowIfNull(argument, paramName);
#endif
    }
}
