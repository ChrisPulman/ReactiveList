// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace CP.Reactive;

/// <summary>
/// A simple grouping implementation for change grouping.
/// </summary>
/// <typeparam name="TKey">The type of the grouping key.</typeparam>
/// <typeparam name="TElement">The type of elements in the group.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ChangeGrouping{TKey, TElement}"/> class.
/// </remarks>
/// <param name="key">The group key.</param>
/// <param name="elements">The elements in the group.</param>
internal sealed class ChangeGrouping<TKey, TElement>(TKey key, IEnumerable<TElement> elements) : IGrouping<TKey, TElement>
{
    /// <inheritdoc/>
    public TKey Key { get; } = key;

    /// <inheritdoc/>
    public IEnumerator<TElement> GetEnumerator() => elements.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
