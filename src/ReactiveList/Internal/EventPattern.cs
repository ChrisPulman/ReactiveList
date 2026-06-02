// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CP.Reactive.Internal;

/// <summary>
/// Event notification payload used by internal event-to-observable adapters.
/// </summary>
/// <typeparam name="TEventArgs">The event argument type.</typeparam>
internal sealed class EventPattern<TEventArgs>
    where TEventArgs : EventArgs
{
    public EventPattern(object? sender, TEventArgs eventArgs)
    {
        Sender = sender;
        EventArgs = eventArgs;
    }

    public object? Sender { get; }

    public TEventArgs EventArgs { get; }
}
