// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;
#else
namespace CP.Primitives.Internal;
#endif
/// <summary>Event notification payload used by internal event-to-observable adapters.</summary>
/// <typeparam name="TEventArgs">The event argument type.</typeparam>
internal sealed class EventPattern<TEventArgs>
    where TEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="EventPattern{TEventArgs}"/> class.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="eventArgs">The event arguments.</param>
    public EventPattern(object? sender, TEventArgs eventArgs)
    {
        Sender = sender;
        EventArgs = eventArgs;
    }

    /// <summary>Gets the source object that raised the event.</summary>
    public object? Sender { get; }

    /// <summary>Gets the event arguments.</summary>
    public TEventArgs EventArgs { get; }
}
