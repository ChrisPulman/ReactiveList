// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;
#else
namespace CP.Primitives.Internal;
#endif
/// <summary>Relays notifications while preserving the sender exposed by a facade.</summary>
/// <typeparam name="TEventArgs">The type of event data delivered by the relay.</typeparam>
internal sealed class NotificationRelay<TEventArgs>
    where TEventArgs : EventArgs
{
    /// <summary>The typed relay that owns the action subscriptions.</summary>
    private readonly TypedNotificationRelay<TEventArgs, Action<object?, TEventArgs>> _relay;

    /// <summary>Initializes a new instance of the <see cref="NotificationRelay{TEventArgs}"/> class.</summary>
    /// <param name="sender">The facade instance to report as the notification sender.</param>
    internal NotificationRelay(object sender) =>
        _relay = new(sender, static (handler, facade, eventArgs) => handler(facade, eventArgs));

    /// <summary>Adds a handler and reports whether this is the first subscription.</summary>
    /// <param name="handler">The handler to add.</param>
    /// <returns>True when this is the first subscription.</returns>
    internal bool Add(Action<object?, TEventArgs>? handler) => _relay.Add(handler);

    /// <summary>Removes a handler and reports whether the final subscription was removed.</summary>
    /// <param name="handler">The handler to remove.</param>
    /// <returns>True when the final subscription was removed.</returns>
    internal bool Remove(Action<object?, TEventArgs>? handler) => _relay.Remove(handler);

    /// <summary>Relays an event using the public facade as its sender.</summary>
    /// <param name="source">The original event sender.</param>
    /// <param name="eventArgs">The notification data.</param>
    internal void OnEvent(object? source, TEventArgs eventArgs) => _relay.OnEvent(source, eventArgs);

    /// <summary>Dispatches notification data to the current action subscribers.</summary>
    /// <param name="eventArgs">The notification data.</param>
    internal void Dispatch(TEventArgs eventArgs) => _relay.Dispatch(eventArgs);
}
