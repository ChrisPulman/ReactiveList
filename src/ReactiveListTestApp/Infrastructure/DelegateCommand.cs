// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows.Input;

namespace ReactiveListTestApp.Infrastructure;

/// <summary>Adapts delegates to the WPF command contract without a reactive command allocation.</summary>
/// <param name="execute">The action executed by the command.</param>
/// <param name="canExecute">The optional availability predicate.</param>
internal sealed class DelegateCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => execute();

    /// <summary>Notifies WPF that command availability may have changed.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
