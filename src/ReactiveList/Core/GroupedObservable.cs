// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ReactiveUI.Primitives.Signals;

namespace CP.Reactive.Core;

internal sealed class GroupedObservable<TKey, TElement>(TKey key) : IGroupedObservable<TKey, TElement>, IDisposable
{
    private readonly Signal<TElement> _signal = new();

    public TKey Key { get; } = key;

    public IDisposable Subscribe(IObserver<TElement> observer) => _signal.Subscribe(observer);

    public void OnNext(TElement value) => _signal.OnNext(value);

    public void OnError(Exception error) => _signal.OnError(error);

    public void OnCompleted() => _signal.OnCompleted();

    public void Dispose() => _signal.Dispose();
}
