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
    /// <summary>Synchronizes changes to the subscribed handlers.</summary>
    private readonly Lock _gate = new();

    /// <summary>The facade instance reported as the sender for each notification.</summary>
    private readonly object _sender;

    /// <summary>The handlers that receive relayed notifications.</summary>
    private Action<object?, TEventArgs>? _handlers;

    /// <summary>Initializes a new instance of the <see cref="NotificationRelay{TEventArgs}"/> class.</summary>
    /// <param name="sender">The facade instance to report as the notification sender.</param>
    internal NotificationRelay(object sender) => _sender = sender ?? throw new ArgumentNullException(nameof(sender));

    /// <summary>Adds a handler to receive subsequent relayed notifications.</summary>
    /// <param name="handler">The handler to add, or <see langword="null"/> to make no change.</param>
    /// <returns><see langword="true"/> when the handler is the relay's first subscription; otherwise, <see langword="false"/>.</returns>
    /// <remarks>Duplicate handlers are retained, matching .NET event subscription semantics.</remarks>
    internal bool Add(Action<object?, TEventArgs>? handler)
    {
        if (handler is null)
        {
            return false;
        }

        lock (_gate)
        {
            var isFirstHandler = _handlers is null;
            _handlers += handler;
            return isFirstHandler;
        }
    }

    /// <summary>Removes the last matching handler from the relay.</summary>
    /// <param name="handler">The handler to remove, or <see langword="null"/> to make no change.</param>
    /// <returns>
    /// <see langword="true"/> when a handler was removed and the relay has no remaining subscriptions;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>When a handler was added more than once, only its most recent subscription is removed.</remarks>
    internal bool Remove(Action<object?, TEventArgs>? handler)
    {
        if (handler is null)
        {
            return false;
        }

        lock (_gate)
        {
            var previousHandlers = _handlers;
            var remainingHandlers = (Action<object?, TEventArgs>?)Delegate.Remove(previousHandlers, handler);
            var wasRemoved = !ReferenceEquals(previousHandlers, remainingHandlers);
            _handlers = remainingHandlers;
            return wasRemoved && remainingHandlers is null;
        }
    }

    /// <summary>Relays an event received from the wrapped source.</summary>
    /// <param name="source">The wrapped source that raised the event.</param>
    /// <param name="eventArgs">The event data to relay.</param>
    /// <remarks>The source is intentionally ignored so subscribers observe the facade as the sender.</remarks>
    internal void OnEvent(object? source, TEventArgs eventArgs)
    {
        _ = source;
        Dispatch(eventArgs);
    }

    /// <summary>Delivers a notification to a snapshot of the current handlers.</summary>
    /// <param name="eventArgs">The event data to relay.</param>
    internal void Dispatch(TEventArgs eventArgs)
    {
        Action<object?, TEventArgs>? handlers;

        lock (_gate)
        {
            handlers = _handlers;
        }

        handlers?.Invoke(_sender, eventArgs);
    }
}
