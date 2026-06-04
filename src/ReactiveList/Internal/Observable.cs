// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace CP.Reactive.Internal;

/// <summary>
/// Minimal observable factories used by ReactiveList internals.
/// </summary>
internal static class Observable
{
    public static IObservable<T> Defer<T>(Func<IObservable<T>> factory)
    {
        ThrowHelper.ThrowIfNull(factory);

        return Signal.Create<T>(observer =>
        {
            IObservable<T> source;
            try
            {
                source = factory();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
                return Disposable.Empty;
            }

            return source.Subscribe(observer);
        });
    }

    public static IObservable<EventPattern<TEventArgs>> FromEventPattern<TEventHandler, TEventArgs>(
        Action<TEventHandler> addHandler,
        Action<TEventHandler> removeHandler)
        where TEventHandler : Delegate
        where TEventArgs : EventArgs
    {
        ThrowHelper.ThrowIfNull(addHandler);
        ThrowHelper.ThrowIfNull(removeHandler);

        return Signal.Create<EventPattern<TEventArgs>>(observer =>
        {
            TEventHandler handler;
            if (typeof(TEventHandler) == typeof(PropertyChangedEventHandler))
            {
                PropertyChangedEventHandler typed = (sender, args) =>
                    observer.OnNext(new EventPattern<TEventArgs>(sender, (TEventArgs)(EventArgs)args));
                handler = (TEventHandler)(object)typed;
            }
            else if (typeof(TEventHandler) == typeof(EventHandler<TEventArgs>))
            {
                EventHandler<TEventArgs> typed = (sender, args) =>
                    observer.OnNext(new EventPattern<TEventArgs>(sender, args));
                handler = (TEventHandler)(object)typed;
            }
            else
            {
                throw new NotSupportedException($"Event handler type '{typeof(TEventHandler)}' is not supported.");
            }

            addHandler(handler);
            return Disposable.Create(() => removeHandler(handler));
        });
    }
}
