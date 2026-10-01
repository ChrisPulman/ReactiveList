// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace CP.Reactive.Internal;
#else
namespace CP.Primitives.Internal;
#endif

/// <summary>Relays typed event delegates without wrapping away their subscription identity.</summary>
/// <typeparam name="TEventArgs">The notification data type.</typeparam>
/// <typeparam name="THandler">The event delegate type.</typeparam>
internal sealed class TypedNotificationRelay<TEventArgs, THandler>
    where TEventArgs : EventArgs
    where THandler : Delegate
{
    /// <summary>Synchronizes changes to the subscribed handlers.</summary>
    private readonly Lock _gate = new();

    /// <summary>The facade instance reported as the sender for each notification.</summary>
    private readonly object _sender;

    /// <summary>Invokes the strongly typed handlers without reflection.</summary>
    private readonly Action<THandler, object?, TEventArgs> _invoke;

    /// <summary>The handlers that receive relayed notifications.</summary>
    private THandler? _handlers;

    /// <summary>Initializes a new instance of the typed notification relay.</summary>
    /// <param name="sender">The facade instance to report as the notification sender.</param>
    /// <param name="invoke">Invokes the event delegate with its facade sender and notification data.</param>
    internal TypedNotificationRelay(object sender, Action<THandler, object?, TEventArgs> invoke)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
    }

    /// <summary>Adds a handler to receive subsequent relayed notifications.</summary>
    /// <param name="handler">The handler to add, or null to make no change.</param>
    /// <returns>True when the handler is the relay's first subscription.</returns>
    internal bool Add(THandler? handler)
    {
        if (handler is null)
        {
            return false;
        }

        lock (_gate)
        {
            var isFirstHandler = _handlers is null;
            _handlers = (THandler?)Delegate.Combine(_handlers, handler);
            return isFirstHandler;
        }
    }

    /// <summary>Removes the last matching invocation sequence from the relay.</summary>
    /// <param name="handler">The handler to remove, or null to make no change.</param>
    /// <returns>True when a handler was removed and the relay has no remaining subscriptions.</returns>
    internal bool Remove(THandler? handler)
    {
        if (handler is null)
        {
            return false;
        }

        lock (_gate)
        {
            var previousHandlers = _handlers;
            var remainingHandlers = (THandler?)Delegate.Remove(previousHandlers, handler);
            var wasRemoved = !ReferenceEquals(previousHandlers, remainingHandlers);
            _handlers = remainingHandlers;
            return wasRemoved && remainingHandlers is null;
        }
    }

    /// <summary>Relays an event using the public facade as its sender.</summary>
    /// <param name="source">The wrapped source that raised the event.</param>
    /// <param name="eventArgs">The event data to relay.</param>
    internal void OnEvent(object? source, TEventArgs eventArgs)
    {
        _ = source;
        Dispatch(eventArgs);
    }

    /// <summary>Delivers a notification to a snapshot of the current handlers.</summary>
    /// <param name="eventArgs">The event data to relay.</param>
    internal void Dispatch(TEventArgs eventArgs)
    {
        THandler? handlers;
        lock (_gate)
        {
            handlers = _handlers;
        }

        if (handlers is null)
        {
            return;
        }

        _invoke(handlers, _sender, eventArgs);
    }
}
