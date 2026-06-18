// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

namespace ReactiveList.Benchmarks;

/// <summary>Provides BenchmarkObservableExtensions.</summary>
internal static class BenchmarkObservableExtensions
{
    /// <summary>Provides observable benchmark helpers.</summary>
    /// <typeparam name="T">The observable item type.</typeparam>
    /// <param name="source">The source observable.</param>
    extension<T>(IObservable<T> source)
    {
        /// <summary>Subscribes an action observer.</summary>
        /// <param name="onNext">The next-value handler.</param>
        /// <returns>The subscription disposable.</returns>
        public IDisposable SubscribeObserver(Action<T> onNext)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(onNext);

            return source.Subscribe(new ActionObserver<T>(onNext));
        }
    }

    /// <summary>Provides ActionObserver.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="onNext">The onNext value.</param>
    private sealed class ActionObserver<T>(Action<T> onNext) : IObserver<T>
    {
        /// <summary>Provides OnCompleted.</summary>
        public void OnCompleted()
        {
        }

        /// <summary>Provides OnError.</summary>
        /// <param name="error">The error value.</param>
        public void OnError(Exception error) => ExceptionDispatchInfo.Capture(error).Throw();

        /// <summary>Provides OnNext.</summary>
        /// <param name="value">The value.</param>
        public void OnNext(T value) => onNext(value);
    }
}
