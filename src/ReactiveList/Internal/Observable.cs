// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace CP.Reactive.Internal;

/// <summary>Minimal observable factories used by ReactiveList internals.</summary>
internal static class Observable
{
    /// <summary>Creates an observable sequence using a deferred factory.</summary>
    /// <typeparam name="T">The observable element type.</typeparam>
    /// <param name="factory">The factory to create the deferred observable.</param>
    /// <returns>An observable sequence that defers invocation to subscription.</returns>
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
                return Scope.Empty;
            }

            return source.Subscribe(observer);
        });
    }

    /// <summary>Creates an observable sequence from an event pattern.</summary>
    /// <typeparam name="TEventHandler">The delegate type for the event handler.</typeparam>
    /// <typeparam name="TEventArgs">The event argument type.</typeparam>
    /// <param name="addHandler">Adds the event handler.</param>
    /// <param name="removeHandler">Removes the event handler.</param>
    /// <returns>An observable sequence of event pattern values.</returns>
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
            return Scope.Create(() => removeHandler(handler));
        });
    }
}
