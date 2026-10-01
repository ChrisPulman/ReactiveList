// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveListTestApp.Infrastructure;

namespace ReactiveListTestApp.Tests;

/// <summary>Verifies command execution and availability notifications.</summary>
public sealed class DelegateCommandTests
{
    /// <summary>Executes the supplied delegate and accepts arbitrary command parameters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Execute_DefaultAvailability_InvokesAction()
    {
        var calls = 0;
        var command = new DelegateCommand(() => calls++);
        command.Execute(new());
        await Assert.That(command.CanExecute(null)).IsTrue();
        await Assert.That(command.CanExecute(new())).IsTrue();
        await Assert.That(calls).IsEqualTo(1);
    }

    /// <summary>Re-evaluates availability and publishes the command as the event sender.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RaiseCanExecuteChanged_PredicateChanges_NotifiesSubscribers()
    {
        var enabled = false;
        var command = new DelegateCommand(static () => { }, () => enabled);
        object? sender = null;
        var calls = 0;
        EventHandler handler = (source, _) =>
        {
            sender = source;
            calls++;
        };
        command.CanExecuteChanged += handler;
        await Assert.That(command.CanExecute(null)).IsFalse();
        enabled = true;
        command.RaiseCanExecuteChanged();
        await Assert.That(command.CanExecute(null)).IsTrue();
        await Assert.That(sender).IsSameReferenceAs(command);
        await Assert.That(calls).IsEqualTo(1);
        command.CanExecuteChanged -= handler;
        command.RaiseCanExecuteChanged();
        await Assert.That(calls).IsEqualTo(1);
    }
}
