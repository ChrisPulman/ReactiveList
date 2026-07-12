// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveListTestApp.Infrastructure;

/// <summary>Forwards observable values to an allocation-conscious delegate.</summary>
/// <typeparam name="T">The observed value type.</typeparam>
/// <param name="onNext">The action invoked for each value.</param>
internal sealed class ActionObserver<T>(Action<T> onNext) : IObserver<T>
{
    /// <inheritdoc/>
    public void OnCompleted()
    {
    }

    /// <inheritdoc/>
    public void OnError(Exception error) => throw error;

    /// <inheritdoc/>
    public void OnNext(T value) => onNext(value);
}
