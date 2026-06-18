// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.Reactive.Core;

/// <summary>Represents a grouped observable sequence keyed by <typeparamref name="TKey"/>.</summary>
/// <typeparam name="TKey">The grouping key type.</typeparam>
/// <typeparam name="TElement">The element type.</typeparam>
/// <param name="key">The key value for this group.</param>
internal sealed class GroupedObservable<TKey, TElement>(TKey key) : IGroupedObservable<TKey, TElement>, IDisposable
{
    private readonly Signal<TElement> _signal = new();

    /// <summary>Gets the key identifying this group.</summary>
    public TKey Key { get; } = key;

    /// <summary>Subscribes an observer to this grouped stream.</summary>
    /// <param name="observer">The observer to register.</param>
    /// <returns>A disposable subscription.</returns>
    public IDisposable Subscribe(IObserver<TElement> observer) => _signal.Subscribe(observer);

    /// <summary>Pushes a new value into this group.</summary>
    /// <param name="value">The value to publish.</param>
    public void OnNext(TElement value) => _signal.OnNext(value);

    /// <summary>Notifies subscribers that the source produced an error.</summary>
    /// <param name="error">The error.</param>
    public void OnError(Exception error) => _signal.OnError(error);

    /// <summary>Notifies subscribers that the grouped stream has completed.</summary>
    public void OnCompleted() => _signal.OnCompleted();

    /// <summary>Disposes this group and releases resources.</summary>
    public void Dispose() => _signal.Dispose();
}
